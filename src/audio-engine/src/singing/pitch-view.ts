/**
 * Pitch view of the singing feedback (contract 2.6.0, data-model 3.5): an engine-owned canvas showing 4 s back and
 * 2 s ahead of the song position, the "now" line at 2/3 of the width; the reference as horizontal bars per run of
 * equal (to 50 cents) voiced frames, the singer as a polyline folded into the octave nearest the target. The vertical
 * range is the reference's 5th-95th percentile +-3 semitones (at least an octave), moved by the 012 transpose. With
 * reduced motion the window pages every 6 s.
 *
 * @packageDocumentation
 */
import { MotionPreference } from '../karaoke/motion';
import { Cursor } from '../timeline/cursor';
import { pageStart } from '../timeline/paging';
import type { LatencyMethod, SingingState, SingingUnit } from '../types';
import { CanvasSurface } from '../views/canvas-surface';
import { DomWriter } from '../views/dom-writer';
import { HOP_MS, foldError } from './score-core';

/** Shown before the position (ms). */
const BEHIND_MS = 4000;
/** Shown after the position (ms). */
const AHEAD_MS = 2000;
/** Reduced-motion page length (ms). */
const PAGE_MS = 6000;
/** Frames of one reference bar differ by at most this (cents). */
const RUN_CENTS = 50;
/** Margin above and below the reference range (cents). */
const RANGE_MARGIN = 300;
/** Smallest vertical range (cents). */
const MIN_RANGE = 1200;
/** Reference bar height (px). */
const BAR_HEIGHT = 6;
/** Colours: host CSS custom properties with fallbacks. */
const PALETTE = { '--pitch-view-reference': '#64748b', '--pitch-view-singer': '#22c55e', '--pitch-view-now': '#e5e7eb' } as const;

/** Host attributes written by the pitch view (removed on dispose). */
export const PITCH_HOST_ATTRIBUTES = ['data-state', 'data-singer-cents', 'data-target-cents', 'data-unit-index', 'data-last-score', 'data-latency-source'] as const;

/** One reference bar. */
interface Bar {
  readonly startMs: number;
  readonly endMs: number;
  readonly cents: number;
}

/** Construction parameters of {@link PitchView}. */
export interface PitchViewOptions {
  readonly host: HTMLElement;
  readonly reference: Int16Array;
  readonly units: readonly SingingUnit[];
  readonly latencySource: LatencyMethod;
  readonly reducedMotion: boolean | undefined;
  readonly transpose: () => number;
  readonly requestFrame: () => void;
}

/** Vertical mapping. */
interface Scale {
  readonly low: number;
  readonly high: number;
}

function percentile(sorted: readonly number[], p: number): number {
  return Number(sorted[Math.min(sorted.length - 1, Math.floor(p * sorted.length))]);
}

/** Reference range +-3 semitones, at least one octave around its centre (C4..C5 without voiced frames). */
function rangeOf(reference: Int16Array): Scale {
  const voiced = [...reference].filter((v) => v > 0).sort((a, b) => a - b);
  const low = voiced.length === 0 ? 6000 : percentile(voiced, 0.05) - RANGE_MARGIN;
  const high = voiced.length === 0 ? 7200 : percentile(voiced, 0.95) + RANGE_MARGIN;
  const centre = (low + high) / 2;
  return high - low >= MIN_RANGE ? { low, high } : { low: centre - MIN_RANGE / 2, high: centre + MIN_RANGE / 2 };
}

function breaksRun(value: number, first: number): boolean {
  return value === 0 || Math.abs(value - first) > RUN_CENTS;
}

/** Runs of voiced frames within 50 cents of the run's first frame. */
function barsOf(reference: Int16Array): Bar[] {
  const bars: Bar[] = [];
  let start = -1;
  [...reference, 0].forEach((value, i) => {
    if (start >= 0 && breaksRun(value, Number(reference[start]))) {
      bars.push({ startMs: start * HOP_MS, endMs: i * HOP_MS, cents: Number(reference[start]) });
      start = -1;
    }
    if (start < 0 && value > 0) {
      start = i;
    }
  });
  return bars;
}

/** The singer's recent frames (song ms, raw cents or null). */
class SingerTrail {
  public readonly times: number[] = [];
  public readonly cents: (number | null)[] = [];

  public add(songMs: number, cents: number | null): void {
    this.times.push(songMs);
    this.cents.push(cents);
    let drop = 0;
    while (drop < this.times.length && Number(this.times[drop]) < songMs - PAGE_MS) {
      drop += 1;
    }
    this.times.splice(0, drop);
    this.cents.splice(0, drop);
  }

  public get latest(): number | null {
    return this.cents.at(-1) ?? null;
  }
}

/** The drawing side of one singing feedback. */
export class PitchView {
  private readonly surface: CanvasSurface;
  private readonly motion: MotionPreference;
  private readonly bars: readonly Bar[];
  private readonly barStarts: readonly number[];
  private readonly barCursor: Cursor;
  private readonly unitCursor: Cursor;
  private readonly range: Scale;
  private readonly trail = new SingerTrail();
  private readonly writer = new DomWriter();
  private lastScore: number | null | undefined;

  /** @param options - Host, reference, units, latency source, motion, transpose and frame request. */
  public constructor(private readonly options: PitchViewOptions) {
    this.bars = barsOf(options.reference);
    this.barStarts = this.bars.map((b) => b.startMs);
    this.barCursor = new Cursor(this.barStarts);
    this.unitCursor = new Cursor(options.units.map((u) => u.startMs));
    this.range = rangeOf(options.reference);
    this.surface = new CanvasSurface(options.host, PALETTE, options.requestFrame);
    this.motion = new MotionPreference(options.reducedMotion, options.requestFrame);
    this.writer.data(options.host, 'latencySource', options.latencySource);
  }

  /** Drawing calls so far. */
  public get primitives(): number {
    return this.surface.painter.primitives;
  }

  /** @param songMs - Aligned song time. @param cents - Singer pitch, or null. */
  public addSinger(songMs: number, cents: number | null): void {
    this.trail.add(songMs, cents);
  }

  /** @param score - Score of the last finished unit. */
  public setLastScore(score: number | null): void {
    this.lastScore = score;
  }

  /** @param state - Singing state for the host's `data-state`. */
  public setState(state: SingingState): void {
    this.writer.data(this.options.host, 'state', state);
  }

  /** @param t - Heard song position (ms). */
  public draw(t: number): void {
    const start = this.motion.reduced ? pageStart(t, PAGE_MS) : t - BEHIND_MS;
    const pxPerMs = this.surface.width / (BEHIND_MS + AHEAD_MS);
    const x = (ms: number): number => (ms - start) * pxPerMs;
    this.surface.painter.clear(this.surface.width, this.surface.height);
    this.drawBars(start, x);
    this.drawSinger(start, x);
    this.drawNow(x(t));
    this.writeHost(t);
  }

  /** Removes the canvas, the host attributes and the observers. */
  public dispose(): void {
    this.motion.dispose();
    this.surface.dispose();
    PITCH_HOST_ATTRIBUTES.forEach((name) => {
      this.options.host.removeAttribute(name);
    });
  }

  private y(cents: number): number {
    const shift = 100 * this.options.transpose();
    const { low, high } = this.range;
    return this.surface.height * (1 - (cents - low - shift) / (high - low));
  }

  private drawBars(start: number, x: (ms: number) => number): void {
    const { painter } = this.surface;
    const end = start + BEHIND_MS + AHEAD_MS;
    const shift = 100 * this.options.transpose();
    painter.beginFill(this.surface.colour('--pitch-view-reference'));
    this.barsBetween(start, end).forEach((bar) => {
      painter.roundRect(x(bar.startMs), this.y(bar.cents + shift) - BAR_HEIGHT / 2, x(bar.endMs) - x(bar.startMs), BAR_HEIGHT);
    });
    painter.fill();
  }

  /** Bars overlapping `[start, end)` (O(1) cursor, then the few visible bars). */
  private barsBetween(start: number, end: number): Bar[] {
    const first = Math.max(this.barCursor.index(start), 0);
    let last = first;
    while (last < this.bars.length && Number(this.barStarts[last]) < end) {
      last += 1;
    }
    return this.bars.slice(first, last).filter((bar) => bar.endMs > start);
  }

  private drawSinger(start: number, x: (ms: number) => number): void {
    const { painter } = this.surface;
    const { times, cents } = this.trail;
    painter.beginStroke(this.surface.colour('--pitch-view-singer'), 2);
    let previous: number | null = null;
    times.forEach((ms, i) => {
      const folded = ms < start ? null : this.fold(cents[i] ?? null, ms, previous);
      if (folded !== null) {
        this.penTo(previous === null, x(ms), this.y(folded));
      }
      previous = folded;
    });
    painter.stroke();
  }

  private penTo(move: boolean, x: number, y: number): void {
    if (move) {
      this.surface.painter.moveTo(x, y);
    } else {
      this.surface.painter.lineTo(x, y);
    }
  }

  /** Folds into the octave nearest the target, else nearest the previous point. */
  private fold(raw: number | null, ms: number, previous: number | null): number | null {
    if (raw === null) {
      return null;
    }
    const target = this.targetAt(ms) ?? previous ?? raw;
    return target + foldError(raw - target);
  }

  private targetAt(ms: number): number | null {
    const value = this.options.reference[Math.round(ms / HOP_MS)] ?? 0;
    return value > 0 ? value + 100 * this.options.transpose() : null;
  }

  private drawNow(x: number): void {
    const { painter } = this.surface;
    painter.beginStroke(this.surface.colour('--pitch-view-now'), 2);
    painter.moveTo(x, 0);
    painter.lineTo(x, this.surface.height);
    painter.stroke();
  }

  private writeHost(t: number): void {
    const { host } = this.options;
    const singer = this.trail.latest;
    const target = this.targetAt(t);
    this.writer.data(host, 'singerCents', singer === null ? '' : String(Math.round(singer)));
    this.writer.data(host, 'targetCents', target === null ? '' : String(target));
    this.writer.data(host, 'unitIndex', String(this.unitAt(t)));
    this.writer.data(host, 'lastScore', this.lastScore === null || this.lastScore === undefined ? '' : String(this.lastScore));
  }

  private unitAt(t: number): number {
    const unit = this.options.units[this.unitCursor.index(t)];
    return unit !== undefined && t < unit.endMs ? unit.index : -1;
  }
}

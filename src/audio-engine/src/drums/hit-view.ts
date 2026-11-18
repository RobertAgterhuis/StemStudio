/**
 * One attached set of drum hit lanes (contract 2.6.0 `attachHitLanes`, data-model 5.2): five lanes (or the chosen
 * pieces) on an engine-owned canvas, the "now" line at 20 % of the width, `lookAheadMs` ahead of it; marks are
 * rounded rectangles 40-100 % of the lane high by strength, hits within 30 ms in the past flash. With reduced motion
 * the window pages per bar of the beat grid (or per `lookAheadMs`) and the now line moves inside the page.
 *
 * @packageDocumentation
 */
import { MotionPreference } from '../karaoke/motion';
import { BarScale, type TimeScale } from '../timeline/bars';
import { Cursor } from '../timeline/cursor';
import { pageStart } from '../timeline/paging';
import type { DrumHitEvent } from '../types';
import { CanvasSurface } from '../views/canvas-surface';
import { DomWriter } from '../views/dom-writer';
import type { HitLaneSettings } from './validate';

/** Share of the width before the now line. */
const NOW_FRACTION = 0.2;
/** Hits this close behind the position flash (ms). */
const FLASH_MS = 30;
/** Mark width (px). */
const MARK_WIDTH = 8;
/** Smallest mark height as a share of the lane. */
const MIN_MARK = 0.4;
/** Colours: host CSS custom properties with fallbacks. */
const PALETTE = { '--hit-lane-mark': '#3b82f6', '--hit-lane-flash': '#f59e0b', '--hit-lane-now': '#e5e7eb' } as const;

/** Host attributes written by the hit lanes (removed on detach). */
export const HIT_HOST_ATTRIBUTES = ['data-position-ms', 'data-next-hit-ms', 'data-next-hit-piece', 'data-visible-hits', 'data-look-ahead-ms'] as const;

/** Construction parameters of {@link HitView}. */
export interface HitViewOptions {
  readonly host: HTMLElement;
  readonly hits: readonly DrumHitEvent[];
  readonly scale: TimeScale;
  readonly settings: HitLaneSettings;
  readonly requestFrame: () => void;
}

/** Visible time window (ms) and its pixel scale. */
interface Window {
  readonly start: number;
  readonly pxPerMs: number;
}

/** A mark to draw. */
interface Mark {
  readonly x: number;
  readonly y: number;
  readonly height: number;
}

/** The drawing side of one `attachHitLanes`. */
export class HitView {
  private readonly surface: CanvasSurface;
  private readonly times: readonly number[];
  private readonly windowCursor: Cursor;
  private readonly positionCursor: Cursor;
  private readonly writer = new DomWriter();
  private settings: HitLaneSettings;
  private motion: MotionPreference;

  /** @param options - Host, hits, scale, settings and frame request. */
  public constructor(private readonly options: HitViewOptions) {
    this.settings = options.settings;
    this.times = options.hits.map((h) => h.timeMs);
    this.windowCursor = new Cursor(this.times);
    this.positionCursor = new Cursor(this.times);
    this.surface = new CanvasSurface(options.host, PALETTE, options.requestFrame);
    this.motion = new MotionPreference(this.settings.reducedMotion, options.requestFrame);
  }

  /** Drawing calls so far. */
  public get primitives(): number {
    return this.surface.painter.primitives;
  }

  /** @param settings - New look-ahead, pieces and motion option. */
  public configure(settings: HitLaneSettings): void {
    if (settings.reducedMotion !== this.settings.reducedMotion) {
      this.motion.dispose();
      this.motion = new MotionPreference(settings.reducedMotion, this.options.requestFrame);
    }
    this.settings = settings;
  }

  /** @param t - Heard track position (ms). */
  public draw(t: number): void {
    const window = this.window(t);
    const { normal, flashing } = this.marks(t, window);
    const { painter } = this.surface;
    painter.clear(this.surface.width, this.surface.height);
    this.fillMarks(normal, this.surface.colour('--hit-lane-mark'));
    this.fillMarks(flashing, this.surface.colour('--hit-lane-flash'));
    this.drawNow((t - window.start) * window.pxPerMs);
    this.writeHost(t, normal.length + flashing.length);
  }

  /** Removes the canvas, the host attributes and the observers. */
  public dispose(): void {
    this.motion.dispose();
    this.surface.dispose();
    HIT_HOST_ATTRIBUTES.forEach((name) => {
      this.options.host.removeAttribute(name);
    });
  }

  private window(t: number): Window {
    const span = this.settings.lookAheadMs / (1 - NOW_FRACTION);
    return { start: this.pageOf(t) - span * NOW_FRACTION, pxPerMs: this.surface.width / span };
  }

  /** The position itself while scrolling; the bar (or look-ahead page) start with reduced motion. */
  private pageOf(t: number): number {
    if (!this.motion.reduced) {
      return t;
    }
    const { scale } = this.options;
    return scale instanceof BarScale ? scale.barStart(t) : pageStart(t, this.settings.lookAheadMs);
  }

  private marks(t: number, window: Window): { normal: Mark[]; flashing: Mark[] } {
    const normal: Mark[] = [];
    const flashing: Mark[] = [];
    const end = window.start + this.surface.width / window.pxPerMs;
    const laneHeight = this.surface.height / Math.max(this.settings.pieces.length, 1);
    const first = this.firstAtOrAfter(window.start);
    this.options.hits.slice(first, this.endBefore(first, end)).forEach((hit) => {
      const lane = this.settings.pieces.indexOf(hit.piece);
      if (lane >= 0) {
        const mark = markOf({ hit, lane, laneHeight, x: (hit.timeMs - window.start) * window.pxPerMs });
        (t - FLASH_MS <= hit.timeMs && hit.timeMs <= t ? flashing : normal).push(mark);
      }
    });
    return { normal, flashing };
  }

  /** Index after the last hit before `end`, scanning from `first`. */
  private endBefore(first: number, end: number): number {
    let i = first;
    while (i < this.times.length && Number(this.times[i]) < end) {
      i += 1;
    }
    return i;
  }

  private firstAtOrAfter(ms: number): number {
    const before = this.windowCursor.index(ms);
    return this.times[before] === ms ? this.firstOfRun(before) : before + 1;
  }

  /** First index of the run of equal times ending at `index`. */
  private firstOfRun(index: number): number {
    let first = index;
    while (first > 0 && this.times[first - 1] === this.times[index]) {
      first -= 1;
    }
    return first;
  }

  private fillMarks(marks: readonly Mark[], colour: string): void {
    if (marks.length === 0) {
      return;
    }
    const { painter } = this.surface;
    painter.beginFill(colour);
    marks.forEach((m) => {
      painter.roundRect(m.x - MARK_WIDTH / 2, m.y, MARK_WIDTH, m.height);
    });
    painter.fill();
  }

  private drawNow(x: number): void {
    const { painter } = this.surface;
    painter.beginStroke(this.surface.colour('--hit-lane-now'), 2);
    painter.moveTo(x, 0);
    painter.lineTo(x, this.surface.height);
    painter.stroke();
  }

  private writeHost(t: number, visible: number): void {
    const { host } = this.options;
    const next = this.nextHit(t);
    this.writer.data(host, 'positionMs', String(t));
    this.writer.data(host, 'nextHitMs', next === undefined ? '' : String(next.timeMs));
    this.writer.data(host, 'nextHitPiece', next?.piece ?? '');
    this.writer.data(host, 'visibleHits', String(visible));
    this.writer.data(host, 'lookAheadMs', String(this.settings.lookAheadMs));
  }

  private nextHit(t: number): DrumHitEvent | undefined {
    const { hits } = this.options;
    let i = this.positionCursor.index(t) + 1;
    while (i < hits.length && !this.isDrawn(hits[i])) {
      i += 1;
    }
    return hits[i];
  }

  private isDrawn(hit: DrumHitEvent | undefined): boolean {
    return hit !== undefined && this.settings.pieces.includes(hit.piece);
  }
}

function markOf(spec: { hit: DrumHitEvent; lane: number; laneHeight: number; x: number }): Mark {
  const height = spec.laneHeight * (MIN_MARK + (1 - MIN_MARK) * spec.hit.strength);
  return { x: spec.x, y: spec.lane * spec.laneHeight + (spec.laneHeight - height) / 2, height };
}

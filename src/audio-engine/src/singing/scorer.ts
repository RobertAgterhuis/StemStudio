/**
 * Live unit scorer (data-model 3.4): follows the aligned singer frames, arms a unit when the song enters it within
 * 50 ms of its start, scores it when the song passes its end (`OnSingingUnitScored`), scores it again on every loop
 * pass (a backwards jump without a seek) and discards the partial unit after a seek.
 *
 * @packageDocumentation
 */
import { Cursor } from '../timeline/cursor';
import type { SingingUnit, SingingUnitScore } from '../types';
import { NEAREST_MS, scoreUnit, summarise } from './score-core';
import type { SingingSummary } from '../types';

/** A unit entered this close after its start is scored (ms). */
const ARM_WINDOW_MS = 50;
/** A backwards jump larger than this without a seek is a loop wrap (ms). */
const WRAP_MS = 100;

/** Construction parameters of {@link UnitScorer}. */
export interface UnitScorerOptions {
  /** Reference frames. */
  readonly reference: Int16Array;
  /** Units in time order. */
  readonly units: readonly SingingUnit[];
  /** Initial tolerance (cents). */
  readonly toleranceCents: number;
  /** Current 012 transpose. */
  readonly transpose: () => number;
  /** Receives every finished unit. */
  readonly onUnit: (score: SingingUnitScore) => void;
}

/** The unit being sung and its singer frames. */
interface Armed {
  readonly unit: SingingUnit;
  readonly times: number[];
  readonly cents: (number | null)[];
}

/** Scores the units of one singing feedback. */
export class UnitScorer {
  private readonly cursor: Cursor;
  private readonly latest = new Map<number, SingingUnitScore>();
  private tolerance: number;
  private armed: Armed | null = null;
  private entered = -1;
  private lastMs = Number.NaN;
  private pass = 1;
  private last: number | null | undefined = undefined;

  /** @param options - Reference, units, tolerance, transpose and callback. */
  public constructor(private readonly options: UnitScorerOptions) {
    this.cursor = new Cursor(options.units.map((u) => u.startMs));
    this.tolerance = options.toleranceCents;
  }

  /** Score of the last finished unit (undefined before the first). */
  public get lastScore(): number | null | undefined {
    return this.last;
  }

  /** @param cents - New tolerance for the current and later units. */
  public setTolerance(cents: number): void {
    this.tolerance = cents;
  }

  /**
   * @param songMs - Aligned song time of the frame.
   * @param cents - Smoothed singer pitch, or null.
   */
  public frame(songMs: number, cents: number | null): void {
    if (songMs < this.lastMs - WRAP_MS) {
      this.pass += 1;
      this.forget();
    }
    this.lastMs = songMs;
    this.keepBoundary(songMs, cents);
    this.finish(songMs);
    this.enter(songMs);
    this.armed?.times.push(songMs);
    this.armed?.cents.push(cents);
  }

  /** A seek or loop change: the partial unit is discarded and passes count from 1 again. */
  public reposition(): void {
    this.forget();
    this.lastMs = Number.NaN;
    this.pass = 1;
  }

  /**
   * @param songMs - Song time.
   * @returns Index of the unit holding `songMs`, or -1.
   */
  public unitAt(songMs: number): number {
    const i = this.cursor.index(songMs);
    const unit = this.options.units[i];
    return unit !== undefined && songMs < unit.endMs ? unit.index : -1;
  }

  /** @returns The summary over the latest score of every unit. */
  public summary(): Omit<SingingSummary, 'latencySource'> {
    return summarise([...this.latest.values()], this.tolerance);
  }

  /** A frame just after the armed unit's end still serves its last reference frames (nearest within 15 ms). */
  private keepBoundary(songMs: number, cents: number | null): void {
    const armed = this.armed;
    if (armed !== null && songMs >= armed.unit.endMs && songMs < armed.unit.endMs + NEAREST_MS) {
      armed.times.push(songMs);
      armed.cents.push(cents);
    }
  }

  private forget(): void {
    this.armed = null;
    this.entered = -1;
  }

  private finish(songMs: number): void {
    const armed = this.armed;
    if (armed === null || songMs < armed.unit.endMs) {
      return;
    }
    this.armed = null;
    const target = { reference: this.options.reference, unit: armed.unit, toleranceCents: this.tolerance, transpose: this.options.transpose() };
    const score: SingingUnitScore = { index: armed.unit.index, score: scoreUnit(target, armed), pass: this.pass };
    this.latest.set(score.index, score);
    this.last = score.score;
    this.options.onUnit(score);
  }

  private enter(songMs: number): void {
    const i = this.cursor.index(songMs);
    const unit = this.options.units[i];
    if (i === this.entered || unit === undefined || songMs >= unit.endMs) {
      return;
    }
    this.entered = i;
    this.armed = songMs - unit.startMs <= ARM_WINDOW_MS ? { unit, times: [], cents: [] } : null;
  }
}

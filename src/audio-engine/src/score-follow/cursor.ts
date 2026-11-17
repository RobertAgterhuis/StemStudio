/**
 * Current-bar cursor (contract 2.4.0): a forward step per frame, a binary search after a seek, loop wrap or attach.
 *
 * @packageDocumentation
 */
import type { ScoreViewBar } from '../types';

/** Finds the bar with `startSeconds <= t < endSeconds`. */
export class BarCursor {
  private index = -1;
  private count = 0;

  /** @param bars - Ascending, contiguous bars (validated). */
  public constructor(private readonly bars: readonly ScoreViewBar[]) {}

  /** Binary searches so far (diagnostics, tests). */
  public get searches(): number {
    return this.count;
  }

  /**
   * @param t - Heard track time (s).
   * @returns Index of the bar at `t`, or -1 before the first or after the last bar.
   */
  public locate(t: number): number {
    if (this.contains(this.index, t)) {
      return this.index;
    }
    this.index = this.contains(this.index + 1, t) ? this.index + 1 : this.search(t);
    return this.index;
  }

  private contains(index: number, t: number): boolean {
    const bar = this.bars[index];
    return bar !== undefined && bar.startSeconds <= t && t < bar.endSeconds;
  }

  private search(t: number): number {
    this.count += 1;
    let lo = 0;
    let hi = this.bars.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      const bar = this.bars[mid];
      if (bar === undefined || t < bar.startSeconds) {
        hi = mid - 1;
      } else if (t >= bar.endSeconds) {
        lo = mid + 1;
      } else {
        return mid;
      }
    }
    return -1;
  }
}

/**
 * Lookup of the current line or word: a cached index that advances forward in O(1) per frame,
 * with a binary search after any jump (seek, loop wrap, offset change).
 *
 * @packageDocumentation
 */

/**
 * Binary search over non-decreasing start times.
 *
 * @param starts - Start times in non-decreasing order.
 * @param t - Time to locate.
 * @returns The last index with `starts[i] <= t`, or -1 when `t` is before the first start.
 */
export function lastAtOrBefore(starts: readonly number[], t: number): number {
  let low = 0;
  let high = starts.length;
  while (low < high) {
    const mid = (low + high) >>> 1;
    // mid < starts.length, so the element exists (Number() only narrows the indexed type).
    if (Number(starts[mid]) <= t) {
      low = mid + 1;
    } else {
      high = mid;
    }
  }
  return low - 1;
}

/** Cached position in a list of start times. */
export class Cursor {
  private current = -1;
  private searchCount = 0;

  /** @param starts - Start times in non-decreasing order. */
  public constructor(private readonly starts: readonly number[]) {}

  /** Number of binary searches so far (normal playback needs none). */
  public get searches(): number {
    return this.searchCount;
  }

  /**
   * @param t - Time to locate.
   * @returns The last index with `starts[i] <= t`, or -1.
   */
  public index(t: number): number {
    if (this.holds(this.current, t)) {
      return this.current;
    }
    if (this.holds(this.current + 1, t)) {
      this.current += 1;
      return this.current;
    }
    this.searchCount += 1;
    this.current = lastAtOrBefore(this.starts, t);
    return this.current;
  }

  private holds(i: number, t: number): boolean {
    const start = i < 0 ? Number.NEGATIVE_INFINITY : (this.starts[i] ?? Number.POSITIVE_INFINITY);
    const next = this.starts[i + 1] ?? Number.POSITIVE_INFINITY;
    return start <= t && t < next;
  }
}

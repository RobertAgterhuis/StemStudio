/**
 * The 5-frame median over voiced singer values (data-model 3.2); an unvoiced frame breaks the line and restarts the
 * filter. Shared by the live feedback and `scoreTake`.
 *
 * @packageDocumentation
 */

/** Voiced frames in the median. */
export const MEDIAN_FRAMES = 5;

function middle(values: readonly number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const half = sorted.length >> 1;
  return sorted.length % 2 === 1 ? Number(sorted[half]) : (Number(sorted[half - 1]) + Number(sorted[half])) / 2;
}

/** Streaming median of the last voiced values. */
export class MedianSmoother {
  private readonly values: number[] = [];

  /**
   * @param cents - Next value, or null for an unvoiced frame.
   * @returns The median of up to five voiced values since the last null, or null.
   */
  public next(cents: number | null): number | null {
    if (cents === null) {
      this.reset();
      return null;
    }
    this.values.push(cents);
    if (this.values.length > MEDIAN_FRAMES) {
      this.values.shift();
    }
    return middle(this.values);
  }

  /** Forgets the values (seek). */
  public reset(): void {
    this.values.length = 0;
  }
}

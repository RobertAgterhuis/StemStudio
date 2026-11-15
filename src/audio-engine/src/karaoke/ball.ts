/**
 * Ball path (research R-05): a parabola from the centre of word *i* to word *i+1* that lands
 * exactly at the start of word *i+1*.
 *
 * @packageDocumentation
 */

/** Hop height as a fraction of the current line height. */
export const HOP_HEIGHT_RATIO = 0.6;
/** Hops shorter than this (ms) get a proportionally lower arc. */
export const SHORT_HOP_MS = 150;

/** A point relative to the banner host (px): `x` = word centre, `y` = word top. */
export interface Point {
  /** Horizontal position (px). */
  readonly x: number;
  /** Vertical position (px), down is positive. */
  readonly y: number;
}

/**
 * @param t - Karaoke time (ms).
 * @param fromMs - Start of the word the ball leaves.
 * @param toMs - Start of the word it lands on.
 * @returns Progress 0..1 (1 when both start together).
 */
export function hopProgress(t: number, fromMs: number, toMs: number): number {
  if (toMs <= fromMs) {
    return 1;
  }
  return Math.min(1, Math.max(0, (t - fromMs) / (toMs - fromMs)));
}

/**
 * @param lineHeight - Height of the current line (px).
 * @param durationMs - Duration of the hop (from one word start to the next).
 * @returns Peak height: 0.6 x line height, scaled by `duration / 150 ms` for shorter hops.
 */
export function hopHeight(lineHeight: number, durationMs: number): number {
  return HOP_HEIGHT_RATIO * lineHeight * Math.min(1, Math.max(0, durationMs) / SHORT_HOP_MS);
}

/**
 * @param from - Word the ball leaves.
 * @param to - Word the ball lands on.
 * @param p - Progress 0..1.
 * @param height - Peak height (px).
 * @returns `x = lerp`, `y = lerp - h * 4p(1 - p)`; exactly `from` at 0 and `to` at 1.
 */
export function ballAt(from: Point, to: Point, p: number, height: number): Point {
  if (p >= 1) {
    return to;
  }
  return {
    x: from.x + (to.x - from.x) * p,
    y: from.y + (to.y - from.y) * p - height * 4 * p * (1 - p),
  };
}

/**
 * @param point - Ball position.
 * @returns The `style.transform` value (`translate3d(x, y, 0px)`, compositor-only; the form browsers serialise back).
 */
export function ballTransform(point: Point): string {
  return `translate3d(${String(point.x)}px, ${String(point.y)}px, 0px)`;
}

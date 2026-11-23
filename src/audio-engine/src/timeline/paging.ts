/**
 * Reduced-motion paging (contract 2.6.0, data-model 3.5 and 5.2): with reduced motion the views do not scroll; their
 * window jumps a page at a time.
 *
 * @packageDocumentation
 */

/**
 * @param position - Current position (any unit).
 * @param pageLength - Page length in the same unit (> 0).
 * @param origin - Start of page 0; default 0.
 * @returns The start of the page holding `position`.
 */
export function pageStart(position: number, pageLength: number, origin = 0): number {
  return origin + Math.floor((position - origin) / pageLength) * pageLength;
}

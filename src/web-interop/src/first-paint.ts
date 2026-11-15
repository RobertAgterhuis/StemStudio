/**
 * Entry of the classic `first-paint.js` script (web-interop.md 1), built as an IIFE by
 * `vite build --mode first-paint`. Phase 0 placeholder owned by the frontend agent (T013/T014):
 * the implementation reads `localStorage["stemstudio.preferences"]` synchronously and sets
 * `data-theme`, `data-density`, `data-motion` and `data-level` on `document.documentElement`,
 * then calls {@link applyFirstPaint} at the end of this module.
 *
 * @packageDocumentation
 */

/**
 * Applies the stored appearance before the first paint; never throws once implemented.
 *
 * @param root - The element receiving the data attributes (normally `document.documentElement`).
 */
export function applyFirstPaint(root: HTMLElement): void {
  throw new Error(`not implemented: applyFirstPaint (${root.tagName})`);
}

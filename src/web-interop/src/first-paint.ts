/**
 * Entry of the classic `first-paint.js` script (web-interop.md 1), built as an IIFE by
 * `vite build --mode first-paint` and loaded synchronously as the first element of `<head>`. It sets
 * `data-theme`, `data-density`, `data-motion` and `data-level` on the document element before any
 * stylesheet applies (no theme flash, SC-004). The logic lives in `theme.ts` ({@link applyFirstPaint});
 * this entry exports nothing so the minified script stays below 1 KB.
 *
 * @packageDocumentation
 */
import { applyFirstPaint } from './theme';

applyFirstPaint(document.documentElement);

# Hand-off: audio-engine / 006 polish (T059)

## What was built

Final state of the 006 engine work (contract 2.1.0), after T025/T026, T035/T036, T051/T052:

- `src/frame-loop.ts` (shared rAF loop, meters on it), `src/karaoke/{clock,timeline,cursor,dom,layout,ball,motion,view,index}.ts`,
  engine methods `attachKaraoke` / `setKaraokeOffset` / `detachKaraoke`, `Transport.positionAt`.
- Polish: explicit `HOST_ATTRIBUTES` list for detach, readonly `SIZE_ATTRIBUTES`, package
  description; tests share `tests/mocks/frames.ts` (`FakeFrames`) instead of a copy in
  `meters.test.ts`, and the duplicated `browserFrames` test was dropped from `frame-loop.test.ts`.
- TSDoc on every exported symbol (ESLint `jsdoc/require-jsdoc` clean).

Commits (all on this branch): `2e1a394` test T025 -> `ba05d6a` feat T026 -> `0ced669` HO;
`c44348d` test T035 -> `9af3d9c` feat T036 -> `e808ab1` HO; `d5d3cf6` test T051 -> `c5a787d`
feat T052 -> `3ea1bd7` HO; `924ae53` refactor T059 + this hand-off.

## Files changed (this step)

- `src/audio-engine/src/karaoke/{dom,layout}.ts`, `src/audio-engine/package.json` (description only)
- `src/audio-engine/tests/{meters,frame-loop}.test.ts`
- `specs/006-synced-lyrics-karaoke/tasks.md` (T059 ticked)

## Tests and coverage

```text
npm run test:coverage   20 files, 287 passed (207 before 006)
Statements 100% (921/921)  Branches 100% (357/357)  Functions 100% (308/308)  Lines 100% (884/884)
```

## Quality checks

```text
npm run lint / npm run typecheck        clean
npm run build                           wwwroot/js/audio-engine.js 67.7 kB (gzip 20.1 kB)
sig_check.py src/audio-engine/{src,tests}  0 errors; production code 0 warnings (test-unit size warnings only)
dependency_check.py                     0 errors, 0 warnings
duplication.py                          0 clones in production code (one pre-existing-style test helper clone: loop.test.ts / mocks/transport-rig.ts)
```

No `setTimeout`/`setInterval` in `frame-loop.ts` or `karaoke/` (the existing metronome wake-up
timer and the 10 Hz .NET position reports are unchanged); no per-frame interop.

## Dependencies added

None (no npm change).

## Open issues / requests

- **architect** (contract text, `audio-engine.md` 2.1.x): (1) latency subtracted before the loop
  wrap; (2) the "Skeleton (Phase 0)" paragraph and `KARAOKE_NOT_IMPLEMENTED` can be removed;
  (3) the DOM details in `audio-engine-006-us1.md` (current slot before the first line,
  `.karaoke-countdown[data-count]` with three `.karaoke-dot`, `translate3d(x, y, 0px)`, word
  starts non-decreasing across lines, `Invalid karaoke options` for a negative `countdownGapMs`);
  (4) the `MutationObserver` on the host `class`/`data-size` for size changes.
- **frontend**: see `audio-engine-006-us1.md` and `audio-engine-006-us3.md` (ball CSS
  positioning, size class / `data-size`, reduced-motion styling, attach after `loadTrack`).
- **qa**: `data-position-ms` = karaoke clock (offset applied); with the ResizeObserver the ball is
  placed in the same rendered frame after a line change.

## Next owner

frontend (006 T037/T038, T053/T054), qa (T039, T055), CTO (merge, 006 T064).

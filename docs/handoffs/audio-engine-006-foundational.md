# Hand-off: audio-engine / 006 foundational (T025, T026)

## What was built

- `src/frame-loop.ts` - `FrameLoop`: one `requestAnimationFrame` loop per engine. `add/remove`
  (continuous subscribers: the first starts the loop, the last stops it), `once` (exactly one
  frame, deduplicated with continuous subscribers in the same frame), paused while
  `document.visibilityState !== 'visible'` (pending one-shot frames are drawn when the page is
  visible again), `dispose`. `FrameScheduler`/`browserFrames` moved here (re-exported from
  `meters.ts`, so the existing meter tests are unchanged).
- `src/meters.ts` - draws on the shared loop (`MetersOptions.loop`; a private loop over
  `frames` when none is injected). Behaviour unchanged.
- `src/karaoke/clock.ts` - `KaraokeClock.positionMs()` =
  `round(1000 * transport.positionAt(audioNow - outputLatency)) - offsetMs`;
  `audioNow` = `getOutputTimestamp()` extrapolated with `performance.now()` (fallback
  `currentTime` when the method is missing or returns zeros); `outputLatency` -> `baseLatency` -> 0;
  `validateOffset` (-5000..5000, `Error("Karaoke offset out of range: <value>")`).
- `src/transport.ts` - `positionAt(time)` (the ADR-005 formula at an arbitrary context time,
  stored position when not playing); `position()` = `positionAt(currentTime)`.
- `src/engine.ts` - one `FrameLoop` per engine, meters on it, disposed with the engine.

Red -> green: `2e1a394` (failing tests) -> `ba05d6a` (implementation).

## Files changed

- `src/audio-engine/src/{frame-loop.ts (new), karaoke/clock.ts (new), meters.ts, transport.ts, engine.ts}`
- `src/audio-engine/tests/{frame-loop.test.ts, karaoke/clock.test.ts, mocks/frames.ts, mocks/transport-rig.ts}` (new), `tests/mocks/audio-context.ts` (+ `getOutputTimestamp`, `outputLatency`, `baseLatency`)
- `specs/006-synced-lyrics-karaoke/tasks.md` (T025, T026 ticked)

## Tests and coverage

```text
npm run test:coverage   13 files, 231 passed
Statements 100% (628/628)  Branches 100% (225/225)  Functions 100% (229/229)  Lines 100% (596/596)
```

Drift check: 1,000 frames with 16.7 +- 5 ms jitter, output timestamp updated per 128-sample
quantum: worst error 0.5 ms (rounding only, no growth).

## Quality checks

- `npm run lint` clean, `npm run typecheck` clean, `npm run build` OK (45.7 kB).
- `tools/quality/sig_check.py src/audio-engine/src src/audio-engine/tests`: 0 errors, no warning
  in production code (test-unit size warnings only).
- No `setTimeout`/`setInterval` in the frame loop or the clock.

## Dependencies added

None.

## Open issues / requests

- **Interpretation (architect, please confirm or adjust the contract text)**: the latency is
  subtracted from the audio time *before* the loop wrap (`positionAt(audioNow - latency)`),
  so the banner wraps when the wrap is heard; the contract formula subtracts after wrapping,
  which would show `loopStart - latency` for a moment while the loop end is still audible.
  Identical everywhere else. Paused/stopped: stored position minus the offset (no latency).
- Note: Chrome's `getOutputTimestamp().contextTime` is already the time at the output; the
  contract additionally subtracts `outputLatency`. Implemented as specified; the e2e landing test
  (T039) will show whether a constant bias remains (the user offset covers it).

## Next owner

audio-engine (T035/T036, same branch); quality-gate T009 (`transport.ts`/`mixer.ts` do not
import `karaoke/` - true: only `engine.ts` will).

# Hand-off: audio-engine, round 014+015 E2E fixes (r14-fix-eng)

Branch `r14-fix-eng` from `014-015-round` (5e5d683f), 2026-11-18. `types.ts` is unchanged. No new dependency.

## 1. Stereo takes were digital silence (`recording-family-drums/keys.spec.ts`)

**Root cause (reproduced in real Chromium):** the E2E stack runs the web with
`StemStudio__Recording__TestLoopback=true`, so the input is the engine's loopback stream, not the fake capture
device. `loopbackStream()` returned the same `MediaStreamAudioDestinationNode.stream` on every open. The input panel
opens `mono-1` first; choosing `stereo` reopens the input, and `InputPort.close()` stops every track of the old stream.
That stopped the loopback track itself, so every later open captured silence. Mono families open only once, which is
why they passed. The channel count was never the problem: the stereo capture worklet, receiver, mapping and WAV
encoder are correct.

**Fix:** `src/recording/surface.ts` `loopbackStream()` now returns `this.loopback.stream.clone()`. The destination node
is still created once, and the calibration clicks still feed it. Each open gets its own tracks, and closing an input
stops only those.

**Tests:**

- Red, then green: `tests/recording/calibrate-engine.test.ts` "a reopen (mono-1 -> stereo) gets a live loopback
  stream".
- Regression guards: `tests/recording/engine-recording.test.ts` "stereo capture". These cover both channels, left
  only, right only, and mono-1 -> stereo reopen, each checked per channel in the 24-bit WAV.
- `tests/mocks/media.ts`: `FakeTrack.clone()` and `FakeStream.clone()`.

**Browser probe** (Playwright Chromium with the fake device, built bundle, outside the repo): loopback with mono-1 ->
stereo reopen gave a peak of 0 before the fix and -3.1 dBFS after it. The fake device was already fine with and
without a reopen.

## 2. Score follower at 50 % shows the wrong bar (`score-follow.spec.ts`, speed case)

**Not reproduced.** No engine change was made.

New test `tests/score-follow-speed.test.ts` drives the full engine (practice rig, fake stretch stage). It sets 50 /
100 / 120 %, seeks into bar 2, plays 3 s and pauses. The shown `data-bar-index` equals the bar of `getPosition()`
both while playing and after the pause. It is green on the unchanged code.

The code paths agree with this:

- The follower reads `transport.positionAt(heardTime)`. The UI's `transport-position` reads
  `transport.position()`. Both go through the same anchor queue.
- After a pause both return `pausedAt`, and the follower always draws one frame on the state change.
- A Chromium probe at 100 % showed `data-position-ms` tracking `getPosition()` within ~0.1 s. 50 % could not be
  probed outside Docker, because `soundtouch.wasm` is built only in the image.

So an engine-side mismatch is not possible after the pause. These need checking in the failing run's trace:

1. `data-position-ms` of the score host next to `transport-position`. If they agree but the bar differs, the
   follower was attached with a different view than the spec's `score.view`, for example another score of the same
   reused track, such as the range score of scenario 5.
2. Whether the poll after the pause saw the final redraw.

## Verification

All logs are in the lane scratchpad.

| Check | Result |
|---|---|
| `npx vitest run --coverage` | 798 tests passed, 54 files. Lines 99.83 %, branches 97.99 %, functions 99.9 %, statements 99.86 %. `surface.ts` is at 99.36 % lines, 92.59 % branches. |
| `npx tsc --noEmit` | Clean |
| `npx eslint . --max-warnings 0` | Clean |
| `npm run build` | Three entries |
| `uv run python tools/quality/run_all.py` | PASS, 0 errors. The jscpd step ran (434 clones). The new or changed engine units add no errors; their warnings are test-file unit size and clones only. |

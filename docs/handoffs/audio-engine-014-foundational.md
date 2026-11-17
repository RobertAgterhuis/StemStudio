# Hand-off: audio-engine, 014 HO:foundational (T042-T043, `OnLoopPass`)

Lane L1, branch `014-015-round-lane-l1-engine`, 2026-11-17. Contract 2.4.0 "Loop passes" (merged in 2.5.0);
`types.ts` unchanged.

## Commits

| Commit | Task |
|---|---|
| `9d84b4b2` | T042 failing tests `tests/loop-pass.test.ts` |
| `a8d9a4d6` | T043 `src/loop-pass.ts`, wiring in `engine.ts`, `callbacks.ts`, `metronome/scheduler.ts`, `karaoke/clock.ts` |

## Behaviour (what L5's recorder can rely on)

- `OnLoopPass({ start, end, pass, speedPercent })` fires once per completed pass of an **enabled** loop, at the
  moment the wrap is heard. "Heard" uses the karaoke clock's time base: `getOutputTimestamp()` extrapolated, minus
  `outputLatency` (new shared helper `heardTime` in `karaoke/clock.ts`).
- The engine counts passes from the transport's heard anchors, not from timer deltas. Native loop anchors count
  `1 + floor((u - end) / (end - start))` wraps, where `u` is the unwrapped track time. Explicit trainer or count-in
  passes count one wrap each, at `until`. The frame loop and the scheduler tick (new `onTick` hook, which runs while
  playing whether the metronome is on or off) both observe. One wrap is never reported twice. When several wraps
  happen between two observations (for example a hidden tab with throttled timers), each one is reported with its
  own pass number.
- `speedPercent = round(rate * 100)` of the anchor in effect at the wrap: the speed of that pass. For the trainer,
  each pass reports its own speed (tested 50 / 60 / 70). Live speed changes are also tested.
- These reset `pass` to 0: `seek`, `stop`, `loadTrack`, and a `setLoop` with a **different** region (or enabling or
  disabling the loop). When the position after a reset is not the loop start, the partial pass up to the next wrap
  is **not** reported. Starting exactly at A gives a full pass. Re-sending the same region does not reset.
  Pause/play continues the count.

## Tests and coverage (engine, `npm run test:coverage`, full suite at this point)

| Component | Tests | Lines | Branches | Functions | Statements |
|---|---|---|---|---|---|
| audio-engine | 538 passed | 99.94 % | 99.87 % | 100 % | 99.94 % |

`tsc --noEmit`, `eslint . --max-warnings 0` and `npm run build` are clean.

## Decisions

- D1: the same region sent again with `setLoop` is not a "loop change" (Blazor re-sends the region on unrelated
  updates). The contract reads "a seek or loop change resets", so this narrows it. Flagged here for the CTO.
- D2: a pause has a small window: a wrap between the last observation and the pause (at most one frame or tick) is
  not reported. This is accepted. The contract only promises "never twice".

## Deviations, open items, blockers

None. No new dependency.

# Hand-off: audio-engine, 015 HO:polish (T045-T046, T052-T053, T059-T060, T073, T077-T078; T086)

Lane L1, branch `014-015-round-lane-l1-engine`, 2026-11-17. Contract 2.5.0 is complete in the engine. `types.ts` is
unchanged and `src/recording/pending.ts` is removed. Earlier checkpoints: `audio-engine-015-foundational.md` and
`audio-engine-015-us1.md`.

## Commits since HO:us1

| Commit | Task |
|---|---|
| `8a94c6e5` / `34aa9823` | T045 / T046 `calibrateLatency` (`recording/calibrate-run.ts`) and `testInput: 'loopback'` |
| `9a10ad30` | T052 / T053 loop recording. The tests were green on the US1 code, so no code change was needed. |
| `d794a358` / `d731a7a7` | T059 / T060 `setTakeCompare` |
| `d84bcc78` | T073 render-vectors 1.1.0 consumer. The take cases are green without a gate change. |
| `e1831241` / `b7613750` | T077 / T078 `setTakeAdjustment`, plus the re-encoded span of the loaded take |

## Contract text to clarify at integration (approved by the orchestrator, 2026-11-17)

1. **`captureWav(<takeId of the loaded take>)`** returns the loaded take re-encoded as a 24-bit WAV for the start
   `max(0, startSample + adjustmentSamples)`. The start is clamped at song sample 0: a shift before the song start
   drops the leading samples. This is the "engine re-encodes the span" of plan step 8 / data-model 2.4 for
   `PUT /api/takes/{id}/audio`. `captureWav(<captureKey>)` still returns the local capture.
2. **`setTakeAdjustment(samples)`** sets the adjustment absolutely, relative to the loaded `startSample` (not as a
   delta). It re-plans at `currentTime + 0.01`, validates `-8820..8820` integer samples, and does nothing without a
   loaded take.
3. From 014 (see `audio-engine-014-foundational.md`): a `setLoop` with an identical region is not a loop change for
   `OnLoopPass`.

## Behaviour added in this part

- `calibrateLatency(options?)` refuses with these errors:
  - `Stop the recording first` while a session runs
  - `A calibration is running`
  - `Open an input first`
  - `Pause playback to calibrate`
  - `Invalid calibration options: ...`

  While it runs it forces monitoring off and mutes every channel through the extra gate, and it locks recording,
  the input and a second calibration (`A calibration is running`). It plays the seeded chirp clicks on the
  destination at `t0 + c_k` (`t0 = currentTime + 0.1`), captures `[t0, last click + 1 s)`, and returns
  `estimateLatency(...)` plus `outputLatencySamples`. After the expected time plus 2 s it gives up waiting and
  evaluates what arrived (`noSignal` and similar), so it never throws for a failed measurement.
- `testInput: 'loopback'` (E2E, R-13): a `MediaStreamAudioDestinationNode` is created once and fed by the master
  tap (`Mixer.tap()`) and the calibration clicks. `openInput` uses its stream instead of `getUserMedia`, and
  `InputInfo.deviceId` is `loopback`.
- `setTakeCompare(mode)`: `Invalid compare mode: <m>`. The gate is swapped at `currentTime` with the 3 ms time
  constant (<= 10 ms). User mute/solo flags are untouched and expanded detail channels are covered. With part
  `none`, `take` mode leaves every stem playing.
- Loop recording, verified at engine level: one take per pass cut to [A, B); the count-in before every pass is not
  captured (explicit passes); a partial last pass is kept from 1 s and marked `partial`; the session stops after 50
  passes or at `maxSeconds`; a loaded loop take is re-scheduled in every pass and never looped.

## Tests and coverage (engine, full suite)

| Component | Tests | Lines | Branches | Functions | Statements |
|---|---|---|---|---|---|
| audio-engine | 790 passed | 99.80 % | 97.99 % | 99.80 % | 99.86 % |

Files under 90 % branch coverage, all within the component gate: `calibrate-run.ts` 85.7 %, `take-channel.ts`
87.5 %, `takes.ts` 88.9 % and `score-follow/follower.ts` 87.5 %. The uncovered branches are defensive `??`
fallbacks. All of these are clean:

- `tsc --noEmit` and `eslint . --max-warnings 0`
- `npm run build`, with three entries
- `tools/quality/run_all.py`: PASS, 0 errors, 374 clones in total. The jscpd step ran. In the engine there are
  only 12 warnings: unit size, and clones between test files.
- `dotnet format StemStudio.slnx --verify-no-changes`

## Security self-review (whole 015 engine part)

- The saved-take span of `loadTake` is validated before the fetch (hostile cases tested).
- `setTakeAdjustment` is range-checked. The re-encoding only drops samples; it never reads outside the buffer.
- Worklet messages are type-checked. Capture memory is capped by `maxSeconds` (at most 1800 s).
- The microphone is opened only by `openInput`, and released by `closeInput`, `dispose` and a lost input.
  Monitoring and calibration clicks go to the destination only (and to the loopback node in E2E mode), never into
  a take.

## Open items

- The T086 row is shared by six owners, so the lane has not ticked it; the engine part is done.
- 015 T007 (quality-gate rule): `src/recording/*` is imported only by `engine.ts` and by other files in the folder
  (`surface.ts` composes it). It uses `callbacks`, `mixer`, `stem-loader`, `transport` and `metronome/timeline`
  from the engine root. That fits the rule as written.

No new dependency and no deviations beyond the clarifications above.

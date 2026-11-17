# Hand-off: audio-engine, 015 HO:us1 (T028-T035)

Lane L1, branch `014-015-round-lane-l1-engine`, 2026-11-17. Contract 2.5.0; `types.ts` unchanged. The Phase 0
skeleton `PendingRecording` is replaced by `src/recording/surface.ts`.

## Commits

| Commit | Task |
|---|---|
| `09403a06` / `d6f2a986` | T028 / T029 `recording/{environment,support,input,monitoring}.ts` |
| `323d449e` / `65921cd7` | T030 / T031 `recording/{capture-worklet,capture}.ts` (third Vite entry already existed) |
| `1061be35` / `185d126c` | T032 / T033 `recording/{recorder,record-options,takes}.ts` |
| `60b28885` / `f5b93dba` | T034 / T035 `recording/{take-channel,surface}.ts`, `engine.ts`, `callbacks.ts`, `practice/controller.ts` |
| `51c1f7fb` / `8148ffc1`, `5a665759`, `92ba3ea9` | security self-review (hostile take source) and refactors |

## What works now (for L6, frontend 015)

| Method | State |
|---|---|
| `recordingSupport`, `listInputDevices`, `openInput`, `closeInput`, `setMonitoring` | done |
| `startRecording`, `stopRecording`, `captureWav`, `releaseCapture` | done |
| `loadTake` (capture key or URL), `unloadTake`, `setChannel('take')`, `attachMeter('take' \| 'input')` | done |
| `calibrateLatency` | still `not implemented: calibrateLatency` until US2 (T046), together with `testInput: 'loopback'` |
| `setTakeCompare` | still `not implemented` until US4 (T060). The gate plumbing exists already. |
| `setTakeAdjustment` | still `not implemented` until US7 (T078). `loadTake({ adjustmentSamples })` already applies and validates it. |

Callbacks: `OnRecordingStateChanged`, `OnRecordingProgress` (250 ms timer, at most 4 Hz), `OnTakesCaptured`,
`OnInputLost('ended' | 'processorError')` and `OnInputDevicesChanged`.

Error messages (each starts with a code where the contract defines one):

- `openInput`: `permissionDenied:`, `notFound:`, `busy:`, `modeUnavailable:`, `unsupported:`. A rate mismatch
  (`NotSupportedError` from `createMediaStreamSource`) is `unsupported: ... 44.1 kHz`, and afterwards
  `recordingSupport().reasonCode` is `rateMismatch`.
- `startRecording`: `Invalid record options: <field> ...`, `A recording is already running`, `Open an input first`,
  `Load a track first`, `recordingSpeed: Recording works at 100 % speed without transpose.` and
  `speedTrainer: Switch the speed trainer off to record.`
- While a session runs (countIn, recording or finishing), `seek`, `setLoop`, `setPractice`, `loadTrack`,
  `openInput` and `closeInput` throw `Stop the recording first`.
- `loadTake`: `Invalid take adjustment: <v> (-8820..8820 samples)`, `Invalid take source: startSample | lengthSamples
  | channels`, `Unknown capture "<key>"`, and the stem-loader messages for the stem named `take` (for example
  `Failed to load stem "take": HTTP 404`). `captureWav` of an unknown key throws `Unknown capture "<key>"`;
  `releaseCapture` of an unknown key does nothing.

## Decisions (for the CTO)

- D1: `stopRecording`, and every other way a session ends, **pauses** the transport, so the position is kept.
  The contract says "Stops the transport". The end of the song stops it as usual.
- D2: a user `pause()` or `stop()` during count-in or recording ends the session. Play, pause and stop are not
  locked, because the contract does not list them.
- D3: `startRecording` while playing first pauses, then starts from the paused position with the count-in.
- D4: count-in bars for the session go through the new internal `PracticeController.overrideCountIn(bars | null)`.
  It replaces the stored bars, keeps `everyPass`, and is restored when the session ends. This is the Phase 0
  request 4 hook; no public API changes.
- D5: the state goes from `countIn` to `recording` when the 012 count-in is no longer active and the music start
  is heard. Without a usable tempo, the session starts in `recording`.
- D6: the tail wait ends when the capture reaches `stopFrame + L`, when the session limit is reached, or after the
  expected time plus 1 s, whichever comes first. A lost input therefore still returns its partial take.
- D7: take playback is planned per heard segment (`segmentsFor`). A take is never looped: each loop pass gets its
  own source, planned up to 64 passes ahead and extended by a 250 ms wake-up. A live rate change or a new explicit
  pass splices at the newest anchor. Seek, pause, stop, load and unload re-plan from `currentTime + 0.01`.
- D8: `loadTake` keeps the compare mode; `unloadTake` and a new track reset it to `both`. A new track unloads the
  take.
- D9: the `take` channel is created lazily by `setChannel('take')`, `attachMeter('take')` and `loadTake`, so
  without them the graph is exactly the 2.4.0 one. `CapturedTake.peakDbfs` is rounded to 0.01 dB with a floor of
  -120. `clippedSamples` counts samples with `|x| >= (2^23 - 1) / 2^23`.

## Tests and coverage (engine, full suite)

| Component | Tests | Lines | Branches | Functions | Statements |
|---|---|---|---|---|---|
| audio-engine | 717 passed | 99.85 % | 98.02 % | 99.89 % | 99.88 % |

Files under 90 % branch coverage, all within the component gate: `recording/surface.ts` 84.6 %,
`take-channel.ts` 87.5 % and `takes.ts` 88.9 %. The uncovered branches are defensive `??` fallbacks.

`tsc --noEmit`, `eslint . --max-warnings 0` and `npm run build` are clean; the build emits
`audio-engine-capture-worklet.js`. `tools/quality/run_all.py`: PASS, 0 errors; the jscpd step ran. Unit-size
warnings only (16-19 NLOC, error limit 30). `dotnet format StemStudio.slnx --verify-no-changes`: clean.

## Security self-review

- **Untrusted input: the saved-take span** passed to `loadTake` (`startSample`, `lengthSamples`, `channels`). It
  is now validated before any fetch: integer start >= 0, length 1..30 min, 1 or 2 channels. Hostile cases are
  tested. Without this, a huge length would allocate an oversized `AudioBuffer`.
- Decoding is left to `decodeAudioData`, through the existing stem-loader. Slicing outside the decoded buffer reads
  zeros.
- Worklet messages are type-checked (`isBlock`); other messages are ignored. The session limit caps memory at
  1800 s.
- The microphone is opened only through `openInput`, and `closeInput`/dispose stop every track. Monitoring goes
  straight to the destination and is never captured.

## Open items

- US2 adds calibrateLatency and the loopback input (T045-T046).
- US3 covers loop handling (T052-T053). The mapping, `maxPasses` and the per-pass take replay already exist, so
  T052 is mostly tests.

No deviations and no new dependency.

# Hand-off: audio-engine, 015 HO:foundational (T012-T019)

Lane L1, branch `014-015-round-lane-l1-engine`, 2026-11-17. Contract 2.5.0. `types.ts` is unchanged and the public
API has not changed: all of this is internal modules for US1 (T028-T035).

## Commits

| Commit | Task |
|---|---|
| `286cfbe5` / `29588d56` | T012 / T013 `src/recording/wav.ts` |
| `e52e930c` / `c2c751d5` | T014 / T015 `src/recording/{mapping,capture-buffer,segments}.ts`, transport anchor sink |
| `150433af` / `4cb70b45` | T016 / T017 `src/recording/calibration.ts` |
| `d098994f` / `9486f26a` | T018 / T019 `src/recording/compare.ts`, `mixer.ts` (take channel, extra gate) |

## What exists now

- `encodeWav24(channels, sampleRate)`: canonical 44-byte PCM header, 24-bit little-endian, mono/stereo
  interleaved. `quantise24 = clamp(round(x * 2^23), -2^23, 2^23 - 1)`, where `round` is `Math.round` (half up),
  NaN maps to 0, and -0 is normalised to 0.
- `CaptureBuffer`: stamped worklet batches on the context-frame timeline. A gap between batches reads as zeros and
  counts as one gap. Overlapping frames are ignored.
- `SegmentLog` plus `Transport.setAnchorSink` (internal). Every new heard anchor (start, explicit pass, live
  change) goes to the log while it is attached, at most 51 entries. Pruning in the transport no longer loses them.
- `mapCaptureToTakes`: `songSample(f) = heardSampleAt(f - L)` per segment. Native loop gives one piece per wrap.
  An explicit pass is one pass that ends at the loop end; its rounded `untilFrame` is not trusted for the length.
  Count-in gaps are dropped. A take is cut to [A, B). A partial pass under 1 s is dropped. Without a loop there is
  one take up to the stop or the end of the track (`partial` only when `interrupted`). Passes are numbered by the
  transport, so a pass the capture missed keeps its number. `P` is not added: it is already in the heard anchors
  (P0-8). The recorder reports `latencySamples = L + P` (US1).
- `estimateLatency`: seeded mulberry32 click schedule (0.5 s lead-in, gaps 0.45-0.75 s), 5 ms log chirp
  500 Hz -> 8 kHz with a Hann window at -6 dBFS, and the R-04 thresholds (>= 0.3, >= 6x median, 6 clicks and 75 %,
  spread <= 44, lag <= 0.8 s, noSignal < -60 dBFS, clipped >= -0.1 dBFS). Failures are checked in this order:
  noSignal, tooFewClicks, tooLong, inconsistent. `validateCalibrationOptions` throws
  `Invalid calibration options: ...`.
- `compareGate`, `recordingGate`, `takeGates`, and `Mixer.setExtraGate(gate, at)`, where the channel gate is
  `levelGate * extra`, ramped at one time. `Mixer.ensureTake()` creates the `take` channel lazily:
  `setChannel('take')` and `analyser('take')` create it, `has('take')` does not. Without it the graph is exactly the
  2.4.0 one. `build()` (new track) removes it.

## Decisions (for the CTO)

- D1 (calibration search): the coarse lag comes from the normalised cross-correlation on a 4x decimated **lag
  grid**, summed over all clicks. Research R-04 describes a "4x decimated envelope" per click instead. The summed
  grid picks one lag for the whole irregular schedule, so a 120 ms echo, the next click inside the window and
  latencies near 0.8 s cannot win. Each click is then refined at full rate within +-64 samples, as R-04 says. The
  search range is 1.0 s (the capture tail) instead of 0.8 s, so `tooLong` can be detected at all. The thresholds
  are unchanged. All R-04 vectors pass.
- D2 (noise vectors): "-20/-40 dB" is read as white-noise RMS at -20/-40 dBFS against the -6 dBFS chirp.
- D3: silence is reported as -120 dBFS (`SILENCE_DBFS`; JSON cannot carry `-Infinity`).

## Tests and coverage (engine, full suite)

| Component | Tests | Lines | Branches | Functions | Statements |
|---|---|---|---|---|---|
| audio-engine | 610 passed | 99.95 % | 99.66 % | 100 % | 99.95 % |

`tsc --noEmit` and `eslint . --max-warnings 0` are clean. `tools/quality/run_all.py`: PASS, 0 errors; the jscpd
step ran with 364 clones in total, and the only engine clones are test-only warnings. Unit-size warnings:
`createParts` 16 NLOC and `writeHeader` 16 NLOC; both are warnings, below the error limit of 30.
`dotnet format StemStudio.slnx --verify-no-changes`: clean.

## Security self-review

No untrusted input is parsed yet: the capture is the user's own microphone and the WAV is written by the engine.
Decoding of downloaded takes (`loadTake` by URL) comes in US1; its fetch and decode go through the existing
stem-loader.

## Deviations, open items, blockers

No contract deviation and no new dependency. The `skeleton.test.ts` "not implemented" cases stay until US1 replaces
`PendingRecording`.

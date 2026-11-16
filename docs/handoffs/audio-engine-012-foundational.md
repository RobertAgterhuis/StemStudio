# Hand-off: audio-engine / 012 HO:foundational (T013-T024)

Branch `012-lane-engine` (worktree `.claude/worktrees/012-engine`), from `012-practice-tempo`; `012-lane-devops`
merged in (46cedb7c) for `scripts/build-soundtouch.*`.

## What was built

- `src/stretch/constants.ts`, `params.ts` (T013/T014): D = 5120, overflow 1024, `c(p) = round(-1100 * log2 p)`
  (never -0), `L(p) = (D + c(p)) / sampleRate`, pitch factor `2^(st/12) / (speed/100)`, neutral detection,
  `CHANGE_LEAD_SECONDS` 0.02, SoundTouch settings of R-04, processor name.
- `src/stretch/fifo.ts` (T015/T016): stereo ring FIFO with fixed delay D, underrun = silence + counter, above
  D + 1024 the oldest frames are dropped back to D (counted), `reset()` refills D silence, no allocation per call.
- `src/stretch/{abi,core,processor}.ts` (T017/T018): ABI v1 exports type, `instantiateSoundTouch` (no imports,
  `_initialize` once), `SoundTouchStream` (buffers allocated once), `StretchCore` (midpoint rule P7, also inside a
  block: the block is split at the frame where `(in + out) / 2` reaches the change), processor with the message
  protocol, `ready`/`error`/`stats` (<= 4 Hz, only on change). Second Vite build `vite.processor.config.ts` ->
  self-contained `wwwroot/js/time-stretch-processor.js` (14 KB, no imports); `npm run build` runs both.
- `third_party/soundtouch/shim/stemstudio_soundtouch.cpp` (T019/T020): ABI v1 over `soundtouch::SoundTouch`,
  `std::nothrow`, argument checks, `aligned_alloc(16)`; every function <= 15 lines. `tests-wasm/` Node suite
  (`npm run test:wasm`, `vitest.wasm.config.ts`), skipped with a message when the module is missing, **fails**
  with `SOUNDTOUCH_WASM_REQUIRED=1` (CI name from devops T011).
- `src/anchors.ts`, `src/transport.ts`, `src/metronome/{timeline,scheduler}.ts` (T021/T022): anchor `{when,
  offset, rate?, until?}` (rate omitted at 1 so 2.1.0 anchors stay `{when, offset}`), a heard and a source-side
  anchor queue, `setTiming`, `retime` (playbackRate automation at U on every source; a change before U replaces
  the pending one), `restart`, `play(at?)`, `startPosition`, restart hook; late detail sources start on the
  source-side anchor with pending automation; clicks mapped `/ rate`, queue split per anchor, `until` gaps silent.
- `src/stretch/{worklet-stage,fake-stage}.ts` (T023/T024): module + bytes once per engine (a failure is retried),
  `Time stretch unavailable: <reason>` (no AudioWorklet, HTTP status, processor error message, processorerror),
  `FailureAware.onfailure` for errors after ready.

## Tests and coverage

```text
npm run lint / npm run typecheck        clean
npx vitest run --coverage               28 files, 392 passed
  Statements 100 % (1292/1292)  Branches 100 % (480/480)  Functions 100 % (438/438)  Lines 100 % (1237/1237)
bash scripts/build-soundtouch.sh        module ok: no imports, ABI 1, 13 exports; 30,271 B
  sha256=4b490ba924ee2e917054bb9c71de0b30a67eb28b98a8b6520ef5e43a4805585f
SOUNDTOUCH_WASM_REQUIRED=1 npm run test:wasm   26 passed: pitch within 1 % at 0.5/1/2, identity after D < 1e-4,
  initial latency <= 4864 at 8 factors, lag <= 4864 at 8 constant factors and over 100 jumps (max 4,775),
  content offset 17.5 ms at 0.5 (c 24.9) and -33.1 ms at 2 (c -24.9), throughput 284x (0.42) / 70x (4)
```

All 291 pre-012 tests are unchanged and green.

## Decisions / deviations

- FIFO overflow drops back to D (restores the fixed delay) rather than to D + 1024.
- The content offset sign: the suite measures `output onset - input onset`; the content of input frame `n`
  lands near SoundTouch output frame `n + c(p)`, which is what `L(p) = (D + c(p)) / sampleRate` needs.
  audio-engine.md "Content offset" says `n - c(p)` (typo) -> architect request below.
- Max output lag in my jump schedule is 4,775 frames (Phase 0: 4,736); still 345 frames below D.

## Dependencies added

None (no npm package; SoundTouch and emsdk as recorded by the architect).

## Docs impact

None user-visible yet.

## Open issues / requests

- architect: audio-engine.md "Content offset": "near SoundTouch output frame `n - c(p)`" should read `n + c(p)`.

## Next owner

audio-engine (US1: T033/T034).

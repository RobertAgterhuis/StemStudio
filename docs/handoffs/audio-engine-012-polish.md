# Hand-off: audio-engine / 012 HO:polish (T062)

All 23 audio-engine tasks of feature 012 are done (T013-T024, T033-T034, T048-T051, T055-T058, T062) on branch
`012-lane-engine`. Earlier checkpoints: `audio-engine-012-{foundational,us1,us2,us3}.md`. Merged in: `012-lane-devops`
(build script) and `012-lane-quality` (new module rules).

## What was built (summary)

Engine contract 2.3.0 implemented: `setPractice`, `getPracticeStatus`, `OnPracticeChanged`; SoundTouch stage in an
AudioWorklet (`time-stretch-processor.js`, fixed delay D = 5120, midpoint rule, no allocation in `process()`); WASM
ABI v1 shim; rate-aware transport with latency compensation; live changes on one shared time; explicit loop passes
with the speed trainer; count-in before play and every pass. Neutral settings load nothing and use the 2.1.0 graph.

## Tests and coverage

```text
npm run lint                      clean (eslint, SIG-aligned rules)
npm run typecheck                 clean
npm run build                     audio-engine.js 118.8 KB, time-stretch-processor.js 14.0 KB (self-contained)
npx vitest run --coverage         36 files, 487 passed (291 pre-012 tests unchanged)
  Statements 100 % (1813/1813)  Branches 100 % (753/753)  Functions 100 % (602/602)  Lines 100 % (1741/1741)
bash scripts/build-soundtouch.sh  module ok: no imports, ABI 1, 13 exports; 30,271 B,
  sha256 4b490ba924ee2e917054bb9c71de0b30a67eb28b98a8b6520ef5e43a4805585f
SOUNDTOUCH_WASM_REQUIRED=1 npm run test:wasm   26 passed (pitch within 1 %, identity < 1e-4 after D, latency and
  lag <= D - 256 incl. 100 jumps (max lag 4,775), content offset within 25 ms of c(p), 70x-284x real time);
  fails instead of skipping when the module is missing and SOUNDTOUCH_WASM_REQUIRED=1
```

## Quality checks

`tools/quality/run_all.py` (with `012-lane-quality` merged): 0 errors; no warning in `src/audio-engine/src`,
`vite.*` or `third_party/soundtouch/shim` (all units <= 15 LOC, CC <= 5, <= 4 parameters; the two clones reported by
quality-gate were removed: `vite.shared.ts`, tests import `DEFAULT_PRACTICE`). Module rules respected: transport,
mixer, metronome and karaoke import nothing from `practice/` or `stretch/`; the worklet chain imports only
`stretch/{constants,fifo,abi,core}`. TSDoc on every export (eslint `jsdoc/require-jsdoc`).

## Decisions

ADR-021 "Implementation notes" E1-E10 (stage after the master gain, reset on pause, gapless passes in source time,
changes during passes, restarts, no reset between passes, FIFO overflow, count-in tempo, contract text fix).

## Dependencies added

None at run time. Build tool and SoundTouch as recorded by the architect (SoundTouch 2.4.1 LGPL-2.1; emsdk 6.0.13).
The shim `third_party/soundtouch/shim/stemstudio_soundtouch.cpp` is LGPL-2.1-or-later (compiled into the module).

## Docs impact

Technical manual: `npm run build` now writes two files (`audio-engine.js`, `time-stretch-processor.js`);
`npm run test:wasm` (needs `scripts/build-soundtouch.*` first); heard latency 66-147 ms while engaged; master-fader
moves are heard L later while the stage is engaged; channel meters lead the sound by L. User manual: count-in clicks
use the metronome volume; changes of speed while the trainer or every-pass count-in runs apply at the next pass;
changing the loop while the trainer runs restarts playback at the current position.

## Open issues / requests

- architect: audio-engine.md "Content offset": `n - c(p)` should read `n + c(p)` (ADR-021 E10).
- CTO: THIRD-PARTY-LICENSES: shim licence LGPL-2.1-or-later (with SoundTouch); no new npm package.
- frontend: `getPracticeStatus().countIn.available` is evaluated at the stored start position (`startPosition`), so
  it can change with seeks on tracks whose grid ends early.

## Next owner

CTO (integration wave T069-T075).

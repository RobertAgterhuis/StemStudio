# Hand-off: stem-separation + devops + quality-gate (lane L3) / 016 HO:foundational

Branch `016-round-lane-l3` (worktree `.claude/worktrees/r16-l3`), from the 016 design head 7463ac54. Not pushed, not
merged. Tasks: T029-T034 (stem-separation), T009-T010 (quality-gate), T011, T012, T144 (devops). The devops part is in
for the mid-wave checkpoint.

## What was built

| Task | Result |
|---|---|
| T029/T030 | `harmony/vendor/btc/` (BTC-ISMIR19 commit `2682317b`: `btc_model.py`, `transformer_modules.py`, label tables as `labels.py`; MIT `LICENSE`, `NOTICE`; `np.float` -> `np.float64`; no mir_eval/pretty_midi/pandas/pyrubberband/yaml). `harmony/btc.py`: CQT 144/24/hop 2048 at 22,050 Hz, `log(|C|+1e-6)`, chroma from the same CQT, `(x-mean)/std`, zero-padded 108-frame instances, softmax posteriors per instance with progress; `TorchBtcRuntime.load` = `torch.load(map_location, weights_only=True)` inside `torch.serialization.safe_globals([(obj, name) for name in BTC_SAFE_GLOBALS])`; `BtcRecognizer(BtcParts(models, devices, runtime, fallback))` runs through the `DeviceRunner` with `BTC_GPU_NEED`, caches per (checkpoint, device), `release()` for the idle hook; weights unavailable -> the injected chroma-hmm recognizer with `fallback=True`. `harmony/__init__.py` committed early (package marker, for L2) |
| T031/T032 | `views_fake.py`: `FakeChordAnalyzer` (bars on the request grid, else 120 BPM 4/4 from 0.5 s; C G Am F bars 1-8, D A Bm G from bar 9; `full`: sevenths + slash chords; key C major 0.9, D major region from bar 9), `FakeDrumHitsTranscriber` (008 groove from 0.5 s + crash every fourth bar), `FakePitchAnalyzer` (orchestrator rule: 120 BPM from 0.5 s, `FAKE_WALK[k % 14]` by the global beat index, voiced only in the 006 fake lyric lines; `detail-bleed` for `vocals.lead`). `jobs/instrument_analysis_ports.py` (`ChordAnalyzer`, `ChordInputs`, `ChordOutcome`, `InstrumentAnalysisParts`), `jobs/instrument_analysis_chords.py` (`PipelineChordAnalyzer`: mix -> recognizer of the requested/configured engine -> `segments` -> `estimate(frames.chroma, chart.segments, grid)` -> `apply_key`; `fallback-engine` added once). Composition by settings in `main.py` (`create_instrument_analysis_parts`, `ChordModelStore` = the 008 model store below `STEMSTUDIO_MODEL_DIR/harmony`) |
| T033/T034 | `jobs/instrument_analysis_store.py`: `instrument-analysis-jobs/<id>.json` with the discriminated request, restart -> failed "interrupted by restart" (stage cleared, files dropped), delete, 24 h sweep (`LaneJobStore`) |
| T009/T010 | `dependency_check`: `harmony`, `analysis_inputs`, `views_fake` in the layer map; scope `harmony/{postprocess,labels,chroma_hmm,key}.py` -> ports, schemas, each other, numpy, librosa; `harmony/btc.py` -> + torch, `harmony.vendor`; `views_fake` -> only `jobs.instrument_analysis_ports`, `lyrics.{fake,interpolate}`, `transcription.{fake,audio}`; `TS_MODULE_IMPORTERS`: only `index.ts` imports `engine.ts`. New `instrument_rules.py` (in `run_all`, report section "Instrument views"): no `weights_only=False` in `worker/src` (vendor included), no DOM/Web Audio globals in `src/audio-engine/src/pitch/**`. `harmony/vendor/` in `EXCLUDED_PATHS` and `vendor_check.VENDOR_ROOTS`. `docs/quality/SIG-REPORT.md` regenerated |
| T011/T012 | `scripts/check-web-assets.{sh,ps1}`: `/js/audio-engine.js`, `time-stretch-processor.js`, `audio-engine-capture-worklet.js`, `audio-engine-pitch-worklet.js` are 200 `text/javascript`, the pitch module registers `stemstudio-pitch-tracker` (the web image already builds it: `npm run build` includes `vite.pitch.config.ts`; Dockerfile comment). Compose/.env.example: `STEMSTUDIO_CHORD_ENGINE`, `_PITCH_ENGINE`, `_ANALYSIS_THREADS` (empty = worker defaults), checked by jq in the `generator-image` job. CI `CONTRACTS_DIR` = `specs/016-instrument-views/contracts` (Redocly of the four 016 documents, `merge_full.py --check`, `generate_chord_spelling_vectors.py --check`, `pytest $CONTRACTS_DIR`); 015 steps under `RECORDING_CONTRACTS`; `test-all.{sh,ps1}` have the 016 steps |
| T144 | CI E2E split: job `e2e` (matrix chromium/firefox, `npm run test:<browser>`, the stack checks on chromium) and job `e2e-passes` (matrix `recording, practice-flow, restart, agent, lyric, gpu, instrument, fake-mic`, `scripts/ci-e2e-pass.sh <pass>`), each on its own fake stack, `timeout-minutes: 90`, `fail-fast: false`; composite action `.github/actions/e2e-browsers`. Playwright projects `fake-mic-on-pitch`, `fake-mic-semitone`, `fake-mic-silence` (Chromium full build, `--use-file-for-fake-audio-capture=tests/fixtures/audio/<wav>` from L2's T014) |

## Contract for L5 specs (please relay; tags are binding for the projects)

- Instrument specs carry `@instrument` (run by `npm run test:instrument`, Chromium + Firefox, CI pass `instrument`).
- Singing specs with the fake microphone carry `@fake-mic` **and** one of `@fake-mic-on-pitch`, `@fake-mic-semitone`,
  `@fake-mic-silence` (run by `npm run test:fake-mic`, CI pass `fake-mic`); `chromium`/`firefox` projects never run
  `@fake-mic`. `npm test` / `test:chromium` / `test:firefox` exclude `@instrument` and `@fake-mic`.

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| Worker | `pytest --cov --cov-branch` (worker/, own `.venv`) | 2906 passed, 3 skipped, 10 deselected | **100 % line, 100 % branch** (12,646 statements, 1,412 branches; `harmony/btc.py`, `views_fake.py`, `jobs/instrument_analysis_*`, `main.py` 100/100; vendor omitted) |
| tools/quality | `uv run --frozen pytest --cov --cov-branch` | 1005 passed | 100 % line, 100 % branch |
| scripts | `pytest -c scripts/pyproject.toml scripts/tests` (quality env) | 121 passed, 4 skipped (yaml part) | bash checks; `test_ci_e2e_split.py` + `test_check_web_assets.py` with PyYAML: 23 passed |
| e2e | `npx tsc --noEmit`, `npx eslint --max-warnings 0 playwright.config.ts` | clean | - |

## Quality checks

`ruff check`, `ruff format --check`, `mypy --strict` clean (worker 219 files, tools/quality 50 files, scripts tests).
`run_all.py`: **PASS**, 0 errors, 1855 warnings, 447 clones, jscpd ran (its own output: duplication 1.08 %).
`dotnet format StemStudio.slnx --verify-no-changes`: clean (no .NET file changed). `docker compose config -q` for
default, gpu and e2e combinations; jq checks of the 016 env pass. No actionlint on this PC: ci.yml is parsed by
`test_ci_e2e_split.py` (PyYAML) instead.

Security self-review: checkpoint loading only with `weights_only=True` + three allow-listed globals (test: the real
torch refuses an extra global); weights only from the pinned URLs with SHA-256 (008 model store); no subprocess; the
fake analyses read only file lengths; `ci-e2e-pass.sh` evals only its own constant commands (pass names are checked
against the list first).

## Decisions

1. BTC frames use the full-signal CQT at hop 2048 (frame = `CHORD_HOP / CHORD_SAMPLE_RATE`, as `ports` says) instead
   of the upstream 10-s chunking (10/108 s per frame); same features apart from the chunk edges, no drift.
2. The parts factory lives in `main.py` (composition root): `jobs` may not import adapters. Until the mid-wave merge
   the L2 adapters are looked up by module path; a missing one fails its jobs readably (`MISSING_PART_TEXT`). After
   the merge this becomes direct imports (L2 constructors as in its HO:foundational, unchanged).
3. `fallback-engine` is added by the pipeline only when the post-processor did not add it (dedup).
4. Fake melody gated by the fake lyric lines with the global beat index (orchestrator rule); the qa WAVs sing the
   continuous walk, so the singing E2E scores units inside lines.
5. The CI default suite runs per browser; the checks that need the stack once (soundtouch, web assets, key, library,
   backups, lyric scripts) run only in the chromium entry.

## Deviations

None from contracts. `@instrument`/`@fake-mic` leave the default `npm test` (they run as CI passes; the union of the
jobs is today's set plus the two 016 passes, checked by `test_ci_e2e_split.py`).

## Open issues / requests

- Orchestrator: relay the tag rule above to L5 (qa part).
- BTC compute time / memory (T063) follows in HO:us1.

## Dependencies added

None (packages). Vendored: BTC subset, MIT, commit `2682317be668032e6e4b269ded36adaa2ad57df0` (record in
`harmony/vendor/btc/NOTICE`); weights `btc-majmin`, `btc-large-voca` (MIT, downloaded at first use, SHA-256 in `ports`).

## Docs impact

Technical manual: `STEMSTUDIO_CHORD_ENGINE` / `_PITCH_ENGINE` / `_ANALYSIS_THREADS` (compose header, `.env.example`),
BTC weights in `/models/harmony`, `scripts/check-web-assets.{sh,ps1}`, the split CI E2E jobs and `npm run
test:instrument` / `test:fake-mic`.

## Next owner

Orchestrator (mid-wave checkpoint: merge L3 before L5 for the Playwright config); L3 continues with T061-T063.

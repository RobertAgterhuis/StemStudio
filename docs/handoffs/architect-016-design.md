# Hand-off: architect / 016 design phase (Phase 0 T001-T008, ready check, lane plan)

Branch `016-round` (worktree `.claude/worktrees/r16`), from `014-015-round` 29f61140, with `main` 7b5cd39e (014+015
and the fixed mixer dock) merged in. Not pushed, not merged. Lane plan: `docs/handoffs/cto-r16-lane-plan.md`.

## What was built

| Task | Result |
|---|---|
| Ready check | spec/plan/tasks/data-model/contracts checked against the merged 012/014/015/022/023 code; 14 findings fixed (lane plan section 6); tasks.md gained "Phase 0 ready-check amendments" (current code paths per task) and "Acceptance of tasks without a red test"; new task T144 (CI E2E split, LESSONS 014/015) |
| T001 | Placeholders reconciled (DELTA.md "Phase 0 outcome", `audio-engine.md` 2.6.0 reconciliation table); mixer dock (now `position: fixed`) taken into the layout rules |
| T002 | `worker-api.openapi.yaml` **1.9.0** built by `contracts/merge_full.py` (text merge, `--check` = freshness + superset); `schemas.py` (`CONTRACT_VERSION` 1.9.0, analysis jobs/results, score extensions with the 422 rules, `Health.instrumentAnalysis`, lane), `ports.py` (`HarmonicMixer`, `ChordRecognizer`, `ChordFrames`, `ChordPostProcessor` + `PostProcessOptions`, `KeyEstimator`/`KeyEstimate`, `PitchAnalyzer`, `TabFingerer`/`TabAssignment`, `PartWriter`/`PartResult`, errors, BTC weights + `BTC_SAFE_GLOBALS` + `BTC_GPU_NEED`, `TUNINGS`, `WRITTEN_INSTRUMENTS`, `GM_DRUM_MAP`; `NotationSettings`/`ScoreDocument` gained optional 016 fields), `config.py` (`InstrumentAnalysisSettings`); `worker/tests/contract/test_instrument_views_contract.py` (65 tests); older version pins follow 1.9.0 |
| T003 | `backend-api.openapi.yaml` **1.15.0** (same script); `StemStudio.Contracts/InstrumentViews/*` (15 enums, 26 records incl. `TrackAnalysisDto<T>`), score DTOs (`ScoreTablatureDto`, `ScorePartDto`, options/facts records, shared base `ScoreNotationOptions`), `TrackDto.InstrumentViews`/`Key`, `ApiRoutes.InstrumentViews`, `ContractLimits.InstrumentViews` (+ `ScoreProfiles`), `InstrumentViewProblemTypes`; 15 operation ids staged in `BackendContractTests.InstrumentViews.cs`; `InstrumentViewsContractTests`, `InstrumentViewsUiTestIdsTests`; worker example copies `analysis-*.json`; `ManifestBuilder.BackendApiVersion` 1.15.0 (one constant, keeps `ManifestApiVersionTests` green); test links switched (current docs = 016, 014 worker and 015 backend kept as `Contract/014`, `Contract/015`) |
| T004 | `audio-engine.md` **2.6.0**, `types.ts` 2.6.0 (types + 12 members + 2 callbacks + `pitchWorkletUrl`), engine stubs (`src/views/pending.ts`, methods in `engine.ts`), `src/pitch/worklet.ts` (registered silent processor), `vite.pitch.config.ts` + build script, `tests/views/instrument-views-contract.test.ts`; Web `EngineMethods.ContractVersion` 2.6.0 and the two callbacks staged in `EngineContractDriftTests`; `ui-test-ids.md` **1.15.0**; help-content **1.1.0** (`help-topic.schema.json` area `instruments`, linked into Web.Tests); `chord-spelling-vectors.json` **1.0.0** with `generate_chord_spelling_vectors.py --check` and `test_generate_chord_spelling_vectors.py` (5 tests), linked into Domain.Tests |
| T005-T007 | Spikes below; results in research.md Phase 0 outcomes, data-model 1/3.3/7.7, ADR-031..033 Accepted |
| T008 | Verification below |

## Spikes (this PC: 24 cores, 133 GB RAM; no Docker, no GPU run)

| Spike | Result | Decision |
|---|---|---|
| BTC weights (T005) | both checkpoints download from the pinned commit, SHA-256 match; legacy pickles; `torch.load(weights_only=True)` works with exactly `numpy.core.multiarray.scalar`, `numpy.dtype`, `numpy.dtypes.Float64DType`; 3.02 M / 3.04 M parameters | `ports.BTC_SAFE_GLOBALS`; ADR-031 Accepted |
| BTC forward time (T005) | **not measured**: running the downloaded model code was refused by the permission system in the design phase; 5 min = 30 windows of 108 frames, estimate < 2 s CPU | measured in T063 (L3, integration test); `BTC_GPU_NEED` 512 MiB until T140 NVML peak |
| CQT / chroma-hmm (T005) | CQT 144/24/2048 of 5 min 1.31 s; chroma + 25-state HMM 3.72 s | estimate factor chroma-hmm 0.015 s/s |
| Key HMM (T005, R-13) | beta 3, `p_stay` 0.995 per beat, +-8-beat smoothing: C -> D fixture found at bar 8 (1 bar early), back at bar 16, no spurious regions; beta 8 spurious, beta 1 misses | start values for T019/T020 |
| pYIN (T006) | `librosa.pyin` 30 s -> 0.99 s (about 10 s for 5 min); `yin` 5 min 1.23 s | default `pyin`; factors pyin 0.035, yin 0.005 s/s |
| Verovio TAB (T006) | 6/5/4-line TAB staves and notation + TAB render with fret texts; `<capo>` not printed | ADR-033 D6 = native Verovio + "Capo n" direction; no fallback drawing |
| YIN core (T007) | Node 22: 0.019 ms avg / 0.33 ms worst per 128-frame block at 48 kHz (44.1: 0.021/0.21, 96: 0.010/0.18); Chromium 159 headless: 0.017 / 0.30 ms; pitch correct (A3) | SC-004 budget holds ~25x; ADR-032 Accepted. Worklet + fake capture run is in qa's fake-mic projects (T107) |

## Files changed (main paths)

`specs/016-instrument-views/{plan,research,data-model,tasks}.md`, `specs/016-instrument-views/contracts/{DELTA.md,
merge_full.py, worker-api.openapi.yaml, backend-api.openapi.yaml, worker-api.delta.openapi.yaml,
backend-api.delta.openapi.yaml, audio-engine.md, audio-engine.delta.md, ui-test-ids.md, ui-test-ids.delta.md,
help-content.delta.md, help-topic.schema.json, generate_chord_spelling_vectors.py, chord-spelling-vectors.json,
test_generate_chord_spelling_vectors.py}`; `docs/decisions/ADR-03{1,2,3}-*.md`; `worker/src/stemstudio_worker/{schemas,
ports,config}.py`, `worker/tests/contract/test_instrument_views_contract.py` + version pins in 10 worker tests;
`src/StemStudio.Contracts/{InstrumentViews/**, Scores/*, Tracks/TrackDto.cs, ApiRoutes.InstrumentViews.cs,
ContractLimits*.cs, InstrumentViewProblemTypes.cs}`; `src/StemStudio.Api/LibraryCare/Archive/ManifestBuilder.cs`
(version constant); `tests/StemStudio.Api.Tests/{StemStudio.Api.Tests.csproj, Contract/*, LibraryCare/
ManifestApiVersionTests.cs, Worker/Examples/*}`; `tests/StemStudio.Domain.Tests/StemStudio.Domain.Tests.csproj` (vector
link); `src/audio-engine/{src/types.ts, src/index.ts, src/engine.ts, src/views/pending.ts, src/pitch/worklet.ts,
vite.pitch.config.ts, package.json, tsconfig.json, tests/views/*, 5 version pins}`; `src/StemStudio.Web/Audio/
EngineMethods.cs`, `src/StemStudio.Web/wwwroot/js/.gitignore` (worklet output); `tests/StemStudio.Web.Tests/
{StemStudio.Web.Tests.csproj, Audio/EngineContractDriftTests.cs}`; this note and the lane plan.

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| .NET | `dotnet test StemStudio.slnx -c Release --no-build` | Domain 1425/1425, Agent 995/995, AgentEval 178/178, Persistence 167/167, Web 3500/3500, Api 3227/3231 (4 skipped), 0 failed | Contracts project has no gate; no 016 production logic yet |
| Worker | `pytest --cov --cov-branch` (worker/, worktree venv from `uv sync --frozen`) | 2828 passed, 3 skipped, 10 deselected | **100 % line, 100 % branch** (12,184 statements, 1,370 branches) |
| Worker lint | `ruff check`, `ruff format --check`, `mypy` strict | clean; "no issues found in 208 source files" | - |
| Engine | `npx vitest run --coverage`, `tsc --noEmit`, `eslint .` | 55 files, 813 tests passed; tsc, eslint clean | 99.84 % statements, 98.0 % branches, 99.9 % functions, 99.86 % lines (the only uncovered line outside 016 code is 015's `capture-worklet.ts:148`) |
| Spelling vectors | `pytest specs/016-instrument-views/contracts`, `generate_chord_spelling_vectors.py --check` | 5 passed; up to date | - |
| OpenAPI | Redocly 2.78.0 on the four 016 documents | valid, 38 warnings (known 1.14.0/1.8.0 ones + `getInstrumentCatalogue` and the idempotent DELETE without 4XX + 15 delta merge fragments) | - |
| Merge / superset | `merge_full.py --check`; 015 `merge_recording_delta.py --check` | superset yes, up to date; 015 still up to date | - |
| Quality gate | `uv run --project tools/quality --frozen python tools/quality/run_all.py` | **PASS**: 0 errors, 1844 warnings, 442 clones (jscpd ran); two errors found on the way and fixed (`ChordPostProcessor.segments` 5 parameters -> `PostProcessOptions`; 14 duplicated C# lines -> base record `ScoreNotationOptions`) | - |

## Quality checks

`dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format StemStudio.slnx --verify-no-changes`:
clean. Worker and engine lint/typecheck clean (above).

## Dependencies added

None (no package). For `docs/THIRD-PARTY-LICENSES.md` at integration (T141), unchanged from the specs hand-off: vendored
BTC subset (MIT, commit `2682317b`, vendored by L3 in T030) and the weights `btc-majmin` / `btc-large-voca` (MIT,
SHA-256 in `ports.py`, model cache at first use).

## Decisions (rows for `docs/decisions/CTO-DECISIONS-*.md`, copied by the integration)

1. Labels confirmed: worker-api 1.9.0, backend-api 1.15.0, audio-engine 2.6.0, ui-test-ids 1.15.0 (ROADMAP 016 row),
   help-content 1.1.0 (next minor after 004's 1.0.0), chord-spelling-vectors 1.0.0 (new). ADR-031..033 Accepted.
2. 016 additions to existing backend schemas are optional on the wire (absent = default); worker summary/health
   additions required; worker request extensions are not dumped when unset (008 byte-identical).
3. The DELTA 1 422 rules sit in `schemas.py` (contract module, frozen in the wave).
4. Engine keeps no calibration; Blazor passes the 015 calibration; latency sources = 015 `LatencyMethod`.
5. `scoreTake(TakeScoreRequest)`; pitch worklet `audio-engine-pitch-worklet.js` via `vite.pitch.config.ts`.
6. Key display "Eb major" (chord spelling rule); catalogue `concert` row = profile clef/range (`auto`, 0..127).
7. ADR-033 D6 native Verovio TAB + "Capo n" direction.
8. Key HMM start values beta 3, `p_stay` 0.995/beat, +-8 beats; `pyin` default; estimate factors per data-model 7.7.
9. New task T144 (CI E2E split) in the devops part of L3.

## Deviations

None from the allocated labels. Two task texts changed by the ready check (T011/T012 names, T004 build entry); every
change is in tasks.md "Phase 0 ready-check amendments". The architect touched three non-architect files minimally to
keep the skeleton green: `ManifestBuilder.BackendApiVersion` (backend, one constant), `EngineMethods.ContractVersion`
(frontend, one constant + drift-test staging), `wwwroot/js/.gitignore` (two build-output lines) - all agreed one-time
edits of the kind earlier Phase 0s made.

## Open issues / requests (for owner / orchestrator)

1. **BTC forward-pass timing and GPU memory** were not measured (permission refused running downloaded model code; no
   GPU stack). T063 (L3) measures CPU time; T140 sets `BTC_GPU_NEED` from the NVML peak. If the orchestrator wants it
   before the wave, allow the spike or run it in the L3 lane.
2. **Spelling of chromatic chords in sharp keys** (owner taste): the data-model rule spells `Bb` in C major as `A#`. A
   common alternative is "flats for b3/b6/b7 in any key". Kept as written (vectors pinned); changing it later is a
   vectors 1.1.0 + C#/TS change. Owner decision welcome.
3. `/health` of the worker reports the skeleton defaults (`btc`, `pyin`) until L3's T062 wires the configured engines.
4. The `instrument` E2E projects need the fake-mic WAVs from L2's T014 before L3 wires the Playwright projects
   (hook in the lane plan).

## Docs impact

User manual (T143): chapter "Instrument views" (per family, key, capo, chord sheet/ChordPro, drum trainer, singing
privacy: microphone audio stays in the browser, tolerance and latency, transposed parts, MIDI action). Technical
manual: `instrument-analysis` worker lane and `STEMSTUDIO_CHORD_ENGINE`/`PITCH_ENGINE`/`ANALYSIS_THREADS`, BTC weights
and safe loading, the pitch worklet file, contracts worker 1.9.0 / backend 1.15.0 / engine 2.6.0 / UI ids 1.15.0.

## Next owner

Orchestrator: review, answer open issues 1-2 if wanted, start the wave from this head with the lane plan.

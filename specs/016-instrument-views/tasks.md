---

description: "Task list for feature 016 Instrument views (Phase 0 by the architect, one parallel wave of owner agents with one user story per instrument family, then a CTO integration wave)"
---

# Tasks: Instrument Views

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.3.1, ADR-031, ADR-032, ADR-033.

**Status**: planned (2026-11-16). Phase 0 starts after 014 and 015 are merged (their contracts are
the base of 016's minors). Scope addition of the CTO (2026-11-16): key detection (US7, S11) and
capo mode (US8, S12) are included below.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off (vendored code under `harmony/vendor/` excluded).
Real models (BTC, pYIN) run only in tests marked `@pytest.mark.integration`.

**Organization**: one user story per instrument family so the families are built in parallel:
US1 harmony (chords), US2 guitar and bass (tablature, MIDI), US3 drums (drum trainer), US4 vocals
(singing feedback), US5 strings/brass/woodwinds (parts), US6 "Practise as" (all families), US7
song key (all families, S11), US8 capo mode (guitar, S12). The
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the ids per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-016-<checkpoint>.md`. **CTO** = orchestrator.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-016-<checkpoint>.md` at `foundational`, every story it
takes part in (`us1`..`us8`) and `polish`, with red -> green commit hashes per task pair, exact
coverage numbers and lint/SIG output. Marker `HO:<checkpoint>` in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`jobs/`, `api/`, composition) | post-processing, key, pitch, mixdown, notation extensions | `ports.py` (T002) | fakes returning fixed `ChordChart`/`VocalPitch`/`ScoreDocument` |
| beat-analysis (`harmony/` post-processing, `analysis/vocal_pitch.py`, `notation/`) | frame/posterior shapes, catalogue | `ports.py`, data-model 2-4 | synthetic chroma/posteriors, rendered triads, sung sines |
| audio-ingest (`analysis_inputs/`, `engraving/`) | mixdown and TAB engraving shapes | `ports.py` `HarmonicMixer`, data-model 4.5 | generated FLAC fixtures, MusicXML written in the test |
| backend | worker analysis jobs, score extensions | worker-api 1.9.0 (T002; examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/`) | stub `HttpMessageHandler` |
| audio-engine | data shapes, 012/015 internals | `types.ts` 2.6.0 (T004), spelling vectors (T004) | `AudioContext`/worklet/canvas mocks, fake 015 input |
| frontend | API + engine | backend-api 1.15.0 + Contracts DTOs (T003), `types.ts` (T004), ui-test-ids 1.15.0 | fake `IStemStudioApiClient`, fake engine module, fake level context |
| qa | UI | ui-test-ids 1.15.0 (T004), fake stack with the fake analyses | suite authored in the wave, executed in T139 |
| devops | worklet asset, env vars, fake-mic CI | plan.md, data-model 1 | skeleton |

---

## Phase 0: Contracts, contract code and measurements (architect, BEFORE the wave)

- [x] T001 [architect] Reconcile with the merged 012/014/015 contracts: replace the placeholder names (score view, sections, count-in, transpose state, 015 input node, round-trip latency, take URL) in `contracts/*.delta.*`, data-model 3.3/3.4/5.3 and audio-engine.delta.md; confirm no id/route/schema collision with 014's worker 1.8.0 and 015's backend 1.14.0; record in DELTA.md "Phase 0 outcome"
- [x] T002 [architect] Worker contract: full `contracts/worker-api.openapi.yaml` 1.9.0 (= 1.8.0 + the delta, extensions applied, Redocly clean); `schemas.py` (`CONTRACT_VERSION = "1.9.0"`), `ports.py` (DELTA 1 incl. `KeyEstimator`), `config.py` (`InstrumentAnalysisSettings`); skeleton tests `worker/tests/test_instrument_views_contract.py` (examples validate, enums match catalogue constants) (FR-002, FR-063)
- [x] T003 [P] [architect] .NET contract: full `contracts/backend-api.openapi.yaml` 1.15.0; `src/StemStudio.Contracts/InstrumentViews/*`, Scores additions, `TrackDto.InstrumentViews`/`Key`, `ApiRoutes`, `ContractLimits`; operations staged in `PendingInstrumentViewOperations`; skeleton tests `tests/StemStudio.Api.Tests/Contract/InstrumentViewsContractTests.cs` (FR-063)
- [x] T004 [P] [architect] Engine and UI contracts: `contracts/audio-engine.md` 2.6.0 and `src/audio-engine/src/types.ts` (+ `ENGINE_CONTRACT_VERSION`), `vite.config.ts` second entry (`audio-engine-pitch.worklet.js`, empty processor stub); `contracts/ui-test-ids.md` 1.15.0; help-content minor (area `instruments`, schema); `contracts/generate_chord_spelling_vectors.py` + `chord-spelling-vectors.json` 1.0.0 (spelling, region keys, keys, capo, capoFixtures) with a test that regenerates and compares
- [x] T005 [architect] Phase 0 measurements, chords and key (throw-away container, read-only volumes): BTC checkpoints load with `torch.load(weights_only=True)` and the exact allow-listed numpy globals; CPU time for 5 min (8 threads) and GPU time + NVML peak -> `BTC_GPU_NEED`; chroma-hmm time; key HMM `p_stay` tuned on the modulation fixture of R-13; record in research.md R-02/R-03/R-13, data-model 1/7.7, ADR-031 status
- [x] T006 [P] [architect] Phase 0 measurements, pitch and tab: `librosa.pyin` time for 5 min at 16 kHz/hop 160 on the reference CPU (decide `pyin` vs `yin` default); Verovio 6.4.0 engraving spike of a MusicXML with notation + TAB (6, 5, 4 lines, capo) -> decide ADR-033 D6 (native vs fallback drawing); record in research.md R-06/R-08 and ADR-033
- [x] T007 [P] [architect] Phase 0 YIN benchmark: a throw-away TS benchmark of the R-05 core in Node and in Chromium with fake audio capture on the reference machine (average/worst ms per 128-frame block) -> confirm SC-004 budget; record in research.md R-05 and ADR-032 status
- [x] T008 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, engine `npm test`/lint/typecheck, `tools/quality/run_all.py` 0 errors, Redocly lint of the full documents; hand-off `docs/handoffs/architect-016-plan.md` (versions, vendored BTC subset record, weights SHA-256, measurements)

**Checkpoint**: skeleton green; wave may start. Outcome: `docs/handoffs/architect-016-design.md`,
DELTA.md "Phase 0 outcome", research.md Phase 0 outcomes (R-02/R-03, R-05..R-08, R-13), ADR-031..033 Accepted.

### Phase 0 ready-check amendments (binding for the wave, 2026-11-18)

Paths and names follow the code as merged (014+015 on `main` 7b5cd39e); where a row below differs from a task
text, the row wins.

| Task(s) | Amendment |
|---|---|
| all backend "un-stage" steps (T064, T086, T095, T116, T123, catalogue in T065) | Staged ids live in `tests/StemStudio.Api.Tests/Contract/BackendContractTests.InstrumentViews.cs`: move each served id from `PendingInstrumentViewOperations` (today `= InstrumentViewsContractTests.Operations`) to `InstrumentViewOperations`. Acceptance: `ServedOpenApiDocument_ListsExactlyTheContractOperations` green with the id served. |
| T041, T043, T055 | Vectors: `specs/016-instrument-views/contracts/chord-spelling-vectors.json` 1.0.0, linked into Domain.Tests as `InstrumentViews/chord-spelling-vectors.json`; engine tests read it by relative path. Sections `cases`, `keys`, `capo`, `capoFixtures` and the `rules` text are binding. |
| T049 | Worker examples are already copied: `tests/StemStudio.Api.Tests/Worker/Examples/analysis-*.json` (6 files, copies of the 1.9.0 examples). |
| T048 | One backend lane owns `Migrations/**` and the model snapshot; migration `AddTrackAnalyses` is the only 016 migration (rule M applies only if the orchestrator splits the backend). |
| T057, T058 | The 004 presets are `src/StemStudio.Web/Mixer/PracticePreset.cs` (`PracticePresets.PlayAlong`); the family rule replaces its stem mute. |
| T062 | `/health` reports the configured engines (`settings.instrument_analysis.chord_engine`/`pitch_engine`, model readiness) and the lane; update the skeleton expectations in `worker/tests/test_smoke.py` and `worker/tests/api/test_jobs_api.py` (they pin the defaults `btc`/`pyin`/`not-downloaded` today). |
| T063 | Also records the BTC forward time per audio second on 8 threads (not measured in Phase 0) and the safe-load with `ports.BTC_SAFE_GLOBALS`. |
| T066-T069, T088, T099-T104, T118, T125 | Engine stubs: `src/audio-engine/src/views/pending.ts` (`pendingView`) and the 2.6.0 methods at the end of `engine.ts`; the pitch worklet is `src/pitch/worklet.ts` (registered, silent). Remove `pending.ts` when the last method is built; `tests/views/instrument-views-contract.test.ts` follows. |
| T099, T100 | `getRoundTripLatency().source` and `SingingSummary.latencySource` are the 015 `LatencyMethod`; the calibration comes in `SingingFeedbackRequest.roundTripMs` + `latencyMethod`, the fallback is the open input's `estimatedLatencySamples`. |
| T103, T104 | `scoreTake(request: TakeScoreRequest)` (`takeUrl`, `startSample`, `reference`, `units`, `toleranceCents`). |
| T070, T071 | Help area `instruments` (help-content 1.1.0): add it to `TopicParser.AreaOrder` after `mixes`; the Web tests already validate against `specs/016-instrument-views/contracts/help-topic.schema.json`. |
| T090, T091, T112 | "014 score view" = `Components/Practice/ScorePanel.razor` with engine `attachScoreFollower`. |
| T070, T090, T105 | Mixer layout: the karaoke banner is inside the fixed `.ss-mixer-dock` (`--ss-dock-inset`, `dock-inset.js`); banner chords grow the dock and must keep focus/targets clear (WCAG 2.4.11, 2.5.8 in bUnit and the a11y spec); the chord lane, drum trainer and singing panel live in the scrolling mixer content, never in the dock. |
| T105, T106 | Remove `OnSingingUnitScored`, `OnSingingStateChanged` from `PendingCallbacks` in `tests/StemStudio.Web.Tests/Audio/EngineContractDriftTests.cs` when the `[JSInvokable]` methods exist. |
| T011, T012 | Worklet file `audio-engine-pitch-worklet.js` (015 naming), ignored like the other build outputs in `src/StemStudio.Web/wwwroot/js/.gitignore` (done in Phase 0). |

### Acceptance of tasks without a red test

| Task(s) | Accepted when |
|---|---|
| T013 (strategy) | `docs/testing/TEST-STRATEGY.md` has a per-family table (harmony, guitar, bass, drums, vocals, strings, brass, woodwinds) naming at least one unit suite and one E2E spec per family, and the integration-only tests (T063, real pYIN). |
| T076, T083, T092, T107, T112, T115, T122, T129 (E2E authoring) | The spec typechecks and lints in `e2e/` (`npm run typecheck`, `npm run lint`), uses only `getByTestId` with ids of ui-test-ids 1.15.0 (or earlier), waits on the job/analysis it started (LESSONS qa), and is listed in the lane hand-off; it is run at the mid-wave checkpoint and in T139 (lanes start no stack). |
| T030 (vendoring) | `harmony/vendor/btc/` holds only the model definition, transformer modules and label tables of commit `2682317b` with its MIT `LICENSE` and a header naming the commit; `tools/quality` excludes it from SIG; no import of `mir_eval`, `pretty_midi`, `pandas`, `pyrubberband`. |
| T130-T138 (polish) | Own suites green with >= 90 % line and branch coverage (vendor excluded), `run_all.py` 0 errors with the jscpd step's own output checked, formatter clean on the whole solution, hand-off written. |
| T139-T143 (integration) | As written; T140 additionally replaces `BTC_GPU_NEED` with the NVML peak + 20 % (test-first in `ports.py`, architect request) and refines `StemStudio:InstrumentViews:Estimates` when off by > 50 %. |

---

## Phase 1: Setup (per owner, no story)

- [ ] T009 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `harmony.{postprocess,labels,chroma_hmm,key}` import only `ports`, `schemas`, numpy, librosa; `harmony.btc` may import torch and `harmony/vendor`; `notation` imports neither `harmony` nor `transcription`; no `weights_only=False` anywhere in `worker/src`; `harmony/vendor/**` excluded from SIG but has LICENSE; engine: `src/pitch/**` imports no DOM/Web Audio globals, nothing imports `engine.ts` except `index.ts`
- [ ] T010 [quality-gate] Implement the rules in `tools/quality/`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T011 [P] [devops] Write the checks first and show them fail: `scripts/check-web-assets.{ps1,sh}` (the web image serves `/js/audio-engine-pitch-worklet.js` with `text/javascript`, built by `vite.pitch.config.ts`); CI job outline for the Playwright fake-mic projects
- [ ] T012 [devops] Web image and engine build emit/serve the worklet file; `docker-compose.yml` passes `STEMSTUDIO_CHORD_ENGINE`, `STEMSTUDIO_PITCH_ENGINE`, `STEMSTUDIO_ANALYSIS_THREADS` (empty = the worker default 4); CI `CONTRACTS_DIR` -> `specs/016-instrument-views/contracts` with the 015 steps kept under a new `RECORDING_CONTRACTS` variable (they name `recording-api.openapi.yaml` and `merge_recording_delta.py`), plus 016 steps: Redocly lint of the four 016 documents, `merge_full.py --check`, `generate_chord_spelling_vectors.py --check` and `pytest specs/016-instrument-views/contracts`; Playwright fake-mic projects (`--use-fake-device-for-media-stream --use-file-for-fake-audio-capture=<wav>`) in CI; wire T011 to pass
- [ ] T013 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: per-family traceability (harmony, guitar, bass, drums, vocals, strings, brass, woodwinds -> specs), fake analyses, fake-mic fixtures, integration tests with real models (SC-001, SC-006, SC-010)
- [ ] T014 [qa] `tests/fixtures/generate_instrument_fixtures.py` + its test: `sing-on-pitch.wav` (fake melody one octave lower, two harmonics), `sing-semitone-off.wav` (+100 cents), `silence.wav`, deterministic (fixed seed, SHA-256 manifest `instrument-manifest.json`) (SC-004, SC-006)

---

## Phase 2: Foundational (blocking inside each owner)

### beat-analysis (tests first)

- [ ] T015 [P] [beat-analysis] Failing tests `worker/tests/harmony/test_postprocess.py`, `test_labels.py`: Harte parse/format for every quality and inversion; beat snapping (changes only on beats, >= 1 beat), 0.5 s merge without a grid, consecutive merge, slash bass from bass chroma, `preferFlats`, `noChords`, warnings (data-model 2.1-2.4) (FR-010, FR-011)
- [ ] T016 [beat-analysis] Implement `harmony/{labels,postprocess}.py` to pass T015
- [ ] T017 [P] [beat-analysis] Failing tests `worker/tests/harmony/test_chroma_hmm.py`: rendered triad sequences (numpy sines) give the expected majmin labels for >= 75 % of the time (SC-001 fallback); `full` request -> `simple` + `vocabulary-reduced`; frame rate 92.88 ms
- [ ] T018 [beat-analysis] Implement `harmony/chroma_hmm.py` to pass T017
- [ ] T019 [P] [beat-analysis] Failing tests `worker/tests/harmony/test_key.py` (S11): synthetic cadences in all 24 keys -> global key correct >= 90 %, correct or fifth/relative 100 %; modulation fixture C -> D reported within 2 bars; regions < 8 bars merged; alternative within 0.1; confidence; no chords -> null key; `key-uncertain` warning (FR-070, SC-010)
- [ ] T020 [beat-analysis] Implement `harmony/key.py` (`KeyEstimator`, research R-13) to pass T019
- [ ] T021 [P] [beat-analysis] Failing tests `worker/tests/analysis/test_vocal_pitch.py`: sung sine sweeps 80-1000 Hz -> frames within 20 cents; unvoiced gaps 0; packing to centi-semitones, length, range percentiles; `yin` engine switch; silent stem -> `NoMelodyError`; `few-voiced`, `detail-bleed` warnings (FR-040)
- [ ] T022 [beat-analysis] Implement `analysis/vocal_pitch.py` to pass T021
- [ ] T023 [P] [beat-analysis] Failing tests `worker/tests/notation/test_tablature.py`: every tuning of data-model 4.1; drop D low D on string 6 fret 0; 5-string low B use; capo relative frets; octave moves and dropped chord notes with counts; span <= 4 fretted frets; golden fingering fixtures >= 95 % expected string; MusicXML has `TAB` clef, `staff-lines`, `staff-tuning`, `capo`, `<string>/<fret>` per note; every fret/string sounds its pitch (FR-020..FR-022, SC-002)
- [ ] T024 [beat-analysis] Implement `notation/tablature.py` (`TabFingerer`) and its notator wiring to pass T023
- [ ] T025 [P] [beat-analysis] Failing tests `worker/tests/notation/test_parts.py`: vectors for every written instrument (written pitches, transposed key signature with respelling beyond 7 accidentals, part name, `<transpose>`), clef choices and rejections, out-of-range counts, MIDI stays concert pitch, `concert`+`auto` output byte-identical to 008; new profiles strings/brass/woodwinds (single voice, ranges, `section-reduced`) (FR-050..FR-054, SC-005)
- [ ] T026 [beat-analysis] Implement `notation/parts.py` and the new profiles to pass T025
- [ ] T027 [P] [beat-analysis] Failing tests `worker/tests/notation/test_chord_symbols_drum_midi.py`: chord symbols quantised and written as `<harmony>` on the top staff (transposed with parts, skipped for `N`); drum MIDI channel 10 with the GM map; `singlePiece` drums scores without `missing-drum-piece` (FR-017, FR-023)
- [ ] T028 [beat-analysis] Implement `notation/{chord_symbols,drum_midi}.py` to pass T027

### stem-separation (tests first)

- [ ] T029 [P] [stem-separation] Failing tests `worker/tests/harmony/test_btc.py` with a fake torch module: CQT 144 bins/24 per octave/hop 2048 at 22,050 Hz; `(x - mean)/std`; zero-padding to 108-frame instances; checkpoint per vocabulary via the model manager (pinned URL/SHA-256); `torch.load` called with `weights_only=True` and the allow-listed globals only; runs through the injected `DeviceRunner` with `BTC_GPU_NEED`; fallback to chroma-hmm with `fallback-engine` when weights are unavailable; progress (FR-010)
- [ ] T030 [stem-separation] Vendor the BTC subset into `harmony/vendor/btc/` (MIT LICENSE, commit header, no mir_eval/pretty_midi/pandas/pyrubberband) and implement `harmony/{__init__,btc}.py` to pass T029
- [ ] T031 [P] [stem-separation] Failing tests `worker/tests/test_views_fake.py`: fake chords (R-11 incl. the key change at bar 9 and `keyRegions`), fake reference melody on the fake lyric lines, fake drum hits = the 008 fake pattern plus crashes; deterministic
- [ ] T032 [stem-separation] Implement `views_fake.py` and the factories (real/fake by settings) to pass T031
- [ ] T033 [P] [stem-separation] Failing tests `worker/tests/jobs/test_instrument_analysis_store.py`: jobs persisted as `instrument-analysis-jobs/<id>.json`; restart -> failed "interrupted by restart"; delete; 24 h sweep (fake clock)
- [ ] T034 [stem-separation] Implement `jobs/instrument_analysis_store.py` to pass T033

### audio-ingest (tests first)

- [ ] T035 [P] [audio-ingest] Failing tests `worker/tests/analysis_inputs/test_mixdown.py`: sums 1-4 FLAC stems of different lengths to mono 22,050 Hz (and 16 kHz for pitch), clipping-safe scaling, missing file -> `InputNotFoundError`, path allow-list respected
- [ ] T036 [audio-ingest] Implement `analysis_inputs/mixdown.py` (`HarmonicMixer`) to pass T035
- [ ] T037 [P] [audio-ingest] Failing tests `worker/tests/engraving/test_tablature_engraving.py`: notation + TAB and TAB only MusicXML fixtures (6/5/4 lines, capo) engrave to SVG pages with the right line counts and fret numbers in the TAB staff and a PDF without missing glyphs (per the T006 decision: native Verovio or the fallback drawing) (FR-020, SC-002)
- [ ] T038 [audio-ingest] Implement `engraving/tablature.py` (checks, and the fallback drawing if T006 chose it) to pass T037

### backend (tests first)

- [ ] T039 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/InstrumentViews/TrackAnalysisTests.cs`: every transition of data-model 7.2, fingerprint over input stems, `stale` on change, auto rules 7.3, `edits-exist` guard (FR-002, FR-012)
- [ ] T040 [backend] Implement `Domain/InstrumentViews/{TrackAnalysis,AnalysisRules}.cs` to pass T039
- [ ] T041 [P] [backend] Failing tests `ChordSpellingTests.cs`, `HarteLabelTests.cs`, `KeyFormatterTests.cs`, `ChordSheetBuilderTests.cs`, `ChordProWriterTests.cs`: all cases of `chord-spelling-vectors.json` (spelling by region key, keys incl. transposed and written keys), Harte parse of the edit pattern, sheet anchoring (word, lead, instrumental rows, no lyrics), ChordPro directives (FR-013, FR-014, FR-016, FR-071..FR-074)
- [ ] T042 [backend] Implement `HarteLabel`, `ChordSpeller`, `KeyFormatter`, `ChordSheetBuilder`, `ChordProWriter` to pass T041
- [ ] T043 [P] [backend] Failing tests `CapoAdvisorTests.cs` (S12): `capo` and `capoFixtures` vectors; ranking with the 0.02 tie rule; `fewOpenShapes`; shortcut for transpose +1..+7 and -5..-1 (FR-076, FR-077, SC-011)
- [ ] T044 [backend] Implement `CapoAdvisor` to pass T043
- [ ] T045 [P] [backend] Failing tests `InstrumentCatalogueTests.cs`, `FamilyRuleTests.cs`: catalogue content = data-model 4.1-4.3/5.3 and the worker constants; family of every channel id incl. `rest` and unsplit `other` (FR-060)
- [ ] T046 [backend] Implement `InstrumentCatalogue`, `FamilyRule` to pass T045
- [ ] T047 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/TrackAnalysesPersistenceTests.cs`: unique `(TrackId, Kind)`, JSON documents round trip (90,000 frames), cascade delete with the track, `Score` JSON with tablature/part facts loads 008 items unchanged
- [ ] T048 [backend] Implement configuration and migration `AddTrackAnalyses` to pass T047
- [ ] T049 [P] [backend] Failing tests `Worker/WorkerInstrumentAnalysisClientTests.cs` with the worker examples: create per kind, get with each result type, delete, problems mapped, device parsed
- [ ] T050 [backend] Implement `Worker/WorkerClient.InstrumentAnalysis.cs` to pass T049
- [ ] T051 [P] [backend] Failing tests `InstrumentViews/TrackAnalysisServiceTests.cs`: inputs per kind (data-model 7.4), grid handling, estimates (7.7), job applier stores documents and statuses, auto starter on standard completion and drums split, worker 503/restart handling
- [ ] T052 [backend] Implement `TrackAnalysisService`, `AnalysisKindStrategies`, `AnalysisJobApplier`, `AnalysisAutoStarter` to pass T051

### audio-engine (tests first)

- [ ] T053 [P] [audio-engine] Failing tests `tests/timeline/*.test.ts`: shared clock = karaoke clock (output latency before loop wrap, 012 rate), cursor O(1) advance and binary search after seek/wrap, one-frame redraw triggers, reduced-motion paging helper (FR-003, SC-007)
- [ ] T054 [audio-engine] Implement `src/timeline/` (and move the karaoke clock onto it without behaviour change) to pass T053
- [ ] T055 [P] [audio-engine] Failing tests `tests/chords/chord-spelling.test.ts`: every case of `chord-spelling-vectors.json` (region keys, transpose, capo, shapes, `N.C.`) (FR-014, FR-073, SC-011)
- [ ] T056 [audio-engine] Implement `src/chords/spelling.ts` to pass T055

### frontend (tests first)

- [x] T057 [P] [frontend] Failing tests `FamilyMixServiceTests.cs`: minus-one per family mutes every family channel (expanded or not), unmutes/unsolos others, keeps volumes/pans; stored previous flags and restore; `play-along-<stem>` presets now family-wide (DELTA 6) (FR-032, FR-060)
- [x] T058 [frontend] Implement `InstrumentViews/FamilyMixService.cs` and the preset change to pass T057
- [x] T059 [P] [frontend] Failing tests of the API client additions (chords, sheet, ChordPro, key, capo suggestions, drum hits, vocal pitch, catalogue with caching, score extensions, `listAssets` channel)
- [x] T060 [frontend] Implement the client methods to pass T059

**Checkpoint**: HO:foundational for every owner with foundational tasks.

---

## Phase 3: User Story 1 - Harmony: synced chord chart (P1)

**Goal**: chords detected automatically, lane + banner + sheet, vocabulary, edits, ChordPro, chords in lead sheets. **Independent test**: quickstart V2-V4; E2E `instrument-chords.spec.ts`.

- [ ] T061 [P] [US1] [stem-separation] Failing API tests `worker/tests/api/test_instrument_analysis_routes.py` (chords): 202 on the `instrument-analysis` lane (not blocked by a running transcribe/render/separation), stages/progress, inline `ChordChart` with `key`/`keyRegions`, validation 422, `/health` `instrumentAnalysis` and lane; responses validate against worker 1.9.0 (FR-002, FR-010, FR-070)
- [ ] T062 [US1] [stem-separation] Implement `jobs/instrument_analysis_service.py`, `api/instrument_analysis_routes.py`, composition and lane in `main.py` (mixdown -> recognizer -> post-processing -> key) to pass T061
- [ ] T063 [US1] [stem-separation] Integration test `worker/tests/harmony/test_chords_integration.py` (`@pytest.mark.integration`, downloads BTC): synthetic chord set through real BTC + post-processing >= 85 % majmin time (SC-001) and key test set >= 90 % (SC-010); records time per audio second
- [ ] T064 [P] [US1] [backend] Failing tests `Api.Tests/InstrumentViews/ChordEndpointsTests.cs`: auto start after the standard job, GET/POST/DELETE, `edits-exist`/`overwriteEdits`, segment edit (pattern, `edited`), sheet and ChordPro with transpose/capo, stale after re-separation, `Track.instrumentViews.chords`; un-stage the chords operations (FR-010..FR-016)
- [ ] T065 [US1] [backend] Implement `ChordEndpoints` to pass T064
- [ ] T066 [P] [US1] [audio-engine] Failing tests `tests/chords/chord-lane.test.ts`: DOM items, states, window by bars, host attributes, validation errors, detach, reduced motion, frame budget (counted writes) (FR-013, SC-007)
- [ ] T067 [US1] [audio-engine] Implement `src/chords/lane.ts` and the engine methods to pass T066
- [ ] T068 [P] [US1] [audio-engine] Failing tests `tests/chords/karaoke-chords.test.ts`: `setKaraokeChords` anchoring (word, lead), states, kept across `attachKaraoke`, `setChordDisplay` re-spelling on capo and 012 transpose change (FR-013, FR-014)
- [ ] T069 [US1] [audio-engine] Implement `src/chords/banner.ts`, `setChordDisplay` to pass T068
- [x] T070 [P] [US1] [frontend] Failing bUnit tests: `chord-lane` host + toggle, `chords-status`/`chords-detect`/`chords-limits`, `chords-vocabulary` (Advanced), `chords-engine` (Expert), `data-chords` on the banner, `chord-sheet` rows in the lyrics card, `chord-sheet-transpose`, `chord-edit-*` (Advanced), `chord-sheet-download`, overwrite confirm; help ids `chords`, `chords-advanced` (FR-012..FR-016, FR-061)
- [x] T071 [US1] [frontend] Implement `ChordLane`, `ChordSheet`, `ChordEditDialog`, wiring and help topics to pass T070
- [ ] T072 [P] [US1] [backend] Failing tests: `includeChords` -> worker `chords` + `preferFlats` (range-shifted), `409 chords-not-ready`, profile rules, `ScoreOptions.chords`, `ScoreFacts.chordsIncluded` (FR-017)
- [ ] T073 [US1] [backend] Implement to pass T072
- [x] T074 [P] [US1] [frontend] Failing bUnit tests: `score-include-chords` (Advanced) enabled only when available, with the reason otherwise (FR-017)
- [x] T075 [US1] [frontend] Implement to pass T074
- [x] T076 [US1] [qa] Author `e2e/tests/instrument-chords.spec.ts`: harmony family as **keys** and as **guitar**: chords ready automatically; `chord-lane` `data-current-symbol` changes on the fake bars (C, G, Am, F); banner `.karaoke-chord` above words; chord sheet rows; Advanced edit persists; ChordPro starts with `{title:`; lead sheet with chords has `<harmony>` (SC-006)

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Guitar and bass: tablature and MIDI (P1)

**Goal**: tab next to notation with tunings and capo; one-click MIDI per channel. **Independent test**: quickstart V5-V6; E2E `instrument-tab.spec.ts`.

- [ ] T077 [P] [US2] [stem-separation] Failing route tests: `tablature`, `part`, `chords`, `preferFlats`, `singlePiece` reach the notator settings; 422 combinations (DELTA 1); `ScoreSummary` extensions returned (FR-020, FR-023, FR-051)
- [ ] T078 [US2] [stem-separation] Implement the transcription request/response extensions to pass T077
- [ ] T079 [P] [US2] [backend] Failing tests: `tablature` validation (`tablature-not-allowed`, `tuning-mismatch`, `capo-not-allowed`), `ScoreOptions.tablature` per profile, facts; `listAssets` `channel` filter newest first; source `drums.<piece>` with profile `drums` -> one-piece `drumInputs` + `singlePiece` (FR-020..FR-023)
- [ ] T080 [US2] [backend] Implement to pass T079
- [x] T081 [P] [US2] [frontend] Failing bUnit tests: `score-tab-layout`/`score-tab-tuning` (Beginner: standard + drop D) and `score-tab-capo` (Advanced) for guitar and bass; `asset-score-tab-<id>`; `midi-<key>` on every channel row and strip menu (reuse ready item -> download; none -> create, `midi-progress-<key>`, then download; `midi-error-<key>`); help ids `tablature`, `midi-download` (FR-020, FR-023, FR-061)
- [x] T082 [US2] [frontend] Implement `ScoreDialog` tab options, `MidiAction`, help topics to pass T081
- [ ] T083 [US2] [qa] Author `e2e/tests/instrument-tab.spec.ts`: **guitar** drop D notation + tab (MusicXML `TAB` clef, 6 `staff-tuning`, `<fret>` values sound the pitches); **bass** 5-string tab only (5 lines); MIDI action on `drums-kick` downloads `MThd` with channel 10 notes 36 (SC-002, SC-006)

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Drums: drum trainer (P1)

**Goal**: hit lanes, Drumless, guide piece, drum score with follow. **Independent test**: quickstart V7-V8; E2E `instrument-drums.spec.ts`.

- [ ] T084 [P] [US3] [stem-separation] Failing route tests (drum-hits): runs the 008 `DrumTranscriber` (fake) per piece with progress, `DrumHits` result, missing pieces listed in `pieces` (FR-030)
- [ ] T085 [US3] [stem-separation] Implement the drum-hits kind to pass T084
- [ ] T086 [P] [US3] [backend] Failing tests `DrumHitEndpointsTests.cs`: auto start after the drums split, `unavailable` "Split the drums first.", `409 drums-not-split`, stale after a new split, GET/POST/DELETE; un-stage (FR-030)
- [ ] T087 [US3] [backend] Implement `DrumHitEndpoints` to pass T086
- [ ] T088 [P] [US3] [audio-engine] Failing tests `tests/drums/hit-lanes.test.ts`: lane order, look-ahead window, strength heights, host attributes (`data-next-hit-*`, `data-visible-hits`), options change, reduced-motion paging, validation, counted primitives (FR-031, SC-003, SC-007)
- [ ] T089 [US3] [audio-engine] Implement `src/drums/hit-lanes.ts` and the methods to pass T088
- [ ] T090 [P] [US3] [frontend] Failing bUnit tests: `drumless` (family mute incl. expanded strips, metronome + one-bar count-in via 012, restore on second click), `drum-trainer`, `drum-lanes` host, `drum-hits-status`, `drum-hits-split-required`/`-start`, `drum-lane-label-<piece>` guide (Advanced), `drum-look-ahead` (Advanced), `drum-bar-text`, `drum-score` (014 view) / `drum-score-generate`; help id `drum-trainer` (FR-031..FR-034, FR-062)
- [ ] T091 [US3] [frontend] Implement `DrumTrainer`, `DrumlessToggle`, help topic to pass T090
- [ ] T092 [US3] [qa] Author `e2e/tests/instrument-drums.spec.ts`: **drums** with fake split: lanes `data-next-hit-piece` follows kick/snare/hihat pattern at the fake beat times; `drumless` -> every `meter-drums*` at -60 dB while other meters move; restore; guide hi-hat audible; drum score follows the bar (014 ids) (SC-003, SC-006)

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Vocals: singing feedback (P1)

**Goal**: reference melody, live pitch line, per-line scores, summary, take scoring. **Independent test**: quickstart V9-V10; E2E `instrument-singing.spec.ts`.

- [ ] T093 [P] [US4] [stem-separation] Failing route tests (vocal-pitch): `PitchAnalyzer` (fake) result, `NoMelodyError` -> failed with the fixed text, progress (FR-040)
- [ ] T094 [US4] [stem-separation] Implement the vocal-pitch kind to pass T093
- [ ] T095 [P] [US4] [backend] Failing tests `VocalPitchEndpointsTests.cs`: source `vocals.lead` when split with a kept lead stem else `vocals`, `409 no-vocals`/`track-not-ready`, estimate, never automatic, response compression for large documents; un-stage (FR-040)
- [ ] T096 [US4] [backend] Implement `VocalPitchEndpoints` to pass T095
- [ ] T097 [P] [US4] [audio-engine] Failing tests `tests/pitch/yin.test.ts`, `decimate.test.ts`: sines/harmonics 80-1000 Hz median error <= 20 cents, voiced detection >= 95 %, noise/silence unvoiced, 44.1/48/96 kHz inputs; `yin.bench.test.ts` reports ms per block (SC-004)
- [ ] T098 [US4] [audio-engine] Implement `src/pitch/{yin,decimate}.ts` to pass T097
- [ ] T099 [P] [US4] [audio-engine] Failing tests `tests/singing/feedback.test.ts` with a fake worklet and fake 015 input: `startSingingFeedback` loads the module once, connects the input, states, errors (no input, already running), overrun guard -> `failed` with the reason, `stopSingingFeedback` idempotent summary, `getRoundTripLatency` calibrated/estimated (FR-041, FR-045)
- [ ] T100 [US4] [audio-engine] Implement `src/pitch/worklet.ts` (processor) and `src/singing/feedback.ts` to pass T099
- [ ] T101 [P] [US4] [audio-engine] Failing tests `tests/singing/scorer.test.ts`, `pitch-view.test.ts`: octave folding, tolerance, 012 transpose, units < 20 voiced frames null, loop passes, seek discards, latency alignment (R-07), callbacks per unit; pitch view window, bars and polyline, host attributes, reduced motion (FR-042, FR-043, SC-004)
- [ ] T102 [US4] [audio-engine] Implement `src/singing/{scorer,pitch-view}.ts` to pass T101
- [ ] T103 [P] [US4] [audio-engine] Failing tests `tests/singing/score-take.test.ts`: decoded take (fake fetch/decode) scored with the same core and units; latency default 0 (FR-046)
- [ ] T104 [US4] [audio-engine] Implement `scoreTake` to pass T103
- [ ] T105 [P] [US4] [frontend] Failing bUnit tests: `singing` panel, `singing-reference-status`/`-analyse`/`-estimate`, `singing-toggle`, `singing-state`, `pitch-view` host, lead vocal muted while running and restored, `singing-headphones-hint`, `singing-error` (permission, device, too slow) with the 015 link, `singing-tolerance` (Advanced/Expert defaults), `singing-latency`, `singing-raw` (Expert), `singing-announcer` text, `karaoke-score`, `singing-summary`, units from lyrics/sections/blocks, `take-score-<takeId>` (P2); help ids `singing-feedback`, `singing-tolerance-latency` (FR-041..FR-047, FR-061, FR-062)
- [ ] T106 [US4] [frontend] Implement `SingingPanel`, `PitchView`, take scoring action, help topics to pass T105
- [ ] T107 [US4] [qa] Author `e2e/tests/instrument-singing.spec.ts` in the fake-mic projects: **vocals** with `sing-on-pitch.wav` every `karaoke-score` >= 90 and summary >= 90; `sing-semitone-off.wav` <= 20 at 50 cents; `silence.wav` 0; no request to the API carries audio or scores (network log); take scoring of a fake 015 take (SC-004, SC-006, FR-047)

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - Strings, brass, woodwinds: transposed parts (P1)

**Goal**: new profiles, written instrument and clef, same bars. **Independent test**: quickstart V11; E2E `instrument-parts.spec.ts`.

- [ ] T108 [P] [US5] [backend] Failing tests: profiles `strings`/`brass`/`woodwinds` suggested for `<parent>.strings|brass|woodwinds`; `part` validation (`part-not-allowed`, `clef-not-allowed`), `ScoreOptions.parts`, `ScoreFacts.part`, recipe round trip (FR-050..FR-053)
- [ ] T109 [US5] [backend] Implement to pass T108
- [ ] T110 [P] [US5] [frontend] Failing bUnit tests: `score-part-instrument` (all levels, `concert` first), `score-part-clef` (Advanced), `asset-score-part-<id>`, new profile names; help id `transposed-parts` (FR-051, FR-052, FR-061)
- [ ] T111 [US5] [frontend] Implement to pass T110
- [ ] T112 [US5] [qa] Author `e2e/tests/instrument-parts.spec.ts`: **brass** as alto sax E-flat (`<transpose><chromatic>-9`, `<fifths>3` for concert C), **woodwinds** as clarinet B-flat, **strings** as viola (alto clef `<sign>C</sign>`); MIDI at concert pitch; the 014 score view shows the same current bar for the part and the concert part at a fixed position (SC-005, SC-006)

**Checkpoint**: HO:us5.

---

## Phase 8: User Story 6 - "Practise as ..." for every family (P2)

- [ ] T113 [P] [US6] [frontend] Failing bUnit tests: `practise-as` menu with every family, applying minus-one and opening `instrument-view` with the family's view, `practise-as-offer` from localStorage (offered, not applied), keyboard operation; help id `instrument-views` (FR-060, FR-062)
- [ ] T114 [US6] [frontend] Implement `PractiseAsMenu` and the view host to pass T113
- [ ] T115 [US6] [qa] Author `e2e/tests/instrument-practise-as.spec.ts`: for **vocals, drums, bass, guitar, keys, orchestral**: muted channels per data-model 5.3 (expanded and collapsed), opened `data-view`; `play-along-drums` with expanded drums mutes the drum detail strips (DELTA 6) (SC-006, SC-009)

**Checkpoint**: HO:us6.

---

## Phase 9: User Story 7 - Song key for every family (P1, scope addition S11)

**Goal**: key next to the tempo, current region, transposed and written keys. **Independent test**: quickstart V12; E2E `instrument-key.spec.ts`.

- [ ] T116 [P] [US7] [backend] Failing tests `KeyEndpointsTests.cs`: `Track.key` summary when chords are ready (null otherwise), `GET /api/tracks/{id}/key` with transpose and instrument (written key), regions with start bars from the grid, uncertainty; un-stage `getKey` (FR-070..FR-074)
- [ ] T117 [US7] [backend] Implement `KeyEndpoints` and the `Track.key` projection to pass T116
- [ ] T118 [P] [US7] [audio-engine] Failing tests `tests/chords/current-key.test.ts`: chord lane host `data-current-key` follows the region at the position and the 012 transpose; spelling uses the region key (D major sharps, D-flat major flats) (FR-071, FR-073)
- [ ] T119 [US7] [audio-engine] Implement to pass T118
- [ ] T120 [P] [US7] [frontend] Failing bUnit tests: `track-key` next to `track-bpm`, `mixer-key`, `mixer-key-current` (mirrors `data-current-key`), transposed display with the 012 transpose, `mixer-key-written` for the orchestral family with a chosen instrument, `mixer-key-confidence` and `track-key-regions` (Advanced); help id `song-key` (FR-071..FR-074, FR-061)
- [ ] T121 [US7] [frontend] Implement the key components and help topic to pass T120
- [ ] T122 [US7] [qa] Author `e2e/tests/instrument-key.spec.ts`: "C major" shown on the track page and in the mixer for **each family** chosen via "Practise as" (vocals, drums, bass, guitar, keys, orchestral); playing past bar 9 shows "D major" in `mixer-key-current`; transpose +2 shows "D major (original C major, +2)"; orchestral with alto sax shows "Your part: A major"; chord lane symbols in the D region use sharps (SC-006, SC-010)

**Checkpoint**: HO:us7.

---

## Phase 10: User Story 8 - Capo mode for guitar (P1, scope addition S12)

**Goal**: capo badge, shapes, suggestions, original-shapes shortcut, tab prefill. **Independent test**: quickstart V13; E2E `instrument-capo.spec.ts`.

- [ ] T123 [P] [US8] [backend] Failing tests `CapoEndpointsTests.cs`: `GET .../chords/capo-suggestions?transpose` (shortcut, ranking from `CapoAdvisor`, `409 chords-not-ready`), sheet/ChordPro with `capo` show shapes and `{capo: n}`; un-stage `getCapoSuggestions` (FR-075..FR-077)
- [ ] T124 [US8] [backend] Implement `CapoEndpoints` to pass T123
- [ ] T125 [P] [US8] [audio-engine] Failing tests `tests/chords/capo-display.test.ts`: `setChordDisplay({capo, showSounding})` shapes in lane and banner, `.chord-sounding` children, host `data-capo`, capo range error (FR-075)
- [ ] T126 [US8] [audio-engine] Implement to pass T125
- [ ] T127 [P] [US8] [frontend] Failing bUnit tests: `capo-mode`, `capo-select`, `capo-badge`, `capo-suggestions`/`capo-suggestion-<n>` (one click applies), `capo-shortcut` after a transpose, `capo-show-sounding` (Advanced), stored per track in the browser, tablature dialog prefilled with the capo, **no capo controls for the bass and keys families**; help id `capo-mode` (FR-075..FR-078, US8 AS5)
- [ ] T128 [US8] [frontend] Implement the capo components and help topic to pass T127
- [ ] T129 [US8] [qa] Author `e2e/tests/instrument-capo.spec.ts`: **guitar**: transpose +3, `capo-shortcut` reads "Capo 3", apply -> lane shows C/G/Am/F, `capo-badge` "Capo 3", chord sheet shapes; suggestion list order equals the R-14 rule (expected values computed in the spec from the vectors); tablature dialog has capo 3; **bass** and **keys**: no `capo-mode` (SC-006, SC-011)

**Checkpoint**: HO:us8.

---

## Phase 11: Polish (inside the wave, per owner)

- [ ] T130 [P] [beat-analysis] Refactor to SIG limits, docstrings, HO:polish with coverage of `harmony/` (non-vendor parts it owns), `analysis/vocal_pitch.py`, `notation/` additions
- [ ] T131 [P] [audio-ingest] Refactor, docstrings, HO:polish with coverage of `analysis_inputs/`, `engraving/tablature.py`
- [ ] T132 [P] [stem-separation] Refactor, docstrings, HO:polish with coverage of `harmony/btc.py`, `views_fake.py`, `jobs/`, `api/` (vendor excluded); vendored-source record with commit id
- [ ] T133 [P] [backend] Refactor, XML docs, HO:polish with Domain/Persistence/Api coverage
- [ ] T134 [P] [audio-engine] Refactor, TSDoc, frame-budget benchmark of all views together (<= 4 ms, counted), HO:polish with coverage
- [ ] T135 [P] [frontend] Accessibility: keyboard paths for every view, text alternatives (chord sheet, `drum-bar-text`, announcer), reduced motion, focus order; help texts final with limits and the BTC credit; HO:polish (FR-061, FR-062, FR-064)
- [ ] T136 [P] [qa] axe checks on every new area (0 serious/critical), traceability matrix (US1-US8, FR-001..FR-078, per family), HO:polish (SC-008)
- [ ] T137 [P] [devops] Validate compose/CI incl. fake-mic projects; image size delta (BTC weights are in the cache, not the image) recorded; HO:polish
- [ ] T144 [devops] (added in Phase 0, LESSONS 014/015) Split the CI E2E job before the eight `instrument-*` specs land: the default suite and the feature passes (recording, practice-flow, agent, lyric, restart, gpu, instrument, fake-mic) run as parallel jobs, each under 90 minutes; acceptance: the workflow validates, each job names its projects/`--grep`, and their union equals today's set plus the 016 projects
- [ ] T138 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T139 [CTO] Merge the owner branches; full build, lint, unit + coverage gates; Playwright suite incl. the eight `instrument-*.spec.ts` on the fake stack (retries 0); `pytest -m integration worker/tests/harmony worker/tests/analysis` once (T063, pYIN)
- [ ] T140 [CTO] Reference-machine validation quickstart V1-V14 with a real song per family (incl. a song with a key change and one in E-flat for capo); record times vs SC-001/SC-003 and the pYIN time, refine `StemStudio:InstrumentViews:Estimates` test-first if off by more than 50 %; SC-007 performance trace; SC-009 first-time-user check per family; record in `docs/STATUS.md`
- [ ] T141 [CTO] `docs/THIRD-PARTY-LICENSES.md`: vendored BTC subset (MIT, Park et al. 2019, commit `2682317b`), weights `btc-majmin` and `btc-large-voca` (MIT, URL, SHA-256); note that no new package was added; README "Instrument views" section (families, limits, microphone privacy)
- [ ] T142 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR
- [ ] T143 [CTO] Update docs/public manuals and regenerate PDFs (constitution 1.3.1, last step of the integration wave): user manual chapter "Instrument views" (per family, key, capo, singing privacy), technical manual (analyses, lane, worklet, contracts 1.15.0/1.9.0/2.6.0, models); `cd tools/docs && npm ci && npm run browser && npm run build`; commit sources, PDFs and `docs/public/build-manifest.json`; `node tools/docs/check.mjs` passes

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding; stories of different families are independent at the contract level.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| beat-analysis | T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> T022 -> T023 -> T024 -> T025 -> T026 -> T027 -> T028 -> HO:foundational -> T130 (HO:polish) | `harmony/{chroma_hmm,postprocess,labels,key}.py`, `analysis/vocal_pitch.py`, `notation/{tablature,parts,chord_symbols,drum_midi}.py`, their tests | synthetic chroma/posteriors, rendered triads, sines |
| stem-separation | T029 -> T030 -> T031 -> T032 -> T033 -> T034 -> HO:foundational -> T061 -> T062 -> T063 -> HO:us1 -> T077 -> T078 -> HO:us2 -> T084 -> T085 -> HO:us3 -> T093 -> T094 -> HO:us4 -> T132 (HO:polish) | `harmony/{__init__,btc}.py`, `harmony/vendor/**`, `views_fake.py`, `jobs/instrument_analysis_*`, `api/instrument_analysis_routes.py`, `main.py`, tests | fake torch, fake `DeviceRunner`, fake post-processor/notator, fake clock |
| audio-ingest | T035 -> T036 -> T037 -> T038 -> HO:foundational -> T131 (HO:polish) | `analysis_inputs/**`, `engraving/tablature.py`, tests | generated FLACs, MusicXML fixtures |
| backend | T039 -> ... -> T052 -> HO:foundational -> T064 -> T065 -> T072 -> T073 -> HO:us1 -> T079 -> T080 -> HO:us2 -> T086 -> T087 -> HO:us3 -> T095 -> T096 -> HO:us4 -> T108 -> T109 -> HO:us5 -> T116 -> T117 -> HO:us7 -> T123 -> T124 -> HO:us8 -> T133 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, tests | stub worker handler with 1.9.0 examples, SQLite, fake clock |
| audio-engine | T053 -> T054 -> T055 -> T056 -> HO:foundational -> T066 -> T067 -> T068 -> T069 -> HO:us1 -> T088 -> T089 -> HO:us3 -> T097 -> ... -> T104 -> HO:us4 -> T118 -> T119 -> HO:us7 -> T125 -> T126 -> HO:us8 -> T134 (HO:polish) | `src/audio-engine/src/**` (not `types.ts`), `src/audio-engine/tests/**` | AudioContext/worklet/canvas/rAF mocks, fake 015 input |
| frontend | T057 -> T058 -> T059 -> T060 -> HO:foundational -> T070 -> T071 -> T074 -> T075 -> HO:us1 -> T081 -> T082 -> HO:us2 -> T090 -> T091 -> HO:us3 -> T105 -> T106 -> HO:us4 -> T110 -> T111 -> HO:us5 -> T113 -> T114 -> HO:us6 -> T120 -> T121 -> HO:us7 -> T127 -> T128 -> HO:us8 -> T135 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake API client, fake engine module, fake level context, bUnit |
| qa | T013 -> T014 -> T076 -> HO:us1 -> T083 -> HO:us2 -> T092 -> HO:us3 -> T107 -> HO:us4 -> T112 -> HO:us5 -> T115 -> HO:us6 -> T122 -> HO:us7 -> T129 -> HO:us8 -> T136 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | suite executed in T139 |
| devops | T011 -> T012 -> T144 -> HO:foundational -> T137 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | skeleton |
| quality-gate | T009 -> T010 -> HO:foundational -> T138 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 -> T006 -> T007 -> T008 (before the wave; contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | T139 -> T140 -> T141 -> T142 -> T143 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, `docs/public/**`, merges | n/a |

Task count per owner (lane): architect 8, quality-gate 3, devops 3, qa 11, beat-analysis 15,
stem-separation 16, audio-ingest 5, backend 31, audio-engine 23, frontend 23, CTO 5 (total 143:
T001-T143), plus T144 (devops, Phase 0).

Task count per story: foundational/setup 60 (T001-T060), US1 16, US2 7, US3 9, US4 15, US5 5,
US6 3, US7 7, US8 7, polish 9, integration 5.

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T004 worklet build entry | audio-engine editing the build config | architect wrote `vite.pitch.config.ts` and the registered processor stub (Phase 0), audio-engine fills the processor (T100) |
| T012 worklet served by the web image | devops editing engine code | devops only copies the build output |
| T058 preset fix | 004 code | same owner (frontend), additive behaviour change recorded in DELTA 6 |
| T063 accuracy below target | tuning in two owners' code | reported in the hand-off as a tuning request (constants in `harmony/postprocess.py`, `harmony/key.py`) |
| T141 licences | owner hand-offs | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 after 014 and 015 are merged (T001 reconciles; the 016 minors sit on 1.14.0/1.8.0/2.5.0)
-> wave -> integration wave. Inside the wave every owner follows its sequence; the eight stories
are independent at the contract level (fakes), so a family never waits for another family. US7
and US8 reuse US1's chords job and chord views only through contracts (key fields and
`setChordDisplay` exist from Phase 0).

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component (vendor excluded), exact numbers in the hand-off; integration accuracy tests pass on the reference machine (I, SC-001, SC-010)
- [ ] SIG limits respected; `tools/quality/run_all.py` passes incl. the new dependency rules (II)
- [ ] Implementations match worker 1.9.0, backend 1.15.0, engine 2.6.0, UI ids 1.15.0; contract tests pass; shared chord vectors pass in C# and TS (III)
- [ ] Only OSI code; no GPL import; vendored BTC with LICENSE; weights downloaded with SHA-256, loaded with `weights_only=True`; no microphone data to the server (IV)
- [ ] Limits shown per analysis; fallback and out-of-date states visible; every family has a view and an E2E case (V, SC-006)
- [ ] No secrets; inputs validated; analyzers/formatters/linters clean; public APIs documented
- [ ] docs/public manuals updated and PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.3.1)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
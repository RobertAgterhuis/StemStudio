---

description: "Task list for feature 020 Insights and handouts (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Insights and Handouts

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [DELTA.md](DELTA.md),
[contracts/](contracts/), constitution 1.3.1, ADR-016, ADR-017, ADR-029.

**Status**: Phase 0 done (2026-11-23, architect; DELTA.md "Phase 0 outcome"). The wave may start.
**G2 is pending** (no MUSDB18-HQ): the tasks marked **(G2)** are struck through and do not run in
this wave; G1 is not run, so T039 waits for the dataset and every label stays `indicative`.
Lanes do not tick this file (merge conflicts, LESSONS 022/023): they report done tasks with commit
hashes in their hand-offs and the CTO ticks at integration.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. The real RoFormer model runs only in T092
(`@pytest.mark.integration`, excluded from the default CI run). The V2 validation (T038) uses
MUSDB18-HQ locally and is never committed or run in CI.

**Organization**: grouped by user story (US1..US5); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-020-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- audio-engine has no tasks in this feature.

## Acceptance rule (binding for every task, Phase 0 ready check)

- **Test task** ("Failing tests ..."): done when the named test file(s) exist, cover every case the
  task lists, run in the default suite, **compile/import** (add the stubs the tests need in the same
  commit, LESSONS 016) and fail only for the missing behaviour; committed as the red commit.
- **Implementation task**: done when its test task passes, the owner's component keeps >= 90 % line
  AND branch coverage, lint/format/type checks are clean (`dotnet format StemStudio.slnx
  --verify-no-changes` on the whole solution; `ruff`, `mypy --strict`; `eslint`/`tsc` for e2e) and
  `python tools/quality/run_all.py <paths>` reports 0 errors with the duplication step's own output
  read (LESSONS 022/023); committed as the green commit.
- Other tasks carry their own **Done when** below.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-020-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us5`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`
in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`jobs/`, `api/`, composition) | analyzer, renderer, re-separator | `ports.py` `QualityAnalyzer`, `HandoutRenderer`, `Reseparator` (T001) | fakes returning fixed `QualityResult` / PDF bytes |
| beat-analysis (`insights/`) | stem audio, result shapes | `ports.py`, `schemas.py` (T001), data-model 4 | generated signals (own generator in `worker/tests/insights/synthetic.py`) |
| audio-ingest (`handouts/`) | document shape, style assets | `schemas.py` `HandoutDocument` (T001), `handouts/assets/` (T007) | documents built in the test; small PDFs written with pypdf as notation fixtures |
| backend | worker insights/handout/reseparation jobs; 014/016 data | worker-api 1.11.0 (T003; examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/`); source ports (T024) | stub `HttpMessageHandler`; fake sources |
| frontend | insights, handouts, practice notes API | backend-api 1.19.0 (T003) + Contracts DTOs (T002) | fake `IStemStudioApiClient` |
| qa | UI | `ui-test-ids.md` 1.19.0 (T003), fake stack with fake analyzer | suite authored in the wave, executed in T102 |
| devops | system libraries, env vars | data-model 1, research R-07 (T006 findings) | skeleton |

---

## Phase 0: Contract code, measurements and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] Worker contract code: `schemas.py` 1.11.0 (`CONTRACT_VERSION`; quality, handout and (G2) re-separation models of contracts/worker-api.delta; `Health.insights`, `Health.lanes.insights`, (G2) `Health.separationPipelines`; allow-list additions), `ports.py` (`StemAudio`, `QualityAnalyzer.analyze(request, progress)`, `HandoutRenderer.render(document, paper, progress) -> RenderedHandout`, `NotationReader`, (G2) `Reseparator`; `QualityAnalysisError`, `HandoutRenderError`, `NOTICE_FOOTER`, `NOTICE_FULL`), `config.py` (`quality_analyzer`, `insights_threads`, (G2) `reseparator`); `pyproject.toml` + `uv.lock`: `weasyprint==70.0`, `jinja2` direct, transitive deps recorded; mypy overrides; skeleton tests `worker/tests/test_insights_contract.py`, `worker/tests/test_handout_contract.py` (FR-003, FR-012, FR-017). **Outcome**: no G2 parts; `InsightsSettings` (`settings.insights.quality_analyzer`, `.threads`); per-job allow-lists `is_quality_input_path`/`is_handout_input_path`; renderer port `render(request, files, progress)`; tests in `worker/tests/contract/`
- [x] T002 [P] [architect] .NET contract code: `src/StemStudio.Contracts/{Insights,Handouts,Separation}/*` (DELTA.md 3), `AssetDto.Handout`, `ProvenanceDto.HandoutRecipe`, `TrackDto.Quality` (+ G2 fields), enum additions, `ApiRoutes`, `ContractLimits`; skeleton tests `tests/StemStudio.Api.Tests/Contract/{Insights,Handout}ContractTests.cs`; operations staged in `PendingInsightsOperations` (FR-017)
- [x] T003 [P] [architect] Contracts: merge the deltas into full `worker-api.openapi.yaml` 1.11.0, `backend-api.openapi.yaml` 1.19.0 and `ui-test-ids.md` 1.19.0 on top of `main` (drop copied base schemas, keep examples), Redocly lint clean apart from documented warnings; help-topics.md unchanged unless 014/016 add screens
- [x] T004 [architect] Reconcile 014/016 (DELTA.md 6): record the merged shapes of sections, setlists, practice log, chords and 016 notation items; adapt data-model 6.1/8 and the adapters' contracts; decide reuse vs replace of any 014 setlist PDF; record under "Phase 0 outcome"
- [x] T005 [architect] Gate G2 spike (throw-away container, GPU image, read-only volumes; research R-06): RoFormer candidate facts (URL, SHA-256, size, licence status per ADR-009, NVML peak VRAM -> `GpuNeed`, s/min GPU and CPU); sum-of-stems vs original difference on fixtures; MUSDB18-HQ test split production vs `vocals-hq` SDR/SIR with `museval` 0.4.1; decide G2; record in research.md, ADR-029 and DELTA.md (remove G2 parts if it fails) (SC-007). **Outcome**: G2 pending owner (no dataset); candidate facts, VRAM and timings measured; G2 parts deferred
- [x] T006 [P] [architect] P0-4 WeasyPrint spike in the worker image (research R-07): Pango/HarfBuzz versions in both variants, Archivo variable WOFF2 incl. `font-stretch` 125 % (else static instances), chord-row alignment, a 20-song throw-away setlist <= 90 s, `url_fetcher` hook blocks `http(s)://`; record findings for devops (packages) and audio-ingest. **Outcome**: passed (research.md "Phase 0 measurements"); Pango 1.56.3 already in the image; WeasyPrint 70 needs a `URLFetcher` subclass
- [x] T007 [P] [architect] `worker/scripts/sync_handout_style.py` (copies the light-theme token block of `src/StemStudio.Web/wwwroot/css/tokens.css` and `wwwroot/fonts/archivo/*` into `worker/src/stemstudio_worker/handouts/assets/`, writes `manifest.json` with SHA-256 of sources and copies) with tests `worker/tests/scripts/test_sync_handout_style.py` (FR-012)
- [x] T008 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, `tools/quality/run_all.py` 0 errors; ADR-029 -> Accepted; hand-off `docs/handoffs/architect-020-plan.md` (versions, dependency + licence records, G1 protocol hand-over to beat-analysis, G2 result)

**Checkpoint**: skeleton green, G2 decided; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T009 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `insights` imports only `ports`, `schemas`, numpy, scipy, soxr, soundfile; `handouts` only `ports`, `schemas`, jinja2, weasyprint, pypdf (and fontTools for the glyph check, if audio-ingest uses it); neither imports the other, `jobs`, `api` or `separation`; drift check: `worker/scripts/sync_handout_style.py --check` exit 0 (manifest hashes equal the app sources); no literal colours in `handouts/styles/*.css` (reuse the `tools/docs` guard); `handouts` never passes a plain function as `url_fetcher` (must be the `URLFetcher` subclass of `fetcher.py`)
- [ ] T010 [quality-gate] Implement the rules in `tools/quality/`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T011 [P] [devops] Write the image check first and show it fail on the current image: `scripts/check-worker-pdf.{ps1,sh}` (`python -c "import weasyprint; print(weasyprint.__version__)"` as the worker user, Pango >= 1.44, `fc-list` lists DejaVu Sans) (FR-012, SC-005). **Done when** the check fails on the current image (no weasyprint) with a readable message and the failing run is in the hand-off
- [ ] T012 [devops] `docker/worker.Dockerfile` (both variants): `libpango-1.0-0`, `libpangoft2-1.0-0` listed explicitly (Pango/HarfBuzz are already pulled in transitively, P0-4), fontconfig, `fonts-dejavu-core`, `fc-cache`; `docker-compose.yml` (and the E2E/fake compose files) pass `STEMSTUDIO_QUALITY_ANALYZER`, `STEMSTUDIO_INSIGHTS_THREADS`; CI `CONTRACTS_DIR`/`scripts/test-all.*` -> `specs/020-insights-handouts/contracts` **while the 016-only checks stay pinned to `specs/016-instrument-views/contracts`** (`generate_chord_spelling_vectors.py --check`, the 016 contract pytest) and `merge_full.py --check` runs for 020; add `worker/scripts/sync_handout_style.py --check` to the worker step; rules for new inputs in `scripts/affected/components.json`; wire T011 into the image job. **Done when** T011 passes on both rebuilt images, `scripts/test-all.*` and CI lint the 020 documents and the 016 checks still run
- [ ] T013 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: V1 synthetic validation, fake analyzer for E2E, per-family fixtures, PDF inspection, traceability US1..US4 -> test files (US5 deferred). **Done when** every FR and SC of spec.md (except the deferred FR-007/SC-007) maps to at least one named test file

---

## Phase 2: Foundational (blocking inside each owner)

- [ ] T014 [P] [beat-analysis] Failing tests `worker/tests/insights/test_representation.py`: block-wise read, mono, 22,050 Hz, 64-band mel power, mix = sum of the six stems, activity floor, memory bound for 15 min (data-model 4.1)
- [ ] T015 [beat-analysis] Implement `insights/representation.py`
- [ ] T016 [P] [stem-separation] Failing tests: `insights` lane (FIFO, one at a time, thread cap, health counters), quality and handout job stores (state machine, 24 h sweep, restart -> "interrupted by restart", idempotent delete) (data-model 2.1, 2.4, 2.5)
- [ ] T017 [stem-separation] Implement the lane and `jobs/{quality,handout}_store.py`
- [ ] T018 [P] [audio-ingest] Failing tests `worker/tests/handouts/test_fetcher.py`: the fetcher is a `weasyprint.urls.URLFetcher` subclass (WeasyPrint 70 API, P0-4) with `allowed_protocols=("file",)`; it serves only the real paths the job passed in `files` and the packaged assets; `..`, absolute paths, symlinks out of the root, `http(s)://`, `data:` beyond images, `file://` elsewhere all refused (FR-018)
- [ ] T019 [audio-ingest] Implement `handouts/fetcher.py`
- [ ] T020 [P] [backend] Failing tests (Persistence): `QualityReport` JSON column, `PracticeNotes` table with cascade, migrations `AddQualityReport`, `AddPracticeNotes`; `Asset.Kind = handout`, `Provenance.HandoutRecipe` round trip, `listAssets?kind=handout` (data-model 5.1, 5.2, 5.4)
- [ ] T021 [backend] Implement entities, configurations and migrations
- [ ] T022 [P] [backend] Failing tests `WorkerClient.Insights`/`.Handouts` against the copied worker examples (create, poll, file download, delete; 400/404/409/422 mapping)
- [ ] T023 [backend] Implement the worker clients
- [ ] T024 [P] [backend] Failing tests for the source ports and adapters `ISectionSource`, `ISetlistSource`, `IPracticeLogSource`, `IChordSource`, `INotationCatalog` over the merged 014/016 services, using the shapes of DELTA.md "Phase 0 outcome" P0-4 (setlist `note` "" -> null; practice progress = `bestTrainerPercent`; chords as 016 chart + key; notation by `ScoreFacts.profile/tablature/part`)
- [ ] T025 [backend] Implement the adapters
- [ ] T026 [P] [frontend] Failing tests: feature-catalogue entries and levels (help-topics.md), every new topic file loads and validates against the 004 schema
- [ ] T027 [frontend] Add the catalogue entries and the help topic files (FR-016; not the deferred G2 topic/entry)
- [ ] T028 [P] [qa] Failing tests for the per-family fixture builder `tests/fixtures/families/` (one fixture song with sections, chords, word-timed lyrics, and score/tab/drum/transposed-part PDFs for all seven families; a three-song setlist; seeding through the fake-stack API)
- [ ] T029 [qa] Implement the fixture builder and the E2E seeding helper (SC-006)

**Checkpoint**: HO:foundational per owner.

---

## Phase 3: User Story 1 - See how clean each stem is (P1)

- [ ] T030 [P] [US1] [beat-analysis] Failing tests `test_bleed.py`: leakage share is linear in injected alpha (0, 0.03, 0.1, 0.3), `mainSource` correct, quiet-frame guard, co-occurrence cap for the four pairs, detail stems vs standard and siblings (data-model 4.2, 4.3)
- [ ] T031 [US1] [beat-analysis] Implement `insights/bleed.py`
- [ ] T032 [P] [US1] [beat-analysis] Failing tests `test_artefacts.py`: flicker ratio rises with TF-mask probability, dropouts with gating rate, both ~1 on clean stems relative to the mix; detail kinds `model-residual` and `rest-share` (data-model 4.4)
- [ ] T033 [US1] [beat-analysis] Implement `insights/artefacts.py`, `insights/detail.py`
- [ ] T034 [P] [US1] [beat-analysis] Failing tests `test_rating.py` + `test_validation_v1.py`: levels, rating order, `not-present`, `too-short`, reason codes; V1 Spearman >= 0.8 and >= 80 % correct levels, "playing together" control never High (FR-003, SC-002)
- [ ] T035 [US1] [beat-analysis] Implement `insights/rating.py`, `thresholds.py` (calibrated on V1, `ANALYZER_VERSION`), `analyzer.py`
- [ ] T036 [P] [US1] [beat-analysis] Failing tests for the fake analyzer (fixed ratings per channel, data-model 2.6)
- [ ] T037 [US1] [beat-analysis] Implement `insights/fake.py`
- [ ] T038 [P] [US1] [beat-analysis] Failing test: the reliability table in `thresholds.py` starts with **every** dimension `indicative` and "not measured" (G1 not run, Phase 0), marks guitar, piano, detail and rest stems always `indicative`, keeps all levels for not-measured dimensions and replaces low/medium by `unknown` only for a dimension marked "failed" (FR-004, data-model 4.5)
- [ ] T039 [US1] [beat-analysis] **Blocked on the owner's dataset** (Phase 0): write the G1 run script (research R-04 V2: separate with `htdemucs_6s`, `museval` 0.4.1 in a throw-away environment, Spearman per stem type and dimension) as an `@pytest.mark.integration` test or a script under `worker/scripts/` that never commits data; run it when MUSDB18-HQ is available and set the reliability table. **Done when** the script runs on a two-track stand-in (synthetic) end to end, and either the results table is in the hand-off or the hand-off states "pending dataset" 
- [ ] T040 [P] [US1] [stem-separation] Failing tests quality routes and service: create (six standard stems or 422, path 400/404), get, delete/cancel, progress stages, composition picks real/fake from settings, `/health.insights` (contracts worker 1.11.0)
- [ ] T041 [US1] [stem-separation] Implement `api/insights_routes.py`, `jobs/quality_service.py`, composition in `main.py`
- [ ] T042 [P] [US1] [backend] Failing tests `QualityService`, `QualityJobApplier`, `QualityTrigger`: request (409 `track-not-ready`, `quality-running`, 503), automatic run after standard and detail completion (setting on/off), previous ratings kept, failure keeps the last ready stems, `outdated` (analyzer version, `StemsVersion`) (FR-001, FR-002)
- [ ] T043 [US1] [backend] Implement them
- [ ] T044 [P] [US1] [backend] Failing tests endpoints `GET/POST /api/tracks/{id}/quality`, `Track.quality` badge, reason texts of data-model 4.6, disclaimer
- [ ] T045 [US1] [backend] Implement the endpoints
- [ ] T046 [P] [US1] [frontend] Failing bUnit tests `QualityPanel`, `QualityRow`, `QualityBadge`: states, polling, level visibility (Beginner/Advanced/Expert), text + icon (R20-1), `aria-label`, outdated notice, "was ..." (US1 scenarios 1-4, FR-016)
- [ ] T047 [US1] [frontend] Implement the components on the track page and the mixer strip badge
- [ ] T048 [US1] [qa] E2E `e2e/tests/stem-quality.spec.ts`: report after separation for all six stem types, detail rows after a split, levels per journey level, "Check again" on an existing track (wait on the report's own state, LESSONS 013/022). **Done when** the spec typechecks and lints in the wave and passes on a real fake stack at the mid-wave checkpoint (LESSONS 022/023)

**Checkpoint**: HO:us1 per owner.

---

## Phase 4: User Story 2 - Get a tip that leads to an action (P1)

- [ ] T049 [P] [US2] [backend] Failing tests `TipRules` (Domain): each rule of data-model 5.3, ordering, max two, actions, no GPU tip ever (FR-006), rule 3 only when the pipeline is available (G2 off -> never)
- [ ] T050 [US2] [backend] Implement `Domain/Insights/TipRules.cs`
- [ ] T051 [P] [US2] [backend] Failing tests: report view builds tips from the current track state (detail phase, outcomes, stem set) and the 003 device estimate text; tips change after a detail split without re-analysis (FR-005)
- [ ] T052 [US2] [backend] Implement the view assembly
- [ ] T053 [P] [US2] [frontend] Failing tests `TipList`: split-detail calls the 002 start endpoint, open-detail navigates to the mixer with the parent expanded, mute patches the current mix, buttons keyboard operable
- [ ] T054 [US2] [frontend] Implement `TipList` and the action handlers
- [ ] T055 [US2] [qa] E2E tips: other -> "Split further" starts the detail separation and the report is remade; piano -> mute; guitar -> hard to isolate; no tip text contains "GPU" (SC-003). **Done when** as T048

**Checkpoint**: HO:us2 per owner.

---

## Phase 5: User Story 3 - One-click lesson handout per song and family (P1)

- [ ] T056 [P] [US3] [backend] Failing tests `HandoutChords` (Domain, an adapter over 016's `ChordSpeller`, `ChordDisplay` and `KeyFormatter`; no own parser): symbols with ASCII accidentals that match the contract's `ChordSymbol` pattern (slash chords, `N.C.`), transposition B-flat +2, E-flat +9, F +7 with the target key's spelling, `transpositionNote` texts (FR-011)
- [ ] T057 [US3] [backend] Implement `HandoutChords`
- [ ] T058 [P] [US3] [backend] Failing tests: chords over lyrics map 016's `ChordSheetBuilder` rows to `LyricLine` (word-synced, line-synced, instrumental rows with empty text, `chordPlacement` word/line/none) and `BarMapper` (start bar from 014 `startBar` when set else from the grid, pickup 0, bars per section, chords per bar, `%` repeats) (research R-08 with the Phase 0 note)
- [ ] T059 [US3] [backend] Implement them
- [ ] T060 [P] [US3] [backend] Failing tests `IngredientPlanner`: defaults per family (data-model 6.2), omissions with reasons, 422 for codes not offered to the family, notation selection per family (6.1) (FR-008, FR-010)
- [ ] T061 [US3] [backend] Implement `IngredientPlanner` and `Families`
- [ ] T062 [P] [US3] [backend] Failing tests `HandoutAssembler` (song): facts (tempo, metre, key source), sections, lyrics with chords, notes, fixed notice texts, attributions (lyrics source, piano CC BY 4.0, artwork), naming, `generatedOn` from `TimeProvider` (FR-013)
- [ ] T063 [US3] [backend] Implement `HandoutAssembler`, `HandoutNotice`, `HandoutNaming`
- [ ] T064 [P] [US3] [backend] Failing tests `HandoutOptionsService`, `HandoutService`, endpoints: options with ingredient status and create-actions, create (202 asset, 409 codes, 422, 503), asset sync `WorkerJobKind.Handout` fills `Asset.handout`, "Make again" with the recipe, track deletion keeps the item (FR-014)
- [ ] T065 [US3] [backend] Implement them
- [ ] T066 [P] [US3] [backend] Failing tests practice notes endpoints: per family, 4,000 limit, control characters removed, empty deletes (204) (FR-015)
- [ ] T067 [US3] [backend] Implement them
- [ ] T068 [P] [US3] [audio-ingest] Failing tests `test_layout.py`: a song document renders to a PDF whose text (pypdf) contains facts, form chart, chords above the right words, notes; footer on every page; outline; title and `lang` metadata; HTML in notes is escaped; tokens only (FR-012)
- [ ] T069 [US3] [audio-ingest] Implement `handouts/templates/*.html.j2`, `styles/handout.css`, `layout.py`
- [ ] T070 [P] [US3] [audio-ingest] Failing tests `test_notation.py`: one frame page per notation page, scaling A4 -> Letter without cropping (`notation-scaled`), unreadable PDF -> note page + `notation-unreadable`, page count and outline stay correct
- [ ] T071 [US3] [audio-ingest] Implement `handouts/notation.py`
- [ ] T072 [P] [US3] [audio-ingest] Failing tests `test_renderer.py`: notice texts validated, missing glyphs detected and warned, fallback font used, deterministic page count and outline for the same document, no network (fetcher spy) (SC-005, FR-018)
- [ ] T073 [US3] [audio-ingest] Implement `handouts/document.py`, `renderer.py`
- [ ] T074 [P] [US3] [stem-separation] Failing tests handout routes: create (422 notice/song count, 400/404 paths), get, file (409 `handout-not-completed`), delete; composition
- [ ] T075 [US3] [stem-separation] Implement `api/handout_routes.py`, `jobs/handout_service.py`
- [ ] T076 [P] [US3] [frontend] Failing tests `HandoutMenu` (seven families, one-click create, toast with open action, Beginner "Not included" list) and `PracticeNotesEditor` (family selector, counter, save status live region)
- [ ] T077 [US3] [frontend] Implement them on the track page
- [ ] T078 [P] [US3] [frontend] Failing tests `HandoutDialog` (Advanced+: ingredients with status and create links, notation choices, transposition only for melodic, paper, estimate, notice) and `HandoutAssetRow` (preview, download, omitted, warnings, make again, `library-filter-handout`)
- [ ] T079 [US3] [frontend] Implement them
- [ ] T080 [US3] [qa] E2E `e2e/tests/handouts.spec.ts`: seven families parametrised on the fixture song (defaults in `handout.included`, PDF downloads, page count via pdf-lib), melodic B-flat, a missing ingredient listed, practice note appears (SC-006). **Done when** as T048

**Checkpoint**: HO:us3 per owner.

---

## Phase 6: User Story 4 - Setlist handout (P2)

- [ ] T081 [P] [US4] [backend] Failing tests setlist assembly: entry order, entry notes (`note` "" -> null), only the entries 014 still has (deleted tracks are already removed), a song that is not completed included with its available ingredients, > 50 entries -> 409 `too-many-songs`, no entries -> 409 `setlist-empty` (FR-009)
- [ ] T082 [US4] [backend] Implement setlist assembly and the setlist source path of the endpoints
- [ ] T083 [P] [US4] [audio-ingest] Failing tests setlist layout: cover, running order, contents with page numbers, each song on a new page, one bookmark per song, 20-song render within the budget (SC-004)
- [ ] T084 [US4] [audio-ingest] Implement the setlist templates
- [ ] T085 [P] [US4] [frontend] Failing tests `setlist-handout-menu` and customise entry on the setlist page
- [ ] T086 [US4] [frontend] Implement them
- [ ] T087 [US4] [qa] E2E `e2e/tests/setlist-handout.spec.ts` (three-song fixture setlist, running order and bookmarks via facts/outline). **Done when** as T048

**Checkpoint**: HO:us4 per owner.

---

## Phase 7: User Story 5 - High-quality vocal re-separation (P3, **(G2)**)

- [ ] ~~T088 [P] [US5] [stem-separation] (G2) Failing tests `separation/hq/pipeline.py` with fake models: input = sum of six stems, instrumental = input - vocals, Demucs vocals folded into `other` or dropped by the 002 gate, `DeviceRunner` use and CPU fallback, cache by summed-input hash, progress stages (data-model 2.7)~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T089 [US5] [stem-separation] (G2) Implement the RoFormer adapter on the vendored MSST code, the pipeline and the fake re-separator~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T090 [P] [US5] [stem-separation] (G2) Failing tests re-separation routes, job store and stem files; `Health.separationPipelines`~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T091 [US5] [stem-separation] (G2) Implement them~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T092 [US5] [stem-separation] (G2) `@pytest.mark.integration` test: real RoFormer checkpoint (SHA-256 verified download) on the short fixture, six non-silent stems~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T093 [P] [US5] [backend] (G2) Failing tests `ReseparationService`, `StemSetReplacer`: 409 codes, `discardDetail` consent, atomic replacement (temp + rename), mix pruning, failure/cancel keeps stems, `StemSet` set, quality trigger with previous ratings (FR-007)~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T094 [US5] [backend] (G2) Implement them~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T095 [P] [US5] [backend] (G2) Failing tests options and endpoints (estimate per device, consequences texts, cancel)~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T096 [US5] [backend] (G2) Implement them~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T097 [P] [US5] [frontend] (G2) Failing tests `ReseparateDialog` (estimate, consequences, discard-detail checkbox only with detail stems), phase progress, `stem-set-pipeline`~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T098 [US5] [frontend] (G2) Implement them~~ (deferred: G2 pending, Phase 0)
- [ ] ~~T099 [US5] [qa] (G2) E2E `e2e/tests/reseparation.spec.ts` (fake re-separator: tip -> dialog -> stems replaced -> "was ..." ratings)~~ (deferred: G2 pending, Phase 0)

**Checkpoint**: HO:us5 per owner.

---

## Phase 8: Polish and integration

- [ ] T100 [quality-gate] `tools/quality/run_all.py` on all 020 code (SIG limits, duplication, dependency rules, style drift); refactor requests to owners; update `docs/quality/SIG-REPORT.md`. **Done when** `run_all.py` exits 0 with jscpd's own output read and the report lists the 020 numbers
- [ ] T101 [P] [qa] Accessibility: axe scans of the quality panel, tips, handout menu/dialog, practice notes, setlist menu; keyboard-only paths; screen-reader names (FR-016). **Done when** the scans report no serious/critical violation and every new control is reached and operated by keyboard in a spec
- [ ] T102 [qa] Run the full E2E suite on the fake stack (Chromium + Firefox); traceability table in `docs/testing/`. **Done when** the default suite and the 020 specs are green on both browsers on a real fake stack (own compose project) and the table maps US1-US4 to specs
- [ ] T103 [P] [beat-analysis] SC-001 timing (marked integration): 5-minute reference song, six stems + twelve detail stems, analysis <= 30 s on the reference CPU; result in the hand-off. **Done when** the measured seconds are in the hand-off and are <= 30 (else a refactor request)
- [ ] T104 [P] [audio-ingest] SC-004 timing: song handout <= 15 s and 20-song setlist <= 90 s in the worker image; result in the hand-off. **Done when** both measured times are in the hand-off and within budget (P0-4 spike: 0.9 s and 8.0 s)
- [ ] T105 [CTO] Manuals (constitution 1.3.1): user manual chapter "Stem quality and lesson handouts" (ratings, tips, honesty note, families, transposition, notice), technical manual (insights lane, env vars, Pango/fonts); regenerate PDFs; `node tools/docs/check.mjs`. **Done when** `check.mjs` passes after the last edit
- [ ] T106 [CTO] `docs/THIRD-PARTY-LICENSES.md` and README: WeasyPrint and its Python deps, Pango, HarfBuzz, DejaVu fonts, Jinja2/MarkupSafe as direct deps (rows added by the architect in Phase 0; CTO reviews), README mention. **Done when** every new runtime dependency of the hand-offs has a row
- [ ] T107 [CTO] Integration wave: merge the owners' branches, contract tests against the full 1.11.0/1.19.0 documents, quickstart V1-V10 (without the G2 steps), SC-008 moderated check, `docs/STATUS.md`. **Done when** `scripts/test-all.*` and CI are green on the merge commit, every job to the end
- [ ] T108 [architect] Post-wave reconciliation: contract refinements from the wave in DELTA.md "Reconciliation", research.md "Phase 0 measurements" completed with T039/T103/T104 results. **Done when** DELTA.md and research.md hold the numbers and every contract change of the wave is merged into the full documents (`merge_full.py --check` green)

---

## Dependencies and order

- Phase 0 is done; G2 is pending, so T088-T099 do not run in this wave.
- Within each owner: Phase 2 before its story tasks; test task before its implementation task.
- US2 builds on US1 (report data); US4 builds on US3 (assembler, layout); US5 on US1/US2.
- qa E2E tasks (T048, T055, T080, T087, T099) are authored in the wave and executed in T102.
- T105/T106 before T107 closes the integration wave.

## Parallel examples

```text
Wave start (after T008): T009, T011, T013, T014, T016, T018, T020, T022, T024, T026, T028 in parallel.
US1: T030, T032, T034, T036, T038 (beat-analysis, sequential pairs) || T040 || T042, T044 || T046.
US3: T056, T058, T060, T062, T064, T066 (backend pairs) || T068, T070, T072 || T074 || T076, T078.
```

## Execution plan per owner (parallel wave)

| Owner | Tasks (in order) | Hand-offs |
|---|---|---|
| architect | T001-T008 (Phase 0), T108 | HO:plan, HO:reconciliation |
| quality-gate | T009, T010, T100 | HO:foundational, HO:polish |
| devops | T011, T012 | HO:foundational |
| beat-analysis | T014, T015, T030-T039, T103 | HO:foundational, HO:us1, HO:polish |
| stem-separation | T016, T017, T040, T041, T074, T075 (G2 T088-T092 deferred) | HO:foundational, HO:us1, HO:us3 |
| audio-ingest | T018, T019, T068-T073, T083, T084, T104 | HO:foundational, HO:us3, HO:us4, HO:polish |
| backend | T020-T025, T042-T045, T049-T052, T056-T067, T081, T082 (G2 T093-T096 deferred) | HO:foundational, HO:us1..us4 |
| frontend | T026, T027, T046, T047, T053, T054, T076-T079, T085, T086 (G2 T097, T098 deferred) | HO:foundational, HO:us1..us4 |
| qa | T013, T028, T029, T048, T055, T080, T087, T101, T102 (G2 T099 deferred) | HO:foundational, HO:us1..us4, HO:polish |
| CTO | T105, T106, T107 | integration |

## Task counts

| Lane | Tasks | of which G2-gated |
|---|---|---|
| architect | 9 | 0 |
| quality-gate | 3 | 0 |
| devops | 2 | 0 |
| beat-analysis | 13 | 0 |
| stem-separation | 11 | 5 |
| audio-ingest | 11 | 0 |
| backend | 32 | 4 |
| frontend | 14 | 2 |
| qa | 10 | 1 |
| CTO | 3 | 0 |
| **Total** | **108** | **12** (deferred in Phase 0) |

Per story: Phase 0 8, Setup 5, Foundational 16, US1 19, US2 7, US3 25, US4 7, US5 12, Polish 9.

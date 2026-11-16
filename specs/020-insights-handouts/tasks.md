---

description: "Task list for feature 020 Insights and handouts (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Insights and Handouts

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [DELTA.md](DELTA.md),
[contracts/](contracts/), constitution 1.3.1, ADR-016, ADR-017, ADR-029.

**Status**: planning done (2026-11-16). **Phase 0 starts when 014 and 016 are merged** (their
contract minors are the base of 020's). Tasks marked **(G2)** run only if gate G2 passes in T005;
otherwise they are struck through in this file and recorded in DELTA.md "Phase 0 outcome".

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

- [ ] T001 [architect] Worker contract code: `schemas.py` 1.11.0 (`CONTRACT_VERSION`; quality, handout and (G2) re-separation models of contracts/worker-api.delta; `Health.insights`, `Health.lanes.insights`, (G2) `Health.separationPipelines`; allow-list additions), `ports.py` (`StemAudio`, `QualityAnalyzer.analyze(request, progress)`, `HandoutRenderer.render(document, paper, progress) -> RenderedHandout`, `NotationReader`, (G2) `Reseparator`; `QualityAnalysisError`, `HandoutRenderError`, `NOTICE_FOOTER`, `NOTICE_FULL`), `config.py` (`quality_analyzer`, `insights_threads`, (G2) `reseparator`); `pyproject.toml` + `uv.lock`: `weasyprint==70.0`, `jinja2` direct, transitive deps recorded; mypy overrides; skeleton tests `worker/tests/test_insights_contract.py`, `worker/tests/test_handout_contract.py` (FR-003, FR-012, FR-017)
- [ ] T002 [P] [architect] .NET contract code: `src/StemStudio.Contracts/{Insights,Handouts,Separation}/*` (DELTA.md 3), `AssetDto.Handout`, `ProvenanceDto.HandoutRecipe`, `TrackDto.Quality` (+ G2 fields), enum additions, `ApiRoutes`, `ContractLimits`; skeleton tests `tests/StemStudio.Api.Tests/Contract/{Insights,Handout}ContractTests.cs`; operations staged in `PendingInsightsOperations` (FR-017)
- [ ] T003 [P] [architect] Contracts: merge the deltas into full `worker-api.openapi.yaml` 1.11.0, `backend-api.openapi.yaml` 1.19.0 and `ui-test-ids.md` 1.19.0 on top of `main` (drop copied base schemas, keep examples), Redocly lint clean apart from documented warnings; help-topics.md unchanged unless 014/016 add screens
- [ ] T004 [architect] Reconcile 014/016 (DELTA.md 6): record the merged shapes of sections, setlists, practice log, chords and 016 notation items; adapt data-model 6.1/8 and the adapters' contracts; decide reuse vs replace of any 014 setlist PDF; record under "Phase 0 outcome"
- [ ] T005 [architect] Gate G2 spike (throw-away container, GPU image, read-only volumes; research R-06): RoFormer candidate facts (URL, SHA-256, size, licence status per ADR-009, NVML peak VRAM -> `GpuNeed`, s/min GPU and CPU); sum-of-stems vs original difference on fixtures; MUSDB18-HQ test split production vs `vocals-hq` SDR/SIR with `museval` 0.4.1; decide G2; record in research.md, ADR-029 and DELTA.md (remove G2 parts if it fails) (SC-007)
- [ ] T006 [P] [architect] P0-4 WeasyPrint spike in the worker image (research R-07): Pango/HarfBuzz versions in both variants, Archivo variable WOFF2 incl. `font-stretch` 125 % (else static instances), chord-row alignment, a 20-song throw-away setlist <= 90 s, `url_fetcher` hook blocks `http(s)://`; record findings for devops (packages) and audio-ingest
- [ ] T007 [P] [architect] `worker/scripts/sync_handout_style.py` (copies the light-theme token block of `src/StemStudio.Web/wwwroot/css/tokens.css` and `wwwroot/fonts/archivo/*` into `worker/src/stemstudio_worker/handouts/assets/`, writes `manifest.json` with SHA-256 of sources and copies) with tests `worker/tests/scripts/test_sync_handout_style.py` (FR-012)
- [ ] T008 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, `tools/quality/run_all.py` 0 errors; ADR-029 -> Accepted; hand-off `docs/handoffs/architect-020-plan.md` (versions, dependency + licence records, G1 protocol hand-over to beat-analysis, G2 result)

**Checkpoint**: skeleton green, G2 decided; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T009 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `insights` imports only `ports`, `schemas`, numpy, scipy, soxr, soundfile; `handouts` only `ports`, `schemas`, jinja2, weasyprint, pypdf; neither imports the other, `jobs`, `api` or `separation`; `separation/hq` not imported by `insights`/`handouts`; drift check: `handouts/assets/manifest.json` hashes equal the app sources; no literal colours in `handouts/styles/*.css` (reuse the `tools/docs` guard)
- [ ] T010 [quality-gate] Implement the rules in `tools/quality/`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T011 [P] [devops] Write the image check first and show it fail on the current image: `scripts/check-worker-pdf.{ps1,sh}` (`python -c "import weasyprint; print(weasyprint.__version__)"` as the worker user, Pango >= 1.44, `fc-list` lists DejaVu Sans) (FR-012, SC-005)
- [ ] T012 [devops] `docker/worker.Dockerfile` (both variants): Pango, HarfBuzz, fontconfig, `fonts-dejavu-core`, `fc-cache`; `docker-compose.yml` passes `STEMSTUDIO_QUALITY_ANALYZER`, `STEMSTUDIO_INSIGHTS_THREADS`, (G2) `STEMSTUDIO_RESEPARATOR`; CI `CONTRACTS_DIR`/`scripts/test-all.*` -> `specs/020-insights-handouts/contracts`; wire T011 into the image job
- [ ] T013 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: V1 synthetic validation, fake analyzer for E2E, per-family fixtures, PDF inspection, traceability US1..US5 -> test files

---

## Phase 2: Foundational (blocking inside each owner)

- [ ] T014 [P] [beat-analysis] Failing tests `worker/tests/insights/test_representation.py`: block-wise read, mono, 22,050 Hz, 64-band mel power, mix = sum of the six stems, activity floor, memory bound for 15 min (data-model 4.1)
- [ ] T015 [beat-analysis] Implement `insights/representation.py`
- [ ] T016 [P] [stem-separation] Failing tests: `insights` lane (FIFO, one at a time, thread cap, health counters), quality and handout job stores (state machine, 24 h sweep, restart -> "interrupted by restart", idempotent delete) (data-model 2.1, 2.4, 2.5)
- [ ] T017 [stem-separation] Implement the lane and `jobs/{quality,handout}_store.py`
- [ ] T018 [P] [audio-ingest] Failing tests `worker/tests/handouts/test_fetcher.py`: allow-listed library paths and packaged assets only; `..`, absolute paths, symlinks out of the root, `http(s)://`, `data:` beyond images, `file://` elsewhere all refused (FR-018)
- [ ] T019 [audio-ingest] Implement `handouts/fetcher.py`
- [ ] T020 [P] [backend] Failing tests (Persistence): `QualityReport` JSON column, `PracticeNotes` table with cascade, migrations `AddQualityReport`, `AddPracticeNotes`; `Asset.Kind = handout`, `Provenance.HandoutRecipe` round trip, `listAssets?kind=handout` (data-model 5.1, 5.2, 5.4)
- [ ] T021 [backend] Implement entities, configurations and migrations
- [ ] T022 [P] [backend] Failing tests `WorkerClient.Insights`/`.Handouts` against the copied worker examples (create, poll, file download, delete; 400/404/409/422 mapping)
- [ ] T023 [backend] Implement the worker clients
- [ ] T024 [P] [backend] Failing tests for the source ports and adapters `ISectionSource`, `ISetlistSource`, `IPracticeLogSource`, `IChordSource`, `INotationCatalog` over the merged 014/016 services (data-model 8, DELTA 6)
- [ ] T025 [backend] Implement the adapters
- [ ] T026 [P] [frontend] Failing tests: feature-catalogue entries and levels (help-topics.md), every new topic file loads and validates against the 004 schema
- [ ] T027 [frontend] Add the catalogue entries and the help topic files (FR-016)
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
- [ ] T038 [P] [US1] [beat-analysis] Failing test: the reliability table in `thresholds.py` marks guitar, piano, detail and rest stems `indicative`, and `unknown` replaces low/medium for dimensions that failed G1 (FR-004)
- [ ] T039 [US1] [beat-analysis] Run G1 locally on MUSDB18-HQ (research R-04 V2; never committed), set the reliability table, put the results table in the hand-off for research.md
- [ ] T040 [P] [US1] [stem-separation] Failing tests quality routes and service: create (six standard stems or 422, path 400/404), get, delete/cancel, progress stages, composition picks real/fake from settings, `/health.insights` (contracts worker 1.11.0)
- [ ] T041 [US1] [stem-separation] Implement `api/insights_routes.py`, `jobs/quality_service.py`, composition in `main.py`
- [ ] T042 [P] [US1] [backend] Failing tests `QualityService`, `QualityJobApplier`, `QualityTrigger`: request (409 `track-not-ready`, `quality-running`, 503), automatic run after standard and detail completion (setting on/off), previous ratings kept, failure keeps the last ready stems, `outdated` (analyzer version, `StemsVersion`) (FR-001, FR-002)
- [ ] T043 [US1] [backend] Implement them
- [ ] T044 [P] [US1] [backend] Failing tests endpoints `GET/POST /api/tracks/{id}/quality`, `Track.quality` badge, reason texts of data-model 4.6, disclaimer
- [ ] T045 [US1] [backend] Implement the endpoints
- [ ] T046 [P] [US1] [frontend] Failing bUnit tests `QualityPanel`, `QualityRow`, `QualityBadge`: states, polling, level visibility (Beginner/Advanced/Expert), text + icon (R20-1), `aria-label`, outdated notice, "was ..." (US1 scenarios 1-4, FR-016)
- [ ] T047 [US1] [frontend] Implement the components on the track page and the mixer strip badge
- [ ] T048 [US1] [qa] E2E `e2e/tests/stem-quality.spec.ts`: report after separation for all six stem types, detail rows after a split, levels per journey level, "Check again" on an existing track

**Checkpoint**: HO:us1 per owner.

---

## Phase 4: User Story 2 - Get a tip that leads to an action (P1)

- [ ] T049 [P] [US2] [backend] Failing tests `TipRules` (Domain): each rule of data-model 5.3, ordering, max two, actions, no GPU tip ever (FR-006), rule 3 only when the pipeline is available (G2 off -> never)
- [ ] T050 [US2] [backend] Implement `Domain/Insights/TipRules.cs`
- [ ] T051 [P] [US2] [backend] Failing tests: report view builds tips from the current track state (detail phase, outcomes, stem set) and the 003 device estimate text; tips change after a detail split without re-analysis (FR-005)
- [ ] T052 [US2] [backend] Implement the view assembly
- [ ] T053 [P] [US2] [frontend] Failing tests `TipList`: split-detail calls the 002 start endpoint, open-detail navigates to the mixer with the parent expanded, mute patches the current mix, buttons keyboard operable
- [ ] T054 [US2] [frontend] Implement `TipList` and the action handlers
- [ ] T055 [US2] [qa] E2E tips: other -> "Split further" starts the detail separation and the report is remade; piano -> mute; guitar -> hard to isolate; no tip text contains "GPU" (SC-003)

**Checkpoint**: HO:us2 per owner.

---

## Phase 5: User Story 3 - One-click lesson handout per song and family (P1)

- [ ] T056 [P] [US3] [backend] Failing tests `ChordSymbol` parsing (ASCII, slash chords, N.C.) and `Transposer` (B-flat +2, E-flat +9, F +7; flat/sharp spelling by target key) (FR-011)
- [ ] T057 [US3] [backend] Implement them
- [ ] T058 [P] [US3] [backend] Failing tests `ChordPlacer` (word timings with 150 ms tolerance, line timings, none) and `BarMapper` (start bar, pickup 0, bars per section, chords per bar, `%` repeats) (research R-08)
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
- [ ] T080 [US3] [qa] E2E `e2e/tests/handouts.spec.ts`: seven families parametrised on the fixture song (defaults in `handout.included`, PDF downloads, page count via pdf-lib), melodic B-flat, a missing ingredient listed, practice note appears (SC-006)

**Checkpoint**: HO:us3 per owner.

---

## Phase 6: User Story 4 - Setlist handout (P2)

- [ ] T081 [P] [US4] [backend] Failing tests setlist assembly: entry order, entry notes, deleted tracks (`available=false`, warning `track-missing`), > 50 songs -> 409 `too-many-songs`, empty -> 409 `setlist-empty` (FR-009)
- [ ] T082 [US4] [backend] Implement setlist assembly and the setlist source path of the endpoints
- [ ] T083 [P] [US4] [audio-ingest] Failing tests setlist layout: cover, running order, contents with page numbers, each song on a new page, one bookmark per song, 20-song render within the budget (SC-004)
- [ ] T084 [US4] [audio-ingest] Implement the setlist templates
- [ ] T085 [P] [US4] [frontend] Failing tests `setlist-handout-menu` and customise entry on the setlist page
- [ ] T086 [US4] [frontend] Implement them
- [ ] T087 [US4] [qa] E2E `e2e/tests/setlist-handout.spec.ts` (three-song fixture setlist, running order and bookmarks via facts/outline)

**Checkpoint**: HO:us4 per owner.

---

## Phase 7: User Story 5 - High-quality vocal re-separation (P3, **(G2)**)

- [ ] T088 [P] [US5] [stem-separation] (G2) Failing tests `separation/hq/pipeline.py` with fake models: input = sum of six stems, instrumental = input - vocals, Demucs vocals folded into `other` or dropped by the 002 gate, `DeviceRunner` use and CPU fallback, cache by summed-input hash, progress stages (data-model 2.7)
- [ ] T089 [US5] [stem-separation] (G2) Implement the RoFormer adapter on the vendored MSST code, the pipeline and the fake re-separator
- [ ] T090 [P] [US5] [stem-separation] (G2) Failing tests re-separation routes, job store and stem files; `Health.separationPipelines`
- [ ] T091 [US5] [stem-separation] (G2) Implement them
- [ ] T092 [US5] [stem-separation] (G2) `@pytest.mark.integration` test: real RoFormer checkpoint (SHA-256 verified download) on the short fixture, six non-silent stems
- [ ] T093 [P] [US5] [backend] (G2) Failing tests `ReseparationService`, `StemSetReplacer`: 409 codes, `discardDetail` consent, atomic replacement (temp + rename), mix pruning, failure/cancel keeps stems, `StemSet` set, quality trigger with previous ratings (FR-007)
- [ ] T094 [US5] [backend] (G2) Implement them
- [ ] T095 [P] [US5] [backend] (G2) Failing tests options and endpoints (estimate per device, consequences texts, cancel)
- [ ] T096 [US5] [backend] (G2) Implement them
- [ ] T097 [P] [US5] [frontend] (G2) Failing tests `ReseparateDialog` (estimate, consequences, discard-detail checkbox only with detail stems), phase progress, `stem-set-pipeline`
- [ ] T098 [US5] [frontend] (G2) Implement them
- [ ] T099 [US5] [qa] (G2) E2E `e2e/tests/reseparation.spec.ts` (fake re-separator: tip -> dialog -> stems replaced -> "was ..." ratings)

**Checkpoint**: HO:us5 per owner.

---

## Phase 8: Polish and integration

- [ ] T100 [quality-gate] `tools/quality/run_all.py` on all 020 code (SIG limits, duplication, dependency rules, style drift); refactor requests to owners; update `docs/quality/SIG-REPORT.md`
- [ ] T101 [P] [qa] Accessibility: axe scans of the quality panel, tips, handout menu/dialog, practice notes, setlist menu, (G2) dialog; keyboard-only paths; screen-reader names (FR-016)
- [ ] T102 [qa] Run the full E2E suite on the fake stack (Chromium + Firefox); traceability table in `docs/testing/`
- [ ] T103 [P] [beat-analysis] SC-001 timing (marked integration): 5-minute reference song, six stems + twelve detail stems, analysis <= 30 s on the reference CPU; result in the hand-off
- [ ] T104 [P] [audio-ingest] SC-004 timing: song handout <= 15 s and 20-song setlist <= 90 s in the worker image; result in the hand-off
- [ ] T105 [CTO] Manuals (constitution 1.3.1): user manual chapter "Stem quality and lesson handouts" (ratings, tips, honesty note, families, transposition, notice), technical manual (insights lane, env vars, Pango/fonts, G2 model and its licence); regenerate PDFs; `node tools/docs/check.mjs`
- [ ] T106 [CTO] `docs/THIRD-PARTY-LICENSES.md` and README: WeasyPrint and its Python deps, Pango, HarfBuzz, DejaVu fonts, Jinja2/MarkupSafe as direct deps, (G2) RoFormer weights with licence status, SHA-256 and source URL
- [ ] T107 [CTO] Integration wave: merge the owners' branches, contract tests against the full 1.11.0/1.19.0 documents, quickstart V1-V10, SC-008 moderated check, `docs/STATUS.md`
- [ ] T108 [architect] Post-wave reconciliation: contract refinements from the wave in DELTA.md "Reconciliation", research.md "Phase 0 measurements" completed with T039/T103/T104 results

---

## Dependencies and order

- Phase 0 needs 014 and 016 merged; T005 decides whether T088-T099 run.
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
| stem-separation | T016, T017, T040, T041, T074, T075, (G2) T088-T092 | HO:foundational, HO:us1, HO:us3, HO:us5 |
| audio-ingest | T018, T019, T068-T073, T083, T084, T104 | HO:foundational, HO:us3, HO:us4, HO:polish |
| backend | T020-T025, T042-T045, T049-T052, T056-T067, T081, T082, (G2) T093-T096 | HO:foundational, HO:us1..us5 |
| frontend | T026, T027, T046, T047, T053, T054, T076-T079, T085, T086, (G2) T097, T098 | HO:foundational, HO:us1..us5 |
| qa | T013, T028, T029, T048, T055, T080, T087, (G2) T099, T101, T102 | HO:foundational, HO:us1..us5, HO:polish |
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
| **Total** | **108** | **12** |

Per story: Phase 0 8, Setup 5, Foundational 16, US1 19, US2 7, US3 25, US4 7, US5 12, Polish 9.

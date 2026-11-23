# Hand-off: architect / 020 Phase 0 (plan)

Feature 020 "Insights and Handouts", Phase 0 (T001-T008), branch `020-insights-handouts` from `main`
cc314608. Design: `specs/020-insights-handouts/` (spec, plan, research, data-model, tasks, DELTA),
ADR-029 (stays Accepted; D5 deferred, see its "Phase 0 outcome").

## What was built

- **Ready check** (cross-artifact consistency, speckit-analyze style) and fixes in the 020 files
  (list under "Decisions" and "Ready-check changes").
- **T003 contracts**: `specs/020-insights-handouts/contracts/merge_full.py` builds the full
  documents on top of `main`'s 016 documents: `worker-api.openapi.yaml` **1.11.0**,
  `backend-api.openapi.yaml` **1.19.0** (`x-stemstudio-contains` ends `1.15.0, 1.19.0`),
  `ui-test-ids.md` **1.19.0**. `--check` proves freshness and the superset of 016. G2 parts are
  deferred and not merged.
- **T001 worker contract code**: `schemas.py` (CONTRACT_VERSION 1.11.0, quality and handout models,
  fixed notice texts with 422 validation, `Health.insights`, `Health.lanes.insights`), `ports.py`
  (`QualityAnalyzer`, `QualityInputs`, `StemAudio`, `HandoutRenderer`, `RenderedHandout`,
  `NotationReader`, per-job allow-lists, errors and texts), `config.py` (`InsightsSettings`:
  `STEMSTUDIO_QUALITY_ANALYZER`, `STEMSTUDIO_INSIGHTS_THREADS`), `weasyprint==70.0` and
  `jinja2==3.1.6` pinned.
- **T002 .NET contract code**: `StemStudio.Contracts/{Insights,Handouts}/*`, `ApiRoutes.Insights.cs`,
  `ContractLimits.Insights.cs`, `AssetDto.Handout`, `ProvenanceDto.HandoutRecipe`,
  `TrackDto.Quality` (optional), `AssetKindDto/AssetOriginDto.Handout`; the six new operations are
  staged in `PendingInsightsOperations` (tests/StemStudio.Api.Tests/Contract/BackendContractTests.Insights.cs).
- **T004** 014/016 reconciliation: DELTA.md "Phase 0 outcome" P0-4 (real shapes), P0-5 (setlist PDF).
- **T005** G2 spike (partial, no dataset) and **T006** WeasyPrint spike: research.md "Phase 0 measurements".
- **T007** `worker/scripts/sync_handout_style.py` + committed `handouts/assets/` (tokens.css light
  theme, Archivo WOFF2 + OFL, manifest.json with SHA-256); `--check` fails on drift.

## Files changed

- `specs/020-insights-handouts/**` (spec, plan, research, data-model, tasks, DELTA, quickstart,
  contracts: deltas `-delta.2`, full documents, `merge_full.py`, `redocly.yaml`, `help-topics.md`)
- `docs/decisions/ADR-029-stem-quality-report-and-handouts.md` (Phase 0 outcome)
- `docs/THIRD-PARTY-LICENSES.md` (new rows, as the orchestrator asked; CTO reviews in T106)
- `worker/pyproject.toml`, `worker/uv.lock`, `worker/src/stemstudio_worker/{schemas,ports,config}.py`,
  `worker/src/stemstudio_worker/handouts/assets/**`, `worker/scripts/sync_handout_style.py`
- worker tests: new `tests/contract/test_insights_contract.py`, `tests/contract/test_handout_contract.py`,
  `tests/scripts/test_sync_handout_style.py`, helper `tests/contract/docs.py::field_mismatch`;
  re-pointed to 1.11.0 (one-time edits): `tests/api/{contract,test_detail_routes,test_jobs_api}.py`,
  `tests/contract/{test_instrument_views,test_practice,test_storage,test_transcription}_contract.py`,
  `tests/{test_detail_contract,test_device_contract,test_render_contract,test_smoke}.py`
- `src/StemStudio.Contracts/**` (above); `src/StemStudio.Api/LibraryCare/Archive/ManifestBuilder.cs`
  (`BackendApiVersion` 1.19.0, agreed one-time edit as 016 did)
- `tests/StemStudio.Api.Tests/`: csproj links (current = 020, 016 kept as `Contract/016`, 020 delta
  and ui ids as `Contract/020`), new `Contract/{InsightsContractTests,HandoutContractTests,
  InsightsUiTestIdsTests,BackendContractTests.Insights}.cs`; one-time edits in `ContractJson.cs`,
  `InstrumentViewsContractTests.cs`, `LyricsContractTests.cs`, `LibraryContractTests.cs`,
  `BackendContractTests{,.StemsAndMixes}.cs`, `LibraryCare/ManifestApiVersionTests.cs`

## Commits

| Commit | Task |
|---|---|
| f9bed136 | T003 merged contracts |
| 2bc0f737 | T001 worker contract code (tests and code in one step; mutation check below) |
| 1c04d856 / ace546eb | T007 red / green |
| 47596492 | ready-check fixes, Phase 0 outcome, measurements (T004-T006 records) |
| 2fd7f1f1 | merge_full.py parameter limit |
| 89f25165 | shared field check helper (removes two test clones) |
| 98fa2591 | T002 .NET contract code (tests and DTOs in one step) |
| (this commit) | T008 hand-off, resume note |

Red/green honesty: T007 has a real red commit. For T001 and T002 (declarative contract code) tests
and code were written in one step; for T001 a mutation check (removing the notice, six-stem and
song-count validators) made 4 tests fail, so the tests do catch the rules.

## Tests and coverage

See the "T008 verification" section at the end (exact numbers from the final run).

| Component | Tests | Line | Branch |
|---|---|---|---|
| worker (whole package, default suite) | see T008 | see T008 | see T008 |
| worker `config.py` / `ports.py` / `schemas.py` (run before T007) | 3822 passed | 100 / 100 / 99.8 % | 100 / 100 / 99 % |
| `worker/scripts/sync_handout_style.py` | 8 passed | 100 % | 100 % |
| StemStudio.Contracts | Api.Tests 3586 passed, 4 skipped | 100 % | 100 % |
| StemStudio.Api (unchanged code, coverage of the run) | (same run) | 98.31 % | 91 % |
| StemStudio.Web.Tests / Domain.Tests (unchanged, regression) | 3848 / 4221 passed | - | - |

## Quality checks

- `dotnet build StemStudio.slnx -warnaserror`: 0 warnings, 0 errors; `dotnet format StemStudio.slnx
  --verify-no-changes`: clean.
- worker: `ruff check`, `ruff format --check`, `mypy --strict` (src, tests, scripts): clean.
- `merge_full.py --check`: superset yes. Redocly 2.78.0: valid, 33 warnings (documented in
  `contracts/redocly.yaml`).
- `tools/quality/run_all.py`: see T008 (worker + contracts run: PASS, 0 errors, 0 production clones;
  jscpd's own output read; the test clones left are pre-existing).

## Dependencies added

| Name | Version | Licence | URL | Reason |
|---|---|---|---|---|
| weasyprint | 70.0 (2026-09-08) | BSD-3-Clause | https://github.com/Kozea/WeasyPrint | handout PDFs from HTML + CSS paged media (ADR-029 D7) |
| pydyf | 0.13.0 | BSD-3-Clause | https://github.com/CourtBouillon/pydyf | WeasyPrint PDF writer |
| tinyhtml5 | 2.1.0 | MIT | https://github.com/CourtBouillon/tinyhtml5 | WeasyPrint HTML parser |
| pyphen | 0.18.1 | MPL-1.1 option of GPL/LGPL/MPL | https://github.com/Kozea/Pyphen | WeasyPrint hyphenation |
| fonttools[woff] | 4.68.0 | MIT | https://github.com/fonttools/fonttools | WOFF2 fonts in WeasyPrint (now also runtime) |
| zopfli | 0.4.3 | Apache-2.0 | https://github.com/fonttools/py-zopfli | fonttools[woff] |
| jinja2 (now direct) | 3.1.6 | BSD-3-Clause | https://github.com/pallets/jinja | handout templates, autoescape |
| system: Pango / HarfBuzz | Debian 13 (1.56.3 / 10.2.0) | LGPL-2.1+ / MIT | https://gitlab.gnome.org/GNOME/pango | WeasyPrint text layout (already in the image transitively) |

`pip-audit` on the new pins: no known vulnerabilities. Rows added to `docs/THIRD-PARTY-LICENSES.md`.
No new .NET or npm packages.

## Docs impact

User-visible (for T105): stem quality panel and badges with estimates and "rough indication"
labels, tips (no GPU tip), lesson handouts per family (song and setlist) in the library, practice
notes. Setup: worker env vars `STEMSTUDIO_QUALITY_ANALYZER`, `STEMSTUDIO_INSIGHTS_THREADS`; worker
image needs Pango (already present), fonts DejaVu. On a setlist, 014's PDF (quick print) and 020's
setlist handout (lesson PDF) both exist.

## Decisions (Phase 0, details in DELTA.md "Phase 0 outcome")

1. **Base**: merged onto 016's documents (017-019 not built; 020 needs nothing from them); labels
   stay 1.19.0 / 1.11.0 / 1.19.0.
2. **G2 pending owner** (no MUSDB18-HQ): US5, FR-007, SC-007 and every G2 contract part, ui id,
   help topic and task (T088-T099) are deferred; they need a new contract minor later.
3. **G1 not run**: all labels `indicative`, all levels shown; `unknown` masking only for a dimension
   measured and failed.
4. **016 domain reuse**: chords, transposition and chords over lyrics use `ChordSpeller`,
   `ChordDisplay`, `ChordSheetBuilder`, `KeyFormatter`; no own chord parser (T056-T059 rewritten).
5. **Setlist PDF**: 014's quick PDF stays; 020's setlist handout is the stored lesson PDF.
6. **014 deleted tracks**: entries are already removed by 014; `available` and `track-missing` dropped.
7. **Optional backend additions** (`Asset.handout`, `Provenance.handoutRecipe`, `Track.quality`).
8. **Per-job allow-lists** (quality: stems; handout: notation PDFs + artwork); the general list unchanged.
9. **Renderer port** `render(request, files, progress)`: the job layer resolves paths once.
10. **WeasyPrint 70 fetcher API**: subclass `weasyprint.urls.URLFetcher`.

### Ready-check changes (020 files)

spec: status, Phase 0 clarification session, US4 scenario 3, US5/FR-007/SC-007 deferred, FR-004
(labels until G1), FR-011 (016 line rule), assumption on 017-019. plan: status, storage, Domain
file list (`HandoutChords`), complexity row. data-model: 1, 2.2 (per-job allow-lists), 2.3 (6..64
and kind rule), 2.4 (`mainSource` StemName), 2.5 (no `available`, chord rows), 2.7/5.1/5.3 G2
deferred, 4.5 (masking rule), 6.1 (notation by 016 facts), 6.3 (016 reuse), 8. research: Phase 0
measurements, R-04 masking rule, R-08 Phase 0 note. tasks: status, binding acceptance rule for
every test/implementation task, **Done when** on every other task, Phase 0 ticked with outcomes,
T009/T012/T013/T018/T024/T038/T039/T056-T059/T081 updated, G2 tasks struck through, execution
table. quickstart: G2 steps marked skip. help-topics: setlist PDF wording, reliability wording,
G2 rows deferred.

## Per lane: what to read, what you get, what you deliver

| Lane | Read | Other lanes deliver to you | Notes |
|---|---|---|---|
| **quality-gate** (T009, T010, T100) | plan.md "Dependency direction"; tasks T009; `worker/scripts/sync_handout_style.py` (`--check`); research P0-4 (fetcher) | architect: assets + manifest (committed) | rules: `insights` / `handouts` import limits (fontTools allowed in `handouts` if the glyph check uses it), drift via `sync_handout_style.py --check`, no literal colours in `handouts/styles/*.css`, fetcher must be the `URLFetcher` subclass |
| **devops** (T011, T012) | tasks T011/T012; research P0-4 "Versions"; data-model 1 | architect: pins in `uv.lock`; the image already has Pango 1.56.3 | list `libpango-1.0-0`, `libpangoft2-1.0-0` explicitly; env vars in compose files; CI/`test-all.*`: `CONTRACTS_DIR` -> 020 **but keep the 016 checks** (`generate_chord_spelling_vectors.py --check`, 016 contract pytest) pinned to `specs/016-instrument-views/contracts`; run `specs/020-insights-handouts/contracts/merge_full.py --check` and `worker/scripts/sync_handout_style.py --check`; `components.json` rules for the new inputs |
| **beat-analysis** (T014, T015, T030-T039, T103) | data-model 2.4, 2.6, 4; research R-02..R-04 and "Phase 0 measurements" (G1); `ports.QualityAnalyzer`, `QualityInputs`, `StemAudio`, `QUALITY_SAMPLE_RATE`, `QUALITY_MIN_ACTIVE_SECONDS`; `schemas.StemQuality` and enums | architect: ports/schemas; stem-separation calls `analyze` from the lane | **G1 protocol hand-over**: R-04 V2 (MUSDB18-HQ test split, `htdemucs_6s`, guitar+piano summed into `other`, `museval` 0.4.1 throw-away env, Spearman per stem type: bleed vs -SIR >= 0.5, artefacts vs -SAR >= 0.4); dataset not on the machine, so T039 delivers the script and "pending dataset"; reliability table starts all `indicative`, nothing masked (T038) |
| **stem-separation** (T016, T017, T040, T041, T074, T075) | data-model 2.1-2.6; worker-api 1.11.0 paths and examples; `ports` errors and `InsightsProgress`; `config.InsightsSettings` | beat-analysis: analyzer + fake; audio-ingest: renderer | wire `Health.insights` (replace `INSIGHTS_NOT_CHECKED`: analyzer kind, `ANALYZER_VERSION`, WeasyPrint/Pango versions, fonts) and `Health.lanes.insights` in `main.py`; check paths with `is_quality_input_path` / `is_handout_input_path` and the existing path guard at job creation, pass the real paths as `files` to the renderer; update `tests/api/test_jobs_api.py` / `tests/test_smoke.py` health bodies when the defaults change |
| **audio-ingest** (T018, T019, T068-T073, T083, T084, T104) | data-model 2.5, 6.4-6.6; research R-07, R-09, R-10, **P0-4**; `ports.HandoutRenderer`, `RenderedHandout`, `NotationReader`, `NotationUnreadableError`, texts; `schemas.HandoutJobRequest`; `handouts/assets/` | architect: assets (do not edit; run the sync script) | WeasyPrint 70: fetcher = `URLFetcher` subclass with `allowed_protocols=("file",)`; check `font-stretch: 125%` visually (fallback: static instances or none); missing-glyph warning = characters in neither Archivo nor DejaVu (fontTools cmap); 014's `documents/` stays untouched |
| **backend** (T020-T025, T042-T045, T049-T052, T056-T067, T081, T082) | backend-api 1.19.0; data-model 5, 6; DELTA "Phase 0 outcome" P0-4 (real 014/016 shapes), P0-5, P0-6; `StemStudio.Contracts.{Insights,Handouts}`, `ApiRoutes.Insights`, `ContractLimits.Insights`; worker-api 1.11.0 examples | architect: DTOs; stem-separation: worker endpoints | move each operation from `PendingInsightsOperations` to `InsightsOperations` when mapped; chords via 016 `ChordSpeller`/`ChordSheetBuilder`/`KeyFormatter` (no own parser); setlist adapter maps `note` "" -> null; `track-not-ready` only for song handouts; `Asset.handout` etc. stay null/absent for other kinds; one lock around check+insert+start for "one report at a time" (LESSONS) |
| **frontend** (T026, T027, T046, T047, T053, T054, T076-T079, T085, T086) | ui-test-ids 1.19.0 "Additions in 1.19.0"; help-topics.md (no G2 rows); spec US1-US4; LESSONS frontend | backend: endpoints; architect: DTOs, `ApiRoutes.Insights` | no G2 ids, no "Re-separate" action (TipActionKind has no `reseparate`); setlist page keeps 014's `setlist-pdf` next to `setlist-handout-menu`; ARIA check at design time (menus, live regions) |
| **qa** (T013, T028, T029, T048, T055, T080, T087, T101, T102) | ui-test-ids 1.19.0; data-model 2.6 (fake analyzer ratings); spec US1-US4; LESSONS qa | all lanes; specs run on a real fake stack at the mid-wave checkpoint | fixtures for all seven families; check every fake against research/contract examples (LESSONS 016); wait on own job ids; no G2 spec |
| **CTO** (T105-T107) | this hand-off; DELTA "Phase 0 outcome" | all | decision-log entry proposal below; manuals; licence rows review |

## Proposed CTO decision-log entries (not edited by me)

1. 2026-11-23, feature 020 Phase 0: contracts merged onto 016's documents (017-019 not built);
   labels backend 1.19.0, worker 1.11.0, ui-test-ids 1.19.0 unchanged; `x-stemstudio-contains`
   `..., 1.15.0, 1.19.0`.
2. 2026-11-23: gate G2 pending the owner's MUSDB18-HQ; User Story 5 deferred, its contract parts not
   merged; a later contract minor (to allocate) when G2 passes.
3. 2026-11-23: 014's setlist PDF and 020's setlist handout coexist (quick print vs lesson PDF).
4. 2026-11-23: one-time edit `ManifestBuilder.BackendApiVersion` = 1.19.0 and test re-pointing in
   worker and Api tests (architect, as in 016).

## Open issues / requests

- **Owner**: provide MUSDB18-HQ (local path) to run G1 (T039) and G2; until then labels stay
  "rough indication" and US5 is out.
- **Owner**: confirm the setlist-PDF decision (two PDFs) or ask to fold 014's into the `overview`
  handout (a breaking change, later major).
- G2 follow-up when resumed: sum of stems vs original measured -27 dB on synthetic audio (planned
  below -30 dB); re-measure on real music; KJ Mel-Band RoFormer needs one new vendored MSST file.
- ROADMAP: no edit made; the CTO may record that 020 was merged onto 016 (entry 1 above).

## Next owner

CTO: start the lane wave (Phase 1/2 tasks of tasks.md "Wave start").

## T008 verification (final, on the committed state)

| Check | Result |
|---|---|
| worker `pytest --cov --cov-branch --cov-fail-under=90` (default suite) | **3832 passed**, 4 skipped, 12 deselected; **100.00 % line, 100 % branch** (14,475 statements, 1,720 branches) |
| worker `ruff check .` / `ruff format --check .` / `mypy --strict src tests scripts` | clean / 454 formatted / no issues in 474 files |
| `tools/quality/run_all.py` (all default roots) | **PASS**: 3,975 files, 32,441 units, 0 errors, 1,992 warnings, 507 clones (none in production code; jscpd output read: the new clones are in .NET contract tests, same pattern as 015/016, plus the pre-existing worker/generator `_choice` helper) |
| `dotnet build StemStudio.slnx -warnaserror` | 0 warnings, 0 errors (T002 run) |
| .NET tests | Api 3586 passed / 4 skipped, Web 3848, Domain 4221; Contracts 100 % line and branch |
| `dotnet format StemStudio.slnx --verify-no-changes` | clean |
| `merge_full.py --check` / Redocly 2.78.0 | superset yes / valid, 33 documented warnings |
| `worker/scripts/sync_handout_style.py --check` | in sync |

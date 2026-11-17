# Hand-off: architect / 014 Phase 0 part 2 (T002-T011)

Branch `r14-p0-014` from `main` e8d6a5d8 (001-013 incl. 012 and 013). Combined round 014+015; the 015
architect works in parallel on `r14-p0-015`. Not pushed, not merged.

## What was built

| Task | Result |
|---|---|
| Ready check | spec/plan/tasks/data-model/deltas cross-checked against `main`. Fixed: PyYAML could not read the backend delta (unquoted `?v=1` in a flow mapping); data-model 4 still had `leadInSeconds` (dropped in the delta); `PracticeSettings` and `practice-panel*` collided with 012 (renamed, see decisions); R-1..R-5 resolved (DELTA.md 7). Lessons applied: no stack started, quiet logs, no scope cut (the merge-restore gap is raised, not cut). |
| T002 | Worker contract code 1.8.0: `schemas.py` (section, score-view and setlist-document jobs, `Health.lanes.practice`, `Health.structure`, `SECTION_WARNING_TEXTS`, `SCORE_VIEW_FAILED_TEXT`, `DOCUMENT_FAILED_TEXT`, contiguity validators), `ports.py` (`StructureAnalyzer`, `ScoreViewRenderer`, `ScoreView`, `SetlistDocumentRenderer`, `SetlistDocument`, `StructureError`, `ScoreViewError`, `DocumentError`, `StemFiles`, named allow-list patterns `STEM_PATH_PATTERN`, `DETAIL_PATH_PATTERN`, `SCORE_MUSICXML_PATTERN`), `config.py` (`structure_analyzer`), `reportlab==5.1.0` locked, mypy override, `worker/tests/contract/test_practice_contract.py`; older tests follow the version bump. |
| T003 | `StemStudio.Contracts`: `Sections/` (6 enums/records + 4 requests), `ScoreFollow/` (5), `Setlists/` (8 incl. `SetlistPdfOptions`), `Practice/` (12, `PracticeLogSettings*`), `LoopRegionDto.SectionId` (omitted when null), `ApiRoutes.PracticeFlow`, `ContractLimits.PracticeFlow` (`ContractLimits` is now `partial`), `PracticeFlowProblemTypes`. Tests `PracticeFlowContractTests` (87 cases) and `PracticeFlowUiTestIdsTests`; the 22 operation ids are staged in `BackendContractTests.PracticeFlow.cs` (`PendingPracticeFlowOperations`, own file). |
| T004 | `types.ts` 2.4.0 (`ScoreViewPage`, `ScoreViewBar`, `ScoreViewMap`, `ScoreFollowOptions`, `LoopPassInfo`, `OnLoopPass`, three `AudioEngine` members with TSDoc); engine stubs throw "not implemented yet (contract 2.4.0)"; `tests/practice-flow-contract.test.ts`. |
| T005 | Full documents in `specs/014-practice-flow/contracts/`: `backend-api.openapi.yaml` 1.13.0, `worker-api.openapi.yaml` 1.8.0, `audio-engine.md` 2.4.0, `web-interop.md` 1.1.0, `ui-test-ids.md` 1.13.0; deltas `practice-api` 1.13.0-delta.2 and `worker-practice` 1.8.0-delta.2; `check_superset.py`, `redocly.yaml`; DELTA.md 7 (outcome) and 8 (re-merge onto 022/023); ADR-023 Accepted (number unique); data-model, plan, research, tasks updated; ROADMAP and CTO decision rows. |
| T006 | `src/StemStudio.Web/wwwroot/js/keymap-defaults.json` (44 commands, 3 sequences, pedal presets, 27 reserved chords, 6 external chords of 023) and `StemStudio.Web/Practice/{KeyMapDefaults,KeyMapCatalogue}.cs`; `Audio/EngineMethods.ContractVersion` 2.4.0 (drift guard); `KeyMapDefaultsTests` (47 cases: every FR-020 row with owner, no chord twice, no default on a reserved/external/sequence/pedal key). |
| T010 | Segmentation spike on the 6-form synthetic set: frozen half size 6 bars, cluster 0.25, share-based solo rule (>= 0.4 and >= 1.3 x median share); recall 0.97, precision 0.97, solo/bridge/break kind 6/6, kinds 41/42, 1.9 s for a 296 s song (research.md R-10). |
| T011 | Font check: Verovio 6.4.0 already embeds the full Leipzig WOFF2 in every page (58.6 KB); as `<img>` the tempo glyph renders in Chromium 159 and Firefox 159; without the font both show a box. The worker keeps Verovio's `@font-face` (no second copy, no fallback) (research.md R-11). |
| T007 | Verification below. |

## Files changed

105+ files; key paths: `worker/src/stemstudio_worker/{schemas,ports,config}.py`, `worker/pyproject.toml`,
`worker/uv.lock`, `worker/tests/contract/test_practice_contract.py`, `src/StemStudio.Contracts/{Sections,ScoreFollow,Setlists,Practice}/**`,
`src/StemStudio.Contracts/{ApiRoutes.PracticeFlow,ContractLimits.PracticeFlow,PracticeFlowProblemTypes}.cs`,
`src/StemStudio.Contracts/Mixes/LoopRegionDto.cs`, `src/audio-engine/src/{types,engine,index}.ts`,
`src/StemStudio.Web/wwwroot/js/keymap-defaults.json`, `src/StemStudio.Web/Practice/*.cs`,
`tests/StemStudio.Api.Tests/Contract/{PracticeFlowContractTests,PracticeFlowUiTestIdsTests,BackendContractTests.PracticeFlow}.cs`,
`tests/StemStudio.Api.Tests/Worker/Examples/{section-*,score-view-*,setlist-document-*}.json`,
`tests/StemStudio.Web.Tests/Practice/KeyMapDefaultsTests.cs`, `specs/014-practice-flow/**`,
`docs/decisions/{ADR-023-practice-flow,CTO-DECISIONS-2026-11-15}.md`, `docs/ROADMAP.md`.

## Tests and coverage

`dotnet test StemStudio.slnx -c Release --no-build -p:CollectCoverage=true` (full run, before the last fixes):

| Project | Passed / total | Line | Branch | Method |
|---|---|---|---|---|
| Domain.Tests | 942 / 942 | 98.87 % | 95.06 % | 97.67 % |
| Agent.Tests | 726 / 726 | 99.03 % | 94.18 % | 98.59 % |
| AgentEval.Tests | 134 / 134 | 99.83 % | 92.85 % | 100 % |
| Persistence.Tests | 121 / 121 | 99.01 % | 95.00 % | 93.62 % |
| Web.Tests (rerun after the drift fix) | 2405 / 2405 | 99.65 % | 93.99 % | 99.74 % |
| Api.Tests | 2233-2234 / 2237 (2 skipped) | not printed (the gate prints only on a green run) | | |

Two drift tests of the Web suite failed in the first run and are fixed: `EngineMethods.ContractVersion` is
"2.4.0" and `OnLoopPass` is staged in `EngineContractDriftTests.PendingCallbacks` (the frontend lane removes it
when the recorder wires the callback, T049/T099). **Api.Tests**: 1-2 agent-executor tests time out per full
run, a different one each time (`ClaudeKeyLeakTests.StoredSentinel_NeverLeaks`,
`ClaudeKeyUnreadableTests.OtherKeyRing_*`, `AgentEndpointsTests.Executor_*`); all 43 pass when run alone.
They are the known flakes that the 022/023 round fixes (`70b2b515` executor survives finish errors,
`af7e735f` deadline-based `Eventually`), which arrive with the `main` merge before the wave; the re-run of the
full suite with coverage belongs after that merge. All 618 contract tests pass after the last changes.

| Component | Command | Result | Coverage |
|---|---|---|---|
| Worker | `pytest --cov` (worker/, final run) | 2573 passed, 3 skipped, 10 deselected (74 s) | **100 % line, 100 % branch** (10506 statements, 1190 branches) |
| Worker lint | `ruff check`, `ruff format --check`, `mypy` (strict) | clean; mypy "no issues found in 190 source files" | - |
| Engine | `npx vitest run --coverage`, `tsc --noEmit`, `eslint .` | 34 files, 492 tests passed; tsc and eslint clean | **100 % statements (1822/1822), 100 % branches (753/753)** |
| OpenAPI | Redocly 2.78.0 with `specs/014-practice-flow/contracts/redocly.yaml` on the four documents | valid, 20 warnings = 11 (1.12.0) + 4 (worker 1.7.0) + 2 known 014 reads without 4XX in the full doc + 3 in the delta (the same 2 + unused `LoopRegion`) | - |
| Superset | `python -I specs/014-practice-flow/contracts/check_superset.py` (1.12.0 -> 1.13.0; delta -> 1.13.0) | superset yes / yes | - |
| Re-merge dry run | 014 delta onto `022-023-round` 1.23.0 (backend) and ui-test-ids 1.23.0 | superset yes both ways; no shared path, operation id, tag, example or UI id | - |
| SIG / quality | `uv run --project tools/quality --frozen python tools/quality/run_all.py` | 0 errors, 1099 warnings (none in the new production code; the two errors first found, a duplicated job block in `schemas.py` and a clone of 010's `SectionMarkerDto`, are fixed) | - |

## Quality checks

`dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format --verify-no-changes`: clean.
ruff/mypy clean, tsc/eslint clean (above); quality gate 0 errors.

## Dependencies added

| Name | Version | Licence | URL | Reason |
|---|---|---|---|---|
| reportlab | 5.1.0 (PyPI 2026-10-22, pure-Python wheel) | BSD (ReportLab open-source licence, OSI "BSD License") | https://pypi.org/project/reportlab/ | setlist PDFs in the worker (ADR-023 D4); adds no other package (pillow 13.0.0 and charset-normalizer 3.5.2 already locked); no extras; no known vulnerabilities on PyPI |

For the CTO: `docs/THIRD-PARTY-LICENSES.md` needs the ReportLab row (and DejaVu fonts when devops adds `fonts-dejavu-core`, T014).

## Decisions (rows already in `docs/decisions/CTO-DECISIONS-2026-11-15.md`)

1. `PracticeLogSettings` naming (012 owns `PracticeSettings`); backend delta label 1.13.0-delta.2.
2. Practice log panel ids `practice-log-*` (012 owns `practice-panel*`); `trainer-region-label` added.
3. Key catalogue with 012's shipped ids; reset `0`, count-in `Shift+K`; `R`/`C` held for 015; 022 `/` and 023 editor keys protected; G sequences fixed.
4. No 014 operation carries the 013 write gate; appliers skip ticks during maintenance.
5. Segmentation constants (T010) and the share-based solo rule.
6. Keep Verovio's embedded Leipzig font (T011).
7. ReportLab 5.1.0 lock.

## Deviations

None from the allocated labels (backend 1.13.0, worker 1.8.0, engine 2.4.0, web-interop 1.1.0, ui-test-ids
1.13.0). Renames 1-2 above touch only names nothing has built yet. `test_practice_contract.py` lives in
`worker/tests/contract/` (where the 006/008/013 contract tests are), not `worker/tests/`.

## Open issues / requests

1. **Decision requested (CTO)**: 013 merge restore (`MergePlanner` fixed categories) does not bring 014
   data of merged tracks. Proposal: sections and score views (+ `view/` files) follow their merged parent
   (backend task T107, test-first); setlists and the practice log are not merged in 014 (manual note).
   Full restore is fine (rebuild copies every table).
2. Backend lane: bump `ManifestBuilder.BackendApiVersion` "1.12.0" -> "1.13.0" (T108; 015 then 1.14.0).
3. Re-merge after 022/023 land (DELTA.md 8): the scratch scripts used here (`merge_backend.py`,
   `merge_uiids.py`, `merge_engine.py`, `merge_webinterop.py`, `merge_worker.py`) take a base document as an
   argument; I can redo it in minutes. Then update `check_superset.py` `OLDER`, the contains list in
   `PracticeFlowContractTests`, `Changed1120Entries` and `PracticeFlowUiTestIdsTests` base to 1.23.0.
4. Merge of the two P0 branches (014 then 015): expected text conflicts in `src/audio-engine/src/types.ts`
   (015 bumps to 2.5.0 on top), `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj` (both add
   links), `BackendContractTests.StemsAndMixes.cs` line 113 (both extend the `Except` chain: keep both),
   `src/audio-engine/src/engine.ts` / `index.ts` (both add members), the version-pin lines of the engine
   tests, `docs/ROADMAP.md`/`CTO-DECISIONS` (append-only: keep both).
5. Worker composition (stem-separation, T033/T053): `Health.structure` must report the configured
   analyser (`settings.structure_analyzer`: `real` -> `stems-novelty`, `fake` -> `fake`).
6. CI/test-all (devops T014): lint and superset-check the 014 documents; `CONTRACTS_DIR` -> 014.
7. Frontend: 012's help topic `shortcuts` and the overlay group "Practice" change with the new keys
   (ui-test-ids 1.13.0 "Changed default keys").

## Lane split suggestion (combined round 014+015, up to 6 agents)

The owner's standing limit is **4 agents at once** (memory "Limit parallel agents"); run lanes 1-4 first
and start 5-6 when two of them reach their foundational hand-off, or ask the owner for 6.

| Lane | Agent role(s) | Tasks | Owned write paths |
|---|---|---|---|
| 1 `engine` | audio-engine | all 015 engine tasks; 014 T042-T043, T080-T081 | `src/audio-engine/**` except `src/types.ts` |
| 2 `worker` | beat-analysis + audio-ingest + stem-separation (+ devops, quality-gate) | 014 T020-T033, T050-T053, T076-T077, T086-T087; 015 T011; devops 014 T014 + 015 T010; quality-gate 014 T015-T016 + 015 T007-T008 | `worker/src/stemstudio_worker/{analysis/structure/**,engraving/score_view.py,documents/**,jobs/practice_*.py,api/practice_routes.py,main.py}` and their tests, `worker/tests/render/test_take_conversion.py`; `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**`; `tools/quality/**`, `docs/quality/**` |
| 3 `be014` | backend | 014 T034-T041, T108, T054-T057, T070-T071, T078-T079, T088-T091, T096-T097, T107 (if decided) | `src/StemStudio.Domain/{Sections,Setlists,Practice}/**`, `src/StemStudio.Persistence/**` (owner of `StemStudioDbContext.cs`, `Migrations/**` incl. the model snapshot; makes the context `partial` in its first commit), `src/StemStudio.Api/{Sections,ScoreFollow,Setlists,Practice}/**`, `src/StemStudio.Api/Worker/WorkerClient.Practice.cs` + job records, `src/StemStudio.Api/ApiComposition.cs`, `src/StemStudio.Api/LibraryCare/**` (T107/T108), their tests incl. `BackendContractTests.PracticeFlow.cs`, `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj` |
| 4 `be015` | backend | all 015 backend tasks | `src/StemStudio.Domain/Recording/**`, `src/StemStudio.Persistence/{Configurations/Recording*,StemStudioDbContext.Recording.cs}`, `src/StemStudio.Api/Recording/**`, their tests incl. 015's own pending-ids file; its migration is created **after** merging lane 3's foundational head (one linear model snapshot); its `MapRecordingEndpoints()` line in `ApiComposition.cs` and csproj links are requested from lane 3 |
| 5 `fe014` | frontend | 014 T044-T049, T058-T069, T072-T075, T082-T085, T092-T095, T098-T101 | `src/web-interop/**`, `src/StemStudio.Web/**` except 015's folders, `tests/StemStudio.Web.Tests/**` except 015's folders; owner of the shared shell and mixer files (`Mixer.razor`, `Mixer.Shortcuts.cs`, layout, navigation rail, `Program.cs`, help registry) |
| 6 `fe015+qa` | frontend + qa | all 015 frontend tasks; qa 014 T017-T018, T060, T068, T074, T084, T094, T100 (T103-T104 at integration) and all 015 qa tasks | `src/StemStudio.Web/{Recording,Components/Recording}/**`, `tests/StemStudio.Web.Tests/Recording/**`; `e2e/**`, `tests/fixtures/**`, `docs/testing/**`; hooks in the mixer markup/rail requested from lane 5 (one line each) |

Rules: `src/audio-engine/src/types.ts`, `src/StemStudio.Contracts/**`, `specs/014-*`, `specs/015-*` and the
key catalogue stay with the architects (contract changes via the orchestrator). Shared logs
(`docs/STATUS.md`, `docs/ROADMAP.md`, `docs/decisions/CTO-DECISIONS-*`, `docs/THIRD-PARTY-LICENSES.md`,
changelogs, `docs/public/**`) are owned by nobody in the wave: lanes write their own hand-offs. Critical
path: lane 1 (015 engine) and lane 5 (014 frontend, 28 tasks); give them the strongest settings and start
them first. Lane 2 needs no stack (all worker tests are synthetic). Briefs per lane: role file, the task
rows above, the contract files of DELTA.md 7, LESSONS.md section of the role.

## Docs impact

User manual (CTO, T106): song sections, score follow, setlists, keyboard and foot pedals (note the moved
012 keys: reset `0`, count-in `Shift+K`), practice log and privacy. Technical manual: `practice` worker lane,
`STEMSTUDIO_STRUCTURE_ANALYZER`, DejaVu fonts, ReportLab, the key catalogue file, API list 1.13.0.

## Next owner

Orchestrator: decide open issue 1, merge `main` (with 022/023) into the round, ask the architect for the
re-merge (DELTA.md 8), then start the wave with the lane split above.

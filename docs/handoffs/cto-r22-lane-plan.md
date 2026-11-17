# Lane plan: round 022 (library by artist and album) + 023 (lyric writing assistant)

Prepared by the architect at the round start (2026-11-17) on branch `022-023-round` after merging `main`
(e8d6a5d8, 013 library care), re-basing 022 onto 013 and merging the 023 design phase. Base head for every lane:
the tip of `022-023-round` that contains this file.

## Contracts the wave builds against (frozen for the wave)

| Contract | Version | Document |
|---|---|---|
| Backend API (current full) | **1.23.0** = 1.22.0 + 023 | `specs/023-lyric-writing-assistant/contracts/backend-api.openapi.yaml` (built by `merge_full.py` there) |
| Backend API 022 | 1.22.0 = 013's 1.12.0 + 022 | `specs/022-library-folders/contracts/backend-api.openapi.yaml`; delta `backend-api.library-folders.openapi.yaml` |
| Backend API 023 delta | 1.23.0-delta.1 | `specs/023-lyric-writing-assistant/contracts/backend-api.delta.openapi.yaml` |
| UI test ids | **1.23.0** (contains 1.12.0, 1.22.0) | `specs/023-lyric-writing-assistant/contracts/ui-test-ids.md`; 022 section "Additions in 1.22.0", 023 section "Additions in 1.23.0" |
| Agent tool catalogue | **1.2.0** (contains 1.0.0, 1.3.0, 1.1.0, 1.2.0; 69 tools) | `specs/011-ai-composer-agent/contracts/tool-catalogue.md` |
| Lyric model output | 1.0.0 | `specs/023-lyric-writing-assistant/contracts/lyric-assist-output.schema.json` |
| Help content, web-interop, worker, generator | unchanged | - |

A lane that finds a contract gap does not edit the contract: it records the gap and its interim choice in its
hand-off (the CTO decides at the mid-wave checkpoint). The 013 gate is real now: every new `/api/*` route gets
`503 library-maintenance` from the middleware; background loops wait on `LibraryGate.WaitWhileMaintenanceAsync`.

## Ownership rules for the wave

- A path belongs to exactly one lane (tables below). Tests mirror the source owner.
- **Owned by nobody in the wave** (only the CTO at integration): `docs/STATUS.md`, `docs/ROADMAP.md`,
  `docs/MoSCoW.md`, `docs/decisions/CTO-DECISIONS-*.md`, `README.md`, `docs/public/**`,
  `docs/THIRD-PARTY-LICENSES.md`, every `*.csproj`, `Directory.*.props`, `StemStudio.slnx`, the contracts under
  `specs/*/contracts/**`, `src/StemStudio.Contracts/**`, `src/StemStudio.Agent/Ports/**`,
  `src/StemStudio.Api/ApiComposition.cs`, `src/StemStudio.Web/Program.cs`,
  `tests/StemStudio.Api.Tests/Contract/*` except the two `BackendContractTests.Pending02x.cs` files,
  `tests/StemStudio.Persistence.Tests/StemStudioDbContextTests.cs`. Lanes write decisions, gaps and requested
  edits to these files in their own hand-off `docs/handoffs/<lane>-<feature>-<part>.md`.
- `tasks.md`: a lane ticks only its own rows.
- **Composition hooks** (skeleton, already called): API `Library/LibraryRegistration.cs` (backend-022),
  `LyricAssist/LyricAssistRegistration.cs` (backend-023); Web `Library/LibraryUiRegistration.cs` (frontend-022),
  `Components/Lyrics/Assist/LyricAssistUiRegistration.cs` (frontend-023). Register and map only there.
- **EF model snapshot** `src/StemStudio.Persistence/Migrations/StemStudioDbContextModelSnapshot.cs` is generated:
  both backend lanes commit their regenerated snapshot with their migration; at integration the CTO merges
  backend-022 first, then deletes 023's `AddLyricAssist` migration and re-adds it on the merged head
  (`dotnet ef migrations add AddLyricAssist`), which regenerates the snapshot and designer consistently.
- Quiet tools: build/test with `-v q`, logs to files, read summaries and failures only; at most 2 heavy processes
  per lane; no lane starts an E2E stack and **no lane touches the owner's Docker stack `stemstudio`**.
- Read `docs/process/LESSONS.md` (all roles + own role) before starting.

## Wave 1 (6 lanes)

If the 4-agents-at-once limit of this PC applies, start backend-022, backend-023, ai-agent and frontend-023
first (frontend-022 and qa can start on fixtures and page objects when a slot frees; their first tasks depend on
nothing else).

### Lane backend-022

- **Tasks (in order)**: T010 -> T011, T012 -> T013, T014 -> T015, T016 -> T017, T030 -> T031, T032 -> T033,
  T034 -> T035, T040 -> T041, T042 -> T043, T044 -> T045, T060 -> T061, T062 -> T063, T072 -> T073,
  T079 -> T080, T086 (022 tasks.md).
- **Writes**: `src/StemStudio.Domain/Library/**`, `src/StemStudio.Domain/Assets/**`,
  `src/StemStudio.Domain/Tracks/**`; `src/StemStudio.Persistence/Configurations/{AssetConfiguration,TrackConfiguration}.cs`,
  `src/StemStudio.Persistence/Repositories/{Asset*,Track*,Library*}`, migration `*_AddLibraryFolders*`;
  `src/StemStudio.Api/Library/**`, `src/StemStudio.Api/{Assets,Scores,Metadata,Sync,Tracks,Importing}/**`,
  `src/StemStudio.Api/Generations/**` except `Song*.cs`, `src/StemStudio.Api/Agent/Loopback/{LoopbackLibraryActions,LoopbackActions}.cs`;
  tests under `tests/StemStudio.{Domain,Persistence,Api}.Tests/{Library,Assets,Scores,Metadata,Sync,Tracks,Importing,Generations}/**`
  (not `Generations/Song*`), `tests/StemStudio.Api.Tests/Agent/Loopback/LoopbackLibraryActions*`,
  `tests/StemStudio.Api.Tests/Contract/BackendContractTests.Pending022.cs` (remove each id when served).
  Repositories are registered in `LibraryRegistration`, not in `PersistenceServiceCollectionExtensions`.
- **Read**: 022 `spec.md` User Stories 1-5, Edge Cases, FR-001..FR-038, SC-001..SC-006; `plan.md` "Key flows";
  `data-model.md` 1-8; `research.md` R-01..R-08, R-11 (incl. "Re-check against 013 as built"), R-13;
  `contracts/backend-api.library-folders.openapi.yaml`, `contracts/DELTA.md` 1-4; tool-catalogue section
  "Feature 022" (for T079/T080); `src/StemStudio.Contracts/Library/*`; 013's `LibraryGate.cs`.
- **013 gate**: `LibraryFilingBackfill` takes `LibraryGate` in its constructor (required), awaits
  `WaitWhileMaintenanceAsync` before each batch and re-counts pending items after a maintenance it waited for
  (T032 tests it with a real gate). Library-info `PUT`/`DELETE` carry no write-gate mark.
- **Fakes / fixtures**: response examples `RootView` (`getLibraryRoot`), `SongView` (`getLibraryFolder`),
  `EmptySearch` (`searchLibrary`), the `setTrackLibraryInfo`/`setAssetLibraryInfo` request examples, problem
  examples `FolderNotFound`, `LibraryQueryInvalid`, `LibraryInfoInvalid`, `NotFileable`, and the six 1.22.0 `Asset`
  examples with `placement` (folder ids by the R-02/R-03 rules, see `merge_full.py placement_lines`); the
  5,000-item perf data is generated in the test (T044), not a checked-in fixture.

### Lane frontend-022

- **Tasks**: T018 -> T019, T036 -> T037, T046 -> T047, T048 -> T049, T050 -> T051, T052 -> T053, T064 -> T065,
  T066 -> T067, T068 -> T069, T074 -> T075, T084, T087. (**T081/T082 move to frontend-023**, which owns the Web
  agent host.)
- **Writes**: `src/StemStudio.Web/Library/**`, `src/StemStudio.Web/Components/Library/**`,
  `src/StemStudio.Web/Api/{IStemStudioApiClient,StemStudioApiClient}.Library.cs` (new partials),
  `src/StemStudio.Web/Components/Export/ExportDialog.razor` and `Components/Scores/ScoreDialog.razor` (save confirmations T036/T037),
  `src/StemStudio.Web/Help/topics/library/**` (4 topics), library styles/tokens under `src/StemStudio.Web/wwwroot/css/library*`;
  tests `tests/StemStudio.Web.Tests/{Library,Export,Scores}/**` (only the dialog tests of T036), `tests/StemStudio.Web.Tests/Help/LibraryHelpTests.cs`
  (own file; do not edit `HelpCatalogTests.cs`/`HelpCoverageTests.cs`), `tests/StemStudio.Web.Tests/Fakes/{AssetData,ScoreData,LibraryData}.cs`.
  `FeatureCatalog.cs` is frontend-023's: 022 elements show at every level (owner decision Q2), so no entry is
  expected; if one is needed, list it in the hand-off.
- **Read**: 022 `spec.md` User Stories 1-4, Edge Cases, FR-013..FR-037 (UI), SC-002, SC-004, SC-005;
  `research.md` R-05, R-07, R-09, R-10, R-11 (frontend note: `503 library-maintenance` during polling is
  transient); `data-model.md` 5 and 7; ui-test-ids 1.23.0 section "Additions in 1.22.0" (incl. "Changed behaviour
  of existing ids"); `quickstart.md`; the Web API client conventions of 013 (`LibraryCareClient`).
- **Fakes**: `StubHttpMessageHandler` with JSON from the same contract examples as backend-022 (`RootView`,
  `SongView`, `EmptySearch`, problem examples, `Asset` examples with `placement`) in `Fakes/LibraryData.cs`.
- **Lessons**: check `/library` at 720 px height and 320 px width; keep `library-search` behaviour of 007 specs on
  `?view=all`.

### Lane backend-023

- **Tasks**: T012 -> T013, T014 -> T015, T016 -> T017, T025 -> T026, T027 -> T028, T029 -> T030, T038 -> T039,
  T045 -> T046, T047 -> T048, T056 -> T057, T061 -> T062, T074 -> T075 (023 tasks.md).
- **Writes**: `src/StemStudio.Domain/Lyrics/**` (incl. `Assist/`), `src/StemStudio.Persistence/StemStudioDbContext.cs`,
  `src/StemStudio.Persistence/PersistenceServiceCollectionExtensions.cs`, `src/StemStudio.Persistence/Configurations/{LyricsVoicesConfiguration,LyricAssist*}.cs`,
  `src/StemStudio.Persistence/Repositories/{Lyrics*,LyricAssist*}`, migration `*_AddLyricAssist*`;
  `src/StemStudio.Api/LyricAssist/**`, `src/StemStudio.Api/Lyrics/**`, `src/StemStudio.Api/Generations/Song*.cs`,
  `src/StemStudio.Api/Agent/Loopback/LoopbackLyricAssistActions.cs` and the `ILyricVerification` adapter (under
  `Api/LyricAssist/`); tests `tests/StemStudio.{Domain,Persistence,Api}.Tests/{Lyrics,LyricAssist,Songs}/**`,
  `tests/StemStudio.Api.Tests/Contract/BackendContractTests.Pending023.cs`.
- **Read**: 023 `spec.md` User Stories 1-6, Edge Cases, FR-001..FR-035, SC-001..SC-008; `plan.md` "Source code
  (owners)", "Phase 0 outcome"; `data-model.md` 1-7; `research.md` R-02..R-04, R-07, R-08, "Phase 0 results";
  `contracts/backend-api.delta.openapi.yaml`, `contracts/DELTA.md` 1, 2, 4, 5 (ports paragraph);
  `docs/handoffs/architect-023-phase0.md` "Lane notes" (backend-023).
- **013 gate**: the assist runner awaits `LibraryGate.WaitWhileMaintenanceAsync` before a queued assist and
  between blocks (T027/T028). Lyric writes carry no write-gate mark.
- **Fakes / fixtures**: `tests/StemStudio.Api.Tests/Contract/LyricAssistSamples.json` (`suggestion`, `summary`,
  `assist`); `tests/fixtures/lyrics/assist/` (gold syllable lists, `rhymes-{nl,en}.json`, `expected.json`,
  `sheets/`, `corpus/`); the fake model is `ScriptedLyricModel` via `LyricScript.Resolve` when
  `STEMSTUDIO_AGENT_BACKEND=fake` and `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` is set (scripts in `e2e/lyric-scripts/`).

### Lane frontend-023

- **Tasks**: T022 -> T023, T033 -> T034, T035 -> T036, T042 -> T043, T051 -> T052, T058 -> T059, T063 -> T064,
  T072, T076 -> T077 (023 tasks.md) **+ 022 T081 -> T082** (UI tool host `open_library_folder`, page context
  `library` for `/library/{folderId}`, incl. the `AgentPageContexts` mapping listed under 022 T021).
- **Writes**: `src/StemStudio.Web/Components/Lyrics/Assist/**`, the 010 lyrics pages in
  `src/StemStudio.Web/Components/Songs/**` (the editor hosts the pane), `src/StemStudio.Web/Components/Generation/TakeDetails.razor`
  (`take-lyrics-assisted`), `src/StemStudio.Web/Components/Agent/**` (approval diff), `src/StemStudio.Web/Agent/**`,
  `src/StemStudio.Web/Api/{IStemStudioApiClient,StemStudioApiClient}.LyricAssist.cs`,
  `src/StemStudio.Web/Experience/FeatureCatalog.cs`, `src/StemStudio.Web/Help/topics/lyric-assist/**` (6 topics);
  tests `tests/StemStudio.Web.Tests/{Songs,Agent,Components/Agent,Experience}/**`, `tests/StemStudio.Web.Tests/Generation/TakeDetails*`,
  `tests/StemStudio.Web.Tests/Help/LyricAssistHelpTests.cs`, `tests/StemStudio.Web.Tests/Fakes/{LyricsData,SongData,AgentData,LyricAssistData}.cs`.
- **Read**: 023 `spec.md` User Stories 1-6 (UI parts), FR on the pane, keyboard and announcements, SC on
  accessibility; ui-test-ids 1.23.0 "Additions in 1.23.0" (Level column binding, keys, live-region texts);
  `data-model.md` 2-4, 7; `contracts/backend-api.delta.openapi.yaml` (SSE `LyricAssistEventTypes`);
  architect-023-phase0 "Lane notes" (frontend-023); 022 tasks T081/T082 + tool-catalogue `open_library_folder`.
- **Fakes**: `Fakes/LyricAssistData.cs` from `LyricAssistSamples.json`; SSE through the existing `SseHttpHandler`
  (read-only use); `SheetAssistSettingsUpdate` always sends `rhythmTarget`.

### Lane ai-agent (022 + 023)

- **Tasks**: 022 T020 -> T021 (registry group `library` (9) only; the Web `AgentPageContexts` part goes to
  frontend-023); 023 T018 -> T019, T020 -> T021, T031 -> T032, T040 -> T041, T049 -> T050, T054 -> T055,
  T066 -> T067; 022 T077 -> T078, T083, T088; **023 T008 spike** (any time before wave 2's T068).
- **Writes**: `src/StemStudio.Agent/**` except `Ports/`, `tests/StemStudio.Agent.Tests/**` (incl.
  `Fixtures/Catalogue.cs`), `tools/agent-eval/**`, `tests/StemStudio.AgentEval.Tests/**`,
  `specs/011-ai-composer-agent/evaluation/**` (022 LIB-01..05, 023 cases 31-35, S4, S5); for T008 only:
  023 `research.md` R-05/R-06 numbers and ADR-037 "Phase 0 outcome" (agreed one-time edit).
- **Read**: tool-catalogue 1.2.0 sections "Rules", "Feature 022", "Feature 023", "Counts", "Selection per
  request"; 022 `research.md` R-12; 023 `research.md` R-05, R-06, R-08, R-09, `contracts/lyric-assist-output.schema.json`,
  `DELTA.md` 3 and 5 (ports); architect-023-phase0 "Lane notes" (ai-agent).
- **T008 spike**: Ollama only in an own throwaway container (own name, own port, own volume, removed
  afterwards) or native Ollama when free; **never the owner's `stemstudio` stack**. If neither is possible, skip
  with the reason in the hand-off; SC-004 and `BlockLines = 8` stay estimates.
- **Fakes**: `FixtureLibraryActions` (501 until T083) answering with `RootView`/`SongView`/`EmptySearch`;
  `FixtureActions` + `ScriptedLyricModel`/`AdversarialLyricModel` with the scripts and samples of
  `check_output_schema.py` and `e2e/lyric-scripts/`; eval fixture sheets `l-zomer`, `l-inject`, `l-fake-song`.

### Lane qa (022 + 023)

- **Tasks**: 022 T022, T023, T038, T039, T054, T070, T071, T076, T090; 023 T024, T037, T044, T053, T060, T065,
  T078. Specs are written against contracts and test ids, typechecked and linted; **no E2E stack in the wave**.
- **Writes**: `e2e/**` (incl. `e2e/lyric-scripts/`, `e2e/agent-scripts/`), `tests/fixtures/**`, `docs/testing/**`.
- **Read**: both `quickstart.md`; 022 spec User Stories 1-5 + SC; 023 spec User Stories 1-6 + SC;
  ui-test-ids 1.23.0 sections 1.22.0 and 1.23.0; 022 research R-10 (page-object migration); 023 DELTA.md 5
  (fake-script folder rule), decision 023-P0-10.
- **Fakes**: fake stack as today (`docker-compose.e2e.yml`, fake worker/agent); lyric scripts selected by sheet
  title (`<title-slug>-<kind>.json`); not-own/verbatim via `setLyrics` with `t-fake-song.en.txt`.
- **Lesson**: the default `npm test` includes the feature specs (about 1.2 h at integration).

## Wave 2 (short, after wave 1 is merged)

| Lane | Tasks | Writes |
|---|---|---|
| ai-agent | 023 T068 (reference-machine lyric suite; same Ollama rule as T008) | `tools/agent-eval/**`, hand-off |
| devops | 023 T069 (mount `e2e/lyric-scripts/`, `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` in the E2E override and non-Docker script, image size check); 022 T085 (only if qa reported the need); CI: Redocly lint of the 022 and 023 documents, `python -I specs/022-library-folders/contracts/merge_full.py --check` and `python -I specs/023-lyric-writing-assistant/contracts/merge_full.py --check`, `check_output_schema.py`, same steps in `scripts/test-all.*` | `docker-compose.e2e.yml`, `.github/workflows/**`, `.github/actions/**`, `scripts/**` |
| quality-gate | 022 T024 (dependency rules, test first), T089; 023 T071 | `tools/quality/**`, `docs/quality/**`, Sonar config |
| qa | 023 T070, 022 T092 (full E2E on the fake stack, one heavy suite at a time) | `e2e/**`, `docs/testing/**` |

Integration (CTO): 022 T091, T093; 023 T073, T080 (after T068); migration snapshot step above; STATUS, ROADMAP,
MoSCoW, CTO-DECISIONS from the hand-offs. Run heavy suites sequentially, visual baselines only in the CI visual
job's environment.

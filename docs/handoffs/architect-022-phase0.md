# Hand-off: architect / 022 Phase 0 part 2 (full contracts, contract code, skeleton)

022 tasks T002-T005 on branch `r22-p0-022` (worktree `.claude/worktrees/r22-p0-022`, from `main`
31d43422 = 001-012 + claude-key). Combined round {022 library by artist and album, 023 lyric writing
assistant}; 023's design runs in parallel on its own branch. 013 (library care) is being integrated in
parallel and is **not** on this branch: everything here is additive and the 013 dependencies are
listed below. Marker `HO:phase0`.

## What was built

### Contract versions (binding for the wave)

| Contract | Version | Document | State |
|---|---|---|---|
| Backend API | **1.22.0** | `specs/022-library-folders/contracts/backend-api.openapi.yaml` | = 1.11.0 (012) + delta 1.22.0-delta.1, built by `contracts/merge_full.py` (textual, base bytes unchanged); `x-stemstudio-contains` = 1.11.0's list + 1.22.0. Redocly 2.78.0: valid, 9 warnings = the 9 of 1.11.0; delta alone: valid, 0 warnings. `merge_full.py --check`: superset of 1.11.0 and of the delta |
| UI test ids | **1.22.0** | `specs/022-library-folders/contracts/ui-test-ids.md` | = 1.11.0 + changelog paragraph + "Additions in 1.22.0 (feature 022)" (the delta unchanged: 39 rows, 48 ids, 46 new, 2 reused) |
| Agent tool catalogue | **1.1.0** | `specs/011-ai-composer-agent/contracts/tool-catalogue.md` (in place, as 012 did for 1.3.0) | = 1.3.0 + delta: 61 tools, group `library` 9, page context `library` = 32, LIB-01..LIB-05 |
| Worker, generator, audio engine, help format, backup archive | unchanged | - | - |

One editorial addition beyond the delta (DELTA.md section 4): the six base `Asset` examples gain a
`placement` (otherwise invalid against the new required member). Folder ids in those examples are
computed with the R-02/R-03 rules (`arb385da54bfe8c6a9` = "rick astley").

### Code and skeleton

- `src/StemStudio.Contracts/Library/`: enums `FolderLevelDto`, `FolderSortDto`, `PlacementSourceDto`,
  `FolderArtKindDto`, `SearchFieldDto`; records `FolderRefDto`, `FolderCountsDto`, `ArtImageDto`,
  `ArtPlaceholderDto`, `FolderArtDto`, `LibraryFolderDto`, `LibraryFolderViewDto`, `HighlightDto`,
  `FolderHitDto`, `ItemHitDto`, `SearchTotalsDto`, `LibrarySearchResultDto`, `LibraryInfoRequest`
  (all three members `required`, `Automatic`), `LibraryInfoLevelDto`, `LibraryInfoStateDto`,
  `PlacementLevelDto`, `PlacementDto`; `LibraryProblemTypes` (`folder-not-found`,
  `library-query-invalid`, `not-fileable`); `InstrumentLabels` (+ `InstrumentFamily`,
  `InstrumentChannel`); `ApiRoutes.Library.cs` (`LibraryFolders`, `LibraryFolder(id)`, `LibrarySearch`,
  `TrackLibraryInfo(id)`, `AssetLibraryInfo(id)`, `LibraryETag(revision)`); `ContractLimits.Library.cs`
  (`ContractLimits` is now `partial`).
- `AssetDto.Placement` (`PlacementDto?`, not `required` in C# so no existing construction breaks; it is
  **always written**, `null` included, which satisfies the required-but-nullable wire member).
- Agent port: `ILibraryActions` (`BrowseLibraryFolderAsync`, `SearchLibraryAsync`, `GetLibraryInfoAsync`,
  `SetLibraryInfoAsync`), `LibraryFolderQuery`, `LibrarySearchQuery`, `LibraryInfoTarget`
  (`ForTrack`/`ForItem`, `LibraryInfoScope`); `IStemStudioActions.Library`.
- Skeletons: `src/StemStudio.Api/Agent/Loopback/LoopbackLibraryActions.cs` (wired in `LoopbackActions`,
  every method throws `NotImplementedException` until T080), `tools/agent-eval/Fixture/FixtureLibraryActions.cs`
  (every call `AgentActionException(501, "not-implemented")` until T083).
- Contract tests: `LibraryContractTests` (+ `.Samples`: fully populated DTOs, reusable by consumer fakes),
  `LibraryUiTestIdsTests`, `InstrumentLabelsTests`, `LoopbackLibraryActionsTests`,
  `FixtureLibraryActionsTests`, `LibraryPortTests`. `BackendContractTests`: the 9 operation ids are in
  the operation list and in `PendingLibraryOperations` (served-document check) until the backend
  serves them (T043, T063, T073).
- Agreed one-time edits outside the lane (same pattern as 012/013): `StemStudio.Api.Tests.csproj` links
  022's 1.22.0 as `Contract/backend-api.openapi.yaml`, 012's 1.11.0 as `Contract/012/`, the 022 delta and
  UI documents as `Contract/022/`; `ContractJson` gains `Backend012` (identical to 013's line) and
  `Backend022Delta`; `PracticeContractTests` reads its merge facts from `Contract/012` (013's diff applied
  verbatim, only the doc comment differs); `PortRecordTests` group list + `Library`.

### Research re-check against 013 as built (research.md R-11, ADR-036 "Phase 0 part 2 outcome")

Read-only check of `013-library-care` (e0e48e72). The R-11 row holds; two refinements are now binding
(tasks T032/T033, data-model 4):

1. **Backfill trigger**: a 013 restore (replace or merge) switches the library **without a restart**,
   and a pre-022 archive arrives with `FiledVersion = 0` (013 rebuilds the archive at its migration
   level, then migrates forward). `LibraryFilingBackfill` therefore runs at start-up **and** whenever a
   library read sees `pendingFiling > 0` (single-flight `ILibraryFilingTrigger`).
2. **Maintenance**: the backfill waits while 013's `LibraryGate` is in `Maintenance`
   (`WaitWhileMaintenanceAsync`), resolved with `GetService<LibraryGate>()` until 013 is merged in
   (pattern of 013's `AgentRunExecutor`).

Confirmed unchanged: backup copies the database (no archive change); merge restore adds EF entities
(owned placement carried); clean-up never touches items; library-info writes stay outside the
`backupRunning` gate (renames and metadata edits are allowed by 013); 013's UI lives on `/care` (no id or
help-topic collision); 013's 1.12.0 changes no `Asset` member and no 022 name. 014 and 015 are now built
after 022: `/` is bound through the 004 key map (014 adopts it later); the recording exclusion is a
predicate that 015 extends (`409 not-fileable` cannot occur before 015).

### Docs

`docs/ROADMAP.md` build order (owner decision 2026-11-17: {022, 023} -> {014, 015} -> 016 ->
{017, 018} -> {019, 021} -> 020), ADR-036 accepted in the 022 paragraph; `docs/decisions/CTO-DECISIONS-2026-11-15.md`
one row (build order). ADR-036: number confirmed (036 only for 022 on `main`, `013-library-care` and
the 023 branch), "Phase 0 part 2 outcome". `specs/022-library-folders/`: DELTA.md section 4, tasks.md
(T002-T005 done, refined T032/T033, lane split), research.md R-11 re-check, data-model 4, plan.md.

## Files changed

New: `specs/022-library-folders/contracts/{backend-api.openapi.yaml,ui-test-ids.md,merge_full.py}`,
`src/StemStudio.Contracts/Library/*` (26 files), `src/StemStudio.Agent/Ports/{ILibraryActions,LibraryFolderQuery,LibrarySearchQuery,LibraryInfoTarget}.cs`,
`src/StemStudio.Api/Agent/Loopback/LoopbackLibraryActions.cs`, `tools/agent-eval/Fixture/FixtureLibraryActions.cs`,
`tests/StemStudio.Api.Tests/Contract/{LibraryContractTests,LibraryContractTests.Samples,LibraryUiTestIdsTests,InstrumentLabelsTests}.cs`,
`tests/StemStudio.Api.Tests/Agent/LoopbackLibraryActionsTests.cs`, `tests/StemStudio.Agent.Tests/Ports/LibraryPortTests.cs`,
`tests/StemStudio.AgentEval.Tests/FixtureLibraryActionsTests.cs`, this hand-off.

Changed: `docs/ROADMAP.md`, `docs/decisions/{CTO-DECISIONS-2026-11-15,ADR-036-library-by-artist-and-album}.md`,
`specs/011-ai-composer-agent/contracts/tool-catalogue.md`, `specs/022-library-folders/{tasks,research,data-model,plan}.md`,
`specs/022-library-folders/contracts/{DELTA.md,redocly.yaml}`, `src/StemStudio.Contracts/{ContractLimits.cs,Assets/AssetDto.cs}`,
`src/StemStudio.Agent/Ports/IStemStudioActions.cs`, `src/StemStudio.Api/Agent/Loopback/LoopbackActions.cs`,
`tools/agent-eval/Fixture/FixtureActions.cs`, `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj`,
`tests/StemStudio.Api.Tests/Contract/{ContractJson,PracticeContractTests,BackendContractTests,BackendContractTests.StemsAndMixes}.cs`,
`tests/StemStudio.Agent.Tests/Ports/PortRecordTests.cs`.

## Tests and coverage

Red first: the first run of the new contract tests failed 3 (limits read as doubles, `latestAt`
round trip) and `PortRecordTests.StemStudioActions_HasOneGroupPerFeatureArea` failed on the new group;
all fixed, then green.

| Command | Result |
|---|---|
| `dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true` | **1871 passed, 2 skipped, 0 failed**; StemStudio.Api **98.89 % line / 91.97 % branch**; StemStudio.Contracts **100 % / 100 %** (gate 90/90 passed) |
| `dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true` | **730 passed**; StemStudio.Agent **98.85 % / 94.26 %** |
| `dotnet test tests/StemStudio.AgentEval.Tests -p:CollectCoverage=true` | **139 passed**; StemStudio.AgentEval **99.86 % / 92.53 %** |
| `dotnet test tests/StemStudio.Web.Tests` (consumer of `AssetDto`) | **2084 passed** |
| `dotnet test tests/StemStudio.Api.Tests --filter Contract` | 588 passed (incl. 152 new library/instrument/loopback cases) |

Domain, Persistence, worker and audio engine are untouched (not run).

## Quality checks

- `dotnet build StemStudio.slnx` and `-c Release`: **0 warnings, 0 errors**.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `uv run --project tools/quality python tools/quality/run_all.py`: **0 errors**, exit 0 (869 warnings
  repo-wide, baseline). New production code: no warning (the `InstrumentLabels` static data was split to
  stay under the unit-size limit). New test code: 2 warnings (`LibraryContractTests.Samples` data table
  20 lines; 8 lines shared with the `PracticeUiTestIdsTests` file-loading pattern).
- Redocly 2.78.0 (`specs/022-library-folders/contracts/redocly.yaml`): delta valid 0 warnings; full 1.22.0
  valid, 9 inherited warnings. `python -I specs/022-library-folders/contracts/merge_full.py --check`:
  `superset: yes`. `ruff check`/`ruff format` (tools/quality config) clean on `merge_full.py`.

## Dependencies added

None (.NET BCL only; `merge_full.py` uses PyYAML, already used by the 012/013 contract scripts).

## Docs impact

None for `docs/public` in Phase 0 (the CTO writes the "Library" chapter in T093).

## Open issues / requests

1. **013 merge (CTO)**: when 013 lands on `main` and `main` is merged into the round branch: re-run
   `merge_full.py --base specs/013-library-care/contracts/backend-api.openapi.yaml` (+ `--check`),
   re-merge `ui-test-ids.md` onto 013's 1.12.0, re-point `LibraryContractTests.Base` and
   `LibraryUiTestIdsTests.Base` to `Contract/013`, and resolve the expected conflicts listed in
   DELTA.md section 4 item 3. Backend then replaces the optional `LibraryGate` lookup by the injected
   gate if it prefers.
2. **023 merge (CTO)**: expected text conflicts with the 023 design branch (both add a group or
   entries in the same places): `IStemStudioActions` / `LoopbackActions` / `FixtureActions` (one property
   each), `PortRecordTests` group list, `BackendContractTests` operation list and pending lists,
   `StemStudio.Api.Tests.csproj` (the `Contract/backend-api.openapi.yaml` link: 1.23.0 = 1.22.0 + 023 by
   containment), `tool-catalogue.md` (1.1.0 and 1.2.0 sections and counts: 61 + 023's tools; the page
   context table), `docs/ROADMAP.md` only if 023 touched it (it should not).
3. **CI (devops/CTO bundle)**: add to the `contracts` job: Redocly lint of the 022 delta and full
   document with `specs/022-library-folders/contracts/redocly.yaml`, and
   `uv run --no-project --with "$PYYAML" python -I specs/022-library-folders/contracts/merge_full.py --check`.
   Not done here (`.github/` is devops-owned).
4. `AssetDto.Placement` is nullable in C#: the backend must set it for every non-recording item it
   returns (`filed = false` preview before the backfill, T034/T035); the contract test
   `AssetWithoutPlacement_WritesAnExplicitNull` documents that null stays valid on the wire.
5. `src/StemStudio.Contracts/StemStudio.Contracts.csproj` still says it mirrors 1.21.0 (stale since
   012); left for the CTO to avoid a third conflicting edit with 013/023.

## Lane notes (what each lane must know)

- **backend-022**: DTOs, routes, limits, problem types are fixed (`StemStudio.Contracts.Library`,
  `ApiRoutes.Library*`, `ContractLimits.Library*`, `LibraryProblemTypes`). Remove ids from
  `PendingLibraryOperations` as you serve them. Replace `LoopbackLibraryActions` (T080) and its skeleton
  test with the T079 tests. Use `InstrumentLabels.SearchWordsOf/TryGetLabel` in `InstrumentTerms`, never
  a second word list. Backfill refinements 1-2 above (T032/T033). The migration `AddLibraryFolders`
  must only add columns and indexes (013 `KnownSchema`/`DatabaseRebuild` accept it as is). Recording
  exclusion as a predicate in `LibraryFilingRules` (015 extends it). Folder ids: SHA-256 rule of R-03
  (`merge_full.py.folder_id` is a reference implementation for simple names; the examples' ids come
  from it, e.g. artist "Rick Astley" -> `arb385da54bfe8c6a9`).
- **frontend-022**: consume `LibraryFolderViewDto`/`LibrarySearchResultDto`/`LibraryInfoStateDto`;
  build fakes from `LibraryContractTests.Samples` and the contract examples (`RootView`, `SongView`,
  `EmptySearch`). `/` shortcut through the 004 key map (014 is built later). Treat `503
  library-maintenance` during folder polling as transient once 013 is in (keep the last view). Labels
  for channels from `InstrumentLabels`. UI ids: `specs/022-library-folders/contracts/ui-test-ids.md`
  1.22.0 (and the `/library?view=all` migration of `LibraryManagementTests`).
- **ai-agent (shared with 023)**: catalogue 1.1.0 is in `specs/011-ai-composer-agent/contracts/tool-catalogue.md`;
  update `tests/StemStudio.Agent.Tests/Fixtures/Catalogue.cs` (57 -> 61, `library` 9) in T020; the port
  is `IStemStudioActions.Library` (`ILibraryActions`); `set_library_info` sends a complete
  `LibraryInfoRequest` (merge the GET values; `LibraryInfoTarget.ForTrack/ForItem`). Seed
  `FixtureLibraryActions` for LIB-01..LIB-05 in T083 (replace the 501 skeleton and its test).
- **qa (shared with 023)**: ids 1.22.0 as above; page-object migration T022 first; fixtures T023; the
  pre-022 database fixture must be a real pre-`AddLibraryFolders` database (013's restore check accepts
  only known migrations).
- **quality-gate**: T024 dependency rules (Contracts.Library depends only on Contracts; Agent tools on
  Ports only).

## Lane split for the round (suggestion, CTO decides; tasks.md "Lane split for the combined round")

Up to 6 agents: **backend-022** (29 tasks), **frontend-022** (24), **backend-023**, **frontend-023**,
**ai-agent** shared (022: 6 tasks in waves A and C; 023's lane fills 022's idle wave B), **qa** shared
(022: 10 tasks; one E2E stack at a time). quality-gate T024/T089, the conditional devops T085 and the
CI additions (open issue 3) run in a short second wave or in an idle slot.

## Next owner

CTO: start the lane wave from this branch's head (all lanes branch from it); merge `main` (013) in
before or during the wave per open issue 1.

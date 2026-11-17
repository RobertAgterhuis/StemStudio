# Hand-off: backend-022b / library info and loopback library actions (T072-T073, T079-T080)

Lane `backend-022b` (split from backend-022), branch `r22-lane-be022b`, based on f9968494 plus backend-022's
e3b9cea1 (folder view endpoints) and 41c502b1 (search, with 1c493240), merged in.

## What was built
- `src/StemStudio.Api/Library/LibraryInfoService.cs`: GET/PUT/DELETE library info of a track and of an item.
  - Track: `value` = the track's user name, `effective` + `source` = `PlacementResolver.Resolve` with the track
    info, `automatic` = `PlacementResolver.Automatic` (band info > import > fallback). PUT validates through
    `Track.SetLibraryInfo` (domain `LibraryInfoRules`), then `ILibraryFiler.RefileTrackAsync` and one save;
    `movedItems` = items whose song folder changed. DELETE = PUT with all null.
  - Item: `automatic` = what the item gets without its own names (the track's user name, else automatic);
    `placement` = `LibraryMapper.PlacementOf` (stored, `filed = true` after a write). PUT/DELETE answer
    409 `not-fileable` when `LibraryFilingRules.IsFileable` is false; GET of such an item is 200 with
    `placement: null` (the contract declares no 409 on GET).
- `LibraryInfoEndpoints.cs` (six operation ids, tag `library`), `LibraryInfoProblems.cs` (404 texts, 409
  `not-fileable`, 400 field errors `artist`/`album`/`song`).
- `LibraryRegistration.cs`: two appended lines - `services.AddScoped<LibraryInfoService>()` and
  `.MapLibraryInfoEndpoints()` chained after `MapLibraryFolderEndpoints()`.
- `BackendContractTests.Pending022.cs`: the six `*LibraryInfo` ids removed; after the merge of 41c502b1 the
  list is empty (every 022 operation is served).
- `src/StemStudio.Api/Agent/Loopback/LoopbackLibraryActions.cs`: browse (`/api/library/folders[/{id}]` with
  `sort`, `kind`, `limit`), search (`/api/library/search` with `q`, `within`, `limit`), get/set library info.
  The folder id is escaped with `Uri.EscapeDataString`, so a model value like `../tracks` stays one path segment.
  `LoopbackActions.cs` unchanged (it already wired the group). The skeleton test
  `tests/StemStudio.Api.Tests/Agent/LoopbackLibraryActionsTests.cs` was replaced by
  `tests/StemStudio.Api.Tests/Agent/Loopback/LoopbackLibraryActionsTests.cs`.

## Decisions (for the CTO log)
- E1 Item-scope `automatic` includes the track's user name (source `track`): the item dialog's "Automatic"
  box shows what the item gets when its own name is cleared (DELETE item = "the track's or automatic names").
- E2 The track-scope state uses a provenance built from the track (title/artist); the track facts come first,
  so this only matters for a track without any names.
- E3 GET item of a not-fileable item answers 200 with `placement: null` (no 409 on GET in the contract).
- E4 Library-info writes carry no `RequireGateOpen` (backup) mark, as research "013 library care" says; the
  global middleware still answers 503 `library-maintenance` during maintenance and 403 for cross-site writes.
- E5 The 400 message is the domain's `LibraryInfoRules.Message` ("Enter a name of 1 to 200 characters without
  control characters, or leave it automatic."), not the contract example text; the contract example is
  illustrative only (schema checked by `ContractAssert`).

## Tests and coverage
- New: `tests/StemStudio.Api.Tests/Library/LibraryInfo{Api,TrackTests,ItemTests,SecurityTests}.cs` (37 tests),
  `tests/StemStudio.Api.Tests/Agent/Loopback/LoopbackLibraryActionsTests.cs` (13 tests, incl. search with and
  without `within` and a 422 as a readable tool error); every response checked
  with `ContractAssert` against backend-api 1.22.0.
- Covered: sources and automatic names (import, match, fallback, track, item), `movedItems` (1, then 0 on the same
  names), item levels kept on a track edit, a later band-info lookup never overrides user names, reset, 400 per
  field (blank, empty, control characters, tab, 201 characters; 200 accepted; trimmed), malformed body 400,
  404 on all six, 409 `not-fileable`, an item whose track is gone.
- Final full run on the merged head (681b9b67) `dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`:
  2656 passed, 3 skipped, 3 failed. `HistoryAndUndoTests.Reload_RestoresMessagesPlansAndSteps` and
  `DetailAutoQueueTests.HostedService_SubmitsPendingPhasesOnTheTick` are load flakes (pass alone; an earlier run
  also showed `AgentEndpointsTests.Executor_FailsTheRunWhenTheAgentThrows` and the known
  `ClaudeKeyLeakTests.StoredSentinel_NeverLeaks`). `LibraryPerfTests.FolderViewsWithin100ms_RebuildWithin200ms_SearchWithin50ms`
  (backend-022's) fails here also alone: search p95 90 ms against the 50 ms limit on this machine (Debug,
  instrumented build); not caused by this lane - for backend-022 to look at.
- StemStudio.Api line 98.46 %, branch 91.70 %. New files: LibraryInfoService 156/156 lines, 30/32 branches
  (the two misses are `ArgumentNullException.ThrowIfNull` arms); LibraryInfoEndpoints 42/42; LibraryInfoProblems
  22/22; LoopbackLibraryActions 42/42 lines, 8/8 branches.
- Test-first note: the T072 tests and T073 code were written in one step and committed together (c651f9e5), not as
  a separate red commit.

## Quality checks
- Build 0 warnings; `dotnet format whitespace --verify-no-changes` clean.
- `tools/quality/sig_check.py` on all new files: 0 errors, 0 warnings (after the unit-size fix 366d78fa).

## Security self-review
- PUT/DELETE of both resources: cross-site `Origin`/`Sec-Fetch-Site` gives 403 and changes nothing; during
  maintenance 503 `library-maintenance` and nothing changes (8 tests, `LibraryInfoSecurityTests`).
- Input limits: names trimmed, 1..200 characters (contract `maxLength` 200 = `ContractLimits.LibraryNameMaxLength`),
  no control characters; a body missing a required member, with a wrong type or not JSON is 400.
- Names are untrusted text: path-, markup- and SQL-like names are stored verbatim as display names, the folder ids
  stay `^(ar|al|so)[0-9a-f]{16}$` and no file or directory appears in the data directory (5 cases). EF parameters
  only, no raw SQL.
- Loopback: model folder ids are escaped into one path segment (tests for `../tracks` and `?`-injection).
- `set_library_info` risk `change` (approval in a tainted run) and names as `untrusted_text` are declared in the
  tool catalogue 1.2.0 (lines 258-272) and implemented by the ai-agent lane (T077/T078), not in this lane; the
  loopback returns the names unchanged for those tools to wrap.

## Dependencies added
None.

## Docs impact
None (the user manual for the library info dialog is frontend-022's).

## Open issues / requests
- Search `within=<artist folder>` does not return the artist folder itself as a hit (only what is inside); the
  loopback test follows that behaviour. If the agent's `search_library_items` expects otherwise, backend-022 decides.
- Perf test above (backend-022).
- Integration: `LibraryRegistration.cs` and `BackendContractTests.Pending022.cs` were edited by both lanes; merging
  `r22-lane-be022b` after `r22-lane-be022` leaves the registration with both chains and an empty pending list.

## Next owner
backend-022 (T086 polish) and the CTO at integration: `LibraryRegistration.cs` has the two appended lines above.

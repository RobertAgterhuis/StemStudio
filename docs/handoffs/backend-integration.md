# Hand-off: backend / integration

## What was built

1. **SIG hard error fixed**: `MixTests.Validate_RejectsMetronomeOutOfRange` takes one `MetronomeCase` record
   (`TheoryData<MetronomeCase>`), 1 parameter.
2. **`ListLogger.Log<TState>`** keeps the `ILogger.Log` signature; marked with
   `// sig-exempt: interface-mandated signature (ILogger.Log)` on the line above. `tools/quality` in this branch does
   not yet honour the marker, so the gate still reports this single error until quality-gate's change merges.
3. **Orphaned imports (open issue #2 closed)**:
   - `ImportService`: when the worker call is cancelled (client disconnect) or fails unexpectedly, the inserted track is
     failed with `"Import interrupted"` (saved with `CancellationToken.None`, retryable) and the exception is rethrown.
     Known worker outcomes are unchanged: 400/413/415 rejection or 503 outage still remove the track (nothing persisted,
     the client gets the reason; Spotify without match = synchronous 400 per contract 1.0.2).
   - `OrphanedImportSweeper` (new, scoped) runs at the start of every `JobSyncService` tick and fails queued tracks with
     no worker job whose `UpdatedAt` is older than `StemStudio:OrphanedImportTimeout` (default 120 s, deliberately
     longer than the 90 s create-job timeout so an import still waiting on spotDL is never swept).
   - Persistence: `ITrackRepository.ListOrphanedAsync(updatedBeforeUtc)`.
4. **duplicateMix 409**: endpoint metadata declares 409; contract test
   `DuplicateMix_ConcurrentNameClash409MatchesContract` (unique index rejects the insert) validates against the YAML.
5. **Title/artist from worker metadata**: already applied on every poll (`TrackJobApplier.ApplyMetadata`) and from the
   result on completion, truncated by the domain (500/500). New tests: placeholder `"YouTube video <id>"` replaced by
   the real title on a later poll, replaced by result metadata on completion, and 600-char values cut to the limits.
   No code change needed; once the worker sends the real title it shows up.
6. **Worker timeouts**: per-request attempt timeout through the standard resilience handler's `TimeoutGenerator`
   (`WorkerTimeouts.For`): POST (create job) `StemStudio:WorkerCreateJobTimeout` = 90 s, everything else
   `StemStudio:WorkerPollTimeout` = 15 s. Total timeout and breaker sampling derive from these. Tests: a POST slower
   than the poll timeout succeeds, a GET slower than the poll timeout gives `WorkerUnavailableException`, and defaults
   are asserted (create >= 60 s > poll; orphan > create).
7. **Soft warnings in production code** reduced from 11 to 1 (behaviour-preserving splits of try/catch units in
   `ImportService`, `UploadRequestReader`, `MixService`, `MixValidator`, `AtomicFile`, `HashingCopy`, `WorkerClient`).
   Remaining: `WorkerClient.cs *global* 20 LOC` (usings, JSON options, records, log declarations).
8. **CTO addition, Persistence branch coverage**: `UnitOfWorkTests` hit every branch of `IsUniqueViolation`
   deterministically with a failing `DbContext`: 2067 and 1555 become `DuplicateKeyException`; 787, a non-SQLite inner
   exception and no inner exception are rethrown unchanged. `InternalsVisibleTo StemStudio.Persistence.Tests` added.

## Red -> green evidence

| Item | Failing test commit | Passing implementation commit |
|---|---|---|
| 1-6 | a011f23 (no compile: missing sweeper, options, `WorkerTimeouts`, `ListOrphanedAsync`) | 9a2b83c |
| 7 refactor | (all tests green before and after) | c2e91ee |
| 8 coverage | tests only | b22fe08 |

## Files changed

- `src/StemStudio.Api/{ApiComposition,StemStudioApiOptions}.cs`, `Importing/{ImportService,UploadRequestReader}.cs`,
  `Mixes/{MixEndpoints,MixService,MixValidator}.cs`, `Storage/{AtomicFile,HashingCopy}.cs`,
  `Sync/{JobSyncService,OrphanedImportSweeper}.cs`, `Worker/{WorkerClient,WorkerClientRegistration,WorkerTimeouts}.cs`
- `src/StemStudio.Persistence/Repositories/{ITrackRepository,TrackRepository}.cs`, `StemStudio.Persistence.csproj`
- `tests/StemStudio.Domain.Tests/Mixes/MixTests.cs`
- `tests/StemStudio.Persistence.Tests/{StemStudioDbContextTests,UnitOfWorkTests}.cs`
- `tests/StemStudio.Api.Tests/Contract/BackendContractTests.StemsAndMixes.cs`, `Importing/ImportServiceTests.cs`,
  `Sync/{JobSyncServiceTests,OrphanedImportSweeperTests}.cs`, `Worker/WorkerClientTests.cs`,
  `TestSupport/{Eventually,ListLoggerProvider,MixRepositoryStubs}.cs`

## Tests and coverage

`dotnet test StemStudio.slnx -p:CollectCoverage=true` (coverlet gate 90 % line AND branch per component, passes):

| Project | Passed / Failed | Line | Branch | Method |
|---|---|---|---|---|
| StemStudio.Domain.Tests | 173 / 0 | 97.73 % | 97.72 % | 96.96 % |
| StemStudio.Persistence.Tests | 27 / 0 | 99.04 % | 100 % | 97.5 % |
| StemStudio.Api.Tests (Api) | 233 / 0 | 99.89 % | 98.26 % | 100 % |
| StemStudio.Api.Tests (Contracts) | (same run) | 100 % | 100 % | 100 % |

Persistence stability, `dotnet test tests/StemStudio.Persistence.Tests -p:CollectCoverage=true` three times:
27/0 passed, 99.04 % line, 100 % branch each run.

## Quality checks

- `dotnet build StemStudio.slnx`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: exit 0.
- `uv run --project tools/quality python tools/quality/run_all.py`: `341 files, 2485 units, 15 clones, 1 errors,
  111 warnings -> exit 1`. The one error is `ListLoggerProvider.cs:30 ListLogger::Log<TState> has 5 parameters`, the
  sig-exempt line (item 2) waiting for quality-gate's marker support. Backend production code: 1 soft warning
  (`WorkerClient.cs *global*`). The `MixTests` hard error is gone; no new clones in backend paths (shared
  `Eventually.WaitUntilAsync` and `MixRepositoryStubs` instead of copies).

## Dependencies added

None.

## Open issues / requests

1. quality-gate: honour `// sig-exempt:` above `ListLogger.Log<TState>` (then the gate exits 0 for backend paths).
2. devops/docs: new optional settings `StemStudio__WorkerCreateJobTimeout` (default `00:01:30`),
   `StemStudio__WorkerPollTimeout` (`00:00:15`), `StemStudio__OrphanedImportTimeout` (`00:02:00`); keep the orphan
   timeout above the create-job timeout.
3. Rare edge: if a create-job call outlives the orphan timeout anyway (misconfiguration), the sweeper fails the track
   and the late job id is still stored on the failed track; the user can retry.
4. Remaining soft warnings in backend test code (long test methods, duplicated `using`/fixture blocks) are untouched.

## Next owner

CTO: review and merge. quality-gate: item 1.

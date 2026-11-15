# Hand-off: backend / polish

## What was built

- **Structured logging (T111)** with source-generated `[LoggerMessage]` methods (CA1848-clean):
  - `TrackSynchronizer`: `Track {TrackId}: {From} -> {To} {Error}` (Information) on every status change, worker
    unreachable (Warning, no exception attached), unexpected per-track failure (Error, with exception; server log only).
  - `JobSyncService`: failing tick (Error, the loop continues).
  - `WorkerClient`: `Processing service unreachable on {Request}: {Reason}` (Warning, exception message only, never a
    stack trace) for transport failures and 5xx; `Processing service rejected {Request} with {Status}: {Detail}`
    (Information) for 4xx problems.
  - `ImportEndpoints`: `Import ({Kind}) answered {Status}` for URL and upload imports.
  - Clients never see stack traces: unhandled exceptions become a bare 500 `application/problem+json`
    (`UseExceptionHandler` + `AddProblemDetails`), asserted in `LoggingTests`.
- **Refactor for SIG**: long units split (`TrackMapper`, `MixMapper`, `ImportService`, `TrackService`,
  `TrackJobApplier`, `TrackSynchronizer`, `WorkerClient`, `MixEndpoints`, both EF configurations); the duplicated
  temp-file + rename logic of `StemFileStore` and `UploadStore` now lives in `Storage/AtomicFile`.

## Red -> green evidence

| Task | Failing test commit | Passing implementation commit |
|---|---|---|
| T111 logging | 200d4ff | 1e7f6c2 |
| SIG refactor (behaviour-preserving) | (all 414 tests green before and after) | 7b5c111 |

## Files changed

- `src/StemStudio.Api/Worker/WorkerClient.cs`, `src/StemStudio.Api/Importing/{ImportEndpoints,ImportService}.cs`,
  `src/StemStudio.Api/Sync/{TrackSynchronizer,TrackJobApplier}.cs`, `src/StemStudio.Api/Tracks/{TrackMapper,TrackService}.cs`,
  `src/StemStudio.Api/Mixes/{MixMapper,MixEndpoints}.cs`, `src/StemStudio.Api/Storage/{AtomicFile,StemFileStore,UploadStore}.cs`,
  `src/StemStudio.Persistence/Configurations/{TrackConfiguration,MixConfiguration}.cs`
- `tests/StemStudio.Api.Tests/LoggingTests.cs`, `TestSupport/{ListLoggerProvider,ApiFactory}.cs`, `Worker/WorkerClientTests.cs`
- `specs/001-stemstudio-mvp/tasks.md` (T111 ticked; all backend tasks are now `[X]`)

## Tests and coverage (final gate)

| Project | Passed / Failed | Line | Branch | Method |
|---|---|---|---|---|
| StemStudio.Domain.Tests | 173 / 0 | 97.73 % | 97.72 % | 96.96 % |
| StemStudio.Persistence.Tests | 21 / 0 | 99.03 % | 100 % | 97.43 % |
| StemStudio.Api.Tests (Api) | 220 / 0 | 99.88 % | 98.18 % | 100 % |
| StemStudio.Api.Tests (Contracts) | (same run) | 100 % | 100 % | 100 % |

Commands: `dotnet test tests/StemStudio.{Domain,Persistence,Api}.Tests -p:CollectCoverage=true` (coverlet gate
90 % line AND branch per component: passes).

## Quality checks

- `dotnet build StemStudio.slnx --no-incremental`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: exit 0.
- Unit size: `tools/quality/sig_check.py` is not in this worktree and downloading lizard was not permitted, so a
  local heuristic counted every non-blank line of each method **including the signature and braces**: the largest
  units are 16-17 lines (8 units: `HashingCopy.PumpAsync/CopyAsync`, `AtomicFile.WriteAsync`,
  `UploadRequestReader.ReadAsync`, `WorkerClient.EnsureSuccessAsync`, `MixService.CreateAsync/SaveAsync`,
  `ImportService.StoreAndImportAsync`), all try/catch/finally shapes, well under the hard limit of 30; everything else
  is <= 15. Parameters <= 4 everywhere (parameter objects/tuples where needed). The quality-gate run of
  `tools/quality/run_all.py` after the merge is the authoritative number.

## Dependencies added

None in this checkpoint. Whole backend wave: none new (YamlDotNet 18.1.0, MIT, was already pinned by the architect
and is now referenced by `StemStudio.Api.Tests`).

## Open issues / requests

1. (from US1) A queued track can stay without a worker job if the client disconnects between insert and worker
   call; consider a timeout that fails such tracks.
2. (from US4) `duplicateMix` may answer 409 on a concurrent name clash, which the contract does not list.
3. (from foundational) `ContractLimits` could expose the title/artist/stage/error limits for the Web app.
4. CTO: the security finding on `$`-anchored regexes is fixed (`\A...\z` everywhere, tests for LF/CRLF/tab/NUL),
   see `backend-foundational.md`.

## Next owner

CTO: review and merge (T119). quality-gate: run `tools/quality/run_all.py` on `src/StemStudio.{Domain,Persistence,Api}`.

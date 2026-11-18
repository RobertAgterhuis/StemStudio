# Backend hand-off: detail phase re-queued after a cancel (fix/agent-detail-requeue)

## Root cause

`DetailSynchronizer` loads every detail-active track at the start of a pass and handles them one at a time.
When a pending phase (queued, no worker detail job yet) is cancelled after the load, the pass still holds the
stale "queued" snapshot. It submits it (`POST /v1/detail-jobs`) and saves it, which overwrites "cancelled" and
leaves the worker job running. A cancel that arrives during the worker call is lost in the same way. The race
dates from feature 002. It started to show in CI because more detail-active tracks are left over from earlier
specs, so a pass takes longer and the window is wider.

Evidence (main run 44212916383, compose logs, chromium us2-detail): track `f3d902a3` completed at 04:07:05.159
and the detail pass loaded it at 05.160. The test polled it and cancelled it locally (`UPDATE DetailPhase` at
05.195, no worker cancel call). The pass then reached the track: `POST /v1/detail-jobs` at 05.279 and
`UPDATE "Tracks" SET "DetailPhase"` at 05.293, which put the status back to queued. Firefox did send a worker
cancel at 04:07:11.777 because its cancel came after the submission was saved.

## Fix

- `src/StemStudio.Api/Detail/DetailPhaseLocks.cs` (new, singleton) keeps one `SemaphoreSlim` per track. It is
  held around each synchroniser step and around `DetailService.StartAsync` and `CancelAsync`.
- `ITrackRepository.IsDetailPhaseCurrentAsync` compares the stored phase with the tracked one using the
  `DetailPhaseJson` comparer. When they differ, the synchroniser skips the track until the next tick.

## Tests

- `tests/StemStudio.Api.Tests/Detail/DetailConcurrencyTests.cs` covers a cancel after the pass loaded the track
  and a cancel during the worker call. Both failed before the fix (status was queued).
- `tests/StemStudio.Api.Tests/Detail/DetailPhaseLocksTests.cs` and
  `tests/StemStudio.Persistence.Tests/DetailPersistenceTests.cs` (`IsDetailPhaseCurrent_*`).
- All suites pass in Release with `CollectCoverage`. Line / branch coverage: Api 98.52 / 91.13, Persistence
  99.31 / 93.93, Domain 98.97 / 94.06.
- Release build has 0 warnings and `dotnet format --verify-no-changes` is clean. `tools/quality/run_all.py`
  passes with 0 errors (warnings only).

## Not run

The agent E2E was not run locally. The integration test reproduces the logged interleaving directly.

## Dependencies

None added.

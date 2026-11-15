# Hand-off: backend / delete track (contract 1.1.0, `deleteTrack`)

## What was built

- `DELETE /api/tracks/{trackId}` (`deleteTrack`, tag `tracks`), mapped in
  `src/StemStudio.Api/Tracks/TrackEndpoints.cs` next to the other track endpoints, served by the new scoped
  `src/StemStudio.Api/Tracks/TrackDeletionService.cs` (separate class so `TrackService` keeps 4 constructor
  parameters).
  - **204** (no body) when the track is `completed` or `failed`.
  - **404** problem details, detail `Track {id} does not exist.` (same as `getTrack`).
  - **409** problem details for any non-terminal status (`queued`, `downloading`, `separating`, `analysing`):
    - `title`: `Track is still being processed`
    - `detail`: `The track is still being processed; wait until it has completed or failed.`
    (also exposed as `TrackDeletionService.StillProcessingTitle` / `StillProcessingDetail`).
- Order of work: (1) the track row and **all** its mixes are removed in one `SaveChanges` (one transaction) via
  the new `ITrackRepository.RemoveWithMixesAsync`; (2) then, best effort, the track directory
  `<DataDir>/tracks/<trackId>/` (new `IStemFileStore.DeleteTrackFiles`) and the stored upload (when the track
  has one and it still exists). `IOException`/`UnauthorizedAccessException` during file clean-up is logged as a
  warning (`Track {TrackId} deleted, but its {What} could not be removed from disk`) and the response is still
  204; any other exception surfaces as a 500 but the DB rows are already gone (consistent state).
- The worker is never called (its per-source cache stays, as the contract describes).
- **No migration.** The Mix -> Track FK stays `Restrict`; mixes are deleted explicitly in the repository. EF
  orders the deletes (mixes before track) in the single unit of work.
- No CORS change (the browser never calls DELETE; the Blazor server does).

## Tests

- Persistence: `Track_RemoveWithMixesDeletesTheTrackAndOnlyItsMixes` (theory: 0 and 2 mixes; other track's mix
  survives).
- Api: `Tracks/TrackDeletionTests.cs` (204 with mixes + stems removed and other track untouched, no worker
  calls; failed upload removes the stored upload; upload already gone; 409 for every non-terminal status with
  exact title/detail and nothing removed; 404; file clean-up failure theory: IO/Unauthorized -> 204 + warning,
  unexpected -> 500 with row deleted); `Storage/StemFileStoreTests` (`DeleteTrackFiles` removes only that
  track's directory; missing directory is a no-op); contract: `DeleteTrack_EveryStatusMatchesContract`
  (204/409/404 validated against the OpenAPI document), operation-id list updated, served OpenAPI document
  test now includes `deleteTrack`.

## Evidence

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `dotnet test tests/StemStudio.Persistence.Tests -c Release -p:CollectCoverage=true`: 29 passed;
  StemStudio.Persistence line 99.17%, branch 100%.
- `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true`: 247 passed;
  StemStudio.Api line 99.86%, branch 97.5%. `TrackDeletionService` and `StemFileStore` 100% line and branch.
- `uv run --project tools/quality python tools/quality/run_all.py`: 0 errors (104 warnings, none new in
  production code; the only remaining warning touching new files is the shared test-class boilerplate clone
  that every Api test class already has).

## For the frontend

- Call `DELETE /api/tracks/{id}` from the Blazor server; treat 204 as success, 404 as "already gone", 409 as
  "still processing" and show the problem `detail` text above. After deletion, mixes of that track are gone
  from `GET /api/mixes`.

## Dependencies

- None added.

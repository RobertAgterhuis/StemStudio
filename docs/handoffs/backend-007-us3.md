# Hand-off: backend / 007-us3 (T049-T050)

## What was built

- `GET /api/assets` (`listAssets`, `AssetLibraryService` + `AssetListQuery`):
  - filters: `kind`, `trackId`, `status`, `q` (case-insensitive "name contains")
  - paging: `limit` 1..200 (default 100) and `offset`
  - newest first; `total` and `totalSizeBytes` count every item that matches the filters
  - invalid values -> 422 naming the parameter (`limit`, `offset`, `q`, `status`, `kind`)
- `PATCH /api/assets/{id}` (`renameAsset`, `AssetService.RenameAsync`): the name is trimmed, must be 1..120
  characters and contain no control characters; violations -> 422. Unknown item -> 404.
- `DELETE /api/assets/{id}` (`deleteAsset`): removes the row and `assets/<id>/` and answers 204. An active render is
  cancelled with `DELETE /v1/render-jobs/{id}`; an unreachable worker is ignored (a late completion finds no item).
  Unknown item -> 404.
- `POST /api/assets/{id}/retry` (`retryAsset`, `AssetRetryService`):
  - only a failed item can be retried; otherwise 409 `asset-not-failed`
  - `ExportPlanner.Replan` plans the stored recipe again; if the track is gone or a channel is missing -> 409
    `source-unavailable`
  - the job is submitted again -> 202; an unreachable worker -> 503 and the item stays failed
  - unknown item -> 404
- Deleting a track keeps its items, with `provenance.trackAvailable = false` and the title snapshot (tested).
- `ITrackRepository.ExistingIdsAsync` computes `trackAvailable` with one query per page (Persistence test added).
- `PendingAssetOperations` now holds only `createStemPack` (US5).

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T049 / T050 library | de4bfcb (16 of 17 tests failed at run time; the commit message wrongly says 22 of 26) | 1deb50e |

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true
  657 passed   StemStudio.Api line 99.5 %  branch 95.3 %   Contracts 100 / 100
Persistence 66 passed
```

`ContractAssert` checks:
- `listAssets`: 200, 422 for four kinds of invalid filter
- `renameAsset`: 200, 404, 422
- `deleteAsset`: 204, 404
- `retryAsset`: 202, 404, 409 ×2, 503

## Quality checks

- `dotnet format --verify-no-changes` is clean.
- `sig_check.py src/StemStudio.Api`: 87 files, 393 units, 0 errors, 0 warnings.

## Dependencies added

None.

## Next owner

backend (US4: T054/T055).

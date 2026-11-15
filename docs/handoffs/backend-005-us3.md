# Hand-off: backend / 005-us3 (T046-T047)

## What was built

- `PUT /api/tracks/{trackId}/artwork` (`setTrackArtwork`):
  - Unknown track -> 404. No multipart body or no `file` part -> 400.
  - `Artwork/ArtworkUploadReader` reads the `file` part in 80 KB chunks and stops at 10 MiB + 1 byte -> 413
    "Picture too large", before the worker is called. Exactly 10 MiB is accepted.
  - `Artwork/UserArtworkService` forwards the bytes with the part's file name and media type to the worker's
    `POST /v1/artwork`, which re-encodes them and strips EXIF/GPS.
  - The service then downloads `cover` and `thumb` of the normalised upload, stores them in `artwork/user/`, sets
    `Track.SetUserArtwork` and answers 200 with the track (`artwork.source = user`). The automatic picture is kept.
  - Worker 400/413/415/422 keep their status, use the contract titles ("Not an image", "Picture too large",
    "Unsupported picture type", "Picture too large") and carry the worker's detail. Any other 4xx -> 400.
  - Unreachable worker, normalised files that vanished, or an invalid worker hash -> 503.
- `DELETE /api/tracks/{trackId}/artwork` (`resetTrackArtwork`): goes back to `AutoArtwork` (or none), deletes
  `artwork/user/` and answers 200. Without an own picture the track is unchanged. Unknown track -> 404.
- `PendingArtworkOperations` is now **empty**. Every 005 operation is served, and
  `ServedOpenApiDocument_ListsExactlyTheContractOperations` passes.
- `UploadRequestReader.Boundary`/`FileName` became `internal` so the artwork reader can reuse them.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T046 / T047 own picture | 7634618 (15 of 17 tests failed at run time) | 4927e95 |

## Files changed

- `src/StemStudio.Api/Artwork/{ArtworkUploadReader,UserArtworkService}.cs` (new), `ArtworkEndpoints.cs`,
  `ApiComposition.cs`, `Importing/UploadRequestReader.cs`
- `tests/StemStudio.Api.Tests/Artwork/UserArtworkEndpointTests.cs` (new),
  `Contract/BackendContractTests.StemsAndMixes.cs`

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true
  574 passed   StemStudio.Api line 99.37 %  branch 97.09 %   StemStudio.Contracts 100 % / 100 %
```

`setTrackArtwork` (200, 400, 404, 413, 413/415/422 from the worker, 503) and `resetTrackArtwork` (200, 404) are
asserted with `ContractAssert`.

## Quality checks

- `dotnet format --verify-no-changes` is clean.
- `sig_check.py src/StemStudio.Api`: 67 files, 304 units, 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- The upload is buffered in memory (at most 10 MiB) so the limit can be enforced before the worker sees a byte, and
  so the request can be sent with a known length. Pictures are small, so no temp file is used.

## Next owner

backend (polish T053); frontend T048/T049.

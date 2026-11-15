# Hand-off: backend / us2

## What was built

- `GET /api/tracks/{trackId}/stems/{stemName}` (`getStem`) in `src/StemStudio.Api/Tracks/StemEndpoints.cs`:
  `StemService` serves the file with `TypedResults.PhysicalFile(..., "audio/flac", lastModified, entityTag,
  enableRangeProcessing: true)`, so ASP.NET Core provides `Accept-Ranges: bytes`, 206 + `Content-Range`, 416 +
  `Content-Range: bytes */<size>`, `If-Range`, and 304 for `If-None-Match` / `If-Modified-Since`, with `ETag`
  and `Last-Modified` on 200/206/304 (contract 1.0.1, I4). The ETag is `"<size hex>-<mtime ticks hex>"`, so it
  changes whenever a stem file is rewritten.
- 404 problem for an unknown track, an unknown or differently cased stem name, a stem the track does not have
  (e.g. `guitar` on a four-stem `htdemucs` track), a track that is not completed, or a missing file. The path is
  built from the track GUID and the validated stem name only (no traversal: `..%2F...` -> 404).
- CORS (policy `web` from US1): only `StemStudio:WebOrigin` gets `Access-Control-Allow-Origin`; `Content-Range`,
  `Accept-Ranges`, `ETag`, `Last-Modified` and `Content-Length` are exposed; the preflight allows the `Range`,
  `If-None-Match`, `If-Modified-Since` and `If-Range` request headers.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T091 / T092 stems | 114df9f | b1fce00 |

## Files changed

- `src/StemStudio.Api/Tracks/StemEndpoints.cs` (new), `src/StemStudio.Api/ApiComposition.cs`
- `tests/StemStudio.Api.Tests/Tracks/StemEndpointTests.cs` (17 tests)
- `specs/001-stemstudio-mvp/tasks.md` (T091, T092 ticked)

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: 178 passed, 0 failed. Api 99.71 % line /
97.72 % branch / 100 % method; Contracts 93.33 % line / 100 % branch. Domain (173 passed, 97.73/97.72) and
Persistence (21 passed, 99.02/100) unchanged since the US1 hand-off.

## Quality checks

`dotnet format StemStudio.slnx --verify-no-changes`: clean. Build: 0 warnings (checked in the following run of the
full gate).

## Dependencies added

None.

## Open issues / requests

None for this checkpoint. `getStem` contract checks (headers per status) are added with T134 in US4.

## Next owner

backend (self): US4 (T102, T103, T134). CTO: review.

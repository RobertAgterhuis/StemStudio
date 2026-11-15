# Hand-off: backend / 005-us2 (T037-T040)

## What was built

- **When a lookup is scheduled** (state none -> pending, once per track):
  - `JobReportApplier` schedules it when a standard poll reports `metadata.durationSeconds`. The query uses
    `lookupTitle`/`lookupArtist` (falling back to title/artist), `album`, `durationSeconds` and `isrc`, so the lookup
    runs while the separation is still going (SC-002).
  - `TrackJobApplier.Finish` schedules it at completion when no duration was reported earlier.
  - FR-012: `TrackService.ScheduleOwnLookupAsync` schedules it for a completed track that was never looked up. It runs
    on `GET /api/tracks/{id}` and `GET /api/tracks/{id}/band-info`.
- `Metadata/MetadataSynchronizer` runs as the fourth pass of the `JobSyncService` tick:
  - pending lookups get `POST /v1/metadata-jobs` and move to searching
  - 4xx -> failed with the worker detail
  - searching lookups are polled
  - an unreachable worker keeps the state; one broken track never stops the others
- `Metadata/MetadataJobApplier` handles each job state:
  - Completed and found:
    - upserts the `Artists` row. An existing artist is replaced only once it is 30 days old (`Artist.IsStale`).
    - offers the Cover Art Archive picture from `/v1/metadata-jobs/{id}/artwork/*` through `ArtworkDownloader`
      (precedence applies)
    - stores recording, album, year and artist name. Warnings are joined into `reason`.
  - Completed with not-found or disabled: facts and `ArtistId` are cleared.
  - Failed job -> failed with the worker's error. A forgotten job (404) -> failed with the default text. A rejected
    result -> failed.
  - Invalid artist facts -> failed with "The band information is invalid: ...".
  - After saving, an artist the track no longer references is deleted when unused (FR-016).
- `GET /api/tracks/{trackId}/band-info` (`getBandInfo`):
  - shows facts only when the status is found
  - `sources` lists:
    - MusicBrainz recording (CC0)
    - Wikidata (CC0)
    - the Wikipedia article (CC BY-SA 4.0)
    - Cover Art Archive release group (licence null), only when the shown picture came from it
- `POST /api/tracks/{trackId}/metadata/lookup` (`lookupTrackMetadata`):
  - responds 202 with the track (`Location` set)
  - no body, `{}` or null names retry with the current names; given names are stored with `correctedByUser` and drop
    the ISRC
  - 409 "Lookup in progress" while searching
  - 422 `UnprocessableRequest` (validation problem, status 422) for names that are empty or longer than 500
  - 404 for an unknown track
- `getBandInfo` and `lookupTrackMetadata` were removed from `PendingArtworkOperations`.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T037 / T038 lookup scheduling + synchroniser | c59d02b (build fails: synchroniser missing) | 5974646 |
| T039 / T040 band info + lookup endpoint | d6e4be2 (build fails: service/routes missing) | 6a97097 |

One test from c59d02b was replaced in 5974646: `AFreshStoredArtist_IsKeptAndAStaleOneIsRefreshed` advanced the
`FakeTimeProvider` by 31 days. That made the hosted service's 1 s `PeriodicTimer` fire about 2.6 million times, so the
test hung. The replacement theory `AStoredArtist_IsReplacedOnlyOnceItIs30DaysOld` (29 and 30 days) seeds the age
instead of advancing the clock.

## Files changed

- `src/StemStudio.Api/Metadata/{MetadataMapper,MetadataJobApplier,MetadataSynchronizer,MetadataService,BandInfoMapper,BandInfoEndpoints}.cs` (new)
- `src/StemStudio.Api/{ApiComposition,ApiProblems}.cs` (`ApiProblems.Unprocessable`), `Sync/{JobReportApplier,TrackJobApplier,JobSyncService}.cs`,
  `Tracks/TrackService.cs`
- `tests/StemStudio.Api.Tests/Metadata/{MetadataSyncTests,BandInfoEndpointTests}.cs` (new), `TestSupport/ApiFactory.cs`
  (`SyncMetadataAsync`), `Contract/BackendContractTests.StemsAndMixes.cs`

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true
  558 passed   StemStudio.Api line 99.18 %  branch 96.88 %   StemStudio.Contracts 100 % / 100 %
Domain 339 passed (98.78 / 97.04), Persistence 60 passed (97.81 / 95.83), unchanged
```

`ContractAssert` checks every new status against backend 1.5.0:
- `getBandInfo`: 200 found, 200 found without artist, 200 searching, 200 failed, 404
- `lookupTrackMetadata`: 202, 404, 409, 422 (four cases)

## Quality checks

- Build has 0 warnings. `dotnet format --verify-no-changes` is clean.
- `sig_check.py src/StemStudio.Api`: 65 files, 290 units, 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- A found track whose artist row is more than 30 days old is not re-looked up by itself. The facts are replaced the
  next time any track of that artist completes a lookup, or when the user presses retry. There is no bulk refresh job
  (spec: no bulk re-enrichment).
- **frontend**: `TrackDto.metadata.status` changes to `pending` on the first `GET` of an old completed track. Poll the
  track or the band info while the status is pending or searching.

## Next owner

backend (US3: T046/T047); frontend T041/T042.

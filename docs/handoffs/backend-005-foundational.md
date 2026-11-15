# Hand-off: backend / 005-foundational (T016-T021)

## What was built

- **Domain** (`src/StemStudio.Domain`)
  - `Tracks/ArtworkSource` (the enum order is the rank), `ArtworkCandidate`, `TrackArtwork` (validated SHA-256,
    `Version` = first 12 hex characters) and `ArtworkPrecedence` (data-model 3.1).
  - `Track.Artwork.cs`: `Artwork`, `AutoArtwork`, `AcceptsArtwork`, `OfferArtwork` (never `user`),
    `SetUserArtwork` and `ResetArtwork`. A user picture stays shown while automatic offers update `AutoArtwork`.
  - `Tracks/TrackMetadata.cs` (lookup snapshot, `MetadataStatus`, `LookupQuery`, `LookupResult`, `LookupOutcome`) and
    `Track.Metadata.cs`, which implements state machine 3.4:
    - `ScheduleLookup` (none -> pending, once) and `ScheduleOwnLookup` (FR-012)
    - `RetryLookup(title?, artist?)`: corrections are stored with `CorrectedByUser` and drop the ISRC. It is
      rejected while searching.
    - `StartLookup`, `CompleteLookup` (facts and `ArtistId` only when found), `FailLookup` (from pending or searching)
  - `Artists/{Artist, ArtistFacts, ArtistRules}`: value limits from data-model 2.5/2.6 and the backend `ArtistInfo`
    schema (genres <= 8 x 100, members <= 12, years 1000..2999, https links, `Q`-ids, summary fields), plus
    `IsStale` (30 days) and `Refresh`.
  - Refactor: the old title/artist input record `TrackMetadata` is now `SourceMetadata`, which frees the name for
    data-model 3.2.
- **Persistence**
  - Migration `20261115143937_AddArtworkAndArtists`:
    - adds `Tracks.Artwork` and `Tracks.AutoArtwork` (JSON text through `TrackArtworkJson`)
    - adds `Tracks.Metadata`, a complex JSON property. The status is stored as its name. The default `'{}'` loads as
      `TrackMetadata.None`, so 001-003 rows migrate.
    - adds `Tracks.ArtistId` with an FK to the new `Artists.Mbid`, `ON DELETE SET NULL`, and an index
  - The new `Artists` table stores genres, members and summary as JSON.
  - `IArtistRepository`/`ArtistRepository` (`FindAsync`, `Add`, `RemoveIfUnusedAsync`, `SaveAsync`).
  - `ITrackRepository.ListLookupActiveAsync` filters pending/searching in SQL with `json_extract`.
  - `RemoveWithMixesAsync` removes the artist in the same save when no other track references it (FR-016).
- **Api / worker client**
  - `WorkerJob.Artwork` and `WorkerSourceMetadata.{Album, Isrc, DurationSeconds, LookupTitle, LookupArtist}`. These
    are optional, so 1.2.0 bodies parse to null.
  - Metadata jobs: `CreateMetadataJobAsync`, `GetMetadataJobAsync` (404 -> null), `GetMetadataResultAsync`.
  - `GetArtworkAsync(WorkerArtworkRef)` covers jobs, metadata jobs and normalised uploads. It returns JPEG bytes and
    404 -> null. The body is capped at 1 MiB, and the variant, owner and id are validated before any request.
  - `NormalizeArtworkAsync` streams the part. Worker 400/413/415/422 keep their status and detail as
    `WorkerRequestException`.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| (refactor) `TrackMetadata` -> `SourceMetadata` | - | a49f46f |
| T016 / T017 domain | a31ba4d (build fails: types missing) | 8a9a3cd |
| T018 / T019 persistence + migration | 9a5dfa6 (build fails: repository/migration missing) | cb42f5b |
| T020 / T021 worker client | b5382fe (build fails: members missing) | 255e929 |
| SIG refactor | - | bc94bad |

## Files changed

- `src/StemStudio.Domain/Tracks/{ArtworkSource,TrackArtwork,ArtworkPrecedence,Track.Artwork,TrackMetadata,Track.Metadata}.cs`
  (new), `Track.cs`, `TrackInputs.cs`; `src/StemStudio.Domain/Artists/{Artist,ArtistFacts,ArtistRules}.cs` (new)
- `src/StemStudio.Persistence/Configurations/{TrackArtworkJson,ArtistConfiguration}.cs` (new), `TrackConfiguration.cs`,
  `StemStudioDbContext.cs`, `Repositories/{ArtistRepository (new),ITrackRepository,TrackRepository}.cs`,
  `PersistenceServiceCollectionExtensions.cs`, `Migrations/20261115143937_AddArtworkAndArtists*.cs`, snapshot
- `src/StemStudio.Api/Worker/{WorkerArtwork,WorkerMetadataJob,WorkerClient.Metadata}.cs` (new), `IWorkerClient.cs`,
  `WorkerJob.cs`, `Sync/TrackJobApplier.cs` (rename only)
- Tests: `tests/StemStudio.Domain.Tests/{Tracks/ArtworkPrecedenceTests,Tracks/TrackMetadataTests,Artists/ArtistTests}.cs`,
  `tests/StemStudio.Persistence.Tests/ArtworkPersistenceTests.cs`, `tests/StemStudio.Api.Tests/Worker/WorkerMetadataClientTests.cs`,
  `TestSupport/FakeWorkerClient.cs` (new members)

## Tests and coverage

`dotnet test tests/StemStudio.<X>.Tests -p:CollectCoverage=true` (gates on):

```text
StemStudio.Domain.Tests       339 passed   Domain        line 98.78 %  branch 97.04 %
StemStudio.Persistence.Tests   60 passed   Persistence   line 97.81 %  branch 95.83 %
StemStudio.Api.Tests          506 passed   Api           line 99.66 %  branch 98.66 %
                                           Contracts     line 100 %    branch 100 %
```

`dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."

## Quality checks

- `dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` clean.
- `python tools/quality/sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 117 files, 422 units, **0 errors, 0
  warnings** after bc94bad. Including the test projects there are 0 errors and only unit-size warnings in test
  methods.

## Dependencies added

None.

## Open issues / requests

- None blocking. `Artist` refresh rule: an existing artist is replaced by a newer lookup only once it is 30 days old
  (applied in 005 US2).

## Next owner

backend (US1: T026/T027).

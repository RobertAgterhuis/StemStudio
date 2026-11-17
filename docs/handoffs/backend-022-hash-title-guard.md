# Backend hand-off: 022 hash-title guard

## Problem

Before commit 404c8031 the worker's report of an untagged upload put the stored, hashed upload file name
(`<sha256><ext>`, e.g. `28c19d35...`) into `TrackMetadata.LookupTitle`. 404c8031 fixed new uploads only. Feature 022
files songs by `LookupTitle` before `Track.Title`, so tracks already in an existing library would still get a
hash-named song folder when the 022 backfill files them on the first start after the upgrade.

## Decision (orchestrator)

No data migration of `LookupTitle`. A guard where 022 picks the song name instead: a lookup title is treated as absent
(and the next candidate, the track's own title, is used) when it is

- the track's stored upload file name or its stem (case-insensitive), or
- a pure hex hash of 32-64 characters, optionally followed by an extension (`\A[0-9a-fA-F]{32,64}(\.[A-Za-z0-9]{1,10})?\z`).

Shorter hex strings (e.g. `deadbeef`, `cafe`) are real titles and kept.

## Implementation

- `src/StemStudio.Domain/Tracks/StoredUploadName.cs`: pure `IsStoredName(title, uploadFileName)`.
- `src/StemStudio.Domain/Library/PlacementInput.cs`: `TrackFacts` gets an optional `UploadFileName` (filled by
  `TrackFacts.From` from `Track.UploadFileName`, which stays set after completion) and a `LookupTitle` property that
  applies the guard.
- `src/StemStudio.Domain/Library/PlacementResolver.cs`: the song level uses `track.LookupTitle`.

Every 022 path goes through `PlacementResolver` with `TrackFacts.From`: `LibraryFiler` (new items and the
`LibraryFilingBackfill` start-up/on-demand run) and `LibraryInfoService`. The stored `LookupTitle` itself is unchanged.
`LibraryFilingRules.Version` is not bumped: on an upgrade from before 022 every item is still unfiled, so the backfill
already applies the guard.

## Tests

- `tests/StemStudio.Domain.Tests/Tracks/StoredUploadNameTests.cs`: hash (with/without extension, upper case, padded)
  ignored; stored stem/name ignored; normal title, short hex title (29 chars and less) and hash plus text kept; empty.
- `tests/StemStudio.Domain.Tests/Library/PlacementResolverTests.cs`: hashed lookup title and stored-stem lookup title
  fall back to the track title; real titles (`Enter Sandman`, `deadbeef`) kept; `TrackFacts.From` carries the name.
- `tests/StemStudio.Api.Tests/Library/LibraryFilingBackfillTests.cs`: a seeded pre-022 upload track with a hashed lookup
  title and an unfiled item is filed under the track's own title by the backfill.

## Open items

- `MetadataMapper.ToRequest` (band info lookup retry) and `LyricsMapper` still send a stored hashed `LookupTitle` of an
  old track to the worker. Out of this task's scope (folder naming only); the same guard can be applied there if wanted.
- Libraries that already ran a pre-release 022 build keep their hash-named folders until `LibraryFilingRules.Version`
  is bumped (not needed for the owner's upgrade from before 022).

No new dependencies.

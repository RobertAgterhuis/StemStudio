# Contract deltas: feature 005 (Song Artwork and Band Info)

## Phase 0 outcome (architect, 2026-11-15; combined with 007)

Versions assigned (docs/ROADMAP.md): **worker-api 1.3.0**, **backend-api 1.4.0**, **ui-test-ids
1.4.0** (on 004's 1.3.0); full documents next to this file, Redocly 2.78.0 clean. 005 and 007 share
one skeleton: the code and the test links are at the 007 versions (worker 1.4.0, backend 1.5.0),
which contain these documents unchanged (tested in `ArtworkContractTests` and
`worker/tests/test_metadata_contract.py`). Refinements against the delta below:

1. `MetadataLookup.lookup(query, progress)` returns `MetadataFindings(result, artwork)`: the
   contract `MetadataResult` plus the normalised Cover Art Archive JPEGs the job must store.
2. Settings are grouped: `WorkerSettings.online.{lookups_enabled, contact_url,
   wikipedia_languages, fake_mode}` (instead of four flat fields).
3. `POST /v1/metadata-jobs` 422 is problem+json (`ValidationFailed`, with an `errors` map):
   stem-separation installs a `RequestValidationError` handler (FastAPI's default body differs).
4. `Health.onlineLookups` is required on the wire (Python default `true`).
5. Track artwork: `ETag` = quoted artwork version; `304` with `If-None-Match`; `PUT` also answers
   `404` for an unknown track. `Track.metadata` is required on the wire (default `none`),
   `Track.artwork` optional/nullable like `device`.
6. Band info: `DataSource.name` is an enum (MusicBrainz, Wikidata, Wikipedia, Cover Art
   Archive), `licence` is null for Cover Art Archive; backend `ArtistInfo` carries `mbid`, not
   `wikidataId`. Request DTO name `MetadataLookupRequest` (plan.md said `CorrectMetadataRequest`).
7. Help topic ids are `artwork`, `band-info`, `online-lookups` (prefix `help-` dropped, 004 area
   naming); 422 of the backend uses the new response `UnprocessableRequest`.
8. Backend test staging list is named `PendingArtworkOperations`.


Status: **delta only**. Final version numbers are assigned when 005 starts (after 003 and 004
are merged): every contract below gets the next **minor** version over the version current at
that time (all changes are additive). In Phase 0 the architect writes the full documents into
`specs/005-artwork-band-info/contracts/` (worker-api.openapi.yaml, backend-api.openapi.yaml,
ui-test-ids.md, redocly.yaml), Redocly-lint clean (OpenAPI 3.1). Baselines at the time of
writing: worker 1.2.0, backend 1.3.0, UI test ids 1.2.0 (all 003), engine 2.0.0 (002, unchanged
by 005).

## 1. Worker API (baseline 1.2.0 -> next minor)

### 1.1 Changed schemas (additive)

```yaml
SourceMetadata:            # + album, isrc, durationSeconds, lookupTitle, lookupArtist (all nullable)
  album:          { type: [string, "null"], maxLength: 500 }
  isrc:           { type: [string, "null"], pattern: "^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$" }
  durationSeconds:{ type: [number, "null"], exclusiveMinimum: 0, maximum: 900 }
  lookupTitle:    { type: [string, "null"], maxLength: 500 }
  lookupArtist:   { type: [string, "null"], maxLength: 500 }
Job:
  artwork: { oneOf: [ { $ref: ArtworkInfo }, { type: "null" } ] }   # set after the download step
ArtworkInfo:
  required: [source, sha256, coverBytes, thumbBytes]
  source:     { enum: [spotify, embedded, youtube, coverartarchive, user] }
  sha256:     { type: string, pattern: "^[0-9a-f]{64}$" }
  coverBytes: { type: integer, minimum: 1, maximum: 204800 }
  thumbBytes: { type: integer, minimum: 1, maximum: 20480 }
  originUrl:  { type: [string, "null"], format: uri }
Health:
  onlineLookups: { type: boolean }   # false when STEMSTUDIO_ONLINE_LOOKUPS=off
```

`JobState` and the standard pipeline stages gain the stage text "Saving cover" (between
"Downloading audio" and "Converting audio"); its progress weight is 0 (progress unchanged).

### 1.2 New paths

| Method + path | operationId | Body / response | Errors |
|---|---|---|---|
| `GET /v1/jobs/{jobId}/artwork/{variant}` (`cover`\|`thumb`) | `getJobArtwork` | `image/jpeg`, `ETag` = sha256 | 404 job or no artwork |
| `POST /v1/metadata-jobs` | `createMetadataJob` | `CreateMetadataJobRequest {title, artist?, album?, durationSeconds?, isrc?, standardJobId?}` -> 202 `MetadataJob` | 422 validation |
| `GET /v1/metadata-jobs/{metadataJobId}` | `getMetadataJob` | `MetadataJob` | 404 |
| `GET /v1/metadata-jobs/{metadataJobId}/result` | `getMetadataResult` | `MetadataResult` | 404, 409 not completed |
| `GET /v1/metadata-jobs/{metadataJobId}/artwork/{variant}` | `getMetadataArtwork` | `image/jpeg` | 404 |
| `POST /v1/artwork` | `normalizeArtwork` | multipart `file` (<= 10 MB, JPEG/PNG/WebP) -> 201 `ArtworkUpload {artworkId, info: ArtworkInfo}` (source `user`) | 400 not an image, 413 too large, 415 type, 422 > 40 MP |
| `GET /v1/artwork/{artworkId}/{variant}` | `getNormalizedArtwork` | `image/jpeg` (kept 1 h) | 404 |

Schemas `MetadataJob`, `MetadataJobState (queued|running|completed|failed)`, `MetadataResult`,
`ArtistInfo`, `ArtistType`, `ArtistMember`, `WikipediaSummary`, `RecordingMatch`,
`ReleaseGroupInfo` as in data-model.md 2.3-2.6.

### 1.3 Python contract code (Phase 0, architect)

`schemas.py`: models above, `CONTRACT_VERSION` bump. `ports.py`: `ArtworkCandidate`
(`url: str | None`, `data: bytes | None`, `source`), `ArtworkImages` (cover/thumb bytes + sha),
`ImageNormalizer` protocol (`normalize(candidate) -> ArtworkImages`), `MetadataQuery`,
`MetadataLookup` protocol (`lookup(query, progress) -> MetadataResult`), `SourceInfo.album/isrc/
artwork`, `FetchedAudio.artwork/album/track`. `config.py`: section 1 variables of data-model.md.

## 2. Backend API (baseline 1.3.0 -> next minor)

### 2.1 Changed schemas (additive)

```yaml
Track:
  artwork:  { oneOf: [ { $ref: Artwork }, { type: "null" } ] }
  metadata: { $ref: TrackMetadata }            # always present; status none for old tracks
Artwork:
  required: [source, version, coverUrl, thumbUrl]
  source:   { enum: [user, spotify, embedded, coverartarchive, youtube] }
  version:  { type: string, pattern: "^[0-9a-f]{12}$" }
  coverUrl: { type: string }   # /api/tracks/{id}/artwork/cover?v={version}
  thumbUrl: { type: string }   # /api/tracks/{id}/artwork/thumb?v={version}
TrackMetadata:
  required: [status, correctedByUser]
  status: { enum: [none, pending, searching, found, notFound, failed, disabled] }
  reason, album, artistName: nullable strings; year: nullable integer; correctedByUser: boolean
```

### 2.2 New paths

| Method + path | operationId | Body / response | Errors |
|---|---|---|---|
| `GET /api/tracks/{trackId}/artwork/{variant}` | `getTrackArtwork` | `image/jpeg`; `ETag`; `Cache-Control: private, max-age=31536000, immutable` when `v` matches, else `no-cache` | 404 track/no artwork |
| `PUT /api/tracks/{trackId}/artwork` | `setTrackArtwork` | multipart `file` -> 200 `Track` | 400/413/415/422 as worker (problem details), 503 worker unavailable |
| `DELETE /api/tracks/{trackId}/artwork` | `resetTrackArtwork` | 200 `Track` (back to automatic) | 404 |
| `GET /api/tracks/{trackId}/band-info` | `getBandInfo` | `BandInfo {status, reason, recording?, artist?, sources[]}` | 404 track |
| `POST /api/tracks/{trackId}/metadata/lookup` | `lookupTrackMetadata` | `MetadataLookupRequest {title?, artist?}` (1..500 each; empty body = retry with the current names) -> 202 `Track` | 404, 409 while `searching`, 422 |

`BandInfo.sources` lists `{name, url, licence}` for MusicBrainz (CC0), Wikidata (CC0), Wikipedia
(CC BY-SA 4.0, with the article URL), Cover Art Archive (when the picture came from it).

### 2.3 .NET contract code (Phase 0, architect)

`StemStudio.Contracts`: `Tracks/{ArtworkDto, ArtworkSourceDto, TrackMetadataDto,
MetadataStatusDto, MetadataLookupRequest}.cs`, `TrackDto.Artwork`, `TrackDto.Metadata`,
`Artists/{BandInfoDto, ArtistInfoDto, ArtistMemberDto, WikipediaSummaryDto, DataSourceDto,
RecordingInfoDto}.cs`, `ApiRoutes.{TrackArtwork, BandInfo, MetadataLookup}`, `ContractLimits`
(`MaxArtworkUploadBytes = 10 MiB`).

## 3. Audio engine

No change.

## 4. UI test ids (baseline 1.2.0 -> next minor; ids may move into 004's layout, names stay)

| Screen | `data-testid` |
|---|---|
| Track list (`/tracks`) | `track-thumb-<trackId>` (the `img` or the placeholder element; attribute `data-artwork-source` = `user`/`spotify`/`embedded`/`coverartarchive`/`youtube`/`none`) |
| Track detail (`/tracks/{id}`) | `track-cover` (same attribute), `metadata-status` (attribute `data-status` = metadata status), `band-info-open` |
| Mixer (`/tracks/{id}/mixer`) | `mixer-cover` (same attribute), `mixer-album` (text "<album> (<year>)", only when found), `band-info-open` |
| Band info panel (both screens) | `band-info-panel`, `band-info-status` (`data-status`), `band-info-name`, `band-info-type`, `band-info-country`, `band-info-years`, `band-info-genres`, `band-info-members` (only for groups with members), `band-info-website`, `band-info-summary`, `band-info-attribution` (contains the three links), `band-info-sources`, `band-info-retry` (failed/notFound), `band-info-correct` (opens the dialog), `metadata-correct-title`, `metadata-correct-artist`, `metadata-correct-submit`, `band-info-disabled` (lookups off) |
| Artwork actions (track detail, P3) | `artwork-upload-input`, `artwork-reset`, `artwork-error` |
| Help topics (004 framework) | `artwork`, `band-info`, `online-lookups` (Phase 0: `help-` prefix dropped; topic ids; anchors `data-help-id` on the cover, the panel and the import page notice) |

The import page legal notice (`legal-notice`) text gains the sentence on online lookups
(pictures, band information; 006: lyrics).

## 5. Configuration (devops, docker-compose)

Worker env: `STEMSTUDIO_ONLINE_LOOKUPS` (default `on`), `STEMSTUDIO_CONTACT_URL` (unset),
`STEMSTUDIO_WIKIPEDIA_LANGUAGES` (`en`), `STEMSTUDIO_FAKE_ONLINE` (`found`, fake stack only).
`scripts/test-all.*` and CI `CONTRACTS_DIR` -> `specs/005-artwork-band-info/contracts`.

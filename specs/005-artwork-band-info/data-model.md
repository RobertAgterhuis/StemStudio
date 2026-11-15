# Data Model: Song Artwork and Band Info

Builds on the 001-003 data models. Contract changes: [contracts/DELTA.md](contracts/DELTA.md).
Design: ADR-014. Field names on the wire are camelCase; Python uses snake_case with aliases (as
in `schemas.py`).

## 1. Configuration (worker environment)

| Variable | Values | Default | Meaning |
|---|---|---|---|
| `STEMSTUDIO_ONLINE_LOOKUPS` | `on`, `off` | `on` | `off`: no request to MusicBrainz, Cover Art Archive, Wikidata, Wikipedia (and LRCLIB in 006); metadata jobs complete at once with status `disabled`. Source artwork from the import (YouTube/Spotify/upload) is unaffected because it comes with the download. |
| `STEMSTUDIO_CONTACT_URL` | https URL | unset | Contact part of the User-Agent. Validated (research R-07); invalid -> warning without the value, generic token used. Never an e-mail. |
| `STEMSTUDIO_WIKIPEDIA_LANGUAGES` | comma list of 2-3 letter codes | `en` | Order in which Wikipedia sitelinks are tried, max 5 |
| `STEMSTUDIO_FAKE_ONLINE` | `found`, `not-found`, `failed`, `off` | `found` | Only with `STEMSTUDIO_SEPARATOR=fake`: deterministic fake metadata provider and fake source artwork (generated solid-colour images), no network. `failed` simulates three 503s. |

The backend gets no new variable; it reads the status from the worker.

User-Agent: `StemStudio/<worker version> ( <STEMSTUDIO_CONTACT_URL or "local-practice-tool; contact not configured"> )`.

## 2. Worker entities

### 2.1 SourceMetadata (extended, standard job `metadata`)

| Field | Type | Rule |
|---|---|---|
| `title`, `artist`, `sourceUrl` | as 001 | unchanged |
| `album` | string \| null (<= 500) | Spotify `album_name`, upload tag album, yt-dlp `album`; null when unknown |
| `isrc` | string \| null, `^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$` | Spotify only |
| `durationSeconds` | number \| null (> 0, <= 900) | set after normalisation; null before |
| `lookupTitle`, `lookupArtist` | string \| null | cleaned names for the lookup (research R-04); upload: tag values, else the title/artist above |

### 2.2 ArtworkInfo (standard job `artwork`, metadata job result `artwork`)

| Field | Type | Rule |
|---|---|---|
| `source` | `spotify` \| `embedded` \| `youtube` \| `coverartarchive` \| `user` | where the picture came from |
| `sha256` | 64 lower hex | of the `cover` JPEG bytes; the artwork version |
| `coverBytes`, `thumbBytes` | int | sizes of the two JPEGs (cover <= 200 KB, thumb <= 20 KB) |
| `originUrl` | string \| null | the remote image URL (null for embedded/user); not shown to the user |

Files: worker job dir `artwork/{cover,thumb}.jpg`, served by `GET /v1/jobs/{jobId}/artwork/{variant}`
and `GET /v1/metadata-jobs/{id}/artwork/{variant}`. `Job.artwork` is null until captured and
stays null when no source picture exists or capture failed (logged).

### 2.3 MetadataJob (`POST /v1/metadata-jobs`, `GET /v1/metadata-jobs/{id}`)

Request: `title` (1..500), `artist` (nullable), `album` (nullable), `durationSeconds` (nullable),
`isrc` (nullable), `standardJobId` (nullable; only for logging/correlation).

| Field | Type | Rule |
|---|---|---|
| `metadataJobId` | 32 lower hex | |
| `status` | `queued` \| `running` \| `completed` \| `failed` | worker job states (no cancel) |
| `stage` | string (<= 200) | "Searching MusicBrainz", "Fetching album cover", "Reading Wikidata", "Reading Wikipedia" |
| `error` | string \| null (<= 1000) | readable; fixed texts below |
| `createdAt`, `updatedAt` | date-time | |

State machine: `queued -> running -> completed | failed`. Worker restart: `queued`/`running` jobs
are re-run from the start (idempotent; no partial state).

### 2.4 MetadataResult (`GET /v1/metadata-jobs/{id}/result`)

| Field | Type | Rule |
|---|---|---|
| `outcome` | `found` \| `not-found` \| `disabled` | `disabled` when lookups are off |
| `recording` | object \| null | `mbid`, `title`, `lengthSeconds`, `score` (0..100), `matchedBy` (`isrc` \| `search`) |
| `releaseGroup` | object \| null | `mbid`, `title`, `primaryType`, `firstReleaseYear` (int \| null) |
| `artwork` | ArtworkInfo \| null | Cover Art Archive front of the release group (`source=coverartarchive`) |
| `artist` | ArtistInfo \| null | 2.5 |
| `warnings` | string[] (<= 5) | non-fatal step failures, e.g. "Wikipedia could not be reached." |

A failure after the recording was found (e.g. Wikipedia down) still completes with the parts
obtained plus a warning; only a failure of the recording search itself fails the job.

### 2.5 ArtistInfo

| Field | Type | Source | Rule |
|---|---|---|---|
| `mbid` | uuid | MusicBrainz | key |
| `name` | string (<= 500) | MusicBrainz | |
| `type` | `group` \| `person` \| `orchestra` \| `choir` \| `character` \| `other` \| null | MusicBrainz | |
| `country` | ISO 3166-1 alpha-2 \| null | MusicBrainz `country` | |
| `beginYear`, `endYear` | int \| null | MusicBrainz `life-span` (Wikidata P571/P576 when missing) | `endYear` null while active |
| `genres` | string[] (<= 8) | Wikidata P136 labels | CC0 |
| `members` | `{name, fromYear?, toYear?}`[] (<= 12) | Wikidata P527 labels + qualifiers P580/P582 | groups only |
| `website` | https URL \| null | MusicBrainz `official homepage`, else Wikidata P856 | |
| `wikidataId` | `Q[0-9]+` \| null | MusicBrainz `wikidata` rel | |
| `summary` | WikipediaSummary \| null | 2.6 | |
| `fetchedAt` | date-time | | refresh after 30 days |

### 2.6 WikipediaSummary (attribution data, CC BY-SA 4.0)

| Field | Type | Rule |
|---|---|---|
| `language` | `en`, `nl`, ... | the wiki used |
| `title` | string | article title |
| `extract` | string (<= 2000) | API `extract`, unmodified; dropped when `type != standard` or empty |
| `pageUrl` | https URL | `content_urls.desktop.page` |
| `historyUrl` | https URL | `https://<lang>.wikipedia.org/w/index.php?title=<title>&action=history` ("Wikipedia contributors") |
| `revisionId` | string | API `revision` |
| `licence` | const `CC BY-SA 4.0` | |
| `licenceUrl` | const `https://creativecommons.org/licenses/by-sa/4.0/` | |

### 2.7 Fixed worker texts

| Situation | Text |
|---|---|
| Recording search failed after retries | "Band information could not be retrieved; try again later." |
| Lookups disabled | (no error; outcome `disabled`) |
| Wikipedia step failed | warning "Wikipedia could not be reached." |
| Cover download/decoding failed | warning "The album cover could not be loaded." |
| Wikidata step failed | warning "Wikidata could not be reached." |
| MusicBrainz artist step failed | warning "The artist details could not be loaded." |

## 3. Backend entities

### 3.1 Track (extended)

| Field | Type | Rule |
|---|---|---|
| `Artwork` | TrackArtwork \| null (owned, JSON column) | the picture currently shown |
| `AutoArtwork` | TrackArtwork \| null (JSON) | best automatic picture (kept while a user picture is set, for "Use automatic picture") |
| `Metadata` | TrackMetadata (JSON, default `status = none`) | lookup state |
| `ArtistId` | uuid \| null (FK `Artists.Mbid`, `ON DELETE SET NULL`) | |

`TrackArtwork`: `Source` (enum as 2.2), `Sha256` (64 hex), `UpdatedAt`. Files:
`data/tracks/<trackId>/artwork/{auto,user}/{cover,thumb}.jpg` (atomic writes; the shown variant
is chosen by `Artwork.Source`).

Precedence rule (domain method `Track.OfferArtwork(candidate)`): rank `user` 0, `spotify` 1,
`embedded` 2, `coverartarchive` 3, `youtube` 4. A candidate replaces `AutoArtwork` when its rank
is lower (better) or equal with a different hash. `Artwork = user ?? AutoArtwork`.

### 3.2 TrackMetadata (owned JSON)

| Field | Type | Rule |
|---|---|---|
| `Status` | `none` \| `pending` \| `searching` \| `found` \| `notFound` \| `failed` \| `disabled` | state machine 3.4 |
| `Reason` | string \| null (<= 1000) | worker error or warning texts |
| `MetadataJobId` | 32 hex \| null | current worker job |
| `LookupTitle`, `LookupArtist` | string \| null | names used; user corrections overwrite them |
| `CorrectedByUser` | bool | |
| `RecordingMbid`, `ReleaseGroupMbid` | uuid \| null | |
| `Album` | string \| null | |
| `Year` | int \| null | |
| `UpdatedAt` | date-time | |

### 3.3 Artist (table `Artists`)

Columns as ArtistInfo 2.5 (`Mbid` primary key; `Genres`, `Members`, `Summary` as JSON). Deleted
when no track references it (in the same transaction as the track deletion).

### 3.4 Metadata status transitions (backend)

```text
none ──(standard job reports metadata.durationSeconds, or first open of an old track)──> pending
pending ──(metadata job created)──> searching
searching ──(result outcome found)──> found          (artist upserted, CAA artwork offered)
searching ──(result outcome not-found)──> notFound
searching ──(result outcome disabled)──> disabled
searching ──(worker job failed | worker 404 after restart)──> failed
failed | notFound | found | disabled ──(retry / corrected names)──> pending
```

The `JobSyncService` drives `pending`/`searching` exactly like the detail phase (polling, worker
unreachable keeps the state). Source artwork is offered from the standard job poll as soon as
`Job.artwork` is set.

## 4. Backend DTOs (wire, see contracts/DELTA.md)

- `TrackDto.artwork`: `ArtworkDto` \| null = `{source, version (12 hex), coverUrl, thumbUrl}` with
  URLs `/api/tracks/{id}/artwork/cover?v=<version>` and `/thumb?v=<version>`.
- `TrackDto.metadata`: `{status, reason, album, year, artistName, correctedByUser}` (always
  present; `status none` for old tracks).
- `BandInfoDto` (`GET /api/tracks/{id}/band-info`): `{status, reason, recording {title, album,
  year}, artist ArtistInfo-like, sources [{name, url, licence}]}`.

## 5. Storage layout

```text
data/tracks/<trackId>/artwork/auto/{cover,thumb}.jpg
data/tracks/<trackId>/artwork/user/{cover,thumb}.jpg
<worker data>/jobs/<jobId>/artwork/{cover,thumb}.jpg
<worker data>/metadata-jobs/<id>.json, <id>/result.json, <id>/artwork/{cover,thumb}.jpg   # swept after 24 h
```

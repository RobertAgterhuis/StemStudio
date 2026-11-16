# Data model and migrations

The API is the system of record. It stores structured data in SQLite
(`<DataDir>/stemstudio.db`) through EF Core and audio files next to it. Migrations are applied
automatically at start-up.

## Aggregates

**Track**: id, source type (YouTube, Spotify, upload), unique source key (`youtube:<id>`,
`spotify:<id>` or `upload:<sha256>`), source URL, title, artist, status, stage, progress,
error, worker job id, upload file name, duration (≤ 900 s), model, stems (name and size),
beat grid (BPM, beats, downbeats, beats per bar 1 to 12, confidence, reliable), detail phase
(status, stage, current parent, progress, error, worker job id, parent outcomes with detail
stems, device), device (kind, name, CPU fallback and reason), timestamps.

Status moves forward only: queued → downloading → separating → analysing → completed, or
failed. Retry is allowed only from failed.

**Mix**: track id, name (1 to 100 characters, unique per track ignoring case), master volume,
channels (stem, volume 0 to 1, default 0.8, pan −1 to 1, mute, solo), metronome (enabled,
volume, BPM override 30 to 300, offset −2000 to 2000 ms, beats per bar), loop (start, end,
enabled), expanded parents and detail channels.

**Track artwork and metadata (005)**: the chosen picture (source `user`, `spotify`,
`embedded`, `coverartarchive` or `youtube`, version) and the best automatic one, the lookup
result (status `none`, `pending`, `searching`, `found`, `notFound`, `failed`, `disabled`,
recording, release group, warnings, `correctedByUser`) and a link to an **Artist** (MBID,
facts and the Wikipedia summary with its attribution, shared by all tracks of the artist and
refreshed after 30 days).

**Asset (007)**: a library item: kind (audio), name (1 to 120 characters, unique with
" (2)", " (3)" suffixes), status (`queued`, `rendering`, `ready`, `failed`), progress, error,
worker job id, audio facts (format, bit depth or bit rate, duration, `appliedGainDb`),
provenance (track id, title and artist, contents, render recipe) and its files. Items keep
their provenance when the track is deleted; the API then reports `trackAvailable: false`.

**Agent (011)**: one settings row; **conversations** with their messages (role, text, page
context, run); **runs** (`queued` → `running` ⇄ `awaiting_approval` / `awaiting_ui` /
`waiting_job` → `completed`, `failed`, `cancelled` or `interrupted`; at most one active run;
API start-up marks unfinished runs `interrupted` and expires their approvals); **steps** with
the tool call, its risk, the policy decision and its result; **approvals** (`pending`,
`approved`, `rejected`, `expired`); **undo entries** with the inverse calls of a command group.
Since 001 changed in 011: an upload's title is its original file name, no longer the stored
hash (existing tracks are not migrated).

## Migrations

| Migration | Feature | Change |
|---|---|---|
| `20261114215330_Initial` | 001 | Tracks and Mixes |
| `20261115091546_AddDetailPhase` | 002 | Track detail phase |
| `20261115091608_AddMixDetailSettings` | 002 | Mix expanded groups and detail channels |
| `20261115115115_AddJobDevice` | 003 | Processing device per track and detail phase |
| `20261115143937_AddArtworkAndArtists` | 005 | `Tracks.ArtistId` (FK, indexed), `Artwork`, `AutoArtwork`, `Metadata` (JSON); table `Artists` (MBID key, name, type, country, years, website, Wikidata id, genres, members, summary, `FetchedAt` for the 30-day refresh) |
| `20261115152313_AddAssets` | 007 | Table `Assets` (export library: kind, name, status, progress, error, worker job, audio facts, provenance, nullable `TrackId`, size, files; indexes on `CreatedAt`, `Status`, `TrackId`) |
| `20261115163349_AddTrackLyrics` | 006 | `TrackLyrics` table (document, state, offset, alignment job and estimate), `Tracks.LyricsStatus` |
| `20261115171827_AddScoreFacts` | 008 | `Assets.Score` (profile, quality, key, grid, warnings, page count) |
| `20261115190101_AddGenerativeStyles` | 009 | `Styles`, `Generations`, `GenerationTakes`, `Tracks.Origin` |
| `20261115200748_AddLyricsAndVoices` | 010 | `LyricsSheets`, `VoiceProfiles`, `AudioHashes` |
| `20261116064402_AddAgent` | 011 | `AgentSettings` (one row: backend, models, Claude model and effort, limits, notice acknowledgement; never the key), `Conversations`, `ConversationMessages`, `AgentRuns` (status, backend, model, taint, counters, usage), `RunSteps` (tool, arguments, risk, decision and reasons, status, result), `Approvals` (bound to the arguments hash, 24 h expiry), `RunEvents` (SSE replay), `UndoEntries` (inverse calls, `ExpectedUpdatedAt`). Unreleased id kept after the T080 regeneration: development databases that applied an earlier 011 `AddAgent` must be recreated |

## File layout

API data folder (volume `api-data`):

```
stemstudio.db
uploads/<sha256><ext>
tracks/<trackId>/stems/<stem>.flac
tracks/<trackId>/detail/<parent>/<name>.flac
tracks/<trackId>/artwork/{auto,user}/{cover,thumb}.jpg     (005)
assets/<assetId>/audio.<ext> | score.{pdf,musicxml,mid}   (007, 008)
generations/<id>/take-<n>.flac                             (009)
voices/<id>/sample.flac                                    (010)
```

Worker data folder (volume `worker-data`): `jobs/<jobId>.json`, `detail-jobs/<id>.json`,
`work/<jobId>/`, `uploads/<jobId>/`, the result cache `cache/by-source/` and
`cache/by-audio/<sha>/` and detail checkpoints. On restart, non-terminal jobs are marked
failed (*interrupted by restart*); detail retries reuse checkpoints. Models live in the
`model-cache` volume.

Stems are FLAC, 44.1 kHz, stereo, 16-bit (ADR-001).

## Browser storage

User preferences are not in the database. They live in the browser under the
`localStorage` key `stemstudio.preferences` (schema version 1: level, theme, density,
motion, help state). From 006 the karaoke banner adds `stemstudio.karaoke.visible` and
`stemstudio.karaoke.size`.

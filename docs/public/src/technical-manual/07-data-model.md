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
enabled), expanded parents and detail channels; from 012 the practice tempo (speed 50 to
120 %, transpose −12 to +12, speed trainer start/step/target/passes, count-in bars 0 to 2 and
before every pass), stored as nullable JSON; `NULL` reads as the defaults. From 014 the loop
carries `sectionId` (inside the settings JSON, no column; an id that no longer exists reads as
`null`), and from 015 the take channel `TakeChannel` (nullable JSON: take id, volume 0 to 1,
pan −1 to 1, mute, solo; a take that no longer exists is ignored on load; duplicates copy it).

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

**Library placement (022)**: every library item stores where it is filed, as plain columns on
`Assets` (EF 10 complex types): the placement `FolderArtist`, `FolderAlbum`, `FolderSong` (display
name, cut to 200 characters with control characters removed) with their normalised keys
(`Folder*Key`), where each name came from (`Folder*Source`: item, track, match, import, fallback)
and `FolderYear`; the item's own names `LibraryArtist`, `LibraryAlbum`, `LibrarySong` (user library
info, 1 to 200 characters or `NULL` = automatic); `FiledVersion` (the version of the filing rules
that produced the placement; 0 means "not filed yet", and a version below the current rules is filed again) and
`FiledAt`. `Tracks` gains the same three `Library*` columns for the user's names for a whole track.
A shadow column `Placement_Discriminator` is what EF 10 needs for an optional complex type. There
are **no folder tables**: folders, their counts, sizes and art are computed from the placements in
memory (`LibraryIndex`), and a folder exists only while an item sits in it. Folder ids are
`ar`, `al` or `so` plus 16 hex characters of a SHA-256 over the keys. Indexes: `IX_Assets_Folder`
(artist, album, song keys; created in the migration outside the model because its columns belong to
a complex property), `IX_Assets_FiledVersion` (the backfill), `IX_Assets_FiledAt` and
`IX_Assets_UpdatedAt` (the revision stamp). Items keep their placement when their track is deleted.

**Lyric assistant (023)**: `LyricsSheets` (010) gains `Revision` (starts at 1; +1 on every save,
accept, undo or restore; an EF concurrency token), `Sensitivity` (`Loose`, `Balanced`, `Strict`;
default `Balanced`), `RhythmTarget` (BPM 40-220, beats per bar 2-7, bars per line 1-8, syllables
per beat 0.5-4, nullable JSON), `OwnWorkOverride` (statement version and hash, typed name, time,
matched track id and title; nullable JSON) and `LastCause` (default `Created`). New tables:

| Table | Content |
|---|---|
| `LyricAssists` | One assist: sheet and revision, kind, categories, line range, count, instruction (at most 200), sensitivity, language, status (`Queued`, `Running`, `Completed`, `Failed`, `Cancelled`; at most one active; a `Running` assist is `Failed` ("interrupted by restart") at start-up), layers, backend and model, `UsingFallback`, block counts, suggestion counts, discarded counts, token usage, `Origin` (`Editor` or `Agent`) with `AgentRunId`, failure code, message and fix, estimated seconds, times. The newest 20 per sheet are kept |
| `LyricSuggestions` | One anchored suggestion: assist, sheet, category, type, severity, anchor (line, UTF-16 start, length, original text), proposal or alternatives, explanation (at most 300), source layer and model, status (`open`, `accepted`, `rejected`, `stale`, `superseded`), group id |
| `LyricsSheetRevisions` | Title and text snapshots with cause (`Created`, `UserSave`, `Accept`, `Undo`) and the accept group; the last 50 per sheet |
| `LyricsIntentionalMarks` | A word, phrase or line the checker must respect in one sheet (at most 200 per sheet) |
| `LyricsAssistedLines` | Normalised text (at most 400) of lines that came from an accepted suggestion, with the suggestion id; pruned on every save to the lines still present; the count is snapshotted into a song take |
| `LyricWords` | The personal word list: word and language, shared by all sheets of that language |
| `LyricAssistSettings` | One row: the acknowledgement of the lyric Claude notice (time and version; never a key) |
| `LyricAssistEvents` | The events of an assist for Server-Sent Events replay |

Deleting a sheet cancels its active assist and deletes its assists, suggestions, revisions, marks
and assisted lines (cascade). Sheets from before 023 load unchanged: their first revision row is
written at their first change.

**Practice flow (014)**:

| Table | Content |
|---|---|
| `TrackSections` | One row per track: status (`none`, `queued`, `detecting`, `ready`, `failed`), origin (`detected` or `edited` after the first user save), `Revision` (+1 on every replace; the concurrency check of `PUT`), analyser, snap (`downbeat`, `beat`, `time`), warnings, worker job id, error, time |
| `Sections` | Position 0-63, kind (intro, verse, pre-chorus, chorus, bridge, solo, instrumental, outro, other), label 1-40, start and end seconds (sorted, not overlapping, at least 1 s), solo instrument (a channel of the track), confidence, origin (`detected` or `user`); ids are kept across edits |
| `ScoreViews` | One row per score asset: status (`queued`, `rendering`, `ready`, `failed`), track id, `OffsetSeconds` (track time of the score's input start), page count 0-200, the bar map with track times (JSON), worker job id, error, revision |
| `Setlists`, `SetlistEntries` | Name 1-80, note 0-2,000, `RemovedEntries` (entries removed by a track delete, reset when acknowledged); entries with position 0-99, track (cascade), optional mix (set null), note 0-1,000; the same track may appear more than once |
| `PracticeLogSettings` | One row (Id 1, seeded): `Enabled` (default on) and `NoticeAcknowledged` |
| `PracticeSessions`, `PracticeSectionTimes` | A session (id made by the browser, track, optional mix, start and end UTC, at most 12 h, played seconds, loop passes, highest speed) and per section its label and kind snapshot, played seconds, loop passes and the highest trainer speed of a completed pass; `SectionId` has no foreign key, so the times survive a new detection |

Deleting a track deletes its sections, setlist entries (counted in `RemovedEntries`) and practice
sessions; deleting a score asset deletes its view and `view/` files.

**Play-along recording (015)**:

| Table | Content |
|---|---|
| `RecordingTakes` | One take: track (cascade), asset (unique; the library item with `audio.flac`), number per track (never reused), name 1-80, part (`none`, `vocals`, `guitar`, `bass`, `piano`, `drums`, `other`), session id and pass 1-50, `Partial`, `StartSample` and `LengthSamples` (44.1 kHz, at least 0.5 s, inside the track), input mode (`mono-1`, `mono-2`, `stereo`) and channels, latency used and its method (`loopback`, `manual`, `estimated`), calibration (set null), device label, mix (set null), favourite, peak dBFS, clipped samples, dropouts, adjustment samples and `AlignmentRevision`, times. Status, error and size come from the asset. At most 200 per track |
| `InputCalibrations` | Per input device label (unique): latency 0-44,100 samples, method (`loopback`, `manual`), spread (at most 44 samples) and clicks found (6-12) for a loopback, output latency at the time, time |

A take's asset has origin `recording`, the recipe `Provenance.recording` (`takeId`,
`captureFile`) and the audio facts of the FLAC (24-bit, 44.1 kHz, 2 channels, padded from the song
start). Deleting a take removes its asset, files, audio-hash entries and the take channel of mixes
that pointed at it; deleting a track deletes its takes in the same unit of work.

**Instrument views (016)**:

| Table | Content |
|---|---|
| `TrackAnalyses` | One analysis per track and kind (`chords`, `drum-hits`, `vocal-pitch`; key track id + kind, cascade with the track): status (indexed), reason, worker job id, progress, options (JSON), the result document (JSON, bounded before storing; chord edits live in it), engine, device (JSON), source fingerprint (marks the result `stale` when the source stems change) and the estimate in seconds |

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
| `20261116165940_AddAgentSecrets` | 011 claude-key | `AgentSecrets` (name, Data Protection ciphertext, last four characters, purpose version, save time; never the key in plain text) |
| `20261116211814_AddMixPractice` | 012 | `Mixes.Practice` (nullable JSON: speed, transpose, trainer, count-in); older mixes keep `NULL` = defaults |
| `20261117111150_AddLibraryFolders` | 022 | `Assets`: placement columns (`Folder*`, `Folder*Key`, `Folder*Source`, `FolderYear`, `Placement_Discriminator`), `Library*` names, `FiledVersion`, `FiledAt`; `Tracks`: `Library*` names; indexes `IX_Assets_Folder`, `IX_Assets_FiledVersion`, `IX_Assets_FiledAt`, `IX_Assets_UpdatedAt`. Existing items stay unfiled (`FiledVersion` 0) until the start-up backfill files them |
| `20261117115929_AddLyricAssist` | 023 | `LyricsSheets`: `Revision` (existing rows 1), `Sensitivity`, `RhythmTarget`, `OwnWorkOverride`, `LastCause`; tables `LyricAssists`, `LyricSuggestions`, `LyricsSheetRevisions`, `LyricsIntentionalMarks`, `LyricsAssistedLines`, `LyricWords`, `LyricAssistSettings`, `LyricAssistEvents`; indexes on assist sheet and status, suggestion assist and status, and unique language and word |
| `20261117212542_AddPracticeFlow` | 014 | Tables `TrackSections`, `Sections`, `ScoreViews`, `Setlists`, `SetlistEntries`, `PracticeLogSettings` (seeded), `PracticeSessions`, `PracticeSectionTimes`; the loop `sectionId` lives in the existing mix settings JSON |
| `20261117215712_AddRecordingTakes` | 015 | `Mixes.TakeChannel` (nullable JSON); tables `InputCalibrations` and `RecordingTakes` with indexes on track and creation time, track and session, unique track and number, unique asset |
| `20261118055631_AddTrackAnalyses` | 016 | Table `TrackAnalyses` (composite key track id + kind, cascade with the track) with index `IX_TrackAnalyses_Status` |

## File layout

API data folder (volume `api-data`):

```
stemstudio.db
uploads/<sha256><ext>
tracks/<trackId>/stems/<stem>.flac
tracks/<trackId>/detail/<parent>/<name>.flac
tracks/<trackId>/artwork/{auto,user}/{cover,thumb}.jpg     (005)
assets/<assetId>/audio.<ext> | score.{pdf,musicxml,mid}   (007, 008)
assets/<assetId>/view/page-NNN.svg                         (014, score view pages)
assets/<assetId>/audio.flac                                (015, a take; origin recording)
assets/<assetId>/capture.wav                               (015, transient padded upload)
generations/<id>/take-<n>.flac                             (009)
voices/<id>/sample.flac                                    (010)
library-care/jobs/<jobId>.json                             (013, library jobs)
library-care/journal/                                      (013, restore journal)
library-care/staging/<jobId>/                              (013, unpacked archive)
library-care/previous/<id>/                                (013, library kept aside)
library-care/cleanup/<planId>.json                         (013, clean-up plans)
```

Takes (015) are library assets: `capture.wav` is the uploaded WAV padded with silence to the
song position; the render job turns it into `audio.flac`, after which `capture.wav` is deleted
(it is kept while a take is failed, for **Try again**). A backup selects only the `audio.flac` of
ready takes, in the optional category **recordings**, together with the `RecordingTakes` rows; the
storage overview counts them per track as recordings. `InputCalibrations`, sections, setlists and
the practice log travel with the database. The score view pages (`view/`) are not archived: after a
restore they are made again on first open.

The library folders (022) and the lyric assistant (023) add no files to the data folder: their data
lives in the database and is therefore part of a backup (013); folder art references the existing
artwork files, and the dictionaries are part of the API image, not of the data volume.

Library care (013) keeps its state in files, not in the database, because a restore replaces
the database. A track whose detail stems were removed keeps its detail phase with
`filesRemovedAt` (names and mix settings stay). The backup archive format is
`backup-archive.md` 1.0.0: `db/stemstudio.db` first, then `files/<library path>`, `README.txt`
and `manifest.json` last (SHA-256 and size of every entry, consent proof per voice profile,
`schema.migrations` from `__EFMigrationsHistory`).

Worker data folder (volume `worker-data`): `jobs/<jobId>.json`, `detail-jobs/<id>.json`,
from 014 `section-jobs/<id>.json`, `score-view-jobs/<id>/` (`job.json`, `page-NNN.svg`,
`bars.json`) and `setlist-document-jobs/<id>/` (`job.json`, `setlist.pdf`),
`work/<jobId>/`, `uploads/<jobId>/`, the result cache `cache/by-source/` and
`cache/by-audio/<sha>/` and detail checkpoints. On restart, non-terminal jobs are marked
failed (*interrupted by restart*); detail retries reuse checkpoints. Models live in the
`model-cache` volume.

Stems are FLAC, 44.1 kHz, stereo, 16-bit (ADR-001).

## Browser storage

User preferences are not in the database. They live in the browser under the
`localStorage` key `stemstudio.preferences` (schema version 1: level, theme, density,
motion, help state). From 006 the karaoke banner adds `stemstudio.karaoke.visible` and
`stemstudio.karaoke.size`. From 014 the record has schema version 2 with `shortcuts`:
`singleKey` (default on), `pedalMode`, `pedalPreset` (`play-section-start`, `next-previous-section`,
`next-previous-song`, `faster-slower`, `custom`) and `overrides` (command id to at most two key
chords, each in the `keys` or `pedal` layer); a version 1 record reads with the default keys, and
invalid or clashing overrides are dropped and reported once (record at most 16 KB). From 015
`stemstudio.recording` keeps the recording panel's choices (last input, channel mode, part,
mute the original part, count-in bars 0-2 with default 1, monitoring and its volume).
Nothing in browser storage is sent to the API.

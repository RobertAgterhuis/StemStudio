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

## Migrations

| Migration | Feature | Change |
|---|---|---|
| `20261114215330_Initial` | 001 | Tracks and Mixes |
| `20261115091546_AddDetailPhase` | 002 | Track detail phase |
| `20261115091608_AddMixDetailSettings` | 002 | Mix expanded groups and detail channels |
| `20261115115115_AddJobDevice` | 003 | Processing device per track and detail phase |
| `AddArtworkAndArtists` (planned) | 005 | Track artwork and metadata, `Artists` table |
| `AddAssets` (planned) | 007 | `Assets` table (export library) |
| `AddTrackLyrics` (planned) | 006 | `TrackLyrics` table, `Tracks.LyricsStatus` |
| `AddScoreFacts` (planned) | 008 | `Assets.Score` |
| `AddGenerativeStyles` (planned) | 009 | `Styles`, `Generations`, `GenerationTakes`, `Tracks.Origin` |
| `AddLyricsAndVoices` (planned) | 010 | `LyricsSheets`, `VoiceProfiles`, `AudioHashes` |
| `AddAgent` (planned) | 011 | Agent settings, conversations, runs, steps, approvals, undo, events |

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

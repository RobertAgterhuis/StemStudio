# APIs

The OpenAPI documents are the binding definition; this chapter is an overview. Delivered
versions:

- Backend API 1.3.0: `specs/003-gpu-acceleration/contracts/backend-api.openapi.yaml`
- Worker API 1.2.0: `specs/003-gpu-acceleration/contracts/worker-api.openapi.yaml`
- Audio engine 2.0.0: `specs/002-detail-separation/contracts/audio-engine.md`
- UI test ids 1.3.0, help content 1.0.0 and web interop 1.0.0:
  `specs/004-design-system-ux/contracts/`

Later features add their documents to `specs/NNN-*/contracts/` in their Phase 0; until then
each folder holds a `DELTA.md` with the planned changes.

## Backend API (port 8081)

| Method and path | Purpose |
|---|---|
| `GET /health` | Liveness |
| `GET /api/device` | Processing device of the worker (GPU or CPU, reason) |
| `POST /api/imports` | Import from a YouTube or Spotify URL (202 new, 200 existing) |
| `POST /api/imports/upload` | Import an uploaded MP3/WAV/FLAC (multipart, ≤ 200 MiB) |
| `GET /api/tracks`, `GET /api/tracks/{trackId}` | List and read tracks |
| `DELETE /api/tracks/{trackId}` | Delete a track with stems and mixes (409 while busy) |
| `POST /api/tracks/{trackId}/retry` | Retry a failed track |
| `GET /api/tracks/{trackId}/stems/{stemName}` | Stem audio (FLAC, Range, ETag) |
| `POST /api/tracks/{trackId}/detail`, `…/detail/cancel` | Start or cancel detail separation |
| `GET /api/tracks/{trackId}/detail-stems/{parent}/{name}` | Detail stem audio |
| `GET /api/mixes`, `POST /api/mixes` | List (optional `trackId`) and create mixes |
| `GET`, `PUT`, `DELETE /api/mixes/{mixId}` | Read, update (also rename) and delete |
| `POST /api/mixes/{mixId}/duplicate` | Duplicate a mix |
| `GET /api/tracks/{trackId}/artwork/{variant}` | 005: cover (600 px) or thumb (160 px) JPEG; `?v=` makes the URL cache-immutable |
| `PUT`, `DELETE /api/tracks/{trackId}/artwork` | 005: upload an own picture (JPEG/PNG/WebP, ≤ 10 MiB) or go back to the automatic one |
| `GET /api/tracks/{trackId}/band-info` | 005: lookup status, recording, album, artist facts, Wikipedia summary and sources |
| `POST /api/tracks/{trackId}/metadata/lookup` | 005: search again, optionally with a corrected title and artist |
| `POST /api/assets/audio-exports`, `POST /api/assets/stem-packs` | 007: queue an export of one channel, a mix or all stems (202) |
| `GET /api/assets`, `GET /api/assets/{assetId}` | 007: library items (`limit` 1-200, `offset`, `q`) and one item |
| `PATCH`, `DELETE /api/assets/{assetId}` | 007: rename (1-120 characters) and delete |
| `POST /api/assets/{assetId}/retry` | 007: render a failed item again (202) |
| `GET`, `HEAD /api/assets/{assetId}/files/{role}` | 007: the file (role `audio`; `inline=true` for the player); 008 adds the roles `pdf`, `musicxml` and `midi` of a score item |
| `GET`, `PUT`, `DELETE /api/tracks/{trackId}/lyrics` | 006: the lyrics document and state, paste own text (≤ 20,000 characters), remove; all `Cache-Control: no-store` |
| `POST /api/tracks/{trackId}/lyrics/search` | 006: look up LRCLIB again |
| `POST /api/tracks/{trackId}/lyrics/align`, `…/align/cancel` | 006: align plain lyrics to the vocal stem, or cancel |
| `PUT /api/tracks/{trackId}/lyrics/offset` | 006: offset −5,000 to +5,000 ms |
| `GET /api/scores/options` | 008: suggested profile, allowed profiles, grid status, default quality and estimates for `trackId` + `channel` or an `assetId` |
| `POST /api/assets/scores` | 008: queue a score item (202) |

Errors: 400 validation, 404, 409 conflicts (mix name taken, retry of a non-failed track or
item, delete while processing, less than `MinFreeDiskBytes` free), 413 too large, 415
unsupported type, 422 nothing to export, 503 *"Processing service unavailable"*.

### Generative endpoints (009, 010)

| Feature | Endpoints |
|---|---|
| 009 Style transfer | `GET /api/generation/status`, `/api/styles` CRUD and `…/duplicate`, `POST /api/tracks/{id}/restyles`, `/api/generations` with cancel, retry, take audio and import |
| 010 Lyrics to song | `/api/lyrics` CRUD, upload and sections, `POST /api/songs`, `/api/voice-profiles`, `GET /api/consent/voice`, `POST …/takes/{n}/voice-conversions` |

### Planned additions <span class="planned">In development</span>

| Feature | Endpoints |
|---|---|
| 011 Agent | `/api/agent/status`, `/settings` (+ `/test`), `/tools`, `/conversations` (+ messages, undo), `/runs/{runId}` with SSE `events`, `approvals/{stepId}`, `ui-results/{stepId}`, `cancel` |

## Worker API (port 8000, internal)

| Method and path | Purpose |
|---|---|
| `GET /health` | Status, separator, device information, contract version; from 005/007 also the lookup mode, `library {readable, reason}` and the render lane |
| `POST /v1/jobs` | Start a standard job (JSON URL or multipart upload; idempotent per source key) |
| `GET /v1/jobs/{jobId}`, `…/result`, `…/stems/{stem}` | Status, result and stems |
| `POST /v1/detail-jobs`, `GET /v1/detail-jobs/{id}` | Start and read a detail job |
| `POST /v1/detail-jobs/{id}/cancel` | Cancel a detail job |
| `GET /v1/detail-jobs/{id}/result`, `…/stems/{parent}/{name}` | Detail result and stems |
| `GET /v1/jobs/{jobId}/artwork/{variant}` | 005: the source picture saved in the *Saving cover* stage |
| `POST /v1/metadata-jobs`, `GET /v1/metadata-jobs/{id}`, `…/result`, `…/artwork/{variant}` | 005: online lookup (network-only lane) and the Cover Art Archive picture it found |
| `POST /v1/artwork`, `GET /v1/artwork/{artworkId}/{variant}` | 005: normalise an own picture (square, 600 px cover and 160 px thumbnail JPEG, no EXIF/ICC, 40-megapixel cap) |
| `POST /v1/render-jobs`, `GET /v1/render-jobs/{id}`, `…/file`, `DELETE /v1/render-jobs/{id}` | 007: render lane (export) |
| `POST /v1/lyrics-jobs`, `GET /v1/lyrics-jobs/{id}`, `…/result`, `POST …/cancel`, `DELETE /v1/lyrics-jobs/{id}` | 006: LRCLIB lookup (network lane) and alignment (GPU/CPU lane, Whisper large-v3-turbo via stable-ts) |
| `POST /v1/transcription-jobs`, `GET /v1/transcription-jobs/{id}`, `…/files/{role}`, `DELETE /v1/transcription-jobs/{id}` | 008: transcribe lane (basic-pitch, piano CRNN, drum onsets), notation (music21) and engraving (Verovio + CairoSVG, Leipzig font) |

### Render lane (007)

Render jobs run on their own FIFO lane, independent of separation. The worker reads the stems
from the read-only library mount (`STEMSTUDIO_LIBRARY_DIR`); every input path must match the
allowed library path patterns and resolve inside the real root. A render is two passes over
blocks of `STEMSTUDIO_RENDER_BLOCK_SECONDS`: pass 1 mixes and measures the peak, pass 2 mixes
again, applies one gain when the peak exceeds full scale (target −0.1 dBFS, reported as
`appliedGainDb`), quantises (TPDF dither for mixes; raw single channels are not dithered) and
encodes. FLAC and WAV are written with soundfile (libsndfile, with title, artist and comment
tags), MP3 with lameenc (CBR, no tags). A silent result fails with *"Nothing to export: the
selected channels are silent"*. Job states: `queued`, `running`, `completed`, `failed`,
`cancelled`; on restart every queued or running render job becomes `failed` with *"interrupted
by restart"*. The API copies the finished file into `assets/<assetId>/`, checks its hash and
then deletes the worker job.

Online lookups (005) are limited to the allow-listed hosts (musicbrainz.org, coverartarchive.org,
archive.org, www.wikidata.org, *.wikipedia.org, i.ytimg.com, i.scdn.co), HTTPS on the default
port, at most three re-validated redirects, 10 MB per answer and a 10 s timeout; one request per
second per service family, with up to three retries on 429/503 (`Retry-After` capped at 30 s).
An ISRC is looked up with `/ws/2/isrc/<isrc>?inc=artist-credits+releases`, followed by one
recording lookup with `inc=artist-credits+releases+release-groups` per listed recording (at
most five), because MusicBrainz refuses `release-groups` on the ISRC resource.

LRCLIB (006) is on the same allow list (`lrclib.net`); a lyrics lookup asks at most three times
(get with album, get, search), at least 500 ms apart, and accepts a record whose duration is within
2 s of the track. Lyrics never appear in logs: a backend test scans the log output for the fake
provider's words (FR-010).

## Generator API (port 8010, internal)

Version 1.0.0 (009): `GET /health`, `POST /v1/generations` (multipart parameters plus 1 to 6
stems), `GET /v1/generations/{id}`, `POST …/cancel`, `GET …/takes/{index}` (FLAC, Range) and
`DELETE /v1/generations/{id}`. Statuses: queued, waitingForGpu, downloadingModel, running,
completed, failed, cancelled. Version 1.1.0 (010) adds the kinds `song` and
`voiceConversion`, `GET …/takes/{n}/vocal` and the voice-sample check endpoints; the generator
and singer containers serve the same contract and answer `409 kindNotServed` for kinds they do
not run.

## Agent tools <span class="planned">In development</span>

The agent's tools are defined in `specs/011-ai-composer-agent/contracts/tool-catalogue.md`:
43 tools when all features are delivered (library, mixes, navigation, open-mixer control,
detail separation, device, help, artist info, lyrics, export, transcription, styles and song
generation). Each tool has a fixed risk class in code (read, change, job, heavy,
destructive). Run events stream over Server-Sent Events and are resumable with
`Last-Event-ID`.

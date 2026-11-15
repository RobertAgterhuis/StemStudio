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

Errors: 400 validation, 404, 409 conflicts (mix name taken, retry of a non-failed track,
delete while processing), 413 too large, 415 unsupported type, 503 *"Processing service
unavailable"*.

### Planned additions <span class="planned">In development</span>

| Feature | Endpoints |
|---|---|
| 005 Artwork, band info | `GET/PUT/DELETE /api/tracks/{id}/artwork[/{variant}]`, `GET /api/tracks/{id}/band-info`, `POST /api/tracks/{id}/metadata/lookup` |
| 006 Lyrics | `GET/PUT/DELETE /api/tracks/{id}/lyrics`, `POST …/lyrics/search`, `POST …/lyrics/align[/cancel]`, `PUT …/lyrics/offset` (all `no-store`) |
| 007 Export library | `GET /api/assets`, `POST /api/assets/audio-exports`, `POST /api/assets/stem-packs`, `GET/PATCH/DELETE /api/assets/{id}`, `POST …/retry`, `GET/HEAD …/files/{role}` |
| 008 Sheet music | `GET /api/scores/options`, `POST /api/assets/scores` |
| 009 Style transfer | `GET /api/generation/status`, `/api/styles` CRUD and `…/duplicate`, `POST /api/tracks/{id}/restyles`, `/api/generations` with cancel, retry, take audio and import |
| 010 Lyrics to song | `/api/lyrics` CRUD, upload and sections, `POST /api/songs`, `/api/voice-profiles`, `GET /api/consent/voice`, `POST …/takes/{n}/voice-conversions` |
| 011 Agent | `/api/agent/status`, `/settings` (+ `/test`), `/tools`, `/conversations` (+ messages, undo), `/runs/{runId}` with SSE `events`, `approvals/{stepId}`, `ui-results/{stepId}`, `cancel` |

## Worker API (port 8000, internal)

| Method and path | Purpose |
|---|---|
| `GET /health` | Status, separator, device information, contract version |
| `POST /v1/jobs` | Start a standard job (JSON URL or multipart upload; idempotent per source key) |
| `GET /v1/jobs/{jobId}`, `…/result`, `…/stems/{stem}` | Status, result and stems |
| `POST /v1/detail-jobs`, `GET /v1/detail-jobs/{id}` | Start and read a detail job |
| `POST /v1/detail-jobs/{id}/cancel` | Cancel a detail job |
| `GET /v1/detail-jobs/{id}/result`, `…/stems/{parent}/{name}` | Detail result and stems |

Planned lanes: metadata jobs and artwork normalisation (005), lyrics jobs (006), render jobs
(007) and transcription jobs (008), each as `POST /v1/<kind>-jobs` plus status, result or
file and delete.

## Generator API (port 8010, internal) <span class="planned">In development</span>

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

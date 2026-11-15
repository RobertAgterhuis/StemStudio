# Contract delta: feature 009 (style transfer)

Final version numbers are assigned at feature start (tasks.md T003), on top of whatever versions
003, 004 and 007 have released by then. Below, `B` = the backend API version current at feature
start, `U` = the UI test ids version current at feature start. All backend and UI changes are
**additive** (minor bumps). The generator API is **new** (1.0.0). The worker API and the audio
engine contract are **unchanged**.

## 1. Generator API 1.0.0 (new, `contracts/generator-api.openapi.yaml`)

OpenAPI 3.1, server `http://generator:8010` (internal network only, no host port). Error bodies
are RFC 9457 problem details like the worker (`type`, `title`, `status`, `detail`, `code`).

| Method + path | operationId | Request | Responses |
|---|---|---|---|
| `GET /health` | `getHealth` | - | `200 GeneratorHealth` (always 200 when the process runs; `ready=false` + `reason` without a usable GPU) |
| `POST /v1/generations` | `createGeneration` | `multipart/form-data`: part `params` (JSON `CreateGenerationRequest`), parts `stem` (1-6 files, FLAC/WAV, each <= 300 MiB) | `202 Generation` (+ `Location`); `400` invalid params/files; `409 gpuRequired` (not ready); `413`; `415`; `422` source shorter than the range |
| `GET /v1/generations/{generationId}` | `getGeneration` | - | `200 Generation`; `404` |
| `POST /v1/generations/{generationId}/cancel` | `cancelGeneration` | - | `200 Generation` (cancelled or already terminal); `404` |
| `GET /v1/generations/{generationId}/takes/{index}` | `getTakeAudio` | `Range` optional | `200`/`206 audio/flac`; `404` (unknown or not completed) |
| `DELETE /v1/generations/{generationId}` | `deleteGeneration` | - | `204` (files removed; running jobs are cancelled first); `404` |

Schemas (fields as data-model.md section 2):

- `GeneratorHealth { contractVersion, ready, reason?, device: DeviceInfo, models: ModelStatus[], queue: { queued, running? } }`
  (`DeviceInfo` copied from worker 1.2.0, same reason texts).
- `CreateGenerationRequest { kind: "restyle", restyle: RestyleParams }` (`oneOf` by `kind`; 010 adds
  `song` and `voiceConversion`).
- `RestyleParams { caption, audioCoverStrength, inferenceSteps = 8, shift = 3.0, seeds[1..2], mode:
  "range" | "inPlace", rangeStartSeconds, rangeEndSeconds }`.
- `Generation { generationId, kind, status, progress, stage?, params, takes: GeneratedTake[], device?: JobDevice, error?: { code, message }, createdAt, updatedAt }`.
- `GeneratedTake { index, seed, durationSeconds, sampleRate, sha256, bytes, model: { id, revision } }`.
- `GenerationStatus` enum: `queued, waitingForGpu, downloadingModel, running, completed, failed, cancelled`.
- `GenerationErrorCode` enum: `gpuRequired, gpuBusy, outOfMemory, modelDownloadFailed, interrupted, invalidInput, engineError`.

Examples (binding test vectors, copied into the .NET tests): `ReadyHealth`, `NoGpuHealth`,
`QueuedRestyle`, `WaitingForGpuRestyle`, `RunningRestyle`, `CompletedRestyle` (2 takes),
`FailedOutOfMemory`, `CancelledRestyle`.

## 2. Backend API `B` -> `B+0.1` (additive)

| Method + path | operationId | Body / query | Responses |
|---|---|---|---|
| `GET /api/generation/status` | `getGenerationStatus` | - | `200 GenerationStatus` (`notInstalled` \| `unavailable` + reason \| `ready`; models + `downloadBytesNeeded`); cached 10 s |
| `GET /api/styles` | `listStyles` | `?kind=preset\|user&genreFamily=&mood=&instrument=&q=` | `200 Style[]` (presets first, then by name) |
| `GET /api/styles/{styleId}` | `getStyle` | - | `200 Style`; `404` |
| `POST /api/styles` | `createStyle` | `StyleInput` | `201 Style`; `400` validation (field + reason, incl. guard hits `code: "imitation"`) ; `409` duplicate name |
| `POST /api/styles/{styleId}/duplicate` | `duplicateStyle` | - | `201 Style` (user copy "<name> (copy)", numbered if taken) |
| `PUT /api/styles/{styleId}` | `updateStyle` | `StyleInput` | `200 Style`; `400`; `403` preset; `404`; `409` |
| `DELETE /api/styles/{styleId}` | `deleteStyle` | - | `204`; `403` preset; `404` |
| `POST /api/tracks/{trackId}/restyles` | `createRestyle` | `RestyleRequest { styleId, mode: "range"\|"inPlace", rangeStartSeconds?, rangeEndSeconds?, includedStems?, strength?, takes = 1 }` | `202 Generation` (+ `Location`); `400`; `404` track; `409` track not completed **or** generation not ready (`code: "notInstalled"\|"gpuRequired"`, reason); `503` generator unreachable |
| `GET /api/generations` | `listGenerations` | `?kind=&sourceTrackId=` | `200 Generation[]` (newest first) |
| `GET /api/generations/{generationId}` | `getGeneration` | - | `200 Generation`; `404` |
| `POST /api/generations/{generationId}/cancel` | `cancelGeneration` | - | `200 Generation`; `404` |
| `POST /api/generations/{generationId}/retry` | `retryGeneration` | - | `202 Generation` (new id); `409` not failed/cancelled or not ready |
| `DELETE /api/generations/{generationId}` | `deleteGeneration` | - | `204` (files + library entries removed; imported tracks kept) |
| `GET /api/generations/{generationId}/takes/{index}/audio` | `getTakeAudio` | `Range` | `200`/`206 audio/flac` |
| `POST /api/generations/{generationId}/takes/{index}/import` | `importTake` | - | `202 Track` (new) or `200 Track` (already imported) ; `409` not completed |

Schemas:

- `Style { id, kind: "preset"\|"user", presetKey?, name, genreFamily, subGenre?, tempoFeel, instrumentation[], moods[], vocalHandling, defaultStrength, description?, createdAt, updatedAt }`.
- `StyleInput` = `Style` without `id`, `kind`, `presetKey`, timestamps.
- Enums: `GenreFamily` (14 values, data-model 3.1), `TempoFeel` (`keep, laidback, driving, halfTime, swing`), `VocalHandling` (`keep, instrumental`), `Strength` (`subtle, light, balanced, strong, free`).
- `Generation { id, kind: "restyle", status, progress, stage?, sourceTrackId?, sourceTrackTitle, range { startSeconds, endSeconds, mode }, includedStems[], style: StyleSnapshot, strength, takes: GenerationTake[], device?: JobDevice, error?: { code, message }, createdAt, updatedAt, completedAt? }`.
- `GenerationTake { index, seed, durationSeconds, audioUrl, libraryAssetId?, importedTrackId?, aiGenerated: true }`.
- `Track` gains `origin { kind: "imported"\|"generated", generationId?, takeIndex?, aiGenerated }` (absent = imported).
- `GenerationStatus { state, reason?, device?: ProcessingDevice, models[], downloadBytesNeeded }`.

## 3. Worker: configuration only (contract unchanged)

`STEMSTUDIO_GPU_LEASE_FILE`, `STEMSTUDIO_GPU_LEASE_WAIT_SECONDS` (data-model 1); new fallback
reason text `"GPU busy with another StemStudio job"` in `JobDevice.fallbackReason` (free text in
1.2.0, so no schema change).

## 4. UI test ids `U` -> `U+0.1` (additive)

| Screen | `data-testid` |
|---|---|
| Any page (status) | `generation-status` (attribute `data-state` = `notInstalled`\|`unavailable`\|`ready`), `generation-status-reason`, `generation-help-link` |
| Track detail | `track-restyle` (button; `disabled` + `aria-describedby` reason when not ready), `track-origin-badge` (AI-generated), `track-origin-source-link` |
| Restyle dialog | `restyle-style-select`, `restyle-style-description`, `restyle-mode-range`, `restyle-mode-inplace`, `restyle-range-start`, `restyle-range-end`, `restyle-stem-<stem>` (checkbox), `restyle-strength` (`aria-valuenow` 0-4), `restyle-takes`, `restyle-start`, `restyle-cancel`, `restyle-error`, `restyle-personal-use-notice` |
| Style library (`/styles`) | `style-filter-genre`, `style-filter-mood`, `style-filter-instrument`, `style-filter-kind`, `style-search`, `style-row-<styleId>` (attribute `data-kind`), `style-duplicate-<styleId>`, `style-edit-<styleId>`, `style-delete-<styleId>`, `style-new` |
| Style editor | `style-name-input`, `style-genre-select`, `style-subgenre-input`, `style-tempo-select`, `style-instrument-tags`, `style-mood-tags`, `style-vocal-select`, `style-strength-select`, `style-description-input`, `style-save`, `style-error` (field + reason) |
| Generations (`/generations`) and track page section | `generation-row-<generationId>` (attribute `data-status`), `generation-progress-<generationId>` (`aria-valuenow` 0-100), `generation-stage-<generationId>`, `generation-cancel-<generationId>`, `generation-retry-<generationId>`, `generation-delete-<generationId>`, `generation-error-<generationId>`, `take-play-<generationId>-<index>`, `take-import-<generationId>-<index>`, `take-details-<generationId>-<index>`, `take-ai-badge-<generationId>-<index>` |

## 5. Help entries (004 framework, keys)

`help.generation.gpu-required`, `help.styles.library`, `help.styles.editor`,
`help.restyle.options`, `help.restyle.strength`, `help.generation.ai-label`,
`help.generation.personal-use`, `help.generation.limits` (quality varies, deny-list limitation).

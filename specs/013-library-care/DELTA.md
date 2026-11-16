# Contract delta: feature 013 (library care)

Feature 013 adds two backend tags (`library-care`, `storage`), one additive field, three worker
endpoints, a new archive format and additive UI test ids. The audio-engine contract (2.3.0 after
012) and the generator API (1.1.0) are **unchanged**. No database migration.

## Status (architect, 2026-11-16)

| Contract | Base | 013 document | Version | State |
|---|---|---|---|---|
| Backend API | 1.11.0 (feature 012, not on this branch; latest full document here: 010's 1.9.0 + 011's 1.10.0 delta) | [contracts/backend-api.delta.openapi.yaml](contracts/backend-api.delta.openapi.yaml) `1.12.0-delta.1`, standalone, additive | **1.12.0** | binding; merged unchanged into the full document by T005 |
| Worker API | 1.6.0 (`specs/008-sheet-music`; 009-012 do not change it) | [contracts/worker-api.openapi.yaml](contracts/worker-api.openapi.yaml), full document | **1.7.0** | binding |
| UI test ids | 1.11.0 (012) | [contracts/ui-test-ids.delta.md](contracts/ui-test-ids.delta.md) | **1.12.0** | names binding; merged by T005 |
| Backup archive | - | [contracts/backup-archive.md](contracts/backup-archive.md) + [backup-manifest.schema.json](contracts/backup-manifest.schema.json) | **1.0.0** (new) | binding |
| help-content | 1.0.0 (004) | 9 topics in the existing area `mixes` | unchanged | - |
| Agent tool catalogue | 1.0.0 (011) | no tools in 013 (roadmap: a later minor) | unchanged | - |

Validation: `npx @redocly/cli@2.78.0 lint --config specs/013-library-care/contracts/redocly.yaml
specs/013-library-care/contracts/backend-api.delta.openapi.yaml specs/013-library-care/contracts/worker-api.openapi.yaml`
-> both valid (OpenAPI 3.1), 7 warnings of the accepted kinds (`operation-4xx-response` on
operations without client errors, the unused merge-only component `DetailPhaseAddition`). The
manifest schema passes the JSON Schema 2020-12 meta-schema check (jsonschema 4.26) and accepts the
example, rejecting traversal (`..`), `//` and absolute paths.

On any difference between this summary and the documents, the documents win.

## 1. Backend API 1.11.0 -> 1.12.0

| Method + path | operationId | Request | Responses |
|---|---|---|---|
| `GET /api/library-care/status` | `getLibraryCareStatus` | - | `200 LibraryCareStatus` (also during maintenance) |
| `GET /api/backups` | `listBackups` | - | `200 BackupList` |
| `POST /api/backups` | `createBackup` | `BackupOptions` | `202 LibraryJob`; `400`; `409 library-job-running \| backups-folder-unavailable \| insufficient-disk-space`; `503` |
| `GET /api/backups/estimate` | `estimateBackup` | query: the six options | `200 BackupEstimate`; `400` |
| `GET /api/backups/{fileName}` | `getBackup` | - | `200 BackupDetails`; `404`; `422 archive-invalid` |
| `DELETE /api/backups/{fileName}` | `deleteBackup` | - | `204`; `404`; `409 archive-in-use`; `503` |
| `GET /api/backups/{fileName}/file` | `downloadBackup` | `Range` | `200`/`206 application/zip`; `404`; `416` |
| `POST /api/backups/{fileName}/verify` | `verifyBackup` | - | `202 LibraryJob`; `404`; `409`; `503` |
| `POST /api/backups/{fileName}/restore` | `restoreBackup` | `RestoreOptions { mode: replace\|merge, voiceProfiles, confirmation }` | `202 LibraryJob`; `400`; `404`; `409 library-job-running \| backup-running \| jobs-active \| previous-library-exists \| insufficient-disk-space`; `422 confirmation-required \| archive-incompatible \| archive-invalid`; `503` |
| `GET /api/library-jobs` | `listLibraryJobs` | `limit` 1-50 | `200 LibraryJob[]` |
| `GET /api/library-jobs/{jobId}` | `getLibraryJob` | - | `200`; `404` (also during maintenance) |
| `POST /api/library-jobs/{jobId}/cancel` | `cancelLibraryJob` | - | `202`; `404`; `409 not-cancellable` |
| `GET /api/library-care/previous` | `getPreviousLibrary` | - | `200 PreviousLibrary`; `404` |
| `POST /api/library-care/previous/revert` | `revertRestore` | - | `202 LibraryJob` (`undoRestore`); `404`; `409`; `503` |
| `POST /api/library-care/previous/discard` | `discardPreviousLibrary` | `{ confirmation: "DISCARD" }` | `204`; `404`; `409`; `422 confirmation-required`; `503` |
| `GET /api/storage` | `getStorageOverview` | `refresh` | `200 StorageOverview`; `503` |
| `POST /api/storage/cleanup/preview` | `previewCleanup` | `CleanupRequest` | `200 CleanupPlan`; `400`; `409 backup-running \| library-job-running`; `503` |
| `POST /api/storage/cleanup` | `executeCleanup` | `{ planId }` | `200 CleanupResult`; `400`; `409 plan-expired \| plan-stale \| backup-running \| library-job-running`; `503` |

Changes to existing operations and schemas (additive):

- Category `recordings` (015 play-along takes, assets with provenance origin `recording`):
  `BackupOptions.recordings`, query `recordings` of `estimateBackup`, `TrackStorage.recordingsBytes`;
  originals, never in a clean-up (CTO cross-feature note 2026-11-16). 015 latency calibrations are
  database rows.
- `DetailPhase.filesRemovedAt: date-time | null` (not required). `POST /api/tracks/{id}/detail` is
  accepted for a completed phase with `filesRemovedAt` set.
- **Write gate**: `409 backup-running` on `DELETE /api/tracks/{trackId}`, `POST /api/tracks/{trackId}/retry`,
  `PUT`/`DELETE /api/tracks/{trackId}/artwork`, `DELETE /api/assets/{assetId}`,
  `DELETE /api/generations/{generationId}`, `DELETE /api/voice-profiles/{profileId}`,
  `DELETE /api/styles/{styleId}`, `DELETE /api/lyrics/{lyricsId}`,
  `DELETE /api/agent/conversations/{conversationId}` while a backup runs.
- **Maintenance**: `503 library-maintenance` + `Retry-After: 5` on every `/api/*` operation during
  a restore switch, except `getLibraryCareStatus` and `getLibraryJob`.
- Problem `type` values (new): `backup-running`, `library-job-running`, `library-maintenance`,
  `backups-folder-unavailable`, `archive-in-use`, `archive-invalid`, `archive-incompatible`,
  `archive-unsafe`, `archive-corrupt`, `jobs-active`, `previous-library-exists`,
  `confirmation-required`, `not-cancellable`, `plan-expired`, `plan-stale`, `migration-failed`
  (job error only); `insufficient-disk-space` (007) reused with `requiredBytes`, `freeBytes`.

Merge instructions (T005): copy the two tags, 16 paths, components (parameters `FileName`,
`JobId` renamed `LibraryJobId` if the full document already has `JobId`, the `Include*` query
parameters, responses `Maintenance`, `LibraryJobRunning`; schemas except `ProblemDetails`,
`HttpValidationProblemDetails` and `NotFound`, which exist), examples; add `filesRemovedAt` to
`DetailPhase`; add the write-gate `409`/`503` responses to the listed operations; add the 1.12.0
changelog line from the delta's description.

## 2. Worker API 1.6.0 -> 1.7.0

| Method + path | operationId | Request | Responses |
|---|---|---|---|
| `GET /v1/storage` | `getWorkerStorage` | - | `200 WorkerStorage` (volumes, cache entries with job ids and `inUse`, job files, scratch, model caches) |
| `POST /v1/storage/cleanup` | `cleanWorkerStorage` | `StorageCleanupRequest { cacheEntries[] \| ["*"], scratch, oldJobFiles }` | `200 StorageCleanupResult { freedBytes, removedEntries, skipped[{ audioSha256, reason: inUse\|notFound }] }`; `400` |
| `POST /v1/jobs/rehydrate` | `rehydrateJob` | `RehydrateRequest { trackId, sourceType, sourceKey, model, stems }` | `201`/`200 RehydratedJob { jobId, audioSha256, status: completed, model, stems, createdAt }`; `400`; `404 stemMissing`; `409 libraryUnavailable`; `422` |

Existing operations unchanged. `schemas.py` `CONTRACT_VERSION = "1.7.0"`; new ports
`StorageMeter`, `CacheJanitor`, `LibraryStemReader` (data-model 7).

## 3. UI test ids 1.11.0 -> 1.12.0

See [contracts/ui-test-ids.delta.md](contracts/ui-test-ids.delta.md): `nav-care`,
`library-job-indicator`, `library-maintenance`, the `/care` page with `care-tab-*`, `backup-*`,
`library-job-*`, `restore-*`, `previous-library-*`, `storage-*`, `cleanup-*`, plus
`detail-files-removed` (track page) and `mixer-detail-removed` (mixer). Level rules per id.

## 4. Backup archive 1.0.0 (new)

ZIP (Zip64) with `db/stemstudio.db`, `files/<data-relative path>`, `README.txt` and
`manifest.json` (last). Manifest: format and versions, schema migrations, included/excluded
categories, counts, totals, every entry with size and SHA-256, consent proofs of voice profiles,
warnings. Reader rules: data-model 3.4. Compatibility promise: backup-archive.md section 6.

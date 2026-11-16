# Data Model: Library Care (feature 013)

Contracts: [backup-archive.md](contracts/backup-archive.md) (format 1.0.0),
[backup-manifest.schema.json](contracts/backup-manifest.schema.json),
[backend-api.delta.openapi.yaml](contracts/backend-api.delta.openapi.yaml) (1.12.0),
[worker-api.openapi.yaml](contracts/worker-api.openapi.yaml) (1.7.0),
[ui-test-ids.delta.md](contracts/ui-test-ids.delta.md) (1.12.0). On any difference the contract
wins.

**No database migration.** Library jobs, the journal and the previous library are kept as JSON
files outside the database (they must survive a restore that replaces the database); the only
schema change is an additive field in the `Tracks.DetailPhase` JSON column (section 5.3).

## 1. Configuration (additions)

| Setting (API, section `StemStudio`) | Env | Default | Meaning |
|---|---|---|---|
| `BackupDir` | `StemStudio__BackupDir` | `../../data/backups` (content-root relative); compose `/backups` | backups folder (bind mount) |
| `BackupHostDirHint` | `StemStudio__BackupHostDirHint` | compose: `${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}` | display only: the host path shown in the UI ("Your backups are in ./backups") |
| `LibraryCare:MinFreeMarginBytes` | `StemStudio__LibraryCare__MinFreeMarginBytes` | 67,108,864 (64 MiB) | margin above the estimate before a backup starts (FR-007) |
| `LibraryCare:StopBelowFreeBytes` | `StemStudio__LibraryCare__StopBelowFreeBytes` | 268,435,456 (256 MiB) | a running backup or restore stops below this free space |
| `LibraryCare:StorageCacheSeconds` | `StemStudio__LibraryCare__StorageCacheSeconds` | 30 | cache of `GET /api/storage` |
| `LibraryCare:CleanupPlanMinutes` | `StemStudio__LibraryCare__CleanupPlanMinutes` | 10 | plan validity (FR-023) |
| `LibraryCare:CopyBufferBytes` | `StemStudio__LibraryCare__CopyBufferBytes` | 1,048,576 | file copy buffer |

| Compose (devops) | Value |
|---|---|
| `api.volumes` | `+ ${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}:/backups` |
| `api.environment` | `StemStudio__BackupDir: /backups`, `StemStudio__BackupHostDirHint: ${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}` |
| worker | unchanged mounts (`/data`, `/models`, `/library:ro` from 007) |

Worker: no new settings. The worker computes model-cache sizes from `STEMSTUDIO_MODEL_DIR`.

## 2. Storage layout (additions, API data directory)

```text
<data>/library-care/                       # never in a backup, never in the clean-up
├── jobs/<jobId>.json                      # LibraryJob records (5.1), last 50 kept
├── journal/<restoreId>.json               # restore/undo journal (5.5)
├── staging/<jobId>/                       # backup: stemstudio.db snapshot; restore: extracted library
│   ├── stemstudio.db
│   └── files/...                          # restore only, mirrors the data layout
├── previous/<restoreId>/                  # library moved aside by a replace restore (at most one)
│   ├── previous.json                      # PreviousLibrary (5.6)
│   ├── stemstudio.db, tracks/, assets/, generations/, voices/, uploads/
└── cleanup/<planId>.json                  # CleanupPlan (6.3), deleted after execution or expiry
<backups>/                                 # bind mount
├── stemstudio-backup-20261116-142233.zip
└── stemstudio-backup-20261116-150001.zip.partial   # only while writing
```

`library-care/` is excluded from the storage walk categories (shown as its own rows "Restore
staging" and "Previous library") and is never offered by the clean-up.

## 3. Archive (summary of backup-archive 1.0.0)

### 3.1 Entries

| Entry | Category | Compression |
|---|---|---|
| `db/stemstudio.db` | `database` | deflate (fastest) |
| `files/tracks/<trackId>/stems/<stem>.flac` | `stems` | store |
| `files/tracks/<trackId>/artwork/(auto\|user)/(cover\|thumb).jpg` | `artwork` | store |
| `files/uploads/<sha256>.(mp3\|wav\|flac)` | `pendingUploads` | store |
| `files/tracks/<trackId>/detail/<parent>/<detail>.flac` | `detailStems` | store |
| `files/assets/<assetId>/<fileName>` (kind `Audio`, not from a generation) | `exports` | store (audio), deflate (other) |
| `files/assets/<assetId>/<fileName>` (kind `Score`) | `scores` | store (PDF), deflate (MusicXML, MIDI) |
| `files/generations/<generationId>/<path>` | `generations` | store (audio), deflate (JSON) |
| `files/assets/<assetId>/<fileName>` (kind `Audio`, provenance origin `recording`, feature 015) | `recordings` | store |
| `files/voices/<profileId>/<fileName>.flac` | `voiceProfiles` | store |
| `README.txt` | - | deflate |
| `manifest.json` (last) | - | deflate |

### 3.2 Categories and regenerability

| Category | Always / optional | Class | How it comes back | Indicative cost |
|---|---|---|---|---|
| `database` (incl. 015 input-latency calibrations per device label) | always | original | - | - |
| `stems` | always | original | re-import the source (URL only) | minutes of separation; impossible for uploads |
| `artwork` | always | original (`user`), regenerable (`auto`: lookup) | lookup again | seconds |
| `pendingUploads` | always | original | - | - |
| `detailStems` | optional, default on | **regenerable** | Split further (instant when the worker cache still has the result) | 2-15 min per track (GPU/CPU) |
| `exports` | optional, default on | original (user product; re-export by hand) | - | - |
| `scores` | optional, default on | original (re-transcribe by hand) | - | - |
| `generations` | optional, default on | original (not reproducible) | - | - |
| `recordings` (015 play-along takes, hidden assets) | optional, default on | original (user performance) | - | - |
| `voiceProfiles` | optional, default on | original | - | - |
| worker caches, model caches, scratch | never in a backup | regenerable | automatic | see storage overview |

### 3.3 Layout allow-list (binding regexes, archive entries and stored paths)

Relative to the data directory, `/` separators, matched against the whole string:

```text
GUID   = [0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}
STEM   = vocals|drums|bass|guitar|piano|other
PARENT = vocals|drums|guitar|piano|other
DETAIL = kick|snare|toms|hihat|cymbals|lead|backing|strings|brass|woodwinds|keys|synth|acoustic-guitar|electric-guitar|percussion|rest
NAME   = [A-Za-z0-9][A-Za-z0-9._-]{0,99}          # generated file names; no "..", see rule 3

tracks/GUID/stems/STEM\.flac
tracks/GUID/artwork/(auto|user)/(cover|thumb)\.jpg
tracks/GUID/detail/PARENT/DETAIL\.flac
uploads/[0-9a-f]{64}\.(mp3|wav|flac)
assets/GUID/NAME
generations/GUID/NAME(/NAME){0,2}
voices/GUID/NAME
```

Rules: (1) archive entries are `files/` + one of these or exactly `db/stemstudio.db`,
`manifest.json`, `README.txt`; (2) no entry is a directory record except `files/`-prefixed
directories that end in `/` (ignored); (3) `NAME` never equals `.` or `..` and never contains `..`;
(4) the Phase 0 task T004 checks these patterns against every path the 001-011 code generates
(test over `StemFileStore`, `DetailStemFileStore`, `AssetFileStore`, `TakeStorage`,
`VoiceProfileService`, `UploadStore`, artwork store) and adjusts `NAME`/depth if a generator
produces something else; the result is binding.

### 3.4 Validation on restore (order; first failure stops)

1. File name matches `^[A-Za-z0-9][A-Za-z0-9._-]{0,119}\.zip$`; it is a regular file in the backups
   folder (no link: `FileAttributes.ReparsePoint` refused).
2. Open as ZIP; central directory readable; entry count <= 2,000,000; `manifest.json` present,
   <= 256 MiB, valid against the schema, `format = stemstudio-backup`, `formatVersion` major 1.
3. Every central-directory entry: name rules (3.3), no duplicates (ordinal), no link (external
   attributes: Unix mode `S_IFLNK`, `0xA000` in the high 16 bits), present in `manifest.entries`
   with the same size; every manifest entry present in the archive.
4. Compatibility: every `schema.migrations` id known to this app (else `archive-incompatible`).
5. Space: `totals.bytes` + 256 MiB <= free space of the data volume.
6. Extract into `library-care/staging/<jobId>/`: destination `Path.GetFullPath(Path.Combine(stagingRoot, entryPathWithoutFilesPrefix))`
   must start with `stagingRoot + Path.DirectorySeparatorChar`; write through a stream that hashes
   and counts and fails when more than `sizeBytes` bytes arrive; compare SHA-256 at the end.
7. Database: open staged `stemstudio.db` read-only, `PRAGMA integrity_check` = `ok`,
   `__EFMigrationsHistory` equals `schema.migrations`; migrate (read-write) when older.
8. Stored paths: `VoiceProfiles.SamplePath`, take/vocal paths in `Generations`, asset file names,
   `Tracks.UploadFileName` match 3.3; every referenced file exists in staging.
9. Consent proofs (section 5.4).

## 4. Backup pipeline (API)

### 4.1 Options and estimate

```csharp
// StemStudio.Contracts.LibraryCare
public sealed record BackupOptionsDto(bool DetailStems = true, bool Exports = true, bool Scores = true,
    bool Generations = true, bool Recordings = true, bool VoiceProfiles = true);
public sealed record BackupEstimateDto(long TotalBytes, IReadOnlyList<CategorySizeDto> Categories,
    long FreeBytes, bool Enough, FolderStateDto Folder);
public sealed record CategorySizeDto(string Category, bool Included, bool Optional, long Bytes, int Files, string Regenerability);
```

### 4.2 Stages and progress weights

| Stage (`LibraryJob.stage`) | Text | Weight |
|---|---|---|
| `snapshot` | "Copying the database" | 1 % |
| `scrub` | "Preparing the database copy" | 1 % |
| `files` | "Writing <category>: <n> of <m>" | by bytes, 96 % |
| `manifest` | "Writing the manifest" | 1 % |
| `check` | "Checking the archive" | 1 % |

`progress` = 0..100 (int); `bytesDone`/`bytesTotal` from the file stage; `etaSeconds` from the
moving average of the last 10 s (null in the first 5 s).

### 4.3 Scrub of the staged database (excluded categories)

See research R-04 table. Implemented as `BackupScrubber` on a `StemStudioDbContext` bound to the
staged file, using the domain's own rules (`DetailPhase with { FilesRemovedAt = snapshotTime }`,
`Generation.DetachVoiceProfile`), then `PRAGMA integrity_check`. `exports` removes only `Audio`
assets whose provenance origin is neither a generation nor `recording`; `recordings` removes only
assets with origin `recording` (015; the scrub is written against the provenance value so it works
unchanged once 015 adds the enum member, and 013's tests use a fixture value). Incomplete voice-profile consent
(5.4 backup rule) is scrubbed the same way, with a warning.

### 4.4 File selection

From the scrubbed snapshot: for each completed track its stems (from `StemCatalog` of its model)
and artwork files that exist; detail stems of completed parents (`ParentOutcome.Split`) unless
excluded; assets with status `completed` by kind; generations with status `completed` and their
take files; voice profiles' sample files; uploads named by `Tracks.UploadFileName` of non-completed
tracks. A file referenced but missing on disk -> warning `missing-file` in the report and the
manifest (`warnings[]`), entry not written, and the restore marks the entity "files missing"
(the same reconcile as for an interrupted job). Order: database, stems, artwork, uploads, detail
stems, exports, scores, generations, recordings, voice profiles (sorted by path inside a category).

## 5. Backend domain and state

### 5.1 LibraryJob (JSON record, `library-care/jobs/<jobId>.json`)

| Field | Type | Notes |
|---|---|---|
| `jobId` | GUID | |
| `kind` | `backup` \| `verify` \| `restore` \| `undoRestore` | |
| `status` | `queued` \| `running` \| `completed` \| `failed` \| `cancelled` | terminal: last three |
| `stage` | string? | 4.2 / 5.5 stages |
| `progress` | int 0..100 | |
| `bytesDone`, `bytesTotal` | long | |
| `currentItem` | string? | e.g. "Track 'Song title' - stems" (no file paths) |
| `etaSeconds` | int? | |
| `archiveFileName` | string | |
| `options` | `BackupOptionsDto` \| `RestoreOptionsDto` | |
| `report` | `BackupReport` \| `RestoreReport` \| `VerifyReport` ? | at the end |
| `error` | `{ code, message }`? | codes = problem types of the contract |
| `cancellable` | bool | false once a restore closes the gate |
| `createdAt`, `startedAt?`, `finishedAt?` | UTC | |

One job at a time (`LibraryJobRunner`, a hosted service with a `Channel<LibraryJob>` of capacity 1;
`POST` while one is active -> `409 library-job-running`). Start-up: a `running` record -> `failed`
`interrupted` + delete `<archive>.partial` and `staging/<jobId>/` (restore: journal recovery first).

### 5.2 LibraryGate

```csharp
public enum LibraryGateMode { Open, BackupRunning, Maintenance }
```

| Mode | Refused (problem) | Allowed |
|---|---|---|
| `Open` | - | everything |
| `BackupRunning` | `409 backup-running` on: `DELETE /api/tracks/{id}`, `DELETE /api/assets/{id}`, `DELETE /api/generations/{id}`, `DELETE /api/voice-profiles/{id}`, `DELETE /api/styles/{id}`, `DELETE /api/lyrics/{id}`, `PUT`/`DELETE /api/tracks/{id}/artwork`, `POST /api/tracks/{id}/retry` (rewrites stems), `POST /api/storage/cleanup*`, `POST /api/backups/*/restore`, `DELETE /api/backups/*`, `DELETE`/`POST revert` previous library, `DELETE /api/agent/conversations/{id}` (011) | every read, every job start, mix and style edits, renames |
| `Maintenance` | `503 library-maintenance` + `Retry-After: 5` on every `/api/*` except `GET /health`, `GET /api/library-care/status`, `GET /api/library-jobs/{id}` | those three |

Implemented as an endpoint filter (`RequireGateOpen` metadata on the listed endpoints) plus a
middleware for maintenance. Synchronisers and the agent executor call `gate.IsMaintenance` at the
start of every tick and skip it.

### 5.3 Track detail state (additive)

`DetailPhase` gains `FilesRemovedAt: DateTime?` (JSON field `filesRemovedAt`, absent in older rows
= null). `Track.RemoveDetailFiles(now)` sets it on a `completed` phase; starting the detail phase
again (`POST /api/tracks/{id}/detail`) is allowed when `FilesRemovedAt != null` and clears it when
the new phase completes. Backend DTO: `DetailPhaseDto.filesRemovedAt` (backend-api 1.12.0). The
mixer treats detail channels of such a track as absent (parents play), settings stay in the mix.

### 5.4 Consent proof

`ConsentStatement.Known(string version) -> (string Text, string Sha256)?` returns every embedded
statement version (010: `consent.v1`). Valid proof = `Known(v) != null` and `Sha256` equals the
stored hash, `personName` trimmed 1-80, `consentedAt <= manifest.createdAt`, sample file present in
the archive with the manifest SHA-256, and the manifest proof equals the database proof (all five
fields). Binding for later features: a new statement version adds a file, it never removes an old
one.

### 5.5 Restore

```csharp
public enum RestoreMode { Replace, Merge }          // empty library: Replace without moving aside
public sealed record RestoreOptionsDto(RestoreMode Mode, bool VoiceProfiles = true, string? Confirmation = null);
```

`Confirmation` must be `"REPLACE"` for `Replace` on a non-empty library (UI typed confirmation;
the API checks it so a script cannot replace by accident). Stages:

| Stage | Text | Weight | Cancellable |
|---|---|---|---|
| `validate` | "Checking the archive" (steps 1-5 of 3.4) | 1 % | yes |
| `extract` | "Unpacking and verifying <n> of <m>" (step 6) | 90 % | yes |
| `database` | "Checking and updating the database" (7-9, migrate) | 4 % | yes |
| `switch` | "Switching to the restored library" (journal) | 3 % | **no** |
| `reconcile` | "Finishing" (FR-015) | 2 % | no |

Journal steps (`journal/<restoreId>.json`): `{ restoreId, kind: restore|undo, mode, steps: [
{ op: "moveAside"|"moveIn", from, to, done } ], state: "staged"|"switching"|"committed"|"rolledBack" }`;
paths in the journal are relative to the data directory and validated against
`library-care/(staging|previous)/GUID(/.*)?` and the top-level item names (`stemstudio.db`,
`stemstudio.db-wal`, `stemstudio.db-shm`, `tracks`, `assets`, `generations`, `voices`, `uploads`).

Reconcile: in the restored database, every track/detail/asset/lyrics-alignment/score/generation in
a non-terminal state -> `failed` with error "Interrupted by restore" (detail: `failed` with the
same text; retry allowed); `WorkerJobId`s kept (stale references are fixed by rehydration, FR-019);
generator job ids of failed generations cleared.

`RestoreReport { mode, restored: CategoryCount[], skipped: SkippedItem[] (category, id, name, reason), migratedFrom?: string, warnings: string[], previousLibraryKept: bool }`.

### 5.6 PreviousLibrary (`previous/<restoreId>/previous.json`)

`{ restoreId, movedAsideAt, sizeBytes, counts: { tracks, mixes, assets, generations, voiceProfiles }, restoredArchive: fileName }`.
At most one; `POST .../revert` runs an `undoRestore` job (same journal, roles swapped; the
restored library becomes the previous library, so undo is itself undoable once);
`DELETE` removes the folder (job-less, recursive delete, typed confirmation `DISCARD` checked by the
API).

### 5.7 Merge (P3)

`LibraryMerger` reads the staged database with a second context and inserts into the live context
in one transaction, order and rules as research R-07; `MergeConflict` items go to
`RestoreReport.skipped` (`reason`: `same-id`, `same-source`, `dependency-skipped`) or are renamed
(`renamed: { from, to }`).

## 6. Storage overview and clean-up (API)

### 6.1 StorageOverview (computed, cached 30 s)

```text
StorageOverview
  measuredAt
  volumes[]: { id: library|backups|workerData|modelCache, label, freeBytes?, totalBytes?, sharedWith[]: id, reason? }
  categories[]: { category, bytes, files, class: original|regenerable, howToRegenerate?, costHint? }
      categories = stems, detailStems, artwork, pendingUploads, exports, scores, generations,
                   recordings, voiceProfiles, database, leftovers, restoreStaging, previousLibrary,
                   workerSeparationCache, workerDetailCache, workerJobFiles, workerScratch
  tracks[]: { trackId, title, artist?, status, totalBytes, sourceBytes, stemsBytes, detailStemsBytes,
              detailFilesRemoved: bool, artworkBytes, exportsBytes, scoresBytes, generationsBytes,
              recordingsBytes, workerCacheBytes? }
  unattached: { exportsBytes, scoresBytes, generationsBytes, voiceProfilesBytes, lyricsSheets: int }
  modelCaches[]: { id, folder, bytes, models[]: string, redownloadBytes }
  backups: { count, bytes, folderAvailable, reason? }
  worker: { available, reason? }
```

`sourceBytes` = pending upload bytes + worker separation-cache bytes attributed to the track.
`workerCacheBytes` = separation + detail cache of the track's `audioSha256` (null when the worker is
unavailable). The library database size includes `-wal` and `-shm`.

### 6.2 Classifier

`StorageClassifier.Classify(relativePath) -> (category, entityId?)` uses the 3.3 patterns plus:
`*.tmp` anywhere, `uploads/*` not referenced by a non-completed track, and `tracks|assets|generations|voices/GUID/` folders without a row -> `leftovers`.
The same classifier is used by the backup selection, the restore checks and the clean-up (one
source of truth, property-tested: SC-007).

### 6.3 CleanupPlan (`library-care/cleanup/<planId>.json`)

```text
CleanupRequest  { actions: [ { kind: detailStems|workerCache|leftovers, trackIds?: GUID[] (null = all) } ] }
CleanupPlan     { planId, createdAt, expiresAt, items: [ { kind, trackId?, title?, files: int, bytes, howToRegenerate } ],
                  skipped: [ { kind, trackId?, title?, reason } ], totalBytes, fingerprint }
CleanupResult   { planId, freedBytes, removed: [ { kind, trackId?, files, bytes } ], failed: [ { kind, trackId?, reason } ] }
```

`fingerprint` = SHA-256 over the sorted `(path, size, lastWriteUtc)` of every planned API file plus
the worker entry ids; execution recomputes it and answers `409 plan-stale` on a difference. Worker
items are sent to `POST /v1/storage/cleanup` with the entry ids; the worker skips entries in use and
reports them. Skip reasons: `detail-active` (detail phase queued/running), `job-active` (worker
entry used by a non-terminal job), `worker-unavailable`, `export-active` (an export job reads the
detail stem).

Actions in detail:

| Kind | API side | Worker side |
|---|---|---|
| `detailStems` | delete `tracks/<id>/detail/**`, `Track.RemoveDetailFiles(now)` | - |
| `workerCache` | - | delete `cache/by-audio/<sha>/` (stems, detail, partial, artwork) and the `cache/by-source` pointers to it; standard job files that referenced it get `cacheEvicted: true` |
| `leftovers` | delete `*.tmp`, unreferenced uploads, orphan entity folders | delete `work/*`, `uploads/*` and job directories of terminal jobs older than 24 h, terminal job JSON older than 7 days |

## 7. Worker (Python, worker-api 1.7.0)

### 7.1 Schemas (`schemas.py`, `CONTRACT_VERSION = "1.7.0"`)

```python
class VolumeUsage(BaseModel):        # path id, free/total bytes
    id: Literal["data", "models"]; free_bytes: int; total_bytes: int
class CacheEntryUsage(BaseModel):
    audio_sha256: str                # 64 hex
    standard_bytes: int; detail_bytes: int; partial_bytes: int; artwork_bytes: int
    job_ids: list[str]               # standard jobs pointing at it
    in_use: bool                     # a non-terminal job uses it
    from_library: bool               # created by rehydrate
    last_used_at: datetime | None
class ModelCacheUsage(BaseModel):
    id: str                          # folder name under /models, e.g. "torch", "acestep"
    bytes: int; models: list[str]    # known model ids found in it (registry in models/catalog.py)
class WorkerStorage(BaseModel):
    measured_at: datetime; volumes: list[VolumeUsage]
    cache_entries: list[CacheEntryUsage]; job_files_bytes: int; job_files_count: int
    scratch_bytes: int; model_caches: list[ModelCacheUsage]
class StorageCleanupRequest(BaseModel):
    cache_entries: list[str] = []    # audio_sha256 values; ["*"] = all not in use
    scratch: bool = False; old_job_files: bool = False
class StorageCleanupResult(BaseModel):
    freed_bytes: int; removed_entries: list[str]
    skipped: list[SkippedEntry]      # audio_sha256 + reason "inUse"
class RehydrateRequest(BaseModel):
    track_id: UUID; source_type: SourceType; source_key: str
    model: ModelName; stems: list[StemName]   # 4 or 6, must match the model's set
```

### 7.2 Ports and paths

- `ports.py`: `StorageMeter` (`measure() -> WorkerStorage`), `CacheJanitor`
  (`clean(request) -> StorageCleanupResult`), `LibraryStemReader`
  (`open(track_id, stem) -> BinaryIO`, raises `LibraryUnavailable`, `StemMissing`); fakes for tests.
- `DataPaths`: `library_stem(track_id, stem) -> <library>/tracks/<uuid>/stems/<stem>.flac` (UUID
  rendered with `str(UUID)`, stem from the enum: no user string in the path).
- Rehydrated cache key: `sha256("library:" + ",".join(sorted(f"{stem}={file_sha256}")))` hex; the
  job JSON is a normal completed `Job` (`sourceType`/`sourceKey` from the request, `progress 1.0`,
  `stage null`) with an extra internal flag `from_library` (not in the public `Job` schema).
- The janitor takes the queue lock while deleting, so no job can start using an entry being
  deleted.

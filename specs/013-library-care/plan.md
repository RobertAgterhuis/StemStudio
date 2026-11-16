# Implementation Plan: Library Care (Backup, Restore, Storage Overview and Clean-up)

**Branch**: `013-library-care` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/013-library-care/spec.md`; constitution 1.3.1; MoSCoW M4,
M5; roadmap allocation (backend-api 1.12.0, worker-api 1.7.0, ui-test-ids 1.12.0, backup-archive
1.0.0); [research.md](research.md); [ADR-022](../../docs/decisions/ADR-022-library-backup-and-storage-care.md).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). 013 is built after 001-011
are merged, in parallel with 012 (roadmap build order `{012, 013}`); its contract edits are
additive and merged by the CTO (tasks.md T005).

## Summary

1. **Backup (API)**: an online SQLite snapshot (`SqliteConnection.BackupDatabase`) plus the files
   the snapshot references, written by the API into one ZIP (Zip64) archive in a bind-mounted host
   folder (`/backups`), with a manifest (format 1.0.0) of every entry's size and SHA-256 and the
   consent proof of every voice profile. Optional categories are scrubbed from the archived
   database, so every archive is self-consistent. A **write gate** refuses file-deleting
   operations during a backup; jobs keep running. Progress, cancel, space checks, post-write check.
2. **Restore (API)**: list archives in the folder, show compatibility, verify everything in a
   staging folder (hashes, layout allow-list, zip-slip and zip-bomb rules, SQLite integrity, stored
   paths, consent proofs), migrate an older schema in staging, then switch the library with a
   journalled rename (crash-safe, seconds of maintenance mode). Replace keeps the previous library
   (undo, discard). Merge (P3) adds missing items in one transaction.
3. **Storage overview and clean-up (API + worker)**: a classified walk of the library plus the
   worker's new `GET /v1/storage` report (caches attributed to tracks, model caches, free space);
   two-step clean-up (preview -> plan id -> execute) that only ever removes regenerable data:
   detail stems, the worker caches and leftovers.
4. **Rehydration (worker 1.7.0)**: `POST /v1/jobs/rehydrate` re-creates a worker cache entry from
   the library stems (read-only `/library` mount of 007), so "Split further" and lyrics alignment
   work after a restore on a new machine or after a cache clean-up.
5. **UI**: a Library care page (`/care`) with Backups and Storage tabs, a shell indicator for a
   running library job, a maintenance overlay, help topics, levels, WCAG 2.2 AA.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (API, Web), Python 3.12 (worker), TypeScript (no change).

**Primary Dependencies (new)**: **none**. ZIP via `System.IO.Compression` (BCL), SQLite backup via
`Microsoft.Data.Sqlite` (already referenced through EF Core), hashing via
`System.Security.Cryptography.SHA256` / `IncrementalHash`; worker uses `hashlib`, `shutil`,
`os.scandir` (stdlib). JSON Schema validation of the manifest: at runtime the reader uses typed
deserialisation plus explicit rules (`ManifestRules`, same limits as the schema); the tests check
writer output against `backup-manifest.schema.json` with the dependency-free
`SchemaSubsetValidator` of 004 (`tests/StemStudio.Web.Tests/Skeleton/`; `JsonSchema.Net` stays
excluded: OSMF EULA, not OSI). Phase 0 T006 moves the validator into a shared test helper and adds
the keywords this schema uses (`const`, `not`, `uniqueItems`, `format` as annotation).

**Storage**: API data volume (`/data`), new `library-care/` folder inside it, new bind mount
`/backups`. **No database migration** (data-model intro).

**Testing**: xUnit v3 + coverlet (API, Domain, Persistence), bUnit (Web), pytest (worker),
Playwright on the fake stack (E2E). File-system seams: `IFileSystem`-style port `ILibraryFiles` for
free-space and disk-full simulation (no real disk filling in tests); `TimeProvider` fake;
crash injection through a `IJournalStepHook` test seam.

**Target Platform**: Docker Desktop on Windows 11 (reference machine), Linux Docker hosts, the
non-Docker path (folders relative to the content root).

**Performance Goals**: SC-002 >= 60 MB/s backup and restore on the reference machine; storage
overview < 2 s for 500 tracks; cancel <= 5 s.

**Constraints**: one library job at a time; no browser upload of archives; no model-cache removal;
no encryption (documented).

**Scale/Scope**: libraries up to 2,000,000 entries / several hundred GB (Zip64).

## Constitution Check

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % | Every unit has failing tests first (tasks.md); file-system and journal seams; hostile-archive corpus; crash injection; property test over generated libraries (SC-007); worker meter/janitor/rehydrate with temp dirs; E2E on the fake stack. | PASS |
| II | SIG 4 stars | New folders `Api/LibraryCare/{Gate,Jobs,Archive,Backup,Restore,Storage,Cleanup}`, `Domain/LibraryCare`, worker `storage/`; pipelines split into small stage classes (snapshot, scrub, select, write, check; validate, extract, database, switch, reconcile); quality-gate rules for layering and banned APIs. | PASS |
| III | Contracts first | backup-archive 1.0.0 + manifest JSON Schema, backend 1.12.0 delta, worker 1.7.0 full document, ui-test-ids 1.12.0 delta (Redocly 2.78.0 valid); DTOs, `schemas.py` 1.7.0 and ports in Phase 0; ADR-022. | PASS |
| IV | Open source, lawful use | No new dependencies. Model weights are never put in an archive (ADR-009: no redistribution); consent proofs travel with voice profiles and are checked on restore (010 rules); no secrets in archives (FR-004). | PASS |
| V | Simplicity and honesty | Full archives only, no scheduler, no encryption, no incremental backups, no model-cache removal (research R-11); limits shown in help (not encrypted, source audio not kept, worker cache rebuilt). Merge is a cuttable P3. | PASS |
| - | Tech & security | Archive treated as untrusted input (Microsoft ZIP best practices; data-model 3.4); no free paths in any endpoint; generated path components only; subprocesses: none; secrets never archived; logs without personal content. | PASS |
| - | Workflow | Owners as below; hand-offs per checkpoint; CTO updates the manuals (constitution 1.3.1). | PASS |

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/013-library-care/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md, DELTA.md
├── checklists/requirements.md
└── contracts/
    ├── backup-archive.md                 # format 1.0.0
    ├── backup-manifest.schema.json       # JSON Schema 2020-12
    ├── backend-api.delta.openapi.yaml    # 1.12.0-delta.1 (merged into backend-api 1.12.0)
    ├── worker-api.openapi.yaml           # 1.7.0 (full document)
    ├── ui-test-ids.delta.md              # merged into ui-test-ids 1.12.0
    └── redocly.yaml
docs/decisions/ADR-022-library-backup-and-storage-care.md
```

### Source Code (additions)

```text
src/StemStudio.Contracts/LibraryCare/*.cs (DTOs), ApiRoutes (+LibraryCare*, Storage*), ContractLimits (+archive limits)   (architect)
src/StemStudio.Domain/LibraryCare/
├── LibraryLayout.cs            # allow-list patterns (data-model 3.3), entry-name rules
├── StorageClassifier.cs        # path -> (category, entity), regenerability table
├── BackupCategory.cs, Regenerability.cs
├── ConsentProof.cs             # 5.4 (uses Domain.Voices.ConsentStatement.Known)
└── CleanupRules.cs             # what each clean-up kind may remove
src/StemStudio.Domain/Tracks/Detail/DetailPhase.cs (+FilesRemovedAt), Track.Detail.cs (+RemoveDetailFiles)
src/StemStudio.Domain/Voices/VoiceConsent.cs (+ConsentStatement.Known)
src/StemStudio.Api/LibraryCare/
├── Gate/        LibraryGate, RequireGateOpenFilter, MaintenanceMiddleware, IGateAware (synchroniser hook)
├── Jobs/        LibraryJobRunner (hosted), LibraryJobStore (JSON), LibraryJobMapper, StartupRecovery
├── Archive/     ArchiveWriter, ArchiveReader, ArchiveEntryRules, HashingLimitedStream, ManifestBuilder, ManifestRules
├── Backup/      BackupService, DatabaseSnapshot, BackupScrubber, BackupSelection, BackupEstimator, BackupFolder
├── Restore/     RestoreService, ArchiveValidator, StagingExtractor, StagedDatabase, LibrarySwitcher, RestoreJournal,
│                RestoreReconciler, PreviousLibraryStore, LibraryMerger (P3)
├── Storage/     StorageOverviewService, LibraryWalker, WorkerStorageAttribution
├── Cleanup/     CleanupPlanner, CleanupExecutor, CleanupPlanStore
└── LibraryCareEndpoints.cs, StorageEndpoints.cs, LibraryCareProblems.cs, LibraryCareOptions.cs
src/StemStudio.Api/Worker/ (+ storage, cleanup, rehydrate client calls; WorkerCacheRehydrator used by Detail and Lyrics)
src/StemStudio.Api/ApiComposition.cs (registrations, filter on the gated endpoints of 001-011)
worker/src/stemstudio_worker/
├── schemas.py (+1.7.0 models), ports.py (+StorageMeter, CacheJanitor, LibraryStemReader)        (architect)
├── storage/meter.py, storage/janitor.py, storage/rehydrate.py, storage/model_catalog.py      (stem-separation)
├── jobs/paths.py (+library_stem), jobs/queue (janitor lock)                                    (stem-separation)
└── api/storage.py (routes)                                                                    (stem-separation)
src/StemStudio.Web/
├── Components/Pages/LibraryCare.razor(+.cs), Care/{BackupsTab,BackupDialog,LibraryJobPanel,BackupList,BackupDetails,
│   RestoreDialog,PreviousLibraryPanel,StorageTab,StorageTracksTable,CleanupDialog}.razor
├── Components/Layout (+nav-care, LibraryJobIndicator, MaintenanceOverlay)
├── Api/ (+ILibraryCareClient, typed client), Help/topics/mixes/{library-care,...}.md (9 topics)
└── Pages/TrackDetail Detail section (+detail-files-removed), Mixer (+mixer-detail-removed)
e2e/tests/library-care-{backup,restore,storage,cleanup,merge}.spec.ts, e2e/pages/LibraryCarePage.ts           (qa)
tests/fixtures/backup/{make_hostile_archives.py, make_schema_archives.md}                                    (qa)
docker-compose.yml (+/backups bind mount, env), .gitignore (+backups/), scripts/check-backup-folder.{sh,ps1},
    e2e compose override (temp backups dir), CI (folder with uid 1654)                                       (devops)
tools/quality/ (+layer rule, banned APIs)                                                                    (quality-gate)
```

**Dependency direction**: `Api.LibraryCare.*` -> `Domain.LibraryCare`, `Persistence`, `Api.Storage`
(file stores, `DataDirectory`), `Api.Worker` (client). Other API folders may reference only
`Api.LibraryCare.Gate` (to declare gated endpoints and skip synchroniser ticks); nothing references
`Backup`, `Restore`, `Storage`, `Cleanup` (quality-gate rule T009). `Web.Care` -> `Web.Api` client
only.

### Ownership map

| Owner | Paths |
|---|---|
| architect | `specs/013-library-care/**`, ADR-022, `src/StemStudio.Contracts/LibraryCare/**`, `ApiRoutes`, `ContractLimits`, worker `schemas.py`/`ports.py` additions |
| backend | `src/StemStudio.Domain/**`, `src/StemStudio.Api/**`, `src/StemStudio.Persistence/**`, `tests/StemStudio.{Domain,Api,Persistence}.Tests/**` |
| stem-separation | `worker/src/stemstudio_worker/{storage,jobs,api}/**`, `worker/tests/**` for these |
| frontend | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` |
| qa | `e2e/**`, `tests/fixtures/backup/**`, `docs/testing/**` |
| devops | `docker-compose*.yml`, `docker/**`, `scripts/**`, `.github/**`, `.gitignore` |
| quality-gate | `tools/quality/**`, `docs/quality/**` |
| CTO | merges, `docs/public/**`, `docs/STATUS.md`, `docs/THIRD-PARTY-LICENSES.md`, README |

## Key flows

1. **Backup**: `POST /api/backups` -> checks (no job running, folder writable, space >= estimate +
   64 MiB) -> `LibraryJob(queued)` -> runner: gate `BackupRunning` -> `DatabaseSnapshot` (backup
   API into `staging/<job>/stemstudio.db`) -> `BackupScrubber` (exclusions, incomplete consent) ->
   `BackupSelection` (files from the scrubbed snapshot, ordered) -> `ArchiveWriter` into
   `<name>.zip.partial` (store/deflate per type, `IncrementalHash` per entry, free-space check every
   64 MiB, cancel token checked per 1 MiB block) -> `README.txt`, `manifest.json` -> close ->
   reopen and compare central directory with the manifest -> rename to `.zip` -> report -> gate
   `Open` -> delete staging. Failure or cancel: delete `.partial` and staging, gate `Open`.
2. **Restore**: `POST /api/backups/{f}/restore` -> checks (no job, no active work, previous library
   absent for replace on a non-empty library, confirmation, space) -> job -> `ArchiveValidator`
   (3.4 steps 1-5) -> `StagingExtractor` (step 6, hashing while extracting) -> `StagedDatabase`
   (integrity, history, migrate, stored paths, consent; drop invalid or deselected profiles with the
   010 detach rule) -> gate `Maintenance` -> wait for in-flight requests (max 10 s, endpoint filter
   counts them) -> `SqliteConnection.ClearAllPools()` -> `LibrarySwitcher` (journal: move aside,
   move in, commit) -> gate `Open` -> `RestoreReconciler` (non-terminal -> failed "Interrupted by
   restore") -> report. Start-up recovery reads unfinished journals before the app serves
   requests.
3. **Rehydration**: Detail start / lyrics align -> worker `409`/`404` cache miss -> 
   `WorkerCacheRehydrator.RehydrateAsync(track)` -> worker `POST /v1/jobs/rehydrate` -> store new
   `WorkerJobId` -> retry the original call once (stage text "Preparing stems").
4. **Storage overview**: `GET /api/storage` -> `LibraryWalker` (classified sums, cached 30 s) +
   worker `GET /v1/storage` (2 s timeout; unavailable -> reason) -> attribution by
   `Track.WorkerJobId` -> `DriveInfo` for `/data` and `/backups` -> volumes grouped by equal totals.
5. **Clean-up**: preview -> `CleanupPlanner` (rules per kind, skip active, fingerprint, plan file)
   -> execute -> fingerprint check -> API deletions (+ `Track.RemoveDetailFiles`) and worker
   `POST /v1/storage/cleanup` -> result; storage cache invalidated.

## Security review points (CTO integration wave, T069)

1. No endpoint accepts a path; `fileName` regex; archive files must be regular files (no reparse
   points) directly in the backups folder.
2. Archive extraction never uses `ExtractToDirectory`/`ExtractToFile`; every destination passes
   `GetFullPath` + `StartsWith(root + separator)`; quality-gate banned-API rule.
3. Hostile corpus H1-H17 refused before any change (SC-004), library tree byte-identical afterwards.
4. Staged database: read-only open for checks, never attached to the live database, no SQL from the
   archive executed; stored path columns validated (H14).
5. Consent: profiles only with valid proof (SC-008); manifest/database proof equality; no
   profile data in logs.
6. Secrets: `ANTHROPIC_API_KEY`, configuration, Data Protection keys never in an archive (test
   greps the archive for the env var value set in the test).
7. Download endpoint: same CORS policy as stems (web origin may GET); no directory listing beyond
   recognised archive metadata.
8. Denial of service: one library job; entry, manifest and size limits; free-space floor during
   writing; clean-up plans expire.
9. Maintenance mode cannot be entered without a validated staging library; start-up recovery
   finishes or rolls back.
10. Bind-mount permissions documented (Linux uid 1654); the API never changes host permissions.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Write gate instead of pausing jobs | consistency without breaking long GPU jobs (research R-03) | pausing generation/separation mid-run is not supported and would make backups wait for hours |
| Library jobs and journal outside the database (JSON files) | a restore replaces the database; job state and the journal must survive the switch and a crash | a table would be swapped away mid-restore |
| Previous library kept aside | "never delete originals silently": a replace must be undoable | deleting the old library on replace loses data on a wrong click; copying it doubles disk use |
| Worker `rehydrate` endpoint | detail and alignment jobs need the worker cache, which does not exist after a restore or a cache clean-up (research R-10) | changing the detail/alignment contracts to accept library stems directly touches two existing job lanes and their tests; rehydrate is one additive endpoint |
| Database scrub for excluded categories | archives must be self-consistent so restore never points at missing files | keeping rows and marking files missing would need "missing" states in four entity types |

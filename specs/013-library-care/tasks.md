---

description: "Task list for feature 013 library care (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Library Care (Backup, Restore, Storage Overview and Clean-up)

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [DELTA.md](DELTA.md), the
contracts in [contracts/](contracts/), constitution 1.4.0, ADR-022.

**Tests**: REQUIRED (constitution I), red -> green per owner sequence, >= 90 % line AND branch per
component. No task needs a GPU or real models: the worker side uses temp directories and fakes,
E2E runs on the fake stack with a temporary backups folder. File-system edge cases (disk full,
free space, rename failures, crashes between journal steps) use the seams `ILibraryFiles` and
`IJournalStepHook`; no test fills a real disk.

**Status**: design complete (architect, 2026-11-16). **Phase 0 done** (architect, 2026-11-17; hand-off
`docs/handoffs/architect-013-phase0.md`): the wave may start.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task).
- **[US1]..[US5]**: user story of spec.md.
- Owners: architect, backend, stem-separation (worker), frontend, qa, devops, quality-gate, CTO.
  audio-engine, audio-ingest, beat-analysis and generative have no tasks (the mixer change is
  Blazor-side: detail channels of a track with removed detail stems are simply not loaded).

## Checkpoint hand-offs (binding, ADR-008)

`docs/handoffs/<owner>-013-<checkpoint>.md` at `foundational`, every story the owner touches
(`us1`..`us5`) and `polish`.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| backend | worker 1.7.0 storage, cleanup, rehydrate | `worker-api.openapi.yaml` 1.7.0 + examples (T002) | stub HTTP handler with the examples |
| stem-separation | library stems on `/library` | 007 mount, data-model 7 | temp directory as library |
| frontend | backend 1.12.0 library-care and storage | delta (T002), DTOs (T006) | fake `ILibraryCareClient` |
| qa | UI ids, archive format | `ui-test-ids.delta.md`, `backup-archive.md` (T002) | fake stack + fixture archives (T012) |
| devops | backups mount, env | data-model 1 | - |

---

## Phase 0: Contracts and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] ADR-022 -> Accepted after CTO review (number check against 012's ADR); record any change in the ADR and DELTA.md
- [x] T002 [architect] Contracts in `specs/013-library-care/contracts/`: `backup-archive.md` 1.0.0, `backup-manifest.schema.json`, `backend-api.delta.openapi.yaml` 1.12.0-delta.1, `worker-api.openapi.yaml` 1.7.0, `ui-test-ids.delta.md` 1.12.0, `redocly.yaml`; Redocly 2.78.0 valid (DELTA.md)
- [x] T003 [architect] Spike on the reference machine and in CI (Linux): `ZipArchive` writes and reads back an entry > 4 GiB and > 65,535 entries (Zip64) on a seekable `FileStream`; `BackupDatabase` while a writer loops (no failed write); same-volume `Directory.Move` of `tracks/` etc. inside `/data`; write throughput to a Docker Desktop bind mount (feeds SC-002); results in research.md R-04/R-12
- [x] T004 [P] [architect] Layout allow-list check (data-model 3.3 rule 4): a test that runs every path generator of 001-011 (`StemFileStore`, `DetailStemFileStore`, `AssetFileStore`, `TakeStorage`, `VoiceProfileService` sample path, `UploadStore`, artwork store) and asserts each generated path matches `LibraryLayout`; adjust the patterns if needed (binding result)
- [x] T005 [architect] After 012 is merged: merge `backend-api.delta.openapi.yaml` into the full `backend-api.openapi.yaml` 1.12.0 and `ui-test-ids.delta.md` into the full `ui-test-ids.md` 1.12.0 (merge instructions in DELTA.md section 1); Redocly lint clean
- [x] T006 [P] [architect] .NET contract code: `src/StemStudio.Contracts/LibraryCare/*` (all DTOs of the delta, enums as strings), `ApiRoutes.LibraryCare*`/`Storage*`, `LibraryCare/LibraryCareLimits` (archive name regex, 2,000,000 entries, 256 MiB manifest, plan 10 min, margins; own class like 012's `PracticeLimits`), `DetailPhaseDto.FilesRemovedAt`; move `SchemaSubsetValidator` to a shared test helper and add `const`, `not`, `uniqueItems`, `format` (annotation); skeleton tests (DTO round trips against the contract examples)
- [x] T007 [P] [architect] Worker skeleton: `schemas.py` 1.7.0 (`CONTRACT_VERSION`), models of data-model 7.1; `ports.py` `StorageMeter`, `CacheJanitor`, `LibraryStemReader` with docstrings; fakes in `worker/tests/storage/fakes.py` (the worker convention: one `fakes.py` per test package, `--import-mode=importlib`); skeleton tests; ruff + mypy strict
- [x] T008 [architect] Verify the skeleton (build 0 warnings, all tests and coverage gates, ruff/mypy, `tools/quality/run_all.py`); hand-off `docs/handoffs/architect-013-phase0.md` (dependencies: none new)

**Checkpoint**: skeleton green; the wave may start.

---

## Phase 1: Setup

- [x] T009 [P] [quality-gate] Failing tests for new rules: (a) only `StemStudio.Api.LibraryCare.Gate` may be referenced from other `StemStudio.Api.*` folders; nothing references `LibraryCare.{Backup,Restore,Storage,Cleanup,Archive}` except `LibraryCare` itself and `ApiComposition`; (b) banned APIs in `src/**`: `ZipFile.ExtractToDirectory`, `ZipFileExtensions.ExtractToDirectory`, `ExtractToFile`, `ZipFile.CreateFromDirectory`; (c) worker: `storage/` may import `jobs.paths` but not `separation`, `detail` or `api`
- [x] T010 [quality-gate] Implement the rules in `tools/quality/`; update `docs/quality/SIG-REPORT.md`
- [x] T011 [P] [devops] `docker-compose.yml`: api volume `${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}:/backups`, env `StemStudio__BackupDir=/backups`, `StemStudio__BackupHostDirHint`; header comment; `.gitignore` `backups/`; `scripts/check-backup-folder.{sh,ps1}` (exists, writable by uid 1654, free space); e2e/CI: temporary backups folder owned by uid 1654; non-Docker path default `data/backups` documented in `scripts/` README (FR-005)
- [ ] T012 [P] [qa] Fixtures: `tests/fixtures/backup/make_hostile_archives.py` (H1-H17 of research R-06, deterministic, small), schema-version archive builder (008 and 010 schemas from the committed migrations, SC-009), `compare_library.py` (row dump + file hash compare, SC-001), a fixture asset with provenance origin `recording` (SC-010); `docs/testing/TEST-STRATEGY.md` section "Library care"

---

## Phase 2: Foundational

### backend (tests first)

- [ ] T013 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/LibraryCare/LibraryLayoutTests.cs`, `StorageClassifierTests.cs`: every pattern of data-model 3.3, entry-name rules (traversal, absolute, backslash, control characters, length, `.`/`..`), classification incl. `recordings` vs `exports` by provenance origin, leftovers; property test over generated libraries: clean-up kinds only match regenerable paths (FR-022, FR-034, SC-007, SC-010)
- [ ] T014 [backend] Implement `Domain/LibraryCare/{LibraryLayout,StorageClassifier,BackupCategory,Regenerability,CleanupRules}.cs` to pass T013
- [ ] T015 [P] [backend] Failing tests `ConsentProofTests.cs`: `ConsentStatement.Known` (v1 text and hash), proof valid/invalid cases of data-model 5.4 (unknown version, hash mismatch, name length, time after archive, manifest vs database difference) (FR-009, FR-016)
- [ ] T016 [backend] Implement `ConsentStatement.Known`, `Domain/LibraryCare/ConsentProof.cs` to pass T015
- [ ] T017 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/LibraryCare/GateTests.cs`: every endpoint of data-model 5.2 answers `409 backup-running` in `BackupRunning`; reads and job starts pass; `Maintenance` answers `503` + `Retry-After: 5` except the three allowed routes; in-flight request counter; synchronisers and the agent executor skip ticks (FR-002, FR-013)
- [ ] T018 [backend] Implement `LibraryCare/Gate/*`, attach `RequireGateOpen` to the 001-011 endpoints listed, wire synchronisers, to pass T017
- [ ] T019 [P] [backend] Failing tests `LibraryJobRunnerTests.cs`, `LibraryJobStoreTests.cs`: one job at a time (`409 library-job-running`), JSON records, last 50 kept, start-up marks `running` as failed `interrupted` and removes `.partial` and staging, cancel token propagation (FR-006)
- [ ] T020 [backend] Implement `LibraryCare/Jobs/*` to pass T019
- [ ] T021 [P] [backend] Failing tests: `DetailPhase.FilesRemovedAt` JSON round trip (old rows without the field), `Track.RemoveDetailFiles`, detail start accepted for a completed phase with removed files and the field cleared on completion, DTO mapping (FR-025)
- [ ] T022 [backend] Implement to pass T021
- [ ] T023 [P] [backend] Failing tests `WorkerClientStorageTests.cs` with the 1.7.0 examples: storage report, cleanup (skipped `inUse`), rehydrate (201/200/404/409/422), 2 s timeout for the storage report
- [ ] T024 [backend] Implement the worker client additions to pass T023

### stem-separation (worker, tests first)

- [x] T025 [P] [stem-separation] Failing tests `worker/tests/storage/test_meter.py`: walk of a temp data dir and model dir (no link following), cache entries with standard/detail/partial/artwork bytes, job-id attribution from job JSON, `in_use` for non-terminal jobs, model catalogue ids, free space via a fake `disk_usage`, 30 s cache
- [x] T026 [stem-separation] Implement `storage/meter.py`, `storage/model_catalog.py`, `DataPaths.library_stem` to pass T025
- [x] T027 [P] [stem-separation] Failing tests `test_janitor.py`: queue lock held, `in_use` entries skipped, `["*"]`, `by-source` pointers removed, `cacheEvicted` flag on referencing jobs, scratch and old job files (7 days, fake clock), freed bytes exact
- [x] T028 [stem-separation] Implement `storage/janitor.py` to pass T027
- [x] T029 [P] [stem-separation] Failing tests `test_rehydrate.py` and API tests `test_storage_routes.py`: stems copied from a temp library, cache key rule, completed job file, idempotent 200, stem set must equal the model's (422), missing stem 404 with `stem`, missing mount 409, path built only from UUID + enum, `/result` and `/stems` 404 for rehydrated jobs, detail job accepts the rehydrated `standardJobId`; contract examples
- [x] T030 [stem-separation] Implement `storage/rehydrate.py`, `api/storage.py` (`/v1/storage`, `/v1/storage/cleanup`, `/v1/jobs/rehydrate`) to pass T029

**Checkpoint**: HO:foundational for backend and stem-separation.

---

## Phase 3: User Story 1 - Back up my whole library (P1) MVP

- [ ] T031 [P] [US1] [backend] Failing tests `DatabaseSnapshotTests.cs`, `BackupScrubberTests.cs`: snapshot while a writer loops (no failed write), `journal_mode=DELETE`, integrity check; scrub per excluded category (research R-04 table incl. `recordings`), incomplete consent scrubbed with warning (FR-002, FR-003, FR-009)
- [ ] T032 [US1] [backend] Implement `Backup/{DatabaseSnapshot,BackupScrubber}.cs` to pass T031
- [ ] T033 [P] [US1] [backend] Failing tests `ArchiveWriterTests.cs`, `ManifestBuilderTests.cs`: entry names and order, store vs deflate per extension, SHA-256 per entry, manifest last and valid against `backup-manifest.schema.json`, `README.txt`, post-write central-directory check, `.partial` rename, Zip64 path via a lowered test threshold seam (FR-001, FR-005, FR-008)
- [ ] T034 [US1] [backend] Implement `Archive/{ArchiveWriter,ManifestBuilder,HashingLimitedStream}.cs` to pass T033
- [ ] T035 [P] [US1] [backend] Failing tests `BackupServiceTests.cs`: estimate per category, space refusal (estimate + 64 MiB), free-space floor during writing (seam), progress and ETA, cancel <= 5 s removes `.partial`, missing file warning, changed-after-snapshot, gate opened in every outcome, secrets never archived (env value grep), logs without personal content (FR-003-FR-009, FR-035, SC-003) **011 claude-key cross-feature rule**: a test asserts that no archive entry comes from the key ring directory (`StemStudio:DataProtection:KeyRingDir`, volume `api-keys`) and that a sentinel Claude key stored through `PUT /api/agent/settings/claude-key` appears in the archive (database snapshot included) only as ciphertext (011 follow-up CK-FR-017, CK-SC-001)
- [ ] T036 [US1] [backend] Implement `Backup/{BackupService,BackupSelection,BackupEstimator,BackupFolder}.cs` to pass T035
- [ ] T037 [P] [US1] [backend] Failing endpoint and contract tests: `getLibraryCareStatus`, `listBackups` (unrecognised files, partial files hidden, cache by name/size/time), `estimateBackup`, `createBackup`, `getBackup`, `downloadBackup` (Range, ETag, 416), `deleteBackup` (`archive-in-use`), `verifyBackup` job, `listLibraryJobs`, `getLibraryJob`, `cancelLibraryJob`; responses match the delta examples
- [ ] T038 [US1] [backend] Implement `LibraryCareEndpoints.cs`, `LibraryCareProblems.cs`, `ArchiveReader` verify mode to pass T037
- [ ] T039 [P] [US1] [frontend] bUnit tests first, then: `/care` page with tabs, `nav-care`, Backups tab (folder state and error, backup dialog with profiles, options at Advanced+, estimate rows, voice notice, start, error), library job panel (progressbar, live region, cancel, report, warnings), archive list (rows, status, download, details, verify, delete confirm), details panel (consent proofs; manifest at Expert); level visibility per the ids delta; WCAG 2.2 AA (FR-030-FR-033)
- [ ] T040 [US1] [frontend] Shell: `library-job-indicator`, `library-maintenance` overlay (alertdialog, focus), polling of `getLibraryCareStatus` (2 s while a job runs, 10 s otherwise); help topics `library-care`, `backup-contents`, `backup-folder`, `backup-voice-data` (area `mixes`)
- [ ] T041 [US1] [qa] E2E `e2e/tests/library-care-backup.spec.ts` (fake stack): complete backup -> archive in the folder, manifest valid, list row; compact backup without detail stems; cancel; delete refused during a backup (`409` text visible) while an export runs; download via `backup-download-*`; axe on `/care`; level checks (Beginner hides options)

**Checkpoint**: HO:us1 for backend, frontend, qa.

---

## Phase 4: User Story 2 - Restore a backup (P1)

- [ ] T042 [P] [US2] [backend] Failing tests `ArchiveValidatorTests.cs`, `StagingExtractorTests.cs`: the hostile corpus H1-H17 (T012) each refused with its code in the order of data-model 3.4; extraction bounded by declared size; hashing while extracting; nothing outside staging touched (file-tree snapshot before/after, SC-004); reparse-point archive refused
- [ ] T043 [US2] [backend] Implement `Restore/{ArchiveValidator,StagingExtractor}.cs`, `Archive/{ArchiveReader,ArchiveEntryRules,ManifestRules}.cs` to pass T042
- [ ] T044 [P] [US2] [backend] Failing tests `StagedDatabaseTests.cs`: read-only checks, integrity, migration history vs app (newer -> `archive-incompatible`), migrate the 008 and 010 schema fixtures, stored path validation (H14), consent proof per profile (drop + detach "(profile not restored)"), `voiceProfiles: false` (FR-011, FR-014, FR-016, SC-008, SC-009)
- [ ] T045 [US2] [backend] Implement `Restore/StagedDatabase.cs` to pass T044
- [ ] T046 [P] [US2] [backend] Failing tests `LibrarySwitcherTests.cs`, `RestoreJournalTests.cs`: empty-library switch, replace (move aside, move in), crash injection after every journal step and start-up recovery (SC-005), maintenance mode entered only after staging is valid, pools cleared, previous library record, undo (roles swapped), discard with `DISCARD`, journal paths validated (FR-012, FR-013, FR-017)
- [ ] T047 [US2] [backend] Implement `Restore/{LibrarySwitcher,RestoreJournal,PreviousLibraryStore}.cs`, `Jobs/StartupRecovery.cs` to pass T046
- [ ] T048 [P] [US2] [backend] Failing tests `RestoreServiceTests.cs` + endpoint tests: empty-library definition, `confirmation-required`, `jobs-active` with `activeWork`, `previous-library-exists`, space check, reconcile non-terminal entities to "Interrupted by restore", report, cancel up to the switch, `not-cancellable` after; `restoreBackup`, `getPreviousLibrary`, `revertRestore`, `discardPreviousLibrary` (FR-012-FR-018) **011 claude-key** (owner Q2 2026-11-16, ciphertext kept): a replace restore on the same computer (same key ring) keeps the stored key readable (status `stored`); after a replace restore whose `AgentSecrets` row is unreadable with the current key ring (another computer), the key status is `stored-unreadable` and the restore report contains "The stored Claude key must be entered again" (011 follow-up CK-FR-017)
- [ ] T049 [US2] [backend] Implement `Restore/{RestoreService,RestoreReconciler}.cs` and the endpoints to pass T048
- [ ] T050 [P] [US2] [backend] Failing tests `WorkerCacheRehydratorTests.cs`: detail start and lyrics alignment on a worker cache miss -> rehydrate -> new `WorkerJobId` -> one retry; second failure surfaces the original error; stage text "Preparing stems" (FR-019)
- [ ] T051 [US2] [backend] Implement `Worker/WorkerCacheRehydrator.cs` and the hooks in `Detail/` and `Lyrics/` to pass T050
- [ ] T052 [US2] [frontend] bUnit tests first, then: restore dialog (empty library text, replace/merge radios by level, voice checkbox, space, blockers with links, typed `REPLACE`, error), previous-library panel (undo, discard dialog with `DISCARD`), restore report; help topics `backup-restore`, `backup-compatibility`
- [ ] T053 [US2] [qa] E2E `library-care-restore.spec.ts`: backup -> delete a track and a mix -> replace restore -> items back, stems play, mix opens; undo; discard; hostile archive (fixture copied into the temp backups folder) refused and the library unchanged; incompatible and older-schema archives shown with their state; restore blocked while an export runs; "Split further" after a worker-volume reset uses rehydration (fake worker)

**Checkpoint**: HO:us2 for backend, frontend, qa.

---

## Phase 5: User Story 3 - See where my disk space goes (P2)

- [ ] T054 [P] [US3] [backend] Failing tests `StorageOverviewServiceTests.cs`: sums per category equal the fixture bytes (0 deviation, SC-006), per-track rows incl. `recordingsBytes`, source attribution (pending upload + worker entry by `WorkerJobId`), unattached items, worker unavailable -> reasons, shared volumes grouping, 30 s cache and `refresh`, 500-track library under 2 s (FR-020, FR-021)
- [ ] T055 [US3] [backend] Implement `Storage/{StorageOverviewService,LibraryWalker,WorkerStorageAttribution}.cs`, `StorageEndpoints.getStorageOverview` to pass T054
- [ ] T056 [US3] [frontend] bUnit tests first, then: Storage tab (volumes as meters, categories with class badges and "how to regenerate", manage links for originals, per-track table with sortable headers and `aria-sort` at Advanced+, top-5 list at Beginner, unattached, model caches without delete, Expert worker entries, backups and previous library rows); help topics `storage-overview`, `storage-regenerable`
- [ ] T057 [US3] [qa] E2E `library-care-storage.spec.ts`: numbers equal `du -sb` inside the containers (helper via `docker compose exec`), worker stopped -> reason shown, sorting, links

**Checkpoint**: HO:us3 for backend, frontend, qa.

---

## Phase 6: User Story 4 - Free space safely (P2)

- [ ] T058 [P] [US4] [backend] Failing tests `CleanupPlannerTests.cs`, `CleanupExecutorTests.cs`: plans contain only regenerable items (property test with stems, exports, scores, takes, recordings and voice samples present: none planned, SC-007, SC-010), skips with reasons (`detail-active`, `export-active`, `job-active`, `worker-unavailable`), fingerprint and `plan-stale`, expiry `plan-expired`, refused during backup/restore, detail removal sets `FilesRemovedAt`, worker clean-up call, leftovers, storage cache invalidated (FR-022-FR-026)
- [ ] T059 [US4] [backend] Implement `Cleanup/*`, `previewCleanup`, `executeCleanup` to pass T058
- [ ] T060 [US4] [frontend] bUnit tests first, then: clean-up buttons and per-row action, clean-up dialog (items, skipped, total, confirm, result, stale/expired error with "Preview again"), `detail-files-removed` on the track page, `mixer-detail-removed` in the mixer (detail channels not loaded, parents play, settings kept); help topic `storage-cleanup`
- [ ] T061 [US4] [qa] E2E `library-care-cleanup.spec.ts`: remove detail stems -> freed bytes equal preview -> notices -> mixer plays parents -> Split further restores; clear worker cache -> Split further on another track works (rehydration); a stale plan is refused

**Checkpoint**: HO:us4 for backend, frontend, qa.

---

## Phase 7: User Story 5 - Merge a backup into my library (P3, cuttable)

- [ ] T062 [P] [US5] [backend] Failing tests `LibraryMergerTests.cs`: dependency order, same id -> skip, same source key -> skip with dependants, unique-name clashes renamed "(restored)", one transaction (failure leaves nothing), files moved after commit and completed by start-up recovery, report entries (US5 scenarios) **011 claude-key**: `AgentSecrets` rows of the archive are never imported by a merge, and the live row is never changed (011 follow-up CK-FR-017)
- [ ] T063 [US5] [backend] Implement `Restore/LibraryMerger.cs` and the `merge` mode to pass T062
- [ ] T064 [US5] [frontend] Merge option (Advanced+) and the skipped/renamed list in the report; bUnit tests first
- [ ] T065 [US5] [qa] E2E `library-care-merge.spec.ts`: library A + archive of B -> merged result as in the US5 independent test

**Checkpoint**: HO:us5 for backend, frontend, qa.

---

## Phase 8: Polish (inside the wave, per owner)

- [ ] T066 [P] [backend] [stem-separation] [frontend] [qa] [devops] [quality-gate] Each owner: refactor to SIG limits, XML docs / docstrings, accessibility review of every new dialog (frontend: keyboard-only and screen-reader walk-through of backup, restore, discard and clean-up), traceability FR/SC -> tests (qa), compose and CI validation with the backups mount (devops), `run_all.py` (quality-gate); HO:polish per owner (one task id, one hand-off per owner)

---

## Integration wave (CTO)

- [ ] T067 [CTO] Merge (after 012); T005 contract merge confirmed; full build, lint, unit + coverage gates; Playwright default suite + the five library-care specs on the fake stack, retries 0
- [ ] T068 [CTO] Quickstart V1-V8 and V10 on the fake stack (V3 with `docker compose down -v`)
- [ ] T069 [CTO] Security review against plan.md "Security review points" 1-10 (hostile corpus, banned APIs, stored paths, consent, secrets grep, download CORS, limits, maintenance, permissions); record findings in `docs/STATUS.md`
- [ ] T070 [CTO] Real-stack validation on the reference machine: V9 throughput (SC-002) for backup, verify and restore of a ~20 GB library to the bind-mounted folder; restore on a fresh set of volumes; record in `docs/STATUS.md`
- [ ] T071 [CTO] Docs: user manual chapter "Library care" (backups folder, backup options, restore modes, previous library, storage, clean-up, what is never removed, not encrypted, recordings once 015 lands), technical manual (03-deployment: backups mount and Linux owner; 07-data-model: `library-care/` and the archive format; 08-security: hostile archives, consent proofs; 11-operations: backup/restore runbook); README; `docs/THIRD-PARTY-LICENSES.md` unchanged (no new dependency); run `cd tools/docs && npm ci && npm run browser && npm run build`; commit sources, PDFs and `build-manifest.json`; `node tools/docs/check.mjs` passes (constitution 1.4.0, FR-036)
- [ ] T072 [CTO] `/speckit-analyze`; `docs/STATUS.md`; PR

---

## Execution plan per owner (parallel wave)

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| architect | T001 -> T002 (done) -> T003 -> T004 -> T006 -> T007 -> T008; T005 after 012 | `specs/013-library-care/**`, ADR-022, Contracts, worker `schemas.py`/`ports.py` | n/a |
| backend | T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> T022 -> T023 -> T024 -> HO:foundational -> T031 -> T032 -> T033 -> T034 -> T035 -> T036 -> T037 -> T038 -> HO:us1 -> T042 -> T043 -> T044 -> T045 -> T046 -> T047 -> T048 -> T049 -> T050 -> T051 -> HO:us2 -> T054 -> T055 -> HO:us3 -> T058 -> T059 -> HO:us4 -> T062 -> T063 -> HO:us5 -> T066 | Domain, Api, Persistence and their tests | stub worker handler, `FakeTimeProvider`, temp data and backups dirs, `ILibraryFiles`, `IJournalStepHook` |
| stem-separation | T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> HO:foundational -> T066 | worker `storage/`, `jobs/paths.py`, `api/storage.py`, tests | temp dirs, fake clock, fake `disk_usage` |
| frontend | T039 -> T040 -> HO:us1 -> T052 -> HO:us2 -> T056 -> HO:us3 -> T060 -> HO:us4 -> T064 -> HO:us5 -> T066 | `src/StemStudio.Web/**`, Web tests | fake `ILibraryCareClient`, bUnit |
| qa | T012 -> T041 -> HO:us1 -> T053 -> HO:us2 -> T057 -> HO:us3 -> T061 -> HO:us4 -> T065 -> HO:us5 -> T066 | `e2e/**`, `tests/fixtures/backup/**`, `docs/testing/**` | fake stack, temp backups folder |
| devops | T011 -> HO:foundational -> T066 | compose, scripts, CI, `.gitignore` | - |
| quality-gate | T009 -> T010 -> HO:foundational -> T066 | `tools/quality/**`, `docs/quality/**` | sample files |
| CTO | T067 -> T068 -> T069 -> T070 -> T071 -> T072 | as before | - |

Parallelism: within the backend lane the `[P]` test tasks of a phase can be written together; the
worker lane (T025-T030), devops (T011), quality-gate (T009-T010) and qa fixtures (T012) run fully in
parallel with backend Phase 2. Frontend starts US1 against the fake client as soon as T006 is done,
so it overlaps backend Phase 2/3.

**Task count per owner** (T066 counted once per owner): architect 8 (T001-T008), backend 37
(T013-T024, T031-T038, T042-T051, T054-T055, T058-T059, T062-T063, T066), stem-separation 7
(T025-T030, T066), frontend 7 (T039, T040, T052, T056, T060, T064, T066), qa 7 (T012, T041, T053,
T057, T061, T065, T066), devops 2 (T011, T066), quality-gate 3 (T009, T010, T066), CTO 6
(T067-T072). **72 task ids** (T001-T072); 77 owner assignments. US5 (merge) is 4 of them
(T062-T065) and can be cut.

## Dependencies & Execution Order

001-011 merged -> Phase 0 (T001-T004, T006-T008) -> wave (Phase 1-8) -> 012 merged -> T005 ->
Integration wave. US1 and US2 are the MVP; US3/US4 depend only on Phase 2; US5 depends on US2.

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component (API, Web, worker) (I)
- [ ] File-system, journal and worker seams; no test fills a disk or needs a GPU (I)
- [ ] SIG limits; `run_all.py` passes incl. the T009 rules (II)
- [ ] backend 1.12.0, worker 1.7.0, ui-test-ids 1.12.0 and backup-archive 1.0.0 implemented; contract tests pass (III)
- [ ] No new dependency; no model weights in archives; consent proofs enforced (IV)
- [ ] Limits documented in help and manuals: not encrypted, source audio not kept, worker cache rebuilt, model caches not removable (V)
- [ ] Hostile corpus refused; secrets never archived; security review T069 done
- [ ] Help topics present and linked; linters clean; public APIs documented
- [ ] docs/public user and technical manuals updated, PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.4.0)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer

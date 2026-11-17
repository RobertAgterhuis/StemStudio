# Hand-off: architect / 013 Phase 0 (ADR, spikes, contract merge, contract code, skeleton)

013 tasks T001, T003-T008 on branch `013-library-care` (from `main` 9ca2d13f, which includes 012).
T002 (contracts) was done with the design. Constitution 1.4.0 (the 013 docs said 1.3.1; updated).
The wave may start.

## Contract versions (binding for the wave)

| Contract | Version | Document |
|---|---|---|
| backend-api | **1.12.0** (= 012's 1.11.0 + 013 delta; `x-stemstudio-contains` ends `1.11.0, 1.12.0`) | `specs/013-library-care/contracts/backend-api.openapi.yaml` (full); delta `backend-api.delta.openapi.yaml` 1.12.0-delta.1 |
| ui-test-ids | **1.12.0** (= 1.11.0 + 013 delta) | `specs/013-library-care/contracts/ui-test-ids.md` (full) |
| worker-api | **1.7.0** | `specs/013-library-care/contracts/worker-api.openapi.yaml`; `schemas.py` `CONTRACT_VERSION = "1.7.0"` |
| backup-archive | **1.0.0** | `contracts/backup-archive.md`, `backup-manifest.schema.json` |
| audio-engine, generator, help-content, agent tools | unchanged | |

Checks: Redocly 2.78.0 valid (backend 1.12.0: 11 warnings = 1.11.0's 9 + 2 new, all
`operation-4xx-response`; delta 3, worker 4, all of the accepted kinds);
`python -I specs/013-library-care/contracts/check_superset.py` -> superset of 1.11.0 (only
`DetailPhase` and the nine gated paths grow) and, with the delta as `older`, every delta entry is
in 1.12.0 unchanged.

## What was done

- **T001**: ADR-022 number free (012 = ADR-021, 022/023 = ADR-036/037) -> **Accepted**, with a
  "Phase 0 outcome" section (P1-P4).
- **T003 spike** (research R-04 "Phase 0 results", R-12): Windows host and a throw-away Linux SDK
  container with a throw-away named volume (`/data`) and a bind mount on E: (`/backups`). Disk
  space checked first (C: 173 GB, E: 611 GB, Docker VM 754 GB), full sizes used. Owner's
  `stemstudio` stack, containers and volumes untouched; spike container and volume removed.
  - Zip64: 4.5 GiB stored entry and 70,000 entries written and read back, hashes equal, Zip64 EOCD
    present (Windows and Linux).
  - `BackupDatabase` x10 on 205 MB under a writer loop: **WAL** 0 failed writes (max write latency
    208 ms Windows / 88 ms Linux); **rollback journal: `BackupDatabase` itself throws `SQLITE_BUSY`**.
  - `Directory.Move` 12,000 files: 24 ms Windows, 0.1 ms Linux volume; fails with an open handle on
    Windows and on bind mounts; a pooled SQLite file cannot be moved on Windows until `ClearAllPools()`.
  - Throughput: bind mount 353-360 MB/s write, 213-386 MB/s read (SC-002 60 MB/s has a wide margin).
- **T004**: `tests/StemStudio.Api.Tests/LibraryCare/LayoutAllowListTests.cs` runs the real
  generators on a temp data dir; every path matches data-model 3.3 **unchanged** (binding result
  recorded in data-model 3.3).
- **T005**: full backend 1.12.0 and ui-test-ids 1.12.0 merged (merge rule in DELTA.md "Phase 0").
- **T006**: `src/StemStudio.Contracts/LibraryCare/*` (enums, `required init` records,
  `LibraryJobReportDto` polymorphic on `type`, `LibraryCareLimits`, `LibraryCareProblemTypes`,
  `ApiRoutes.LibraryCare.cs`), `DetailPhaseDto.FilesRemovedAt`; `SchemaSubsetValidator` moved to
  `tests/Shared/JsonSchema/` (+ `const`, `not`, `maximum`, `format` annotation, `additionalProperties: true`).
  Tests: `LibraryCareContractTests` (+ `.Samples`), `LibraryCareUiTestIdsTests`, `BackupManifestSchemaTests`.
- **T007** (worker): `schemas.py` 1.7.0 models (`VolumeUsage`, `CacheEntryUsage`, `ModelCacheUsage`,
  `WorkerStorage`, `StorageCleanupRequest`, `SkippedEntry`, `StorageCleanupResult`, `RehydrateRequest`,
  `RehydratedJob`, `MODEL_STEMS`, `ALL_CACHE_ENTRIES`), `ports.py` `StorageMeter`, `CacheJanitor`,
  `LibraryStemReader`, `LibraryUnavailableError`, `StemMissingError`; fakes in
  `worker/tests/storage/fakes.py`; `worker/tests/contract/test_storage_contract.py`.

## Decisions taken (binding; recorded in ADR-022 "Phase 0 outcome", research R-04, DELTA.md)

1. **WAL for snapshots** (backend T031/T032): `DatabaseSnapshot` logs a warning when the live
   database is not in WAL, retries `BackupDatabase` on SQLite 5/6 (200 ms .. 3.2 s, 5 attempts),
   then fails the job with `JobError` `io-error`. Add a test with a writer holding a write transaction.
2. **WAL after restore** (backend T046/T047): the archived database is `journal_mode=DELETE`; the
   switch sets `PRAGMA journal_mode=WAL` on the restored live database before the gate opens.
3. **Switch retries** (T046/T047): maintenance gate -> wait for in-flight requests ->
   `ClearAllPools()` -> each move retried 10 x 200 ms -> else `io-error` and journal rollback.
4. **Write-gate merge rule**: the ten gated operations carry `x-stemstudio-write-gate: backupRunning`
   and get `409 BackupRunning` / `503 Maintenance` only where they had no response with that code
   (existing `409 Conflict`/`AgentConflict`, `503 WorkerUnavailable` stay and then also carry the new type).
5. **`BackupDetails` corrected** (pre-release, label unchanged): a closed object instead of `allOf`
   over the closed `BackupSummary` (which no instance could satisfy). Same wire shape.
6. **`LibraryCareLimits`** instead of growing `ContractLimits` (same pattern as 012's `PracticeLimits`).
7. **`DetailPhaseDto.FilesRemovedAt`** is omitted from JSON when null, so every pre-1.12.0 example
   and stored phase serializes unchanged.
8. Worker: `["*"]` must be the only element; error names carry the `Error` suffix (ruff N818);
   fakes live in `worker/tests/storage/fakes.py` (tasks.md T007 wording corrected).

## What the lanes must know

- **backend**: remove ids from `PendingLibraryCareOperations`
  (`tests/StemStudio.Api.Tests/Contract/BackendContractTests.StemsAndMixes.cs`) as you serve them
  (T038, T049, T055, T059). T013/T014: implement data-model 3.3 as `LibraryLayout` and point
  `LayoutAllowListTests.Allowed` at it. Worker examples for T023 are in the linked 1.7.0 document
  (`Contract/worker-api.openapi.yaml`). `LibraryJobReportDto` must be written through
  `LibraryJobDto.Report` (base type) so the `type` discriminator is emitted. Use
  `SchemaSubsetValidator` (shared) for manifest tests (T033).
- **stem-separation**: implement against `ports.py`/`schemas.py` 1.7.0; `RehydrateRequest.stems_match_model()`
  gives the 422; `StorageCleanupRequest.all_entries` marks `["*"]`.
- **frontend**: DTOs in `StemStudio.Contracts.LibraryCare`, routes `ApiRoutes.LibraryCare*`/`Storage*`/`Backup*`,
  ids in `ui-test-ids.md` 1.12.0.
- **devops**: CI (`.github/workflows/ci.yml`) does not lint the 013 contracts yet; add
  `specs/013-library-care/contracts/{backend-api.openapi.yaml, backend-api.delta.openapi.yaml,
  worker-api.openapi.yaml}` with `contracts/redocly.yaml`, and `check_superset.py`.
- **CTO**: `PracticeUiTestIdsTests` was red on `main` (the 012 integration added three disclosure ids,
  902438c0, without updating the counts 26 -> 29); fixed here. `ClaudeKeyLeakTests.StoredSentinel_NeverLeaks`
  failed once in the full parallel run (5 s) and passed three times alone: timing flake under load,
  011 code, not touched.

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release --no-incremental        0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes               passed
dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true  (line / branch)
  Agent.Tests        726 passed   Agent         99.03 % / 94.18 %
  AgentEval.Tests    134 passed   AgentEval     99.83 % / 92.85 %
  Api.Tests         1810 passed, 2 skipped   Api 98.97 % / 92.06 %, Contracts 100 % / 100 %
  Domain.Tests       793 passed   Domain        98.98 % / 94.46 %
  Persistence.Tests  120 passed   Persistence   99.01 % / 95 %
  Web.Tests         2084 passed   Web           99.6 % / 93.71 %
worker: ruff check + format clean; mypy strict 180 files clean;
  pytest 2435 passed, 3 skipped (libcairo), coverage line 100 % (9665/9665), branch 100 % (1092/1092)
tools/quality/run_all.py: 0 errors -> exit 0 (warnings only; one new test-data initializer
  `Samples.Details` over the unit-size warning limit)
Redocly 2.78.0: all 013 documents valid; check_superset.py: superset yes (1.11.0 and delta)
npm (audio-engine, web-interop): unchanged by 013 Phase 0, not run here (node_modules absent in the worktree)
```

## Dependencies

None new (.NET, Python, npm). Licences unchanged.

## Files

- `docs/decisions/ADR-022-library-backup-and-storage-care.md`
- `specs/013-library-care/{plan, research, data-model, tasks, DELTA}.md`,
  `contracts/{backend-api.openapi.yaml (new), ui-test-ids.md (new), check_superset.py (new),
  backend-api.delta.openapi.yaml, redocly.yaml}`
- `src/StemStudio.Contracts/LibraryCare/**` (new), `Tracks/DetailPhaseDto.cs`, `StemStudio.Contracts.csproj`
- `tests/Shared/JsonSchema/SchemaSubsetValidator.cs` (moved), `tests/StemStudio.Web.Tests/{csproj, Help/HelpCatalogTests.cs, Skeleton/Feature004SkeletonTests.cs}`
- `tests/StemStudio.Api.Tests/{csproj, Contract/(LibraryCareContractTests(.Samples), LibraryCareUiTestIdsTests,
  BackupManifestSchemaTests, ContractJson, PracticeContractTests, PracticeUiTestIdsTests, LyricsContractTests,
  BackendContractTests(.StemsAndMixes)).cs, LibraryCare/LayoutAllowListTests.cs}`
- `worker/src/stemstudio_worker/{schemas, ports}.py`, `worker/tests/{contract/test_storage_contract.py, storage/*}`,
  worker contract tests repointed to 1.7.0

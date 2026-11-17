# Hand-off: backend / 013 security review and E2E findings

Branch `013-cto-security` (worktree `.claude/worktrees/013-sec`, from `013-library-care` 27512fd0). Not pushed or merged.
Test-first: each item has failing tests before the fix (the containment part of item 1 was written in the same step).

## Per item

| # | Finding | Result |
|---|---|---|
| 1 (High) | `StoredPaths` skipped `GenerationTake.VocalFilePath`; `TakeStorage.FullPath` was a plain `Path.Combine` | **Fixed.** `StoredPaths` checks take *and* vocal paths (path columns only, by projection; tables an older schema lacks are skipped). New `DataDirectory.ContainedPath`/`RequireContainedPath` (`GetFullPath` + `StartsWith(root + separator)`) used by `TakeStorage.FullPath`/`Find`, `VoiceProfileService` (sample download 404, sample store) and `ConversionStemWaiter` (conversion fails with `SampleMissing`, nothing is sent to the singer). `BackupSelection.GenerationFiles` archives `VocalFilePath` files (layout `generations/<id>/vocal-<n>.flac` is allowed), so converted vocals come back after a restore. |
| 2 (Medium) | Archived triggers/views survive and fire on EF writes | **Fixed with the CTO-decided rebuild** (third iteration, see below). Read-only check (restore and verify, `ArchivedDatabase.CheckReadOnlyAsync` + `KnownSchema`): any `sqlite_master` object other than table/index, any `pragma_table_list` entry other than an ordinary table (virtual, shadow, view) and any table no migration creates -> `archive-unsafe`, regardless of name (a view named `sqlite_stat1`, triggers named `sqlite_sequence` / `__EFMigrationsLock` are refused); unknown migration ids -> `archive-incompatible`. Restore then rebuilds (`DatabaseRebuild`): a fresh database is migrated with this app's migrations to the archive's level, rows are copied table by table (`ATTACH` + `INSERT INTO main."T" (cols) SELECT cols FROM archived."T"`, column lists from the fresh schema's `pragma_table_xinfo`, quoted, never archived DDL; foreign keys off during the copy), the rebuilt file replaces the staged one, then forward migration, stored paths, consent, reconcile and a final stored-path re-check run on it. Rows that do not fit their level -> `archive-corrupt`. All archived connections run `trusted_schema=OFF` and `SQLITE_DBCONFIG_DEFENSIVE` (`SQLitePCL.raw.sqlite3_db_config`). Verify runs the same read-only checks and stored paths but **not** the rebuild (it writes nothing; hostile DDL on known tables is harmless to a verify and is removed by every restore). |
| 3 | Verify did only `integrity_check` | **Fixed.** `VerifyRun` copies the database into its own staging folder and runs the same `ArchivedDatabase.CheckReadOnlyAsync` + `CheckStoredPathsAsync` as restore (no migration, nothing written outside staging). `h13b-foreign-database.zip` -> `archive-corrupt`, `h14-stored-path.zip` -> `archive-unsafe` for verify (`ok:false` + code) and restore. |
| 4 | `h17b-entry-count.zip` -> `io-error` | **Fixed.** `ZipArchive` opens lazily and throws `InvalidDataException` from `Entries`, outside the old `try`. New `ZipDirectory.DeclaredEntries` reads the EOCD/Zip64 count before the central directory is built (> 2,000,000 -> `archive-invalid`), and `InvalidDataException` from the directory is mapped to `archive-invalid`. Verify, restore (422 before a job) and the list (unrecognised) agree. |
| 5 | `format-2.zip` listed as `unrecognised` | **Fixed.** `ArchiveReader.ReadManifest` reads a loose `ManifestHeader` first and runs `ManifestRules.CheckFormat` (wrong/missing `format`/`formatVersion` -> invalid, other major -> incompatible) before the full deserialisation and rules. `BackupCatalog` describes such archives with `BackupDescription.OtherFormat`: `recognised: true`, `formatVersion`, `backupId`/`createdAt`/`appVersion` when readable, `compatibility.restorable: false` + reason -> web state `incompatible`; details answer 200; restore 422 `archive-incompatible`. |
| 6 (Low) | NRE on null members, one bad zip -> list 500, duplicate manifest paths -> `ArgumentException` | **Fixed.** `ManifestJson.Options`: `RespectNullableAnnotations` and `RespectRequiredConstructorParameters` (`ManifestEntry.Entity` got `= null`, the only optional positional member of the schema); `ManifestRules` refuses null list elements and duplicate entry paths (`archive-invalid`); `BackupCatalog.Read` maps any unexpected exception of one archive to an unrecognised row (not cached). |
| 7 (Low) | Unbounded manifest read | **Fixed.** The manifest is copied through `HashingLimitedStream` limited to 256 MiB (fails at + 1 byte) whatever the header declares. |
| 8 | `/API/...` bypassed the gate | **Fixed.** `StartsWithSegments("/api", OrdinalIgnoreCase)`; `IsAlwaysAllowed` is case-insensitive. |
| 9 | Switch ignored `WaitForIdleAsync == false` | **Fixed.** On timeout the journal is saved `RolledBack`, maintenance ends (finally) and the job fails `io-error` before any move. |
| 10 | Crash after `Committed` hid `previous/<id>` | **Fixed.** `RestoreJournalRecord.Previous` carries the previous library's record (set by `RestoreRun`/`UndoRun`, minimal diff); start-up `RestoreJournal.CompleteCommittedAsync` finishes committed journals idempotently (post-commit folder removal shared with the switcher as `FinishCommitted`, writes the missing `previous.json` with the size), then `UseWal`. |
| 11 | Expired clean-up plans never pruned | **Fixed.** Each preview deletes expired or unreadable plan files. |
| 12 (CSRF) | Body-less POSTs fireable cross-site | **Fixed.** `Security/CrossSiteRequestGuard` (after CORS, before the gate): non-GET/HEAD/OPTIONS under `/api` (case-insensitive) -> 403 problem when `Origin` is present and not `StemStudio:WebOrigin` (reuses `KeyEntryPolicy.IsAllowedOrigin`) or `Sec-Fetch-Site: cross-site`. No `Origin` passes (Blazor server, Playwright API, curl); preflights unchanged. The key-entry endpoints keep their own local-only problem (`OriginCheckedByEndpoint` metadata). |

## Decisions and deviations (for CTO-DECISIONS / STATUS)

- **Item 2 approach: rebuild from the migrations (CTO decision, supersedes two earlier iterations).** Iteration 1 refused
  triggers/views by a `LIKE` on `sqlite_master.sql`; iteration 2 compared the schema structurally with a migrated
  reference (`ReferenceSchema`). Review found bypasses in both (name-based ignores, CHECK compared by count, `WITHOUT
  ROWID`/`STRICT` not compared, normalised partial-index text). Reason for the rebuild: no archived DDL can reach the
  live library at all, so the check no longer has to recognise every hostile construct; the read-only type/table check
  stays as cheap defence in depth and is what verify runs. `ReferenceSchema` was removed. Restore and the (parallel)
  merge both go through `StagedDatabase.PrepareAsync`, so a merge built on it inherits the rebuild.
- The older-schema migration test now uses the committed `schema-010.zip` (a database built by the Up migrations of that
  level, as an older StemStudio writes it) instead of a Down-migrated copy.
- Verify of an archive whose last migration is unknown (`schema-newer.zip`) now reports `archive-incompatible` like
  restore (CTO row "verify and restore report the same code"); before it reported `ok: true`.
- CSRF 403 is a generic problem (`title` "Cross-site request refused"); backend-api 1.12.0 does not list 403 on every
  operation. **Contract fix request (architect):** document the 403 cross-site refusal for state-changing operations.
- Restore step 8 "every referenced file exists in staging" is still not checked (unchanged; missing files are 404s).

## Tests

New: `Storage/DataPathContainmentTests`, `LibraryCare/ZipDirectoryTests`, `LibraryCare/CommittedCorpusTests` (committed corpus linked as
`Fixtures/backup/*.zip` in the test csproj), `Security/CrossSiteRequestGuardTests`. Extended: `StagedDatabaseTests`
(vocal path; refused: trigger, odd-case triggers, view, comment-prefixed and named virtual tables, unknown table, a view
named `sqlite_stat1`, triggers named `__EFMigrationsLock` / `sqlite_sequence` via `writable_schema`; rebuilt away with
the schema equal to a freshly migrated library: unknown index, generated and default columns, expression / partial /
non-unique replacement indexes, an added CHECK; rows not fitting their level -> corrupt; identifier quoting),
`ArchiveValidatorTests` (duplicate path, null app/entry, no formatVersion, other format shape), `GateTests` (`/API`),
`LibrarySwitcherTests` (idle timeout, committed restore/undo recovery), `CleanupTests` (pruning), `BackupServiceTests`
and `RestoreServiceTests` (vocals archived and restored), `VoiceConversionTests` (sample outside the data root).

### Results (Release, this branch's head)

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true`: 2110 passed, 2 skipped, 0 failed.
  Coverage StemStudio.Api **98.56 % line, 90.78 % branch**, 98.83 % method (before: 90.98 % branch). Domain and
  Persistence were not changed. One earlier coverage run had `StorageOverviewTests.FiveHundredTracks_AreMeasuredQuickly`
  fail on its time budget (instrumented run, untouched code); it passed in the final run.
- `run_all.py`: 8 errors, all pre-existing in `src/StemStudio.Web` / `tests/StemStudio.Web.Tests` (frontend lane, files
  not touched here: `LibraryCareData`/`StorageData` parameters, `BackupsTab`/`StorageTab` and
  `CleanupDialog`/`RestoreDialog` using-block clones); none in backend paths (two clones this work introduced were fixed).
- Restore time with the rebuild (test host, Debug/Release on a desktop): `valid-base.zip` verify + restore about 1 s,
  `schema-008.zip` about 3 s and `schema-010.zip` about 4 s (these also migrate forward), the replace restore of the
  fixture library with undo record about 5 s (`RestoreServiceTests.Replace_BringsBackTheLibraryAndKeepsThePreviousOne`).
  The rebuild adds one migration of an empty database plus one `INSERT ... SELECT` per table.

## Dependencies

None new. `SQLitePCL.raw` is used through the existing Microsoft.Data.Sqlite dependency (SQLitePCLRaw, Apache-2.0).

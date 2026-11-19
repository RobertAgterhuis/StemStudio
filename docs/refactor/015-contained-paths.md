# 015 Resolve every stored data-relative path through one containment check
- Status: proposed · Date: 2026-11-19 · Area: Api / Storage and LibraryCare (backup, cleanup, restore, merge) · Priority: P2

## Problem statement
`DataDirectory.ContainedPath` / `RequireContainedPath` (`src/StemStudio.Api/Storage/DataDirectory.cs:25-44`) turns a stored relative
path into an absolute path and refuses any path that leaves the data folder. It uses `GetFullPath` plus a `StartsWith(root + separator)`
check, and its own doc comment calls it "defence in depth for paths read from the database". It has 10 call sites. Several
Library Care paths do the same join by hand with `Path.Combine(root, relative.Replace('/', ...))` and no containment check:

- `src/StemStudio.Api/LibraryCare/Backup/BackupSelection.cs:125` (`Source`). The paths come from database rows
  (`take.FilePath`, `take.VocalFilePath`, `profile.SamplePath`, lines 112-120), and the file is copied into the backup archive.
- `src/StemStudio.Api/LibraryCare/Cleanup/CleanupExecutor.cs:68`: `File.Delete(full)` on a path read from the stored cleanup plan
  JSON.
- `src/StemStudio.Api/LibraryCare/Cleanup/CleanupPlanner.cs:126` (`Describe`, file facts for the plan fingerprint).
- `src/StemStudio.Api/LibraryCare/Restore/LibraryMerger.cs:140`: the merge target. It is only safe by coincidence, because the source
  of the same relative path is checked against the staging folder by `StagingExtractor.Destination`
  (`ArchiveValidator.cs:98-106`).
- `src/StemStudio.Api/LibraryCare/Restore/RestoreJournal.cs:154` (`Full`, journal paths; partly protected by the `CarePath()` regex).

## Impact
- Security (defence in depth): a row that was changed, or merged in from a foreign backup archive, with `FilePath` set to
  `../../<somewhere>` makes the next backup copy a file from outside the library into the archive. A changed cleanup plan can make
  `CleanupExecutor` delete a file outside the data folder. In both cases the classifier or category check is the only barrier left.
- Maintainability: five hand-written copies of a security rule. Reviewers cannot see which ones are intended to be safe.

## Risk
- Leaving it: low likelihood, since it needs a crafted archive or local file tampering, but high impact (data exfiltration into a
  backup, deletion outside the library).
- The change: low. `RequireContainedPath` throws, so each call site must choose between skip-and-report (backup, cleanup) and fail
  the job (`LibraryJobException` with `archive-unsafe`, as the restore path already does). Rollback is per call site.

## Effort
S (1 day).

## Step-by-step plan
1. Safety net: tests per call site with a traversal path (`../outside.bin`, an absolute path, and a path with `..` in the middle).
   Backup selection skips the file and reports it, cleanup refuses to delete, merge fails with `archive-unsafe`.
2. Replace the five joins with `DataDirectory.ContainedPath` or `RequireContainedPath`. `LibraryCarePaths.Data` already exposes the
   `DataDirectory`. Check: step-1 tests green, and the Library Care tests and the backup fixture archives
   (`tests/fixtures/backup/archives`) unchanged.
3. Add a house rule to `tools/quality/library_care_rules.py`, or an analyzer test, that forbids
   `Path.Combine(<data root>, ... .Replace('/'` outside `DataDirectory`. That stops the pattern from coming back. Coordinate with the
   tools owner.
4. Verify: `grep -n "Path.Combine(.*Root, .*Replace('/'"` in `src/StemStudio.Api` finds only `DataDirectory.cs`, and a backup,
   restore and cleanup E2E round-trip passes. Roll back with a revert per call site.

## Dependencies and related proposals
Fits into security review 013. No contract change.

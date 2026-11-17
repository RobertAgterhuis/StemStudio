# Hand-off: backend / 013 US1 - back up the library (T031-T038)

## What was built

- `LibraryCare/Backup`: `DatabaseSnapshot` (online backup API, WAL warning, busy retries 200 ms .. 3.2 s then `io-error`,
  copy in `journal_mode=DELETE`, integrity check), `BackupScrubber` (excluded categories and incomplete consent with the
  domain rules; conversions detached "(profile not in backup)"), `BackupSelection` (files from the snapshot, layout-filtered,
  archive order), `BackupEstimator`/`BackupEstimates` (classified walk), `BackupFolder` (create, write probe, space),
  `BackupService` (folder, one job, space = estimate + 64 MiB, name `stemstudio-backup-<UTC>.zip`, `-2`, ... on a clash),
  `BackupRun`/`BackupArchiveWriter` (stages and weights of data-model 4.2, free-space floor, missing/changed warnings, check,
  rename), `BackupCatalog` (newest first by modification time, cache per name/size/mtime, compatibility), `VerifyRun`
  (structure, names, hashes, database integrity; a damaged archive completes with `ok: false` and the problem).
- `LibraryCare/Archive`: `ArchiveWriter` (store/deflate, hashing copy, central-directory check; Zip64 by `ZipArchive`, tested
  with 70,000 entries), `BackupManifest`, `ManifestBuilder`, `ManifestRules`, `ArchiveReader`, `HashingLimitedStream`, `ByteThrottle`.
- Endpoints (`LibraryCareEndpoints`): status, backups list/estimate/create/details/download (Range, ETag, 416)/delete (gated,
  `archive-in-use`)/verify, library jobs list/get/cancel; `LibraryCareProblems`.
- CTO/QA request: `StemStudio:LibraryCare:ThrottleBytesPerSecond` (default 0 = off) throttles backup, verify and restore
  archive streams (CTO-DECISIONS row).
- 011 claude-key rule: a test proves the archive holds the stored key only as ciphertext and nothing from the key ring.

## Contract readings (QA hand-offs on `013-lane-qa` checked)

Newest archive first: yes (file modification time). Rejection codes follow data-model 3.4 (a listed entry with a wrong size
is `archive-corrupt`, an unlisted entry `archive-unsafe`). `formatVersion` 2.0.0 gives `archive-incompatible` (checked before
the list rules). Verify does not check free space. No conflict with the contract found.

## Tests and coverage

```text
dotnet test tests/StemStudio.{Domain,Persistence,Api}.Tests -c Release -p:CollectCoverage=true   (final run of the lane)
  Domain.Tests       936 passed               Domain       98.86 % line / 95.13 % branch
  Persistence.Tests  121 passed               Persistence  99.01 % / 95 %
  Api.Tests         2029 passed, 2 skipped    Api          98.54 % / 90.98 %, Contracts 100 % / 100 %
dotnet build StemStudio.slnx: 0 warnings; dotnet format --verify-no-changes: clean
tools/quality/run_all.py: 0 errors, exit 0 (warnings only)
```

## Dependencies added

None.

## Docs impact

Backups folder (`StemStudio__BackupDir`, host hint), what a backup contains and leaves out, verify, download, delete refused
during a backup.

## Open issues / requests

- A file that changes while it is copied keeps its copied bytes with a `changed-after-snapshot` warning (no re-read; CTO row).

## Next owner

frontend (T039/T040), qa (T041).

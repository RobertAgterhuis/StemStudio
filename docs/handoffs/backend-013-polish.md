# Hand-off: backend / 013 polish (backend part of T066)

## What was done

SIG limits: `tools/quality/run_all.py` 0 errors (parameter objects for the backup run, the estimate query and the status
endpoint; complexity split in `ManifestRules` and `StorageAttribution`; shared `CategoryCounts`, `BackupDescription.Counts`,
`BackupSelection.CountAsync` instead of duplicated blocks). XML docs on every public type; `dotnet format` clean.

## Tests and coverage

```text
dotnet test tests/StemStudio.{Domain,Persistence,Api}.Tests -c Release -p:CollectCoverage=true
  Domain.Tests       936 passed               Domain       98.86 % line / 95.13 % branch
  Persistence.Tests  121 passed               Persistence  99.01 % / 95 %
  Api.Tests         2029 passed, 2 skipped    Api          98.54 % / 90.98 %, Contracts 100 % / 100 %
dotnet build StemStudio.slnx: 0 warnings, 0 errors
dotnet format StemStudio.slnx --verify-no-changes: clean
tools/quality/run_all.py: 0 errors -> exit 0 (982 warnings repo-wide; library-care units of 16-25 lines among them)
```

## Dependencies added

None (BCL `System.IO.Compression`, `Microsoft.Data.Sqlite` and EF Core were already referenced).

## Docs impact

User manual: backups folder, backup options and estimate, verify, download, restore (empty or replace with typed REPLACE),
previous library (undo, typed DISCARD), storage overview, clean-up (only regenerable data, never originals), rehydration
after a worker cache reset. Technical manual: `StemStudio__BackupDir`, `StemStudio__BackupHostDirHint`,
`StemStudio__LibraryCare__*` incl. `ThrottleBytesPerSecond` (e2e only), the `library-care/` folder, journal recovery at start-up.

## Open issues / requests

- US5 merge cut; "Preparing stems" stage text not shown during a rehydration.
- Api branch coverage is 90.98 % (gate 90 %); thin margin.
- For quality-gate T009 (a): the 001-011 folders reference only `LibraryCare.Gate` (endpoints, `Sync/PeriodicSyncService`,
  `Agent/AgentRunExecutor`); rehydration lives in `Worker/` and does not reference `LibraryCare`.

## Next owner

CTO (T067 integration).

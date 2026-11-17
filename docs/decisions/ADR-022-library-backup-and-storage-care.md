# ADR-022: Library backup, restore and storage care (feature 013)

- **Status**: **Accepted** (architect, Phase 0 T001, 2026-11-17; proposed 2026-11-16). Number
  check: 012 took ADR-021, 022/023 took ADR-036/037, ADR-023-ADR-035 belong to 014-021 and the
  011 claude-key follow-up; ADR-022 is free and stays with 013 (no renumbering). Constitution 1.4.0.
- **Context documents**: `specs/013-library-care/{spec.md, research.md, plan.md, data-model.md}`,
  `specs/013-library-care/contracts/*`, MoSCoW M4 and M5, ADR-004 (stem storage), ADR-009 (model
  weights), ADR-016 D5 (the API is the only writer of the library; read-only mount in the worker),
  ADR-019 D3 (consent).

## Context

Everything StemStudio makes lives in Docker volumes. Losing `api-data` loses hours of GPU work and
the user's own material (uploads, voice samples, generated takes, mixes). Detail stems,
generations and the worker cache also fill the disk quickly, and the owner wants a clean machine.
The library is large (tens to hundreds of GB) and must be backed up while jobs run, restored on a
new machine, and cleaned without ever losing originals. Archives may come from elsewhere and are
untrusted input. Voice profiles carry consent obligations (010).

## Decisions

### D1 The API makes the archive; the worker helps with numbers, its caches and rehydration

The API owns every library file and the database (ADR-016 D5), so backup and restore are API
library jobs. The worker (1.7.0) adds `GET /v1/storage`, `POST /v1/storage/cleanup` and
`POST /v1/jobs/rehydrate`. Worker data, generator/singer data, Data Protection keys and model
caches are never in an archive (model weights are never redistributed, ADR-009).

### D2 Consistency: online SQLite backup plus a write gate, no paused jobs

`SqliteConnection.BackupDatabase` takes a consistent snapshot (it briefly blocks writers;
the database is small). Files are selected from the snapshot. A write gate refuses operations that
delete or replace library files while a backup runs (`409 backup-running`); jobs keep running and
their new files are simply not in this backup. Pausing jobs was rejected (long GPU jobs).

### D3 Format: ZIP (Zip64) with a manifest; backup-archive 1.0.0

One ZIP per backup: `db/stemstudio.db`, `files/<data-relative path>`, `README.txt`,
`manifest.json` last (central directory gives random access). Audio and pictures stored,
text deflated. Manifest: versions, schema migrations, categories, counts, every entry's size and
SHA-256, voice-profile consent proofs, warnings. TAR (no index, link entries), 7z/zstd (extra native
dependency, no gain on FLAC) and plain folder copies were rejected. No encryption in 1.0.0
(`encryption: none` reserved); the UI says so.

### D4 Selective backups are self-consistent

Always: database, stems, artwork, pending uploads. Optional (default on): detail stems, exports,
scores, generations, recordings (015 play-along takes; reserved now), voice profiles. Excluded
categories are scrubbed from the archived database (e.g. detail phases marked
`filesRemovedAt`, assets deleted, profiles removed with the 010 detach rule), so a restore never
points at missing files.

### D5 Destination: a bind-mounted host folder; download, but no upload

Archives are written to `/backups`, bind-mounted from `${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}`,
as `.partial` until checked. The API serves a finished archive for download (Range, resumable).
Restoring needs the archive in the folder; browser uploads of tens of GB are not offered. A named
volume was rejected (it would be lost with the other volumes).

### D6 Restore: verify everything in staging, then a journalled switch

Archives are untrusted: entry-name allow-list (the generated layout), no traversal, absolute paths,
backslashes, links, duplicates or unlisted entries, size-bounded extraction with hashing (zip
bombs), SQLite `integrity_check`, migration-history check, validation of stored path columns,
consent proofs. Extraction goes into `library-care/staging/` on the data volume; nothing else is
touched until everything passed. An older schema is migrated in staging; a newer one is refused.
The switch is a sequence of same-volume renames recorded in a journal, with a few seconds of
maintenance mode (`503 library-maintenance`); start-up recovery completes or rolls back an
unfinished switch. Replace keeps the previous library (undo, discard with typed confirmation);
merge (P3) adds missing items in one transaction. Restores need an idle library.

### D7 Consent proofs travel with voice profiles and are checked on restore

A profile is archived only with its complete consent record, repeated in the manifest. On restore
the proof must match an embedded statement text by version and SHA-256; otherwise the profile is
dropped and its takes kept with a name snapshot. **Binding for later features**: embedded consent
statement versions are never removed, so old proofs stay verifiable.

### D8 Clean-up removes only regenerable data, in two steps

Clean-up kinds: detail stems (Split further restores them; instant while the worker cache has the
result), the worker caches (rebuilt from library stems by rehydration), leftovers (temp files,
orphans, finished uploads, worker scratch, old job files). Originals are never offered: stems,
artwork, exports, scores, generation takes, recordings, voice samples, the database, backups, the
previous library. Preview returns a plan with a fingerprint; execution refuses expired or stale
plans. Model caches are shown but not removable in 013 (the generator and singer may hold them).

### D9 State outside the database

Library jobs, the restore journal, the previous-library record and clean-up plans are JSON files in
`<data>/library-care/` (they must survive a database swap). No database migration in 013; the
detail phase gains the additive JSON field `filesRemovedAt`.

### D10 Rehydration of the worker cache

Detail and alignment jobs read stems from the worker cache. After a restore or a cache clean-up the
API calls `POST /v1/jobs/rehydrate` on the first cache miss and retries once; the worker copies the
library stems from its read-only `/library` mount into a new cache entry.

## Consequences

- Positive: one-click, verifiable backups that survive volume loss; safe restores on new machines;
  a clear picture of disk use; reclaiming tens of GB without risk to originals; no new dependency.
- Negative: full archives only (no incremental); archives are unencrypted; a replace restore needs
  free space equal to the archive's size (the previous library stays until discarded); short
  maintenance window; the write gate refuses deletes during a backup.
- Follow-ups (backlog): encrypted archives, scheduled backups with retention, model-cache removal
  coordinated across containers, agent tools for backup and storage (tool catalogue minor).

## Phase 0 outcome (T001/T003, 2026-11-17)

Spike results: `specs/013-library-care/research.md` R-04 "Phase 0 results" and R-12. D1-D10 stand
unchanged; the spike adds four binding refinements (no contract change):

- **P1 Zip64** works on .NET 10 for a 4.5 GiB entry and 70,000 entries on Windows and Linux; no
  writer switch is needed.
- **P2 WAL for snapshots**: `BackupDatabase` fails with `SQLITE_BUSY` under a concurrent writer when
  the live database uses a rollback journal; with WAL (EF Core's default for databases it creates)
  10 snapshots under a writer loop caused 0 failed writes. `DatabaseSnapshot` checks the journal
  mode and retries busy snapshots with back-off (job error `io-error` after 5 attempts).
- **P3 WAL after a restore**: the archived database is a single file (`journal_mode=DELETE`); the
  switch sets `PRAGMA journal_mode=WAL` on the restored live database before the gate opens.
- **P4 Switch retries**: renames inside the named volume are O(1); on Windows (non-Docker path) and
  on bind mounts an open handle blocks a directory move, so the switch clears the SQLite pools,
  waits for in-flight requests and retries each move (10 x 200 ms) before rolling back.
- Bind-mount throughput 353-360 MB/s write, 213-386 MB/s read: SC-002 (>= 60 MB/s) has a wide
  margin and is not changed.

# Research: Library Care (feature 013)

Architect, 2026-11-16. Inputs: the current code (`src/StemStudio.Api/Storage`, `src/StemStudio.Persistence`,
`worker/src/stemstudio_worker/jobs/paths.py`), `docker-compose*.yml`, ADR-004, ADR-016, ADR-018,
ADR-019, specs 001-011. Decisions are summarised in
[ADR-022](../../docs/decisions/ADR-022-library-backup-and-storage-care.md).

## R-01 What is "the library" and where does it live

Inventory of the volumes (compose files on `main` at 5ae61cf):

| Volume (mount) | Owner (writer) | Content | In a backup? |
|---|---|---|---|
| `api-data` (`/data` in api; `/library:ro` in worker) | API | `stemstudio.db` (+ `-wal`, `-shm`), `tracks/<id>/stems/<stem>.flac`, `tracks/<id>/detail/<parent>/<name>.flac`, `tracks/<id>/artwork/{auto,user}/{cover,thumb}.jpg`, `assets/<id>/<file>` (exports, stem packs, scores), `generations/<id>/...` (takes, converted vocals), `voices/<id>/sample.flac`, `uploads/<sha256><ext>` (kept until the track completes) | **yes** (the library) |
| `worker-data` (`/data` in worker) | worker | `jobs/`, `detail-jobs/`, `metadata-jobs/`, `render-jobs/`, `lyrics-jobs/`, `transcription-jobs/`, `cache/by-source/*.json`, `cache/by-audio/<sha>/{result.json, stems/, detail/, detail.partial/, artwork/}`, `work/`, `uploads/`, `artwork/` | no (cache and scratch) |
| `model-cache` (`/models` in worker, generator, singer) | worker, generator, singer | Demucs (`torch/`), detail models, Whisper, basic-pitch/piano, ACE-Step (`acestep/`), SoulX + RMVPE + whisper-base (`soulx/`), `hf/` | no (downloaded again at first use; ADR-009 says weights are never redistributed) |
| `generator-data`, `singer-data` | generator, singer | job JSON and scratch | no |
| `web-keys` | web | ASP.NET Data Protection keys | no (protect circuits/antiforgery only; nothing persisted depends on them) |
| `gpu-lease` | worker, generator, singer | lock file | no |

Consequences:

- The **API** can produce a complete backup alone: it is the only writer of `api-data` (ADR-016 D5)
  and the database lives there. No worker job is needed for the archive itself (the MoSCoW note
  "API + worker job" is refined: the worker contributes storage numbers, cache clean-up and
  rehydration, worker-api 1.7.0).
- StemStudio does **not keep the source audio** of a finished track: uploads are deleted when the
  track completes (`TrackJobApplier.DeleteUpload`), and the worker deletes `work/<job>` when a job
  is terminal. The only remaining copies of source-derived audio are the API stems and the worker's
  `cache/by-audio/<sha>/stems`. "Source" in the storage overview therefore means "pending upload"
  (API) plus "worker separation cache" (attributed through `Track.WorkerJobId` ->
  `jobs/<id>.json` -> `audioSha256`).
- Consequence for regenerability: **stems are originals**. A URL track could be re-imported, but the
  video may be gone and the separation costs GPU time; an upload track cannot be re-made without the
  user's file. Stems are therefore never offered by the clean-up and are always in a backup.

## R-02 Consistent database snapshot

| Option | Consistency | Effect on the running app | Notes |
|---|---|---|---|
| Copy `stemstudio.db` (+ WAL) while running | not guaranteed (torn pages, WAL not checkpointed) | none | rejected |
| `VACUUM INTO '<file>'` | consistent (one read transaction) | readers and writers continue (WAL) | compacts; needs a SQL string with a path (built from a generated temp name only) |
| `SqliteConnection.BackupDatabase(destination)` (SQLite online backup API) | consistent | "backs up the database as quickly as possible and blocks other connections from writing" ([Microsoft Learn: Online backup](https://learn.microsoft.com/dotnet/standard/data/sqlite/backup)) | first-class API of Microsoft.Data.Sqlite, no SQL string building |

**Decision**: `BackupDatabase` into `data/library-care/staging/<jobId>/stemstudio.db`. The database is
small (a library of 500 tracks is a few MB; the big data is in files), so the write block lasts
milliseconds. EF Core writers that hit the lock wait on SQLite's busy timeout (default 30 s in
Microsoft.Data.Sqlite's command timeout), so no request fails. The staged copy is then **scrubbed**
for excluded categories (R-04) with a second `StemStudioDbContext` on that file, and `PRAGMA
integrity_check` is run on it before it is written into the archive.

## R-03 Files consistent with the snapshot, without pausing jobs

Library files are written atomically (temp name + rename: `AtomicFile`), and folders are keyed by
generated ids. After the snapshot three kinds of change can happen:

1. **New files** of jobs that finish later (new stems, exports, takes): harmless, the snapshot does
   not reference them and the backup selects files *from the snapshot* (R-04).
2. **Deleted files** (track delete, asset delete, clean-up, voice-profile delete): would make the
   archive reference missing files. -> **write gate**: these operations are refused with `409
   backup-running` while a backup runs.
3. **Replaced files** (own artwork upload or reset rewrites `artwork/user/*`; a restyle retry may
   rewrite a take of a failed generation): also behind the gate (artwork) or not referenced (a
   failed generation has no takes in the snapshot).

Pausing all jobs was rejected: a style-transfer or song job can run for many minutes on the GPU and
cannot be paused safely; a backup must not wait for or break them. A full "maintenance mode" is
only needed for the short switch step of a restore (R-07).

Files referenced by **non-terminal** entities in the snapshot (a track still separating) are not
copied, except its pending upload (so a restored upload track can be retried). Those entities are
failed "Interrupted by restore" after a restore (FR-015).

## R-04 Archive format

| Option | Pros | Cons |
|---|---|---|
| **ZIP (Zip64)** via `System.IO.Compression` | central directory (list and seek without reading everything), per-entry store or deflate, built into .NET and every OS, no link entry type | 4 GiB/65,535 limits need Zip64 (automatic in .NET for large archives); no built-in encryption |
| TAR (`System.Formats.Tar`) or tar.gz | streaming, PAX for large files | no index (reading the manifest of a 50 GB archive means scanning or putting it first, which needs all hashes before writing); symbolic and hard link entries are an extra attack surface ([Microsoft Learn: ZIP and TAR best practices](https://learn.microsoft.com/dotnet/standard/io/zip-tar-best-practices)); not opened by Windows Explorer |
| tar.zst / 7z | better compression | extra native dependency; audio is FLAC/MP3 and does not compress further anyway |
| Folder copy (no archive) | simplest | not "one archive"; hard to verify, move or download |

**Decision**: ZIP. Audio (`.flac`, `.wav`, `.mp3`), pictures and PDFs are stored
(`CompressionLevel.NoCompression`: deflate on FLAC gains < 1 % and costs CPU); the database, JSON,
MusicXML, MIDI and text are deflated (`CompressionLevel.Fastest`). `manifest.json` is the **last**
entry: it needs every file's SHA-256, which is computed while the file is copied (one read per
file). Readers find it through the central directory. Phase 0 spike T003 confirms on .NET 10 that a
> 4 GiB entry and > 65,535 entries are written as Zip64 and read back (`ZipArchive` in `Create`
mode on a seekable `FileStream`).

**Layout**: `files/` + the data-relative path, so restore maps an entry straight to its target and
the allow-list is the existing generated layout (data-model 3.3). `README.txt` explains the content
for a person who opens the archive.

**Selective backup** removes rows of excluded categories from the **staged** database (scrub), so a
restored library never points at missing files:

| Excluded | Scrub on the staged database |
|---|---|
| `detailStems` | completed detail phases get `removedAt = snapshot time`, per-parent stems list kept (so "Split further" restores the same names); files not copied |
| `exports` | `Assets` of kind `Audio` whose provenance origin is neither a generation nor `recording` (audio exports, stem packs) deleted |
| `scores` | `Assets` of kind `Score` deleted |
| `generations` | `Generations` and their takes deleted; library items whose files live under `generations/` deleted; tracks imported from takes are kept (they own their stems) |
| `recordings` (015) | `Assets` of kind `Audio` with provenance origin `recording` deleted (their files live under `assets/<id>/`); latency calibrations stay (database) |
| `voiceProfiles` | `VoiceProfiles` deleted; conversions detached with the name snapshot "<name> (profile not in backup)" (010 detach rule) |

## R-05 Where the archive goes (multi-GB)

A library with detail stems is easily 20-100 GB (a 4-minute song: 6 stems about 150 MB FLAC, 16
detail stems about 400 MB). Options:

| Option | Verdict |
|---|---|
| Download through Blazor Server (SignalR) | rejected: not built for GB streams; ties the circuit up for an hour |
| Stream a ZIP directly in an HTTP response from the API (no file) | rejected as primary: a broken download means starting again; no list of past backups; restore needs an upload of the same size |
| Upload an archive through the browser for restore | rejected: the API's request limit is 200 MiB on purpose (001); tens of GB uploads through Kestrel and a browser tab are fragile |
| Named Docker volume `backups` | rejected: the point is to survive the loss of Docker volumes |
| **Host folder bind-mounted at `/backups`** | **chosen**: survives `docker volume rm` and Docker Desktop resets; the user sees the files in Explorer and can copy them to an external drive; restore = put the file there |

A finished archive is also **downloadable** from the API (`GET /api/backups/{fileName}/file`, Range
and ETag, the 001 file-serving code path), so a user without access to the host folder (e.g. a
browser on another machine on a LAN, future C4) still gets the file.

Compose (devops): `api.volumes += ${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}:/backups`,
`StemStudio__BackupDir=/backups`. On Docker Desktop for Windows the folder is created by Docker if
missing and is writable. On Linux hosts the folder must be writable for the API user (uid 1654 in
the .NET `aspnet` image): `mkdir -p backups && sudo chown 1654:1654 backups` (quickstart). The API
probes writability at start and on every Backups request (create and delete
`.stemstudio-write-test-<guid>`), and reports `available: false` with the reason.

Non-Docker path: `StemStudio:BackupDir` defaults to `../../data/backups` relative to the content
root (next to the default data directory).

## R-06 Hostile and damaged archives

Restore treats the archive as untrusted input (it may come from another machine, be damaged on a
USB stick, or be crafted). Rules follow the Microsoft guidance "Don't use `ExtractToDirectory` for
untrusted input; iterate entries manually; validate destination paths with `Path.GetFullPath()` +
`StartsWith()` on a directory path ending in a separator; validate names against an application
policy; enforce decompression limits" ([Microsoft Learn: Best practices for working with ZIP and TAR archives](https://learn.microsoft.com/dotnet/standard/io/zip-tar-best-practices#handle-untrusted-archives-safely)).

Test corpus (SC-004, qa fixture generator `tests/fixtures/backup/make_hostile_archives.py`, each a
small ZIP):

| # | Vector | Expected |
|---|---|---|
| H1 | entry `files/../../etc/passwd` | `archive-unsafe` (traversal) |
| H2 | absolute entry `/data/stemstudio.db`, and `C:\x` | `archive-unsafe` |
| H3 | backslash separators `files\tracks\...` | `archive-unsafe` |
| H4 | entry with control characters or > 260 characters | `archive-unsafe` |
| H5 | entry with Unix symlink mode in external attributes | `archive-unsafe` (link) |
| H6 | duplicate entry names (two central-directory records) | `archive-unsafe` |
| H7 | entry not listed in the manifest | `archive-unsafe` |
| H8 | manifest entry missing from the archive | `archive-corrupt` |
| H9 | entry whose decompressed size exceeds the manifest size (zip bomb) | extraction stops at declared size + 1 byte -> `archive-corrupt` |
| H10 | wrong SHA-256 (one flipped byte) | `archive-corrupt` |
| H11 | entry that matches no layout pattern (`files/tracks/<id>/run.sh`) | `archive-unsafe` |
| H12 | manifest that fails the JSON Schema, or `format` not `stemstudio-backup` | `archive-invalid` |
| H13 | database failing `PRAGMA integrity_check`, or not a StemStudio schema | `archive-corrupt` |
| H14 | database with a stored relative path outside the layout (`VoiceProfiles.SamplePath = '../../x'`) | `archive-unsafe` |
| H15 | voice profile with a wrong consent statement SHA-256 | profile not restored (warning), restore continues |
| H16 | manifest declaring more total bytes than the free space | `insufficient-disk-space` before extraction |
| H17 | more than 2,000,000 entries or a manifest > 256 MiB | `archive-invalid` |

The staged database is opened with `Mode=ReadOnly` for the checks, `Pooling=False`, and is never
`ATTACH`ed to the live database; no SQL from the archive is executed (the database is data).

## R-07 Restore modes and the switch

**Empty library** = no rows in `Tracks`, `Mixes`, `Assets`, `Generations`, `LyricsSheets`,
`VoiceProfiles`, no user styles (`Styles` with `Kind = User`), and no agent conversations
(011). Then a restore is a switch without moving anything aside.

**Replace**: the current library is moved aside, not deleted, by `rename` on the same volume
(constant time, no extra space for the old library): `stemstudio.db*`, `tracks/`, `assets/`,
`generations/`, `voices/`, `uploads/` -> `library-care/previous/<restoreId>/`. Then the staged,
verified, migrated library in `library-care/staging/<restoreId>/` is renamed into place. Both
directories are under `/data`, so renames never cross a file system. Space needed: the archive's
uncompressed size (staging) + 256 MiB; the previous library costs nothing extra until it is
discarded. "Undo restore" swaps back with the same journal.

**Journal** (`library-care/journal/<restoreId>.json`, written with write-through + rename): steps
`staged` -> `gate-closed` -> `moved-aside:<n>` (one record per moved item) -> `moved-in:<n>` ->
`committed`. Start-up recovery: before `committed`, roll back the recorded moves in reverse order;
after `committed`, finish the post-steps (reconcile, report). Each rename is idempotent to replay
(source missing and target present = done).

**Maintenance mode**: while the gate is closed (seconds), endpoints answer `503
library-maintenance` + `Retry-After: 5`; the five synchronisers (tracks, detail, assets, lyrics,
generations) and the agent run executor check the gate and skip their tick; EF connections are
closed (`SqliteConnection.ClearAllPools()`) before the database file is renamed.

**Merge (P3)**: entity-by-entity in a single transaction on the live database, from the staged
database, in dependency order (artists, styles, lyrics sheets, voice profiles, tracks, mixes,
assets, generations, audio hashes, agent conversations). Rules: same primary key exists -> skip;
same `Track.SourceKey` with another id -> skip with dependants; unique names -> 007 copy-name rule
with the suffix "(restored)" (a `CopyNames.Next` variant with a suffix parameter). Files of added entities are moved from staging into place after the commit; a
failure before commit leaves nothing; a failure while moving files after commit is completed from
the journal at start-up (files are idempotent moves).

## R-08 Schema versions and migrations

The archive's `schema.lastMigration` is the last row of `__EFMigrationsHistory` in the snapshot,
`schema.migrations` the full list. Restore:

- every archive migration id must be in the app's migration list (`context.Database.GetMigrations()`);
  otherwise the archive is from a newer StemStudio -> `archive-incompatible` "made by a newer
  StemStudio (<appVersion>); update StemStudio first";
- if the archive's last migration is older, `Database.MigrateAsync()` runs on a context bound to the
  **staged** file before the switch. A migration failure leaves the live library untouched;
- format: `formatVersion` major must be 1; a higher minor is accepted (additive fields are ignored).

Fixtures: qa generates archives at the 008 and 010 schemas from the committed migrations
(`dotnet ef migrations script` is not needed: the test creates a DB with `Migrate(targetMigration)`
and fills it through SQL inserts of the fixture rows).

## R-09 Voice profiles and consent (010 rules)

010 stores the consent record in the `VoiceProfiles.Consent` JSON column (`statementVersion`,
`statementSha256`, `personName`, `consentedAt`), the sample path in `VoiceProfiles.SamplePath`
(relative, `voices/<id>/<file>.flac`) and its hashes in `SamplePcmSha256`/`SampleFileSha256`. ADR-019 D3 requires that no profile exists without consent. For 013:

- **Backup**: a profile is archived only when its consent record is complete and its sample file
  exists; `manifest.voiceProfiles[]` repeats the proof (id, display name, statement version and
  SHA-256, person name, time, sample path and SHA-256), so the proof can be read without opening the
  database. A profile without a complete record (should not exist) is left out with a warning and
  scrubbed from the staged database.
- **Restore**: for each profile, `ConsentStatement.Known(version)` must return the embedded text
  whose SHA-256 equals the stored hash. 010 embeds `consent.v1` only; when a later feature adds
  `consent.v2`, it MUST keep the v1 text embedded (recorded in ADR-022 as a binding rule for future
  features) so old proofs stay verifiable. A failing profile is dropped (row deleted in staging,
  sample not extracted), conversions detached with "<name> (profile not restored)".
- The archive is **not encrypted**. A sample is biometric-like personal data; the dialog says so,
  and the help topic `backup-voice-data` recommends an encrypted drive (BitLocker, LUKS, FileVault).
  Encrypted ZIP (AES) would need a third-party library (e.g. SharpZipLib, MIT) plus password
  handling; deferred (R-11).

## R-10 Storage overview and the worker cache

**Measuring**: a walk of `api-data` (`DirectoryInfo.EnumerateFiles` with `RecurseSubdirectories`,
`AttributesToSkip = ReparsePoint`) classifies each file by the layout patterns (data-model 3.3).
500 tracks x about 40 files = 20,000 `stat` calls, well below a second on an SSD; the result is
cached for 30 s (`?refresh=true` bypasses). Asset sizes are also in the database (`SizeBytes`), but
the walk is authoritative (SC-006).

**Free space**: `DriveInfo(path).AvailableFreeSpace/TotalSize` for `/data` and `/backups` in the API;
`shutil.disk_usage` for `/data` and `/models` in the worker. On Docker Desktop all named volumes
share the VM's virtual disk, so their free numbers are equal; the UI groups mounts with the same
total and free into one "Docker disk" line (the bind-mounted backups folder shows the Windows
drive).

**Worker cache attribution**: `GET /v1/storage` lists cache entries by `audioSha256` with the
standard job ids that point at them (from `jobs/*.json`). The API maps `Track.WorkerJobId` to the
entry. Entries without a track ("orphans", e.g. after a track delete) are shown as leftovers.

**Why rehydration is needed**: the worker's detail jobs (002) and lyrics alignment (006) read the
parent/vocal stems from its own cache (`cache/by-audio/<sha>/stems`), referenced by the standard
job id. After a restore on a new machine, or after "Clear the worker cache", that cache is gone and
`POST /v1/detail-jobs` answers `409` ("stems are no longer in the worker cache"). The worker already
mounts the API library read-only at `/library` (007, `STEMSTUDIO_LIBRARY_DIR`), so worker-api 1.7.0
adds `POST /v1/jobs/rehydrate`: the API sends the track id, source key/type, model and stem list;
the worker copies `/library/tracks/<id>/stems/<stem>.flac` into a new cache entry (key: SHA-256 of
`"library:" + the sorted stem file hashes`), writes a completed standard job file and returns its
job id, which the API stores in `Track.WorkerJobId`. The API calls it lazily (FR-019): on the first
cache-miss answer, then retries the original request once. Copy cost: about 150 MB per track, a
few seconds.

## R-11 Out of scope (backlog)

| Item | Why not now |
|---|---|
| Encrypted archives | needs a third-party AES-ZIP writer and password UX; drive encryption covers the risk |
| Scheduled / automatic backups | needs a scheduler and retention rules; manual backups first (YAGNI) |
| Incremental backups | content-addressed chunk store is a different design; full archives are simple and verifiable |
| Cloud targets | W2/W5 (no services); users can sync the backups folder themselves |
| Removing model caches | the generator and singer may have weights memory-mapped; deleting under them breaks running jobs, and coordinating three containers is not worth it for data that re-downloads; the overview shows sizes and the manual `docker volume rm stemstudio_model-cache` route in help |
| Archive upload through the browser | sizes (R-05) |
| Agent tools for backup/storage | later agent tool-catalogue minor (roadmap note) |

## R-12 Throughput estimate (SC-002)

Per file: read (NVMe inside the Docker VM, > 1 GB/s), SHA-256 (SHA-NI on the Core Ultra 9: about
2 GB/s per core), ZIP store (memcpy), write to the bind mount. The bind mount from WSL2 into a
Windows folder is the bottleneck: measured figures for Docker Desktop file sharing are typically
100-300 MB/s for large sequential writes. SC-002 sets a conservative >= 60 MB/s; the CTO measures
on the reference machine (quickstart V9). Buffer: 1 MiB `FileStream` buffers, `FileOptions.SequentialScan`,
async I/O; one file at a time (ordered manifest, simple cancel).

## R-13 Throughput of restore and verify

Verify reads the whole archive once (hash). Restore = verify + extract (one more read, one write
into staging on `/data`) + renames. To avoid reading twice, restore verifies **while extracting**
into staging (hash the decompressed stream, compare at the end of each entry) and aborts on the
first mismatch; nothing outside staging is touched until every entry passed. "Verify" alone exists
for checking an archive without restoring it.

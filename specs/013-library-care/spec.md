# Feature Specification: Library Care (Backup, Restore, Storage Overview and Clean-up)

**Feature Branch**: `013-library-care`

**Created**: 2026-11-16

**Status**: Draft (architect, 2026-11-16)

**Input**: MoSCoW backlog items **M4** ("Backup and restore of the whole library (database, stems,
mixes, lyrics, scores, styles, voice profiles) as one archive. Everything lives in Docker volumes
today; losing a volume loses hours of GPU work. API + worker job; restore checks versions. Voice
profiles stay subject to consent rules.") and **M5** ("Storage overview and clean-up: disk use per
track (stems, detail stems, exports, generations) and per model cache, with safe 'remove derived
files' actions. Detail stems and generations grow fast; the owner wants a clean machine."), roadmap
feature 013 ([docs/ROADMAP.md](../../docs/ROADMAP.md): backend-api 1.12.0, worker-api 1.7.0,
ui-test-ids 1.12.0, new backup-archive format 1.0.0), CTO brief of 2026-11-16.

## Clarifications

### Session 2026-11-16

Answered by the architect from the CTO brief, the current storage layout and the research in
[research.md](research.md); recorded in [ADR-022](../../docs/decisions/ADR-022-library-backup-and-storage-care.md).

- Q: Who makes the archive, the API or the worker? -> A: the **API**. Everything that belongs to the
  library (SQLite database, stems, detail stems, artwork, exports, scores, generation takes, voice
  samples, pending uploads) lives in the API data volume, and the API is its only writer (ADR-016
  D5). The worker only holds caches and job scratch, which are never part of a backup. The worker
  is involved for the storage overview (its caches and the model caches it can see), clean-up of
  its caches, and "rehydration" of its separation cache from library stems after a restore or a
  clean-up (worker-api 1.7.0).
- Q: How is the backup consistent while the app runs? -> A: an online SQLite snapshot with the
  SQLite backup API (`SqliteConnection.BackupDatabase`), then the files that the snapshot
  references. During a backup every operation that deletes or replaces library files is refused
  with `409 backup-running` (track, asset, generation, voice-profile, style and lyrics deletes,
  own-artwork replace/reset, clean-up, restore). Jobs keep running: they only add new files, which
  the snapshot does not reference. No job is paused.
- Q: Archive format? -> A: one **ZIP (Zip64)** file, `stemstudio-backup-<yyyyMMdd-HHmmss>.zip`,
  with `db/stemstudio.db`, `files/<data-relative path>` and `manifest.json` (format
  `stemstudio-backup` 1.0.0, app and schema versions, every entry with size and SHA-256, the consent
  proof of every voice profile). Audio and pictures are stored uncompressed, text and the database
  deflated. ZIP was chosen over TAR because it has a central directory (fast listing, random
  access to the manifest), no link entries, and opens on every OS without extra tools.
- Q: Selective backup? -> A: always included: the database, stems, artwork and pending uploads.
  Optional (all on by default): detail stems (regenerable), exports, scores, generations,
  recordings (015), voice profiles. An excluded category is also removed from the database snapshot inside the archive,
  so a restored library never points at missing files. Model caches, worker and generator data,
  browser preferences, configuration and secrets are never included.
- Q: Where is the archive written? -> A: into a **host folder bind-mounted at `/backups`** (default
  `./backups` next to the compose file, `STEMSTUDIO_BACKUP_HOST_DIR` to change it). Multi-GB
  archives are never streamed through Blazor; a finished archive can also be **downloaded** by the
  browser straight from the API (Range requests, resumable). Restoring needs the archive in the
  backups folder; there is no browser upload of archives (sizes of tens of GB).
- Q: Restore into a non-empty library? -> A: an empty library is restored directly. A non-empty
  library can be **replaced**: the current library is moved aside (not deleted) and can be
  restored with one click ("Undo restore") or discarded later. **Merge** (add what is missing,
  keep what exists) is a P3 story with fixed conflict rules. Restores need an idle library (no
  queued or running job).
- Q: Older archives? -> A: the archive records the last EF Core migration. An archive from an older
  schema is migrated in staging before it replaces anything; an archive from a newer StemStudio
  (unknown migrations) or a different format major version is refused with the reason.
- Q: Voice profiles and consent (010)? -> A: a profile is only ever written into an archive together
  with its consent record (statement version, statement SHA-256, person name, time), which the
  manifest repeats. On restore each profile's consent proof is checked against the statement
  texts this StemStudio knows; a profile without valid proof is not restored (its converted takes
  are kept as in 010's "profile deleted" rule). The user can leave voice profiles out of a backup
  or a restore.
- Q: Storage overview: what is "source"? -> A: StemStudio does not keep the downloaded source audio
  after a track is separated (001 data-model); the only source copies are pending uploads in the
  API and the worker's separation cache. The overview shows both, attributed to the track where
  possible.
- Q: What may the clean-up remove? -> A: only **regenerable** data: detail stems (restored by "Split
  further"), the worker's separation and detail caches, and leftovers (temporary files, orphaned
  folders, finished uploads, worker scratch, old job files). Originals (stems, artwork you chose,
  exports, scores, generation takes, voice samples, the database) are never offered by the
  clean-up; the overview links to the screens where they can be deleted one by one, with the
  existing confirmations. Model caches are shown read-only in 013 (removal is deferred: the
  generator and singer containers may hold them open).
- Q: What about play-along recordings and latency calibrations (feature 015, built after 013)? ->
  A: CTO cross-feature note 2026-11-16: recorded takes (hidden library assets with origin
  `recording`, 24-bit FLAC, up to 200 per track) are **user-created originals**: they get their own
  backup category `recordings` (optional, default on; reserved in format 1.0.0 so 015 needs no
  format change), their own column in the storage overview, and are never offered by the clean-up.
  Input-latency calibrations (per device label) are rows in the database and travel with the
  `database` category. 013 implements the classification now (an asset whose provenance origin is
  `recording` is never counted as an export); until 015 is merged the category is simply empty.
- Q: Encryption? -> A: not in 1.0.0 (no open-source encrypted-ZIP writer in the .NET base library;
  a password in the UI would also need key handling). The UI says that a backup contains voice
  samples and should be kept on a protected drive. `manifest.encryption` is reserved.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Back up my whole library (Priority: P1)

The user opens **Library care -> Backups**, sees an estimate of the archive size per category and
the free space in the backups folder, optionally leaves out large regenerable files, and starts a
backup. A progress bar shows the stage, bytes written, the current item and the time left; the
user may cancel. When it finishes the archive appears in the list with its size, date and
contents and can be downloaded.

**Why this priority**: The core of M4: losing a Docker volume today loses hours of GPU work.

**Independent Test**: On the fake stack with three tracks (one with detail stems), one mix, one
export, one score, one generation and one voice profile: start a backup with all categories;
the archive exists in the backups folder, its manifest validates against
[backup-manifest.schema.json](contracts/backup-manifest.schema.json), every entry's SHA-256
matches, and the archive lists all items.

**Acceptance Scenarios**:

1. **Given** a library and a writable backups folder with enough space, **When** the user starts a
   backup with the defaults, **Then** an archive `stemstudio-backup-<timestamp>.zip` is written
   (as `.partial` until complete), its manifest lists every included file with size and SHA-256,
   and the backups list shows it with status "Verified".
2. **Given** a running backup, **When** the user deletes a track or runs a clean-up, **Then** the
   action is refused with "A backup is running; try again when it has finished" and nothing is
   deleted; **When** the user starts a new export, **Then** it runs normally and is not part of
   this backup.
3. **Given** a running backup, **When** the user cancels, **Then** within 5 seconds the job is
   `cancelled` and no `.partial` file remains.
4. **Given** too little free space for the estimate, **When** the user starts a backup, **Then** it
   is refused with the required and free sizes; **Given** the disk fills up during a backup,
   **Then** the job fails with "Not enough free space", the partial file is removed and the
   library is unchanged.
5. **Given** the option "Detail stems" is off, **When** the backup completes, **Then** no detail
   stem is in the archive and the database inside the archive marks those tracks' detail phases
   as "removed" (so a restored track offers "Split further").
6. **Given** the API restarts during a backup, **When** it starts again, **Then** the job is shown
   as failed "Interrupted by restart" and the partial file is gone.
7. **Given** a finished backup, **When** the user clicks Download, **Then** the browser downloads
   the file from the API with resumable Range requests (any size).

---

### User Story 2 - Restore a backup (Priority: P1)

The user copies an archive into the backups folder (or picks one made on this machine), opens it
in Library care, and sees what it contains, when and with which StemStudio version it was made,
and whether it can be restored. On an empty library they restore it directly; on a non-empty
library they choose **Replace my library** and confirm. StemStudio checks the whole archive first,
then switches to the restored library in one step. The previous library is kept until the user
discards it, and "Undo restore" brings it back.

**Why this priority**: A backup that cannot be restored is worthless; restore on a new machine is
the main recovery path.

**Independent Test**: Backup of the US1 library -> remove all volumes -> start the fake stack ->
restore: every database row and every file is identical (row compare and SHA-256 compare), mixes
open in the mixer, stems play, the voice profile shows its consent details.

**Acceptance Scenarios**:

1. **Given** an empty library and a valid archive, **When** the user restores it, **Then** the
   archive is verified completely before anything changes, the library is switched in one step,
   and the restore report lists the restored counts per category.
2. **Given** a non-empty library, **When** the user picks "Replace my library", **Then** a
   confirmation shows what will be replaced and the free space needed (the current library is kept
   aside); after the restore "Undo restore" and "Discard previous library" are offered.
3. **Given** any queued or running job (import, detail, export, score, lyrics alignment, generation,
   agent run), **When** the user starts a restore, **Then** it is refused with the list of active
   jobs.
4. **Given** an archive made by an older StemStudio (older database schema), **When** it is
   restored, **Then** the database is migrated in staging and the restore succeeds; **Given** an
   archive from a newer StemStudio or another format major version, **Then** it is shown as "Not
   compatible" with the reason and cannot be restored.
5. **Given** an archive with a damaged file, an entry outside the allowed layout (`../`, absolute
   path, link, unlisted file, duplicate name, size larger than declared), or a database that fails
   SQLite's integrity check, **When** the user restores or verifies it, **Then** it is refused
   naming the first problem and the current library is untouched.
6. **Given** a restore during which the API is stopped, **When** it starts again, **Then** the
   library is either the complete previous library or the complete restored one, never a mix.
7. **Given** a restored library, **When** the user opens a track whose worker cache does not exist
   on this machine and clicks "Split further" or "Align lyrics", **Then** StemStudio first
   re-creates the worker cache from the library stems (rehydration) and the job runs normally.

---

### User Story 3 - See where my disk space goes (Priority: P2)

The user opens **Library care -> Storage** and sees the free space of each storage area (library,
backups folder, worker data, model cache), the library total per category, a table of tracks
sorted by size with columns for source copy, stems, detail stems, artwork, exports, scores,
generations and recordings (015), the items that belong to no track (exports of deleted tracks, generations, voice
profiles, lyrics), the worker caches, the model caches per model, the backups and a kept previous
library. Every row says whether StemStudio can regenerate it.

**Why this priority**: The first half of M5; it is also what the clean-up acts on.

**Independent Test**: On the fake stack with a known set of files, every number in
`GET /api/storage` equals the byte sum of the files on disk (scripted `du` comparison), and the
page renders it with the test ids.

**Acceptance Scenarios**:

1. **Given** a library, **When** the user opens Storage, **Then** within 2 seconds the totals, the
   per-track table and the free space are shown, with "Regenerable" or "Original" per category.
2. **Given** the worker is unreachable, **When** the user opens Storage, **Then** the library part
   is shown and the worker and model-cache parts show "Not available: <reason>".
3. **Given** the per-track table, **When** the user sorts by a column or opens a track, **Then** the
   table sorts by that size and the track link opens the track page.
4. **Given** the model caches, **When** they are shown, **Then** each lists its models, its size
   and "Downloaded again at first use (<size>)"; there is no delete button in 013.

---

### User Story 4 - Free space safely (Priority: P2)

From Storage the user chooses a clean-up: "Remove detail stems" (for selected tracks or all),
"Clear the worker cache", or "Remove leftovers". StemStudio shows the exact list of what will be
removed, how much space it frees and how to get it back, and only removes it after the user
confirms. Originals are never part of a clean-up.

**Why this priority**: The second half of M5.

**Independent Test**: Clean-up of all detail stems and the worker cache on the fake stack: the
freed bytes equal the preview; no stem, export, score, take, voice sample or database row other
than the detail-phase state changed; "Split further" on a cleaned track recreates its detail stems.

**Acceptance Scenarios**:

1. **Given** tracks with detail stems, **When** the user previews "Remove detail stems", **Then**
   the dialog lists the tracks, file counts and bytes, says "You can split them again with Split
   further (about <minutes> on <device>)" and needs one confirmation.
2. **Given** a confirmed plan, **When** it runs, **Then** the detail stems are removed, the tracks
   show "Detail stems removed - Split further to restore", mixes keep their detail settings and
   play the parent stems until the detail stems are back.
3. **Given** a track whose detail phase is queued or running, **When** a plan includes it, **Then**
   that track is skipped with the reason and the rest is removed.
4. **Given** a backup or restore is running, **When** the user opens a clean-up, **Then** it is
   refused with the reason.
5. **Given** a preview older than 10 minutes or a library that changed since the preview, **When**
   the user confirms, **Then** StemStudio asks to preview again (no stale plan is executed).
6. **Given** the overview, **When** the user looks for a way to delete stems, exports or takes,
   **Then** there is none in the clean-up; links open the track, library or generations screens
   where single items can be deleted with their existing confirmations.

---

### User Story 5 - Merge a backup into my library (Priority: P3)

The user restores an archive from another machine into a non-empty library with **Merge**: items
that are not in the library are added, items that exist are kept, name clashes get "(restored)".

**Why this priority**: Useful to combine libraries, but replace covers recovery. It can be cut
without affecting US1-US4.

**Independent Test**: Library A (tracks 1, 2) and an archive of library B (tracks 2, 3; track 2 has
the same source key) -> merge: the library holds tracks 1, 2 (A's copy, untouched) and 3 with its
mixes, assets and generations; a style with the same name becomes "<name> (restored)".

**Acceptance Scenarios**:

1. **Given** a non-empty library, **When** the user merges an archive, **Then** each archive item is
   added when neither its id nor its unique key (track source key) exists; otherwise it is skipped
   with its dependants (mixes, assets, generations of a skipped track are skipped), and the report
   lists every skipped item and why.
2. **Given** a name that must be unique (style, voice profile, lyrics sheet, mix per track), **When**
   it clashes, **Then** the restored item is renamed with the 007 copy-name rule ("<name>
   (restored)", "(restored 2)", ...).
3. **Given** a merge, **When** it fails half-way, **Then** nothing of it remains (the merge runs in
   one database transaction and the files are moved in only after the commit; files of a failed
   merge are removed).

### Edge Cases

- **Backups folder missing or read-only** (bind mount not configured, wrong owner on Linux): the
  Backups tab shows "Backups folder not available: <reason>" with the fix from the quickstart;
  backup and restore are disabled, the storage overview still works.
- **Archive renamed or copied from another machine**: archives are recognised by their
  `manifest.json`, not by name; any `*.zip` in the folder is listed, unrecognised files as "Not a
  StemStudio backup".
- **Two archives with the same `backupId`** (a copy): both are listed by file name.
- **Very large libraries** (> 100 GB, > 100,000 files): Zip64 is used automatically; the manifest
  is streamed; progress and cancel work the same.
- **Track deleted during the backup**: refused by the write gate (`409 backup-running`).
- **Job finishes during the backup**: its new files are not in the snapshot and not in the archive;
  the backup report says "N items changed after the snapshot and are not included".
- **Entity in progress at snapshot time** (a track still separating): its row is kept, its pending
  upload is included, and on restore it becomes `failed` "Interrupted by restore" (retry works for
  URL imports and for uploads whose file was included).
- **Restore while the worker is down**: allowed; rehydration happens later on demand.
- **Restore of an archive without voice profiles over a library with profiles (replace)**: the
  restored library has no profiles; the previous library (with them) is kept aside until
  discarded.
- **Consent statement updated after the backup** (a future `consent.v2`): profiles with
  `consent.v1` proof stay valid (their proof is checked against the v1 text this StemStudio still
  embeds); an unknown version is refused for that profile.
- **Stored paths inside the archive database** (e.g. a voice sample path) that do not match the
  generated layout: the restore is refused (`archive-unsafe`), as for zip-slip entries.
- **Free space for replace**: needs the archive's uncompressed size on the library volume (the
  previous library stays until discarded); the confirmation shows both numbers.
- **Clean-up of the worker cache while the worker is busy**: entries used by a queued or running job
  are skipped with the reason.
- **Worker cache removed, then "Split further"**: the API rehydrates the cache from the stems first
  (stage "Preparing stems"), then the detail job runs.
- **Previous library kept after replace and a second restore**: only one previous library is kept;
  a second replace asks to discard the first one before it starts.
- **Recordings (015) before feature 015 exists**: the category is shown with 0 bytes and is
  listed as `excluded` with reason `empty` in the manifest; an archive written after 015 that
  contains recordings restores on a 013-only StemStudio only when its migrations are known (the
  normal schema rule), so no partial state is possible.
- **Browser closed during a backup or restore**: the job continues in the API; reopening Library
  care shows its progress.

## Requirements *(mandatory)*

### Functional Requirements

**Backup (US1)**

- **FR-001**: StemStudio MUST write a library backup as one ZIP archive (Zip64 where needed) in the
  format [backup-archive 1.0.0](contracts/backup-archive.md): `db/stemstudio.db`, `files/<data-relative path>`,
  `README.txt`, and `manifest.json` validated by
  [backup-manifest.schema.json](contracts/backup-manifest.schema.json).
- **FR-002**: The database MUST be captured with the SQLite online backup API into a staging file;
  files MUST be selected from that snapshot. While a backup runs, operations that delete or
  replace library files MUST be refused with `409 backup-running` (list in data-model 5.2); jobs
  and other edits continue.
- **FR-003**: Categories: always `database` (including 015 latency calibrations), `stems`,
  `artwork`, `pendingUploads`; optional (default on) `detailStems`, `exports`, `scores`,
  `generations`, `recordings` (015 play-along takes), `voiceProfiles`. Before starting, the user
  MUST see the estimated size per category and the free space. An excluded category MUST also be
  removed from the database snapshot in the archive (data-model 4.3), so the archive is
  self-consistent.
- **FR-004**: A backup MUST NEVER contain model caches, worker/generator/singer data, Data
  Protection keys, browser preferences, configuration, environment values or secrets (e.g.
  `ANTHROPIC_API_KEY`), other backups, a kept previous library, or temporary files.
- **FR-005**: Archives MUST be written into the backups folder (`StemStudio:BackupDir`, container
  path `/backups`, bind-mounted from the host) as `<name>.zip.partial` and renamed to `<name>.zip`
  only after the post-write check passes. A completed archive MUST be downloadable through the API
  (`Content-Disposition: attachment`, Range, ETag).
- **FR-006**: A backup MUST report stage, progress (bytes done / total), current item and estimated
  time left, and MUST be cancellable; cancelling removes the partial file within 5 seconds. Only one
  library job (backup, verify, restore) runs at a time. A restart fails a running job with
  "Interrupted by restart" and removes its partial file.
- **FR-007**: A backup MUST be refused when the free space of the backups folder is below the
  estimate plus 64 MiB, and MUST stop with `insufficient-disk-space` when free space drops below
  256 MiB during writing.
- **FR-008**: Every entry MUST be hashed (SHA-256) while it is written and recorded in the
  manifest. After writing, StemStudio MUST re-open the archive and check the central directory
  against the manifest (names, sizes, count). A full re-hash ("Verify") MUST be available on demand
  and MUST run before every restore.
- **FR-009**: A voice profile MUST only be written with its consent record (statement version,
  statement SHA-256, person name, time) in the database and in `manifest.voiceProfiles`; a profile
  whose record is incomplete MUST be left out with a warning. The backup dialog MUST state that the
  archive contains voice samples, is not encrypted, and should be stored on a protected drive.

**Restore (US2, US5)**

- **FR-010**: The Backups tab MUST list every `*.zip` in the backups folder with its manifest summary
  (date, StemStudio version, schema, categories, counts, size) and a compatibility verdict: format
  major 1 and a schema not newer than this StemStudio's migrations.
- **FR-011**: Before changing anything a restore MUST: verify every entry's SHA-256 and size;
  refuse entries that are not in the manifest, not matching the layout allow-list, absolute, with
  `..`, backslashes, drive letters or control characters, links, duplicates, or larger than
  declared (zip-slip and zip-bomb rules, data-model 3.4); run `PRAGMA integrity_check` and the
  migration-history check on the staged database; validate every stored relative path column
  against the layout patterns; and check each voice profile's consent proof.
- **FR-012**: An empty library (no tracks, mixes, assets, generations, styles of the user, lyrics
  sheets or voice profiles) MUST be restored directly. A non-empty library MUST offer **Replace**
  (P1) after a confirmation that shows what is replaced and the space needed; the previous library
  MUST be kept aside, revertible ("Undo restore") and discardable (typed confirmation). Only one
  previous library is kept. **Merge** (P3) MUST follow the conflict rules of US5.
- **FR-013**: A restore MUST be refused while any job is queued or running (listing them). During
  the switch the API MUST be in maintenance mode: every endpoint except health, library-care status
  and the job endpoint answers `503 library-maintenance` with `Retry-After`; synchronisers pause.
- **FR-014**: An archive with an older schema MUST be migrated with the app's EF Core migrations on
  the staged database before the switch; one with unknown (newer) migrations or another format
  major MUST be refused (`archive-incompatible`).
- **FR-015**: After the switch StemStudio MUST mark every non-terminal entity of the restored
  database as failed "Interrupted by restore", treat worker job references as stale, and write a
  restore report (counts per category, skipped items with reasons, warnings).
- **FR-016**: On restore a voice profile MUST only be restored when its consent proof is valid
  (known statement version, matching statement SHA-256, person name 1-80, time not after the
  archive's creation, sample present with matching SHA-256); otherwise it is dropped and its
  converted takes are kept with the name snapshot "<name> (profile not restored)". The user MAY
  leave voice profiles out of a restore.
- **FR-017**: The switch MUST be crash-safe: a journal records each step (stage, move aside, move in,
  commit); on start-up an unfinished restore is completed or rolled back so the library is either
  the previous or the restored one.
- **FR-018**: A restore MUST be refused when the library volume's free space is below the archive's
  uncompressed size plus 256 MiB.
- **FR-019**: When a detail job, a lyrics alignment or another worker job needs a track's stems in
  the worker cache and the worker reports them missing (`404`/`409` cache miss), the API MUST
  rehydrate the worker cache from the library stems (worker-api 1.7.0 `POST /v1/jobs/rehydrate`)
  and retry once.

**Storage overview and clean-up (US3, US4)**

- **FR-020**: `GET /api/storage` MUST report: free and total bytes of the library volume, the backups
  folder, the worker data and the model cache; library totals per category; per-track sizes
  (source copy, stems, detail stems, artwork, exports, scores, generations, recordings, worker
  cache); items
  without a track; worker caches (separation, detail, job files, scratch); model caches per model
  folder; backups; a kept previous library; leftovers. A part that cannot be measured MUST carry
  its reason instead of a number.
- **FR-021**: Every category MUST be labelled `regenerable` or `original`, with how it is
  regenerated and an indicative cost.
- **FR-022**: Clean-up MUST offer only `detailStems` (selected tracks or all), `workerCache`
  (selected tracks or all) and `leftovers`. It MUST NEVER remove stems, artwork, exports, scores,
  generation takes, recorded play-along takes (015), voice samples, pending uploads of unfinished tracks, the database, backups or a
  previous library.
- **FR-023**: Clean-up MUST be two-step: a preview returns a plan (every item, bytes, how to
  regenerate, skipped items with reasons) valid for 10 minutes; execution needs the plan id and
  refuses a stale plan (`409 plan-stale`) when the library changed.
- **FR-024**: Clean-up MUST be refused while a backup or restore runs, and MUST skip items used by
  queued or running jobs with the reason.
- **FR-025**: Removing detail stems MUST set the track's detail state to "removed" (new
  `Track.detail.filesRemovedAt`), keep mixes' detail settings, and keep "Split further" available.
- **FR-026**: Deleting a backup and discarding a previous library MUST be explicit actions with a
  confirmation (typed `DISCARD` for the previous library, which is not regenerable).

**Cross-cutting**

- **FR-030**: Help topics `library-care` (screen), `backup-contents`, `backup-folder`,
  `backup-restore`, `backup-compatibility`, `backup-voice-data`, `storage-overview`,
  `storage-regenerable`, `storage-cleanup` (area `mixes`, help-content 1.0.0 format) MUST be
  linked from their controls.
- **FR-031**: Levels: Beginner sees Backups (start, list, download, restore on an empty library or
  replace) and the Storage totals with the three clean-ups; Advanced adds the category options, the
  per-track table, verify and merge; Expert adds the worker cache details, the manifest view and
  entry counts (ui-test-ids delta).
- **FR-032**: All controls MUST be keyboard and screen-reader usable (WCAG 2.2 AA as in 004):
  progress as `progressbar` with `aria-valuenow`, status changes in a polite live region,
  confirmations as modal dialogs with focus trapping, tables with sortable column headers
  (`aria-sort`).
- **FR-033**: Every new element MUST carry the ids of ui-test-ids 1.12.0 (contracts delta).
- **FR-034**: Every path built from archive content, database content or a request MUST come from
  generated components validated against the layout patterns; archive and backup file names MUST
  match `^[A-Za-z0-9][A-Za-z0-9._-]{0,119}\.zip$`; no endpoint accepts a free path.
- **FR-035**: Library jobs and clean-ups MUST be logged (start, end, bytes, counts, outcome) without
  personal content (no person names, no lyrics, no file contents).
- **FR-036**: The user manual (new chapter "Library care") and the technical manual (deployment:
  backups folder; operations: backup/restore; data model; security) MUST be updated by the CTO in
  the integration wave.

### Key Entities

- **Backup archive**: a ZIP file in the backups folder with database snapshot, library files and a
  manifest; identified by file name, described by `backupId`, creation time, versions and contents.
- **Manifest**: the archive's table of contents: format and versions, schema, categories included
  or excluded, counts, entries (path, category, size, SHA-256), voice-profile consent proofs,
  warnings.
- **Library job**: one backup, verify or restore run with stage, progress, options, report and
  error; kept in a journal outside the database (it must survive a restore).
- **Previous library**: the library moved aside by a replace restore, with its creation time and
  size, until reverted or discarded.
- **Storage overview**: a computed report (not stored) of sizes, free space and regenerability.
- **Clean-up plan**: a short-lived, computed list of removable regenerable items with a plan id and
  a fingerprint of the items' state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Round trip on the fake stack: backup -> remove all volumes -> restore reproduces 100 %
  of database rows (row-by-row compare, ignoring job-state columns of non-terminal rows) and 100 %
  of included files bit-identically (SHA-256).
- **SC-002**: On the reference machine (Docker Desktop, bind-mounted backups folder on an NVMe SSD) a
  backup and a restore each run at >= 60 MB/s averaged over a 20 GB library (about 6 minutes).
- **SC-003**: Cancel ends a backup within 5 seconds and leaves no partial file (100 % of 20 runs at
  random moments in the automated test).
- **SC-004**: 100 % of the hostile-archive corpus (at least 14 vectors, research R-06) is refused
  before any library change, and the library's file tree and database are byte-identical
  afterwards.
- **SC-005**: Crash injection after every journal step of a replace restore leaves either the
  previous or the restored library, passing `PRAGMA integrity_check` and a file/row consistency
  check (100 % of steps).
- **SC-006**: Storage overview numbers equal the on-disk byte sums (deviation 0 bytes for library
  categories on the fake stack); the page shows the overview of a 500-track library within 2 s.
- **SC-007**: A clean-up never removes an original: the property test over generated libraries
  finds no removed path outside the regenerable patterns; "Split further" after a detail-stem
  clean-up recreates the detail stems (fake stack).
- **SC-008**: 0 voice profiles restored without a valid consent proof; restored consent fields are
  identical to the backup; a profile excluded by option is absent from the archive's database and
  files.
- **SC-009**: An archive made at every earlier schema of the test fixtures (at least the 008 and 010
  schemas) restores and migrates successfully.
- **SC-010**: An asset with provenance origin `recording` (feature 015) is counted under
  `recordings`, never under `exports`, is included or excluded only by the `recordings` option, and
  is never part of a clean-up plan (unit and property tests with a fixture asset of that origin).

## Assumptions

- Single local user, no authentication (as 001). Anyone who can reach the API can back up and
  restore; this is unchanged from today, where anyone with access can delete the library.
- The API, worker and the optional generative services run on one Docker host. Free space of
  volumes on Docker Desktop all comes from the same virtual disk; the overview shows each mount's
  numbers as the OS reports them.
- The backups folder is a bind mount the user controls (outside Docker's volumes), so a lost volume
  does not lose the backups. Keeping a second copy elsewhere is the user's choice (documented).
- Incremental or scheduled backups, encryption, cloud targets and model-cache removal are out of
  scope for 013 (backlog, research R-11).
- The browser's preferences (level, theme, dismissed hints) are not part of the library (004 keeps
  them in local storage).
- The 011 agent gets tools for backup and storage in a later tool-catalogue minor version (roadmap
  note); 013 adds none.

## Cross-feature note: 011 follow-up "claude-key" (architect, 2026-11-16)

Design: `specs/011-ai-composer-agent/followups/claude-key/` (constitution 1.4.0 ratified by the
owner 2026-11-16). When it is built, the owner can store the Claude API key in Settings,
encrypted with ASP.NET Core Data Protection; the database table `AgentSecrets` holds only the
ciphertext and the last four characters. Binding rules for 013 (they refine FR-003, FR-004 and the
restore requirements; tasks T035, T048 and T062 carry them):

- The API's Data Protection key ring (`StemStudio:DataProtection:KeyRingDir`, Docker volume
  `api-keys` at `/keys`, mounted only into `api`) is **never** read or archived. 013 already reads
  only the library volume; a test proves that no archive entry comes from the key ring directory.
- Owner decision Q2 (2026-11-16): backups **keep the ciphertext**. `AgentSecrets` stays in the
  `database` snapshot as ciphertext (useless without the key ring). A restore on the same computer
  keeps the key; a restore on another computer asks for the key again (status `stored-unreadable`,
  "Enter the key again"), which never blocks the restore or the app. The archive never contains
  the key ring.
- A replace restore whose stored key is unreadable afterwards adds "The stored Claude key must be
  entered again" to the restore report.
- A merge restore never imports `AgentSecrets` rows and never changes the live row.
- Manifest, README and logs never contain a key, ciphertext or last four characters.
- The ciphertext is not stripped from archives (the "strip" alternative of follow-up plan.md Q2
  was not chosen).

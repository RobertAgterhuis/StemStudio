# Library care

Everything you make in StemStudio (tracks, stems, mixes, exports, sheet music, generations and
voice profiles) lives in Docker volumes on your computer. **Library care** keeps that work safe
and your disk tidy. Open it with **Library care** in the navigation. The page has two tabs:
**Backups** and **Storage**.

## One job at a time

A backup, a check or a restore is a *library job*. Only one runs at a time. Its progress shows
on the page (stage, percentage, time left) and as a small indicator at the top of every
screen, so you can keep working. While a backup runs you can still play, mix and export; only
deleting things waits until it has finished. **Cancel** stops a running backup within a few
seconds and leaves no half-written file.

## The backups folder

StemStudio writes every backup into the **backups folder**. With Docker this is the folder
`./backups` next to `docker-compose.yml`, unless `STEMSTUDIO_BACKUP_HOST_DIR` names another
folder. The Backups tab shows the folder and its free space.

- **Keep a copy elsewhere.** A backup on the same disk does not help when that disk fails.
  Copy the archive files to an external drive or another computer, or use **Download** on a
  backup row (a download can resume when it is interrupted).
- **Restore from a copied file**: put the archive into the backups folder; it appears in the
  list.
- **"Not available" or "read-only"**: create the folder and make it writable. On Linux the API
  runs as user 1654: `sudo chown 1654 ./backups`. Then reload the page.

## Making a backup

Press **New backup** and choose a profile:

- **Complete**: everything.
- **Smaller: without detail stems**: detail stems are left out. They are *regenerable*: after
  a restore, **Split further** makes them again.

At the Advanced and Expert levels you can tick each optional part yourself (the profile then
shows **Custom**). The estimate lists every part with its size, number of files and class:
**Original** (only you have it) or **Regenerable** (StemStudio can make it again).

| Part | In a backup |
|---|---|
| Database (tracks, mixes, settings), stems, artwork, uploads still being processed | always |
| Detail stems | optional (regenerable) |
| Exports, sheet music, generations, recordings, voice profiles | optional (original) |

**Never included**: the downloaded AI models, the worker's caches, StemStudio's configuration
and secrets (such as the encryption keys of a stored Claude key) and your browser preferences.
A part you leave out is also removed from the database inside the archive, so a restored
library never points at missing files. **Start backup** stays off while the backups folder has
too little free space.

A backup is one ZIP file named `stemstudio-backup-<date>-<time>.zip`. It contains a
`README.txt` and a contents list with a fingerprint (SHA-256) of every file.

> **Backups are not encrypted.** Anyone who has the file can open it and, for voice profiles,
> listen to the voice samples. Keep backups on a drive only you can read.

## The backup list

Each row shows the date, size and state of an archive:

- **Ready to restore**: made by this StemStudio version.
- **Made by an older StemStudio**: the database inside is updated during the restore, before
  anything in your library changes.
- **Not compatible**: made by a newer StemStudio or in an unknown format. Update StemStudio
  first.
- **Not a StemStudio backup**: the file is not a StemStudio archive.

**Details** shows what the archive contains and, per voice profile, its consent record. At the
Advanced and Expert levels **Check** reads the whole archive and compares every file with its
size and fingerprint without restoring anything; Expert also shows the contents list itself.
**Delete** asks for confirmation and is refused while that archive is being used.

## Restoring a backup

Press **Restore** on a backup row. StemStudio first checks the **whole archive** (every file
against its size and fingerprint, the database, the layout, the consent records). Nothing in
your library changes before that check has passed; a damaged or unsafe archive is refused with
the first problem it found. Then StemStudio switches to the restored library in one step and
shows "Switching to the restored library" for a few seconds.

- **Empty library**: the backup is simply restored.
- **Replace my library**: your current library is **moved aside, not deleted**. Type `REPLACE`
  to confirm.
- **Add what is missing** (Advanced and Expert): items of the backup that your library does not
  have are added; items you already have are kept as they are. No typed word is needed and no
  previous library is kept. The report lists every skipped item with its reason (for example
  *already in your library*) and every item added under a new name, such as *"My style
  (restored)"* when the name was taken.

Untick **Restore voice profiles** to leave them out. A voice profile without a valid consent
record is never restored; the report says so.

A restore needs a quiet library: while an import, a split, an export, sheet music, a lyrics
alignment, a generation or an agent run is busy, the dialog lists them with links. Wait for
them or cancel them first. The library volume also needs free space for the unpacked backup.

Work that was still running when the backup was made is marked *Interrupted by restore*; start
it again if you need it. After a restore on another computer, the first **Split further** or
**Align lyrics** of a track takes a moment longer: the track shows *Preparing stems* while the
processing worker rebuilds its copy of the stems.

## The previous library

After a replace restore the Backups tab shows the **previous library** with its size:

- **Undo restore** switches back to it (the restored library becomes the previous one).
- **Discard** deletes it for good; type `DISCARD` to confirm.

Only one previous library is kept: undo or discard it before the next replace restore.

## Storage

The **Storage** tab shows where your disk space goes:

- **Areas**: free and total space of the library, the backups folder, the worker's data and
  the model cache. With Docker Desktop several areas often share one virtual disk; they are
  then shown on one line.
- **Parts of the library**: every kind of file with its size, number of files and class.
  Original parts link to the screen where you delete items one by one.
- **Tracks**: the five largest tracks (Beginner) or a sortable table of every track with a
  column per kind of file (Advanced and Expert).
- **Model caches**: the downloaded AI models. They are downloaded again at first use and cannot
  be removed here.
- **Worker cache entries** (Expert): one row per song in the worker's cache, with its size,
  whether a job uses it, whether it was rebuilt from your library, and the track.

The numbers are measured at most every 30 seconds; **Refresh** measures again. When the worker
is not running, the worker part says why it cannot be shown.

| Kind | Class | How it comes back |
|---|---|---|
| Stems, artwork, exports, sheet music, generations, recordings, voice samples, database | Original | only from a backup |
| Detail stems | Regenerable | **Split further** on the track page (minutes per track) |
| Worker separation and detail caches | Regenerable | rebuilt from the library stems when needed |
| Leftovers (temporary files, finished uploads, old job files) | Regenerable | not needed |
| Model caches | Regenerable | downloaded again at first use |

## Freeing space safely

There are three clean-ups, each with a **preview** that lists every item, its size and how to
get it back:

- **Remove detail stems**: of all tracks, or of one track in the table (Advanced and Expert).
  The track keeps its mixes and their detail settings; the track page says *Detail stems
  removed*, and **Split further** makes them again. Until then the mixer loads no detail
  channels.
- **Clear worker cache**: the worker's copies of separated songs; they are rebuilt from your
  library when needed.
- **Remove leftovers**: temporary files, processed uploads, orphaned folders and old job files.

Items that a running job uses are skipped, with the reason. A preview is valid for 10 minutes;
when your library changes in between, StemStudio asks you to **preview again**. Clean-up waits
while a backup or a restore runs.

**Never removed by a clean-up**: stems, artwork, exports, sheet music, generation takes,
recordings, voice samples, uploads of unfinished tracks, the database, backups and the previous
library.

## Levels

| Level | What you see |
|---|---|
| Beginner | New backup with the two profiles, the backup list with download and restore (empty library or replace), the previous library, areas, parts, the five largest tracks and the three clean-ups |
| Advanced | Also the backup options per part, **Check**, **Add what is missing**, the track table and clean-up per track |
| Expert | Also the contents list of an archive and the worker cache entries |

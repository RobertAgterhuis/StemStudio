---
id: backup-contents
title: "What a backup contains"
area: mixes
summary: "Database, stems, artwork and pending uploads are always in; detail stems, exports, sheet music, generations and voice data are optional."
anchors: ["backup-dialog", "backup-estimate"]
keywords: ["backup contents", "complete", "smaller", "compact", "detail stems", "estimate", "categories", "regenerable", "original"]
related: [library-care, backup-folder, backup-voice-data, storage-regenerable]
reviewed: "013"
---

A backup is one ZIP file. Press **New backup** and choose:

- **Complete**: everything below.
- **Smaller: without detail stems**: detail stems are left out. They are *regenerable*: after a restore, **Split
  further** makes them again (this takes a while on the processing device).

At the Advanced and Expert levels you can tick each optional part yourself (the profile then shows **Custom**), and
the estimate lists every part with its size, its number of files and whether it is **Original** (only you have it)
or **Regenerable** (StemStudio can make it again).

| Part | Included |
|---|---|
| Database (tracks, mixes, settings of each item), stems, artwork, pending uploads | always |
| Detail stems | optional (regenerable) |
| Exports, sheet music, generations, recordings, voice profiles | optional (original) |

**Never included**: the downloaded AI models, the worker's caches, StemStudio's configuration and secrets (such as a
stored Claude key's encryption keys) and the preferences of your browser.

A part you leave out is also removed from the database inside the archive, so a restored library never points at
missing files. The estimate shows the total size and the free space in the backups folder; **Start backup** stays
off while the space is not enough.

Backups are **not encrypted**. Keep them on a drive only you can read.

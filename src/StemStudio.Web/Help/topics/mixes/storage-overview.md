---
id: storage-overview
title: "Storage overview"
area: mixes
summary: "Free space of the library, backups, worker and model areas, the size of each part of the library and the largest tracks."
anchors: ["care-tab-storage", "storage-volume-*"]
keywords: ["storage", "disk space", "free space", "size", "largest tracks", "model cache", "worker", "docker disk"]
related: [storage-regenerable, storage-cleanup, library-care]
reviewed: "013"
---

The **Storage** tab shows where your disk space goes:

- **Areas**: the free and total space of the library, the backups folder, the worker's data and the model cache. With
  Docker Desktop several areas often live on the same virtual disk; they are then shown on one line.
- **Parts of the library**: every kind of file with its size, its number of files and whether it is **Original** or
  **Regenerable** (see [what can be made again](help:storage-regenerable)). Original parts link to the screen where
  you delete items one by one.
- **Tracks**: the largest tracks (Beginner) or a table of every track with a column per kind of file (Advanced and
  Expert; press a column heading to sort).
- **Model caches**: the downloaded AI models. They are downloaded again at first use and cannot be removed here.

StemStudio does not keep the downloaded source audio after a split. The only source copies are uploads that are still
being processed and the worker's separation cache.

When the worker is not running, the library part is still shown and the worker part says why it is not available.
The numbers are measured at most every 30 seconds; **Refresh** measures again.

---
id: storage-cleanup
title: "Freeing space safely"
area: mixes
summary: "Remove detail stems, the worker's caches or leftovers after a preview; originals are never removed."
anchors: []
keywords: ["clean up", "cleanup", "free space", "remove detail stems", "worker cache", "leftovers", "preview"]
related: [storage-regenerable, storage-overview, detail-stems]
reviewed: "013"
---

There are three clean-ups, each with a **preview** first that lists every item, its size and how to get it back:

- **Remove detail stems**: of all tracks, or of one track in the table (Advanced and Expert). The track keeps its mixes
  and their detail settings; the track page says "Detail stems removed" and **Split further** makes them again. In the
  mixer the detail channels are not loaded until then; their stems play as usual.
- **Clear worker cache**: the worker's copies of separated tracks. They are rebuilt from your library when needed.
- **Remove leftovers**: temporary files, uploads that were processed, orphaned folders and old job files.

Items that a running job uses are skipped, with the reason. The preview is valid for 10 minutes; when your library
changes in between, StemStudio asks you to **preview again**. Clean-up waits while a backup or a restore runs.

**Never removed**: stems, artwork, exports, sheet music, generation takes, recordings, voice samples, pending uploads
of unfinished tracks, the database, backups and a previous library. The model caches cannot be removed here; delete the
model cache volume by hand if you need that space (models are downloaded again at first use).

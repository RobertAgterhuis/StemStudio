---
id: export-range
title: "Save part of a song"
area: tracks
summary: "Save only the loop region or a start and end time of at least 0.5 s. Leave both empty for the whole song."
feature: export.range
anchors: ["export-range"]
keywords: ["range", "part", "start", "end", "loop region", "section", "excerpt", "seconds"]
related: [export-dialog, mixer-loop, loop]
reviewed: "007"
---

From the Advanced level the save dialog can export only part of the song.

- **Only the loop region** (mixer, when a loop is set) fills start and end with the loop's A and B points.
- **Start** and **End** are seconds from the beginning of the song, for example 30 and 75.5. The part must be at least
  0.5 s long and lie inside the song.
- Leave both empty to save the whole song.

The item's contents in the library then show the range, for example "drums, raw, 0:30-1:15".

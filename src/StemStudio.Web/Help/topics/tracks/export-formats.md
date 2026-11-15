---
id: export-formats
title: "File formats"
area: tracks
summary: "FLAC is lossless and the default, WAV is uncompressed, MP3 is small. All files are 44.1 kHz stereo."
feature: export.format-details
anchors: ["export-formats"]
keywords: ["flac", "wav", "mp3", "bit depth", "bit rate", "kbps", "24-bit", "16-bit", "quality"]
related: [export-dialog, library]
reviewed: "007"
---

| Format | What it is | Options (Advanced) |
|---|---|---|
| **FLAC** (default) | Lossless and compressed: the best choice for keeping a stem. | 16-bit or 24-bit |
| **WAV** | Uncompressed; opens in every audio program. | 16-bit or 24-bit |
| **MP3** | Lossy and small; good for a phone. | 192, 256 or 320 kbps (default) |

Every file is 44.1 kHz stereo. Without a choice, stems and stem packs are saved at 16-bit (the stems' own
resolution) and mixes at 24-bit (headroom for the summed channels). A raw stem saved as 16-bit FLAC is bit-identical to the stem StemStudio
separated. MP3 files carry no tags (title, cover) in this version.

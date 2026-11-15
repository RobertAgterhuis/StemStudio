---
id: export-clipping
title: "Lowered to avoid clipping"
area: mixes
summary: "When a mix would go above full scale, the whole export is turned down just enough. The item says by how much."
anchors: ["export-clipping"]
keywords: ["clipping", "lowered", "gain", "peak", "distortion", "too loud", "db", "full scale"]
related: [clipping, dbfs, library, export-dialog]
reviewed: "007"
---

Several loud channels together can add up to more than a digital file can hold (0 dBFS). That would clip: the
peaks are cut off and you hear distortion.

StemStudio never writes a clipped file. When the rendered export would go above full scale, the whole file is
turned down evenly so its loudest peak lands just below full scale (-0.1 dBFS). Nothing else changes: the balance
between the channels stays exactly as in the mixer.

The library then shows **Lowered by X dB to avoid clipping** on that item. To avoid it, pull the master or a few
loud channels down a little before you save.

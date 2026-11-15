---
id: sheet-music-piano
title: "Piano sheet music: accurate or fast"
area: tracks
summary: "Piano scores use a dedicated piano model (accurate) or the general model (fast). The accurate one is slow on a CPU."
feature: score.quality
anchors: ["sheet-music-piano"]
keywords: ["piano", "quality", "accurate", "fast", "piano model", "grand staff", "gpu", "cpu", "attribution", "licence"]
related: [sheet-music, sheet-music-profiles, gpu-cpu]
reviewed: "008"
---

For the **Piano** profile the dialog (Advanced) offers two qualities, each with its expected time:

- **Accurate** uses a model made for piano. It finds chords, fast runs and the sustain pedal much better. It runs on
  the GPU when one is available; on the **CPU** it can take several minutes for a song.
- **Fast** uses the same general model as the other instruments. It takes seconds but misses more notes in dense
  passages.

Without a choice StemStudio uses Accurate on a GPU worker and Fast on a CPU. Both write two staves split at middle C.

Piano items made with the accurate model carry the model's credit in the library: "Piano transcription model: Kong et
al., ByteDance (CC BY 4.0)."

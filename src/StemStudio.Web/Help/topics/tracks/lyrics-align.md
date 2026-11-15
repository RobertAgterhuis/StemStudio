---
id: lyrics-align
title: "Align lyrics to the vocals"
area: tracks
summary: "Times lyrics without timing word by word against the vocal stem. Automatic on a GPU, on request on the CPU."
anchors: ["lyrics-align"]
keywords: ["align", "alignment", "timing", "whisper", "speech model", "gpu", "cpu", "vocals", "paste"]
related: [lyrics-sources, karaoke, karaoke-offset, processing-device, gpu-cpu]
reviewed: "006"
---

Lyrics without timing (plain text from LRCLIB, or text you entered) are aligned to the song's vocal stem by a speech
model, so the karaoke ball can land on every word. When the vocals were split into detail stems, the lead vocal is used.

- On a **GPU** the alignment starts by itself once the stems are ready; it takes seconds.
- On the **CPU** the track page shows how long it will take, for example "about 4 minutes", and the alignment starts only
  when you click **Align to the vocals**.
- The first alignment downloads the speech model (about **1.6 GB**) once.
- While it runs you see the progress and, from the Advanced level, the device. **Cancel alignment** stops it.
- If it fails, the reason is shown and the text stays readable without timing. **Try again** retries; splitting the
  vocals into detail stems first often helps.

At Expert, lyrics that are timed per line can be aligned too, for word timing.

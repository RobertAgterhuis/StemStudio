---
id: stem-quality-measures
title: "How the estimate works"
area: insights
summary: "Bleed, artefacts and the main bleed source behind a stem rating."
feature: quality.details
levels: [advanced, expert]
anchors: ["quality-bleed-*", "quality-artefacts-*", "quality-index-*"]
keywords: ["bleed", "artefacts", "flicker", "dropouts", "leakage", "index", "playing together"]
related: [stem-quality, stem-quality-reliability]
reviewed: "020"
---

From Advanced the quality card shows the two measures behind each rating:

- **Bleed**: how much of a stem looks like a copy of another stem, for example the drums you still hear in the vocals.
  The level (Low, Medium, High) comes with the main source, such as "Bleed: medium, mostly drums".
- **Artefacts**: for now estimated from **dropouts only**: short holes where the stem suddenly falls silent and comes
  back, which the full mix does not have. Each instrument is judged against its own sound, so a drum hit that dies away
  naturally is not a hole. A watery, warbling sound ("flicker") is not measured yet, so a stem can sound watery and
  still show low artefacts. For detail stems two more kinds are listed: a large left-over of the detail model and a
  large share that went to the "rest" stem.

Limits:

- Kick and bass playing exactly together can look like bleed, because both stems then move at the same moments.
  StemStudio checks for this and caps the level at Medium with the reason "may be the two instruments playing together".
- Instruments that hold notes together on the same beats for the whole song, such as a bass and a guitar playing the
  same rhythm, can still read as bleed. The rating is then too strict; listen to the stem before you trust a "Poor".
- Very quiet parts of a stem are left out of the measure. A level shown as "unknown" means the measure was not good
  enough to show for this kind of stem.

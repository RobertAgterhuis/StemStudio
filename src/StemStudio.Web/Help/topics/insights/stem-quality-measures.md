---
id: stem-quality-measures
title: "How the estimate works"
area: insights
summary: "Bleed, artefacts and the main bleed source behind a stem rating."
feature: quality.details
levels: [advanced, expert]
keywords: ["bleed", "artefacts", "flicker", "dropouts", "leakage", "index"]
related: [stem-quality, stem-quality-reliability]
reviewed: "020"
---

From Advanced the quality card shows the two measures behind each rating:

- **Bleed**: how much of a stem looks like a copy of another stem, for example the drums you still hear in the vocals.
  The level (Low, Medium, High) comes with the main source, such as "Bleed: medium, mostly drums".
- **Artefacts**: flicker (a watery, warbling sound) and dropouts (short holes) that the full mix does not have. For
  detail stems two more kinds are listed: a large left-over of the detail model and a large share that went to the
  "rest" stem.

Limits: kick and bass playing exactly together can look like bleed, because both stems then move at the same moments.
StemStudio checks for this and caps the level at Medium with the reason "may be the two instruments playing together".
Very quiet parts of a stem are left out of the measure. A level shown as "unknown" means the measure was not good
enough to show for this kind of stem.

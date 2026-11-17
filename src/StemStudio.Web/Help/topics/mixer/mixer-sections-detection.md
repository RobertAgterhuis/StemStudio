---
id: mixer-sections-detection
title: "How sections are found"
area: mixer
summary: "What the automatic section detection looks at, its limits, and detecting again."
levels: [advanced, expert]
feature: sections.detect-again
anchors: ["section-status"]
keywords: ["detection", "detect again", "confidence", "grid", "automatic"]
related: [mixer-sections, mixer-sections-edit, beat-grid]
reviewed: "014"
---

StemStudio compares the separated stems over time: where the sound changes, where a part repeats and which instrument
leads. Repeating parts with vocals become verses and choruses; a part where one instrument leads without vocals becomes a
solo for that instrument. Everything runs on your own computer, without a GPU and without the internet.

Boundaries snap to bar lines when the tempo grid is reliable, to beats when it is uncertain, and to a tenth of a second
without a grid. When the grid is uncertain the lane says so.

The labels are a guess: songs without repetition, live recordings and medleys get fewer or rougher sections. At Expert
level each section shows how sure the detection was, and **Detect again** runs it once more. When you edited the
sections, StemStudio asks before it replaces them.

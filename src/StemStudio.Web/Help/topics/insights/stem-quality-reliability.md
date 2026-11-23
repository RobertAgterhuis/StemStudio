---
id: stem-quality-reliability
title: "How reliable the ratings are"
area: insights
summary: "Reliability labels, the validation state and the analyzer version of a quality report."
feature: quality.indices
levels: [advanced, expert]
anchors: ["quality-analyzer-version"]
keywords: ["reliability", "validated", "rough indication", "analyzer", "version", "index"]
related: [stem-quality-measures, stem-quality]
reviewed: "020"
---

Each measure carries a reliability label:

- **validated**: the measure was checked against real multitrack recordings for this kind of stem and agreed well
  enough.
- **rough indication**: not validated for this kind of stem.

**Validation state** (November 2026): the check on real music ran on 25 songs of MUSDB18-HQ, a public set of songs
with their separate instruments, and **failed** for vocals, drums, bass and other in both measures: the ratings did not
follow the real amount of bleed and artefacts. So every rating is a **rough indication**, and for vocals, drums, bass
and other Low and Medium are not shown (they read as unknown); only High is shown. The thresholds were checked on a
synthetic set, where known amounts of bleed and artefacts were mixed into clean stems on purpose; there the measures
ranked the amounts correctly. Guitar, piano, detail and rest stems are not in that public set; they stay rough
indications with every level shown. This is why the report is switched off by default
([Stem quality](help:stem-quality)).

What the rough indication leaves out today:

- Artefacts are estimated from dropouts (short holes) only; a watery sound is not measured yet.
- Instruments that hold notes together on the same beats can read as bleed (on the synthetic set, one of six stems in
  a song where every instrument plays all the time was rated High by mistake).

At Expert the card also shows the index values (0 to 1) and the **analyzer version** with the time of measuring. A new
StemStudio version can bring a new analyzer version; reports made with an older one are marked "Made with an older
version" and are not remade on their own. Press **Check again** to remake them.

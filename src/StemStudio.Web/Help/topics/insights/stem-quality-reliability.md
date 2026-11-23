---
id: stem-quality-reliability
title: "How reliable the ratings are"
area: insights
summary: "Reliability labels, the validation state and the analyzer version of a quality report."
feature: quality.indices
levels: [advanced, expert]
keywords: ["reliability", "validated", "rough indication", "analyzer", "version", "index"]
related: [stem-quality-measures, stem-quality]
reviewed: "020"
---

Each measure carries a reliability label:

- **validated**: the measure was checked against real multitrack recordings for this kind of stem and agreed well
  enough.
- **rough indication**: not validated for this kind of stem.

**Validation state** (November 2026): the check on real music has not run yet, so every rating is a **rough
indication**. The thresholds were checked on a synthetic set, where known amounts of bleed and artefacts were mixed
into clean stems on purpose; there the measures ranked the amounts correctly. Guitar, piano, detail and rest stems stay
rough indications even after the real check, because the models separate them less reliably.

At Expert the card also shows the index values (0 to 1) and the **analyzer version** with the time of measuring. A new
StemStudio version can bring a new analyzer version; reports made with an older one are marked "Made with an older
version" and are not remade on their own. Press **Check again** to remake them.

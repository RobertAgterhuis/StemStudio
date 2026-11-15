---
id: stem-limitations
title: "What StemStudio can separate"
area: import
summary: "Six stems: vocals, drums, bass, guitar, piano and other. More detail is limited and varies per song."
anchors: ["stem-limitations"]
keywords: ["limitations", "quality", "instruments", "model"]
related: [detail-limitations, stem]
reviewed: "004"
---

StemStudio splits a song into vocals, drums, bass, guitar, piano and other. When the 6-stem model is not
available the 4-stem fallback model gives vocals, drums, bass and other.

Separating every instrument individually is not reliably possible with current open-source models. After the
standard separation StemStudio tries to split drums, vocals and the other instruments further into
[detail stems](help:detail-stems). Their quality varies per song and instrument, some instruments cannot be
separated, and it takes considerably longer.

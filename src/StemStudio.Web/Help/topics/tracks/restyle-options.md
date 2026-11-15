---
id: restyle-options
title: "Restyle a track"
area: tracks
summary: "Makes a new version of a song in another style with AI: choose a style, a part of the song, the stems and the takes."
anchors: ["restyle-options"]
keywords: ["restyle", "style transfer", "cover", "new version", "ai", "generate", "take", "takes", "range"]
related: [restyle-strength, generation-ai-label, generation-personal-use, generation-limits]
reviewed: "009"
---

**Restyle** makes a new version of a completed track in another style, for example a jazz trio or synthwave version
of the same song. It works on the stems of the track and keeps its melody and structure as well as the style allows.

1. Press **Restyle** on the track page and choose a **style**. The dialog shows what the style sounds like.
2. From the Advanced level you can choose more:
   - **Start** and **End** in seconds: the take is as long as this part of the song, 10 seconds to 8 minutes. By
     default it is the whole song (at most the first 8 minutes).
   - **Stems in the source**: which stems the new version is made from. An instrumental style leaves the vocals out.
   - **Strength**: how far the take may move away from the song ([strength](help:restyle-strength)).
   - **Takes**: one or two versions with different seeds, so you can keep the best.
3. Press **Start**. The restyle waits for the graphics card if another job uses it, downloads the model the first time
   and then runs. You can cancel it while it is queued or running.

When it is done the take plays right here, is saved to your library and can be imported as a track. Every take is
[labelled AI-generated](help:generation-ai-label) and is [for personal use](help:generation-personal-use) only.

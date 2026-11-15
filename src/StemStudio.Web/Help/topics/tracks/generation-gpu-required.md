---
id: generation-gpu-required
title: "Generation needs a GPU"
area: tracks
summary: "Restyling, songs and voice conversion run only on an NVIDIA graphics card. Without one they are off; the rest works."
anchors: ["generation-gpu-required"]
keywords: ["generation not installed", "not available", "nvidia", "cuda", "graphics card", "generator", "model download"]
related: [generation-limits, restyle-options, processing-device]
reviewed: "009"
---

The generative features (restyling a track, and from feature 010 songs from your own lyrics and voice conversion) use
large AI models that only run on an **NVIDIA graphics card** with enough memory. The status line on these pages tells
you whether they can be used:

- **Generation is not installed.** The generator service is not running. Start StemStudio with the generative compose
  file (see the README) on a computer with an NVIDIA graphics card.
- **Generation is not available.** The generator runs but has no usable graphics card; the reason is shown, for
  example "CUDA is not available".
- **Generation is ready.** The first restyle downloads the model files (several GB) into the model cache; later ones
  start right away.

While generation is off, its buttons stay visible but disabled with the reason. Everything else (import, separation,
the mixer, the libraries and your own styles and lyrics) keeps working on the CPU.

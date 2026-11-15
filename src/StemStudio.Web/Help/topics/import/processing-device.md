---
id: processing-device
title: "Processing device: GPU or CPU"
area: import
summary: "Shows whether separation runs on the GPU or the CPU, and why the CPU is used."
anchors: ["processing-device"]
keywords: ["gpu", "cpu", "cuda", "speed", "device", "fallback"]
related: [gpu-cpu, track-progress]
reviewed: "004"
---

Separation is much faster on a supported graphics card (GPU). When no usable GPU is present StemStudio uses
the processor (CPU); it gives the same result, only slower.

If a GPU was requested but cannot be used, the reason is shown next to the device. The track page shows
the device each separation actually used, including a fallback to the CPU.

---
id: recording-latency
title: "Latency calibration"
area: mixer
summary: "Measure the delay of your audio setup so takes line up with the song."
anchors: ["recording-latency"]
keywords: ["latency", "calibrate", "delay", "click", "loopback", "clap test"]
related: [recording, recording-timing]
reviewed: "015"
---
Audio needs a moment to travel out of the speakers and back in through the input. StemStudio moves every take back by
that **latency** so it lines up with the stems.

- **Calibrate latency** plays a few clicks and listens for them. Best: connect an output of your interface to one of its
  inputs with a cable. Without a cable, hold a headphone cup against the microphone (a clap test is less exact).
- The result (for example 25.0 ms, 8 clicks found) is saved for this input and used for every take.
- If it fails, the dialog explains why: no clicks came back, too few, they arrived at different times, or the delay is
  longer than one second. Follow the tip and try again.
- From Advanced you can **enter the latency manually** (0 to 1000 ms in 0.1 ms steps). Expert also shows samples.
- Without a calibration StemStudio uses the browser's estimate and says so.
- "Your audio setup changed" appears when the output latency moved by more than 5 ms: calibrate again.

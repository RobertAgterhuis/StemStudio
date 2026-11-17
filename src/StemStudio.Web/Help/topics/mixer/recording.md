---
id: recording
title: "Play-along recording"
area: mixer
summary: "Record yourself over the mix and hear the take next to the stems. Recordings stay on this computer."
anchors: ["recording"]
keywords: ["record", "take", "microphone", "play along", "input", "privacy"]
related: [recording-permissions, recording-latency, recording-monitoring, recording-speed]
reviewed: "015"
---
Open the mixer of a song and press **Record** in the toolbar. The recording panel explains that recordings stay on this
computer: the audio goes only to the StemStudio server on this machine, never to the internet. It is never offered to the
AI composer and cannot be used as a voice sample.

1. Click **Allow microphone** and accept the browser prompt ([microphone permission](help:recording-permissions)).
2. Choose your **input**: a USB microphone or an input of your audio interface. Check that the input meter moves and does
   not show clip; set the gain on the interface itself.
3. Choose **what you record**: vocals, guitar, bass, keys, drums or other. "Mute the original part while recording" keeps
   the original stem silent so you hear yourself instead.
4. Press **Record** (or `R`). After the count-in you play along; press **Stop** (or `R` again).

The take plays at once in the **Take** strip next to the stems and appears in the **Takes** panel. "Saved" means the
server has stored it as lossless FLAC (24-bit, 44.1 kHz). Up to 200 takes per song.

From Advanced you choose mono input 1, mono input 2 or stereo. Expert shows dropouts, clipped samples and the device per
take. Every instrument family has its own setup tips: [vocals](help:recording-connect-vocals),
[guitar and bass](help:recording-connect-guitar-bass), [keys](help:recording-connect-keys),
[drums](help:recording-connect-drums) and [acoustic and other instruments](help:recording-connect-acoustic).

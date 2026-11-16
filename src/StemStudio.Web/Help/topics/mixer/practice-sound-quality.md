---
id: practice-sound-quality
title: "Sound quality at other speeds"
area: mixer
summary: "Why slow speeds and big transpositions sound smeary, and what the stretch diagnostics mean."
anchors: ["practice-sound-quality"]
keywords: ["quality", "artefacts", "smeary", "chipmunk", "soundtouch", "latency", "dropouts", "licence"]
related: [practice-speed, practice-transpose]
reviewed: "012"
---

Changing the speed without changing the pitch (time stretching) always cuts and overlaps tiny pieces of the sound.
Close to 100 % you hardly hear it. Below **70 %** attacks of drums and plucked strings get smeary, and beyond **7
semitones** voices sound like chipmunks or giants. The mixer shows a hint then; this is normal and not a fault.

- Lower speeds are for learning the notes; move up again as soon as you can.
- Shifted voices have no formant correction.
- The level meters react slightly before you hear the stretched sound (about a tenth of a second).
- Exports to the library always use the original speed and key.

At Expert level the **Stretch** line shows the state, the extra delay in milliseconds and the number of dropouts.
Dropouts mean the computer could not keep up; close other programs or use fewer detail stems.

If your browser cannot run the stretch module, speed and transpose are disabled with the reason, and everything else
keeps working at the original speed and key.

Time stretching uses SoundTouch 2.4.1 (LGPL-2.1), loaded as a separate, replaceable module. Its licence, the exact
source and the build script are shipped with StemStudio: [SoundTouch licence and source](/third-party/soundtouch/index.html).

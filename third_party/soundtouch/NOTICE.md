# NOTICE: SoundTouch in StemStudio

StemStudio (MIT licence) uses **SoundTouch 2.4.1**, an audio processing library by Olli Parviainen
and contributors, licensed under the **GNU Lesser General Public License version 2.1**
(`COPYING.TXT` in this folder, copied unchanged from the upstream release).

- Upstream: <https://www.surina.net/soundtouch/> and <https://codeberg.org/soundtouch/soundtouch>
  (tag `2.4.1`).
- Exact source used: `soundtouch-2.4.1.tar.gz`, SHA-256
  `e07abf20ce8f95850c280132e1f61ad400fc1f4011b7fac698a503de6aab6733` (pinned in
  `soundtouch.lock`). The upstream source is **not modified**.
- What StemStudio adds: the shim `shim/stemstudio_soundtouch.cpp` and the ABI header
  `include/stemstudio_soundtouch.h` (both LGPL-2.1-or-later, because they are compiled into the
  library module), and the build script `build-soundtouch.sh`.
- Form: SoundTouch and the shim are compiled into one separate WebAssembly file,
  `js/soundtouch/soundtouch.wasm`, which the web app loads at run time through the documented ABI
  (`README.md`). StemStudio's own code (`js/time-stretch-processor.js`, `js/audio-engine.js`) is
  MIT-licensed and contains no SoundTouch code.
- Your rights: you may modify SoundTouch, rebuild the module and replace
  `js/soundtouch/soundtouch.wasm` with your build (LGPL-2.1 section 6); `README.md` explains how.
  Nothing in StemStudio's licence or code prohibits this, or reverse engineering for debugging such
  modifications.

Every binary distribution of StemStudio (the web Docker image, release archives) contains this
folder as `/third-party/soundtouch/` with `COPYING.TXT`, this notice, the source archive, the shim,
the header and the build script; the web app serves it at `/third-party/soundtouch/`.

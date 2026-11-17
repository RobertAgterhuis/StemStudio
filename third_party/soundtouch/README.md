# SoundTouch module (`soundtouch.wasm`)

The time-stretch stage of the audio engine (feature 012, ADR-021) runs **SoundTouch 2.4.1**
(LGPL-2.1, unmodified) compiled to a standalone WebAssembly module. This folder holds everything
needed to rebuild that module and to replace it with your own build. Licence and attribution:
[`NOTICE.md`](NOTICE.md), [`COPYING.TXT`](COPYING.TXT).

| Path | Content | Owner |
|---|---|---|
| `soundtouch.lock` | upstream URL + SHA-256, emsdk image + digest, sources, compiler flags, ABI and delay constants | architect |
| `include/stemstudio_soundtouch.h` | the C ABI (version 1) the module exports | architect |
| `shim/stemstudio_soundtouch.cpp` | ABI implementation over `soundtouch::SoundTouch` | audio-engine (012 T020) |
| `upstream/` | downloaded, verified upstream source (git-ignored) | build script |
| `scripts/build-soundtouch.{sh,ps1}` (repository root) | download, verify, compile in the pinned emsdk image | devops (012 T010) |

The built file `src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm` is a build artefact and
is not committed; the git repository distributes no SoundTouch binary.

## How the module is used

`soundtouch.wasm` has **no imports** and exports linear `memory`, `_initialize` (Emscripten reactor
start-up; the host calls it once before anything else) and the functions of ABI version 1:

| Export | Signature (wasm32) | Meaning |
|---|---|---|
| `st_abi_version` | `() -> i32` | `1` |
| `st_create` | `(sampleRate, channels) -> i32` | new instance (tempo 1, rate 1, pitch 1), 0 on failure |
| `st_destroy` | `(h) -> void` | frees the instance |
| `st_set_pitch` | `(h, f64) -> void` | `SoundTouch::setPitch` |
| `st_set_setting` | `(h, id, value) -> i32` | `SoundTouch::setSetting`, 1 = accepted |
| `st_initial_latency` | `(h) -> i32` | `getSetting(SETTING_INITIAL_LATENCY)` in frames |
| `st_put` | `(h, ptr, frames) -> void` | `putSamples`, interleaved float32 |
| `st_receive` | `(h, ptr, maxFrames) -> i32` | `receiveSamples`, frames written |
| `st_clear` | `(h) -> void` | `clear()` |
| `st_malloc` / `st_free` | `(bytes) -> i32` / `(ptr) -> void` | buffers in linear memory |

The AudioWorklet processor (`js/time-stretch-processor.js`, MIT) receives the module's bytes from
the main thread, instantiates them with `WebAssembly.instantiate(bytes, {})` and drives one stereo
instance on the summed mix. Binding description: `specs/012-practice-tempo/contracts/audio-engine.md`
("WASM ABI", "Stretch stage").

## Rebuild

Requirements: Docker (or a local Emscripten SDK of the version in `soundtouch.lock`).

```sh
scripts/build-soundtouch.sh          # Linux, macOS, Git Bash
pwsh scripts/build-soundtouch.ps1    # Windows
```

The script reads `soundtouch.lock`, downloads `SOUNDTOUCH_URL` into `upstream/`, verifies
`SOUNDTOUCH_SHA256`, runs `em++` with `EMCC_FLAGS` over `SOUNDTOUCH_SOURCES` and the shim inside
`EMSDK_IMAGE@EMSDK_DIGEST` without network access, and writes `OUTPUT`. Two builds from the same
lock are bit-identical (measured in Phase 0, checked in CI). `--verify-reproducible` (`-VerifyReproducible`)
builds twice in fresh containers and fails unless both SHA-256 match; `--local` (`-Local`) compiles with the
`em++` of an activated local Emscripten SDK of the locked version instead of Docker.

## Replace the module with your own build (LGPL-2.1 section 6)

1. Take the SoundTouch source you want (for example the shipped `soundtouch-2.4.1.tar.gz`) and change
   it as you like.
2. Compile it together with `shim/stemstudio_soundtouch.cpp` and `include/` using the flags of
   `soundtouch.lock` (or your own, as long as the module exports ABI version 1 above and imports
   nothing). Keep `SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER`, otherwise the engine's fixed delay
   drifts on pitch changes.
3. Replace `js/soundtouch/soundtouch.wasm` in the web root (in the Docker image: mount your file over
   it) and reload the mixer. The processor checks `st_abi_version() == 1`; a module whose initial
   latency exceeds `STRETCH_DELAY_FRAMES - 256` frames may cause underruns.

Changes to SoundTouch itself are covered by the LGPL-2.1; StemStudio's own patches, if any are ever
needed, are kept as patch files next to the shim and published with the source.

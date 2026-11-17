# Research: Practice Tempo (012)

Architect, 2026-11-16. Decisions are binding through ADR-021; Phase 0 spikes (P0-1..P0-3) confirm
the numbers marked *to be measured* before the wave starts.

## R-01 Time-stretch library and WASM build

**Decision**: SoundTouch **2.4.1** (LGPL-2.1, upstream `https://codeberg.org/soundtouch/soundtouch`,
tag `2.4.1`), built by us with **Emscripten 6.0.13** (`emscripten/emsdk:6.0.13` Docker image, pinned
by digest in Phase 0; 6.0.7 was planned, see "Phase 0 results") into a standalone `soundtouch.wasm` with a small C shim exporting the ABI of
`contracts/audio-engine.md` ("WASM ABI").

| Option | Licence | Form | Status | Verdict |
|---|---|---|---|---|
| SoundTouch 2.4.1 + own Emscripten build | LGPL-2.1 | WASM, ~100-150 KB | upstream maintained; 2.4.1 in Debian unstable since 2026-05-08 (>= 2 weeks); 2.4.0 April 2025 | **chosen** (owner decision: SoundTouch as WASM) |
| `soundtouchjs` 0.3.0 (npm, cutterbl/SoundTouchJS) | LGPL-2.1 | hand-ported JavaScript of an older SoundTouch | slow release cadence; JS, not WASM; ships its own AudioWorklet helper | **fallback** if the WASM build fails (ADR-021 D7) |
| Prebuilt community WASM packages of SoundTouch | LGPL-2.1 | WASM | unpinned toolchains, unclear provenance, often bundle JS glue that assumes `fetch`/`importScripts` (not available in `AudioWorkletGlobalScope`) | rejected |
| Rubber Band | GPL-2.0+ / commercial | | GPL code in the browser bundle would force the MIT project to GPL | rejected by the owner (MoSCoW decisions, CTO-DECISIONS) |
| Signalsmith Stretch | MIT | WASM/JS | better quality at 50 % in published comparisons | not chosen: the owner decided on SoundTouch; noted for a possible later owner review (same `TimeStretchStage` port makes a swap local) |

Build (binding since Phase 0): `em++` with the `EMCC_FLAGS` of `third_party/soundtouch/soundtouch.lock`
(`-std=c++17 -O3 -flto -sSTANDALONE_WASM --no-entry -sINITIAL_MEMORY=4MB -sALLOW_MEMORY_GROWTH=0
-sSTACK_SIZE=256KB -fno-exceptions -fno-rtti -DNDEBUG -DST_NO_EXCEPTION_HANDLING=1
-DSOUNDTOUCH_FLOAT_SAMPLES=1 -DSOUNDTOUCH_DISABLE_X86_OPTIMIZATIONS=1
-DSOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER=1`, plus `-ffile-prefix-map` for reproducibility) over
the ten upstream sources listed in the lock plus `third_party/soundtouch/shim/stemstudio_soundtouch.cpp`.
`emcc` instead of `em++` fails to link (`operator delete`); WASM SIMD was measured and dropped (P0-1).
Upstream sources are **unmodified**; the tarball is fetched by `scripts/build-soundtouch.*` and
verified by SHA-256 (recorded in the lock). Versions: SoundTouch 2.4.1 (released
2026-03-29) and emsdk 6.0.13 (2026-10-19) are both older than two weeks.

Loading in the worklet: `AudioWorkletGlobalScope` has no `fetch`/`importScripts`, so the main thread
fetches the WASM bytes once and passes them in `processorOptions.wasmBytes`; the processor calls
`WebAssembly.instantiate(bytes, imports)` and outputs silence until it posts `ready`. This works the
same in Chromium and Firefox, needs no `SharedArrayBuffer` and no COOP/COEP headers. The app sends
no Content-Security-Policy today; if one is added later it must allow `'wasm-unsafe-eval'`
(recorded in the technical manual).

## R-02 Where the stretch happens (push graph)

An `AudioWorkletNode` in the middle of a Web Audio graph is *push-based*: every render quantum it
receives 128 frames and must return 128 frames. It therefore cannot change the duration of the
signal (tempo) by itself. Options:

| Option | How | Verdict |
|---|---|---|
| A. Rate + pitch correction | every `AudioBufferSourceNode` plays at `playbackRate = r` (the browser resamples, duration and pitch change together); one SoundTouch stage on the summed mix shifts the pitch back by `p = 2^(st/12) / r` (tempo 1, so 128 in = 128 out) | **chosen** |
| B. Pull-based stretch source | a worklet owns all stem buffers, stretches and mixes them itself | rejected: re-implements mixer, gates, pan, meters and detail groups in the worklet; copies ~250 MB of PCM into WASM memory; loses the native sample-accurate loop |
| C. Offline pre-render | render stretched stems with an `OfflineAudioContext` per setting | rejected: seconds of delay per change, double memory, not "live" |

Option A keeps everything the engine already does well: one shared `when`/`offset` for every source
(sample lock), native loops (`loopStart`/`loopEnd` are in buffer time and keep working with
`playbackRate`), per-channel gain/pan/meters and the detail-group gate swaps. Speed changes become an
`AudioParam` automation (`setValueAtTime` on every source at the same time) plus one pitch message to
the stage. Quality is the same as using SoundTouch's own tempo control: SoundTouch implements pitch
`p` internally as tempo `1/p` (WSOLA) plus resampling by `p`, i.e. exactly the stretch ratio a
direct tempo change would use; the extra step is the browser's resampling at `r`.

## R-03 Summed mix versus per stem

**Decision**: one stereo stage on the summed mix.

- Sample lock: WSOLA chooses splice points from the signal; separate instances per stem would
  splice at different points and shift stems against each other by up to the seek window
  (~15-25 ms), breaking the <= 1 ms lock (FR-004). One instance on the sum cannot desynchronise
  stems.
- CPU: 6-20 channels would cost 6-20 instances (and 20 FIFOs of latency to keep equal); the sum
  costs one, independent of the channel count (SC-004).
- Per-stem transposition (e.g. keep drums unshifted) is not requested; a later feature could add a
  second stage for a group without changing the contract.
- Consequence: channel meters sit before the stage and lead the sound by `L` while engaged
  (R-05); the master meter sits after it. Documented in help.

## R-04 SoundTouch settings and quality at 50 %

Settings: float samples; `setTempo(1)`, `setRate(1)`, `setPitch(p)`; `SETTING_USE_QUICKSEEK = 0`
(full search, better quality; CPU allows it, R-06); anti-alias filter on (64 taps); sequence and
seek window automatic (SoundTouch adapts them to the effective tempo), overlap 8 ms. Upstream 2.3.0
made a zero tempo change bit-transparent, so pitch factor 1 passes audio through (used when the
user returns to 100 %/0 while engaged).

Quality expectations (WSOLA): at 80-120 % artefacts are hard to hear on most material; at 70 %
slight "phasiness" on sustained chords; at 50 % audible smearing of drum attacks and a doubled,
echo-like sound on vocals. Pitch factor range of the product: `0.42..4.0` (50 % with +12 st gives
4.0, i.e. an internal 25 % stretch, clearly degraded). The UI hint threshold (below 70 % or
beyond +-7 semitones) follows these expectations; P0-1 produces listening samples of the
reference song at 50/70/85 % and +-7/12 st for the CTO's close-out note (not committed: copyrighted
audio stays local). Formants are not preserved (SoundTouch has no formant correction) - limitation
in help.

## R-05 Latency and its compensation

SoundTouch buffers input before producing output (initial latency ~ sequence + seek window,
dependent on the pitch factor; readable with `getSetting(SETTING_INITIAL_LATENCY)`). Its output
count per quantum varies, so the processor wraps it in a FIFO with a **fixed delay D** (input frame
`n` is heard at `n + D`): constant latency for the whole session, independent of the factor.
`D = STRETCH_DELAY_FRAMES` is measured in P0-1 (max initial latency over factors 0.42..4.0 + 2
quanta, rounded up to 128); expected 6,144-10,112 frames (139-229 ms at 44.1 kHz), hard limit 230 ms.

Compensation: sources start `L = D / sampleRate` earlier than the anchor's `when` (`start(when -
L, offset)`), so the anchor still describes what is *heard*. The metronome and count-in clicks
(direct to the destination), the karaoke clock and `getPosition()` therefore need no change of
their own beyond the rate factor. Cost: playback starts `L` later after pressing play when
engaged (`when >= now + L + startDelay`), and a live change is heard after `L + 20 ms` (<= 250 ms
with the 230 ms limit, SC-001; P0-1 confirms the limit or SC-001 is amended before the wave).

Engagement: the stage is loaded on demand and inserted only when needed. Inserting it while playing
changes the latency from 0 to L, so the engine restarts the sources at the heard position once (gap
<= startDelay + L, faded). Afterwards it stays engaged until the next restart at neutral settings, so
returning to 100 % never causes a gap.

## R-06 CPU cost

Per quantum the stage processes 128 stereo frames; WSOLA with full search at 44.1 kHz costs, in
native code, roughly 1-2 % of one core; WASM is typically 1.3-2x slower than native. Budget: the
stage must use <= 35 % of the quantum's real time (2.9 ms) on the reference machine and run >= 10x
real time in Node on the CI runner (SC-004). The channel count does not matter (R-03). P0-2 measures
in Chromium and Firefox with the 20-channel fixture (6 stems + 14 detail stems) at 50 % for 10
minutes, recording `underruns` and the worklet's process time (`performance.now()` is not
available in the worklet in every browser; the processor measures with `currentFrame` deltas against
`Date.now()` in a diagnostics build only).

## R-07 LGPL-2.1 obligations

StemStudio is MIT. SoundTouch is LGPL-2.1, which allows use from non-GPL code when the library stays
replaceable and its source is available:

1. **Separate module**: SoundTouch lives only in `soundtouch.wasm`, a file loaded at run time through
   a documented ABI (contract "WASM ABI"); our code (MIT) is in `time-stretch-processor.js` and the
   engine. This is the "suitable shared library mechanism" of LGPL-2.1 section 6(b): a user can
   replace `wwwroot/js/soundtouch/soundtouch.wasm` with their own build of (modified) SoundTouch.
   `third_party/soundtouch/README.md` documents the ABI, the build command and how to replace the
   file. Nothing in our licence or code prevents modification or reverse engineering.
2. **Source**: unmodified upstream source, pinned by tag and SHA-256; the shim
   (`stemstudio_soundtouch.cpp`, LGPL-2.1-or-later because it is compiled into the library module)
   and the build script are in the repository. Every **binary distribution** (the web Docker image,
   any release archive) carries `/third-party/soundtouch/` with `COPYING.TXT` (LGPL-2.1 text), the
   upstream source archive, the shim, the build script and a NOTICE; the web app serves this folder
   statically, and the help topic `practice-sound-quality` and the technical manual link to it.
   The git repository itself distributes no SoundTouch binary (the WASM is a git-ignored build
   artefact), so no extra obligation arises there.
3. **Changes to SoundTouch**: none planned; any future patch is LGPL-2.1, kept as a patch file next
   to the shim and published with the source (MoSCoW decision 2026-11-16).
4. **Records**: CTO adds SoundTouch 2.4.1 (LGPL-2.1, URL, SHA-256) to `docs/THIRD-PARTY-LICENSES.md`
   with a short "LGPL module" note; emsdk is a build tool (MIT/University of Illinois NCSA, not
   distributed).

## R-08 Interaction with existing functions

| Function | Effect at speed r / transpose st | Decision |
|---|---|---|
| Sample lock (001/002) | all sources share `when`, `offset`, `playbackRate` automation times | kept (one stage on the sum) |
| Position, `OnPositionChanged` | track seconds; advances at `r` | anchor `offset + (t - when) * r` |
| A-B loop (001) | in track time; native loop works with `playbackRate` | unchanged; explicit passes only for trainer / count-in every pass |
| Metronome (001) | grid in track time, mapped with `/ r`; clicks not stretched | timeline gets the rate; `bpmOverride` stays the *song's* tempo; UI shows the effective tempo |
| Beat grid | unchanged (track time) | - |
| Detail groups (002) | gate swaps pre-stage; late-decoded groups start on the current anchor | unchanged rules, latency-aware start time |
| Karaoke (006) | clock reads the rate-aware anchor; latency already compensated | formula unchanged; ball slows with the music; offset stays in track ms |
| Score/lyrics positions (006/008, later S5 in 014) | all in track time | no change; 014's "score follows playback" reads the same position |
| Meters (001) | channel meters lead by L while engaged | documented limitation |
| Exports (007) | rendered by the worker from `ExportMix` | **never** slowed/transposed (R-09) |
| Agent (011) | mix tools round-trip `MixSettingsDto` | `practice` carried through; agent tools for tempo come with a later catalogue minor |

## R-09 Exports

**Decision**: exports always use the original speed and key. Reasons: (1) the worker renders
exports from a server-side plan with bit-exact render vectors (ADR-016); adding SoundTouch to the
worker would add a second, native implementation whose output differs from the browser's and needs
its own LGPL packaging in the worker image; (2) library items should be the real recording, so
sheet music (008), style transfer (009) and the agent (011) never receive a slowed file by
accident; (3) the export dialog states it (`export-practice-note`). A "practice version" export is
better served by play-along recording (015), which records what the user hears; noted there.

## R-10 Speed trainer semantics

A "clean pass" cannot be detected without listening to the player (015/016 territory), so every
completed pass (reaching B) counts and `passesPerStep` (1-8) lets the user repeat each speed.
Speeds are integers; the step is applied per pass boundary and capped at the target. Implementing
steps at pass boundaries needs exact pass times: the engine plays **explicit passes** (one set of
sources per pass, `start(T_k - L, A, B - A)`) created ahead by the look-ahead timer, so each pass has
a constant rate and its own anchor; no automation inside a pass. The trainer progress is not saved
(it restarts on stop), the settings are.

## R-11 Count-in and pre-roll

Count-in = clicks in silence before the music, 1 or 2 bars of the metronome's beats per bar, at the
song's tempo times the speed. To make the music enter "on the beat" the clicks continue the song's
grid backwards from the first beat `b >= A`: clicks at `T0 + (b - A)/r - k*d/r`. Tempo source:
`bpmOverride` or the reliable grid (the same click grid the metronome uses); none -> unavailable.
"Pre-roll" in the DAW sense (play the music from before the start point) is excluded: it changes
the loop semantics and is needed for punch-in recording (015), where it will be specified. Count-in
before every pass uses the explicit passes of R-10 with a gap of N beats.

## R-12 Persistence and compatibility

`MixSettings.practice` (optional, defaults) in backend-api 1.11.0; stored as one JSON column
`Practice` on `Mixes` (EF Core complex property `ToJson`, nullable; migration `AddMixPractice`);
`null` reads as the defaults. `PUT` without `practice` resets it to the defaults (replace semantics,
as `expanded`/`detailChannels` in 1.2.0). Validation: ranges, integers, `startPercent <
targetPercent`; trainer/every-pass without a loop are allowed and inactive (saving must not fail
because a loop was cleared). Duplicating a mix copies `practice`. `MixSummary` unchanged.

## R-13 Testing and E2E measurement

- Engine unit tests: stretch stage replaced by a fake (`timeStretchFactory`); assertions on
  recorded `start(when, offset, duration)`, `playbackRate.setValueAtTime(value, time)`, click
  times and `setPitch(factor, atFrame)` against the mock clock (exact numbers, no audio).
- Processor unit tests: fake `AudioWorkletGlobalScope` + fake ABI (TypeScript identity "SoundTouch"
  with a configurable latency/jitter) to test the FIFO, the fixed delay, underrun/overflow counting,
  the message protocol and the no-allocation rule (spy on `st_malloc` after ready).
- Real WASM in Node (`npm run test:wasm`): sine FFT peak, identity, latency, throughput.
- Playwright on the fake stack with real browsers and the real WASM: an **output probe** init script
  (qa, `e2e/support/output-probe.ts`) wraps `AudioNode.prototype.connect` so every connection to
  `context.destination` is also connected to a probe `AnalyserNode`; the test reads the FFT peak of a
  stereo 440 Hz tone fixture (`tone_440hz_20s.flac`, self-generated) for pitch, and the transport
  position (`transport-position`, 10 Hz) against wall time for speed; a click-schedule probe wraps
  `AudioBufferSourceNode.prototype.start` to record the count-in click times; the karaoke probe of
  006 measures landing errors at 75 %. CI runs Chromium and Firefox with the PulseAudio null sink as
  today.
- Per instrument family (SC-008): the fake stack produces six stems from the fixture; one
  parameterised E2E case per family solos (or mutes) that family's stem, sets a speed/transpose and
  checks the output probe and the position.

## R-14 Browser support

AudioWorklet and WebAssembly (incl. SIMD if used) are available in current Chromium and Firefox
(the tested browsers since 001). Missing support (or a failing module) leads to `stretch:
'unavailable'` with the reason; everything else keeps working (FR-009). Safari is untested, as
before.

## Phase 0 spikes (architect, before the wave)

- **P0-1 WASM build and offline measurements** (scratch container, Node): build SoundTouch 2.4.1 with
  emsdk 6.0.7 and the shim; record wasm size, SIMD on/off throughput and equality, initial latency
  for pitch factors {0.42, 0.5, 0.71, 1, 1.41, 2, 2.83, 4}; choose `STRETCH_DELAY_FRAMES`; FFT pitch
  accuracy on a 440 Hz sine at those factors; write `third_party/soundtouch/soundtouch.lock`
  (version, URL, SHA-256 of the tarball, emsdk image digest). Exit: build reproducible twice
  bit-identically; latency <= 230 ms; >= 10x real time. Failure -> fallback soundtouchjs (ADR-021 D7)
  and plan/tasks amended before the wave.
- **P0-2 Browser spike**: a throw-away page with the processor in Chromium and Firefox: 20-channel
  fixture at 50 % for 10 min, underruns = 0, process time per quantum; live change latency from
  `setValueAtTime` to the probe's frequency change (SC-001).
- **P0-3 Contract merge**: merge 011's backend 1.10.0 and ui-test-ids 1.10.0 (or the delta) into the
  012 documents (1.11.0) and the possible engine 2.2.0 into audio-engine.md 2.3.0; rerun
  `check_superset.py` against 1.10.0 and the Redocly lint.

## Phase 0 results

Measured 2026-11-16 on the reference machine (Intel Core Ultra 9 285K, 127 GB, Windows 11) in
throw-away containers and pages outside the repository; the owner's `stemstudio` stack was not
touched (the spikes used no compose project at all: `docker run --rm --network none` for the
builds, a local Node HTTP server on a random port for the browser page). Decisions taken from these
numbers: ADR-021 "Phase 0 outcome" P1-P8.

### Versions and licences (re-verified)

| Item | Pinned | Checked |
|---|---|---|
| SoundTouch | **2.4.1** (tag 2026-03-29; newest release; 2.4.0 2025-04-06) | `SOUNDTOUCH_VERSION "2.4.1"` in `SoundTouch.h`; `COPYING.TXT` = LGPL-2.1 (SHA-256 `84c6d82d...7e0a`); no source change needed |
| Source artefact | `https://www.surina.net/soundtouch/soundtouch-2.4.1.tar.gz`, 607,365 B, SHA-256 `e07abf20ce8f95850c280132e1f61ad400fc1f4011b7fac698a503de6aab6733` | static release file (Last-Modified 2026-03-29); Codeberg tag archive `35d404e6...77ec` (608,032 B, stable over two downloads) has identical content (`diff -r` empty) but is generated on demand -> information only |
| Emscripten | **6.0.13** image `emscripten/emsdk:6.0.13@sha256:fb7864e398acfda863dfcea78b8bc8adad359fa60c6a0bbeb845931de64aa68a` (2026-10-19) | 6.0.7 was planned; 6.0.13 is the newest 6.0 patch older than two weeks; 7.0.0 (2026-11-06) is too new. MIT / University of Illinois NCSA, build tool only |
| Browsers | Playwright 1.65.1: Chromium 159.0.8154.6 (headless), Firefox 159.0 | as in the E2E suite |

### P0-1 WASM build and offline measurements (Node 22.23.2)

| Measurement | Result | Exit criterion |
|---|---|---|
| Build | `em++` (C++17) with the lock flags; `emcc` fails to link `operator delete` | - |
| Module | **30,168 B** (scalar) / 31,238 B (SIMD); **no imports**; exports `memory`, `_initialize`, the 11 ABI functions (+ Emscripten stack helpers) | - |
| Reproducibility | two builds in fresh containers: identical SHA-256 (scalar `868d3aee...7297`, SIMD `64e15cb8...a8be`; spike shim) | bit-identical: **met** |
| SIMD128 | output bit-identical to scalar (max difference 0) at all 8 factors; throughput within +-1 % | -> **SIMD off** |
| Initial latency `SETTING_INITIAL_LATENCY` (frames) for p = 0.42 / 0.5 / 0.71 / 1 / 1.41 / 2 / 2.83 / 4 | 4377 / 3839 / 4333 / 4013 / 4498 / 4852 / 4851 / 4851 | max 4,852 |
| Output lag (frames put minus frames received, per quantum), constant factors | max 4,736 | - |
| Same, 120 s of random changes every 0.25-1 s, and 120 jumps 0.42 <-> 4 | max 4,736, no drift | - |
| Same **without** `SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER` | random changes 54,554, jumps 205,608 frames (4.6 s): ~1,000 frames lost per crossing of p = 1 | -> flag mandatory |
| `STRETCH_DELAY_FRAMES` | max(4,852, 4,736) + 256, rounded up to 128 = **5,120 frames = 116.1 ms** | <= 230 ms: **met** |
| Pitch accuracy, 440 Hz sine, FFT 65,536 + parabolic interpolation | error 0.001-0.017 % at all 8 factors | 1 %: **met** |
| Identity at p = 1 | max error 3.2e-6, offset 0 (the 2.3.0 "bit-transparent" note is "practically transparent") | < 1e-4: **met** |
| Throughput, stereo 44.1 kHz music-like signal, one core | 85x (p = 4) .. 369x (p = 0.42) real time | >= 10x: **met** |
| Content offset `c(p)` (envelope cross-correlation on 54 noise bursts; mean / p5..p95 spread) | +32 ms / 75 ms at 0.42; +22 / 32 at 0.5; +12.5 / 18 at 0.71; 0 / 0 at 1; -11.6 / 21 at 1.41; -28 / 20 at 2; -28 / 49 at 2.83; -40 / 45 at 4 | model `c(p) = -1100 * log2(p)` frames; residual within the spread |
| Pitch-change timing, combined speed change 100 -> 50 / 100 -> 75 / 75 -> 100 % (wrong-pitch time) | apply at input frame: 70 / 81 / 70 ms; when output reaches the frame: 58 / 58 / 70 ms; **midpoint rule**: 23 / 12 / 12 ms | -> midpoint rule |
| Memory | 4 MiB fixed; 2.1 MiB free before, 1.83 MiB after one stereo instance and 58 s of extreme jumps | growth not needed |
| Argument checks | `st_create(0, 2)` = 0, `st_create(44100, 3)` = 0, unknown setting -> 0 | as in the ABI |

### P0-2 Browser spike (Chromium and Firefox, real AudioWorklet + WASM)

Page: 20 looped stereo sources (sine + noise bursts, 30 s buffers) at `playbackRate` 0.5, each
through gain -> panner -> analyser -> sum -> the spike processor (pitch 2, fixed delay 5,120,
midpoint rule) -> master -> analyser (read every 16 ms like the meters) -> destination; stage cost
probed inside the worklet by running 200 extra quanta through a second instance every 3,000
quanta (`performance` is not available in the worklet scope, `Date.now()` over 200 quanta).

| Browser | 10 min at 50 % with 20 channels | Stage cost per 128-frame quantum (budget 2.90 ms) | Change latency (30 transpose steps, call -> frequency change at the stage output) |
|---|---|---|---|
| Chromium 159 | 600.7 s wall = 600.7 s audio; **0 underruns, 0 drops** | mean 0.019 ms, max 0.025 ms (**0.9 %** of the budget) | 87 / 116 / 151 ms (min / median / max); `outputLatency` reported 0.192 s (headless fake device) |
| Firefox 159.0 | 600.6 s wall = 601.3 s audio; **0 underruns, 0 drops** (a `dotnet build`/test run was active on the same machine for part of the 10 minutes: a stricter run) | mean 0.017 ms, max 0.025 ms (**0.9 %**) | 93 / 119 / 160 ms; `outputLatency` 0.041 s |

Result: SC-004 (0 dropouts in 10 min, stage <= 35 % of the budget) and SC-001 (<= 250 ms from
the input to the probe) hold with a wide margin; ADR-021 is **Accepted**. The device output latency
adds to what is heard at the speaker in every browser, with or without the stage. R1 of the
quickstart repeats the check with the real 20-channel song in the integration wave.

### P0-3 Contract merge

See contracts/DELTA.md section 5: backend-api 1.11.0 = 1.21.0 + 012 (superset yes, Redocly valid, 9
inherited warnings), ui-test-ids 1.11.0 = 1.21.0 + delta, audio-engine 2.3.0 (2.2.0 unused), WASM
ABI 1.

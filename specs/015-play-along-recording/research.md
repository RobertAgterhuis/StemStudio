# Research: Play-along Recording

Decisions behind [plan.md](plan.md) and ADR-024. Each item: decision, rationale, alternatives.
Items marked **Spike** are verified in Phase 0 (tasks T003) on the reference machine before the
wave starts; the design does not depend on their outcome except where stated.

## R-01 Capture mechanism

**Decision**: `getUserMedia` -> `MediaStreamAudioSourceNode` -> `AudioWorkletNode`
(`stemstudio-capture`) in the engine's 44.1 kHz `AudioContext`. The processor copies each
128-frame render quantum of the selected channel(s) into a ring of Float32 blocks and posts a
batch every 2,048 frames with the global `currentFrame` of its first frame. The node's output is
connected to a zero-gain node into the destination so the browser keeps pulling it.

**Rationale**: `AudioWorkletGlobalScope.currentFrame` is the same frame timeline as
`AudioContext.currentTime * sampleRate`, so every sample gets an exact context position; the
worklet runs on the audio thread (no main-thread glitches); the browser resamples a 48 kHz
device to the context rate inside the graph.

**Alternatives**: `MediaRecorder` (010) - lossy, no frame timestamps, start offset unknown.
`ScriptProcessorNode` - deprecated, main thread. Separate capture context - second clock, drift.
WebCodecs `AudioData` from `MediaStreamTrackProcessor` - Chromium only, timestamps on another
clock.

**Spike S-01**: in Chrome and Edge, a 48 kHz USB interface into the 44.1 kHz context: capture is
gap-free for 10 minutes (frame stamps contiguous); measure CPU. In Firefox, check whether
`createMediaStreamSource` still throws `NotSupportedError` for a rate mismatch (ADR-024 D8).

## R-02 Input constraints and channels

**Decision**: constraints `{ deviceId: { exact }, channelCount: { ideal: 2 },
echoCancellation: false, noiseSuppression: false, autoGainControl: false,
voiceIsolation: false, latency: { ideal: 0 } }` (unknown keys are ignored by browsers). Channel
modes: `mono-1` (channel 0), `mono-2` (channel 1, only when the track has 2 channels), `stereo`.
The device's channel count comes from `track.getSettings().channelCount`.

**Rationale**: processing (AEC/NS/AGC) changes the sound, adds latency and would try to cancel
the mix; musicians need the raw signal. Browsers expose at most two channels of a multi-input
interface in practice; the help topic says so and suggests the interface's own routing.

**Alternatives**: software input gain (cannot fix clipping at the converter; omitted, the meter
and help guide hardware gain); more than 2 channels (not reliably available in browsers).

## R-03 Round-trip latency model

The heard mix and the captured input relate through one constant per session:

```text
song audio scheduled at context time T  -> heard at T + P + outLat       (P = 012 tempo stage, 0 when bypassed)
user plays in time with what they hear  -> reaches the capture at T + P + outLat + inLat
click scheduled at T on the destination -> captured at T + outLat + inLat = T + L
=> captured frame f belongs to song position pos(f - L - P)
```

`pos` is the ADR-005 transport mapping (`offset + (t - when)`, wrapped into the loop), evaluated
per segment (a play start, a loop pass, a 012 count-in gap). The loopback click is scheduled on
the destination directly, so it measures `L` without `P`; `P` is added from the engine.

**Decision**: compensate at capture time by choosing which captured frames form the take; no
resampling or stretching. Store `latencySamples = L + P` and `latencyMethod` on every take.

## R-04 Loopback calibration algorithm

**Decision**:
1. Pause playback, mute the mix and monitoring, open the selected input.
2. Render a click template once: 5 ms logarithmic chirp 500 Hz -> 8 kHz, Hann-windowed, peak
   -6 dBFS.
3. Schedule 8 clicks on the destination at `t0 + c_k`, with gaps drawn from 0.45-0.75 s by a
   seeded PRNG (irregular, so a periodic echo cannot be mistaken for the click); capture from
   `t0` to the last click + 1.0 s (total about 6.5 s).
4. For each click, search the capture window `[c_k, c_k + 0.8 s)` for the maximum of the
   normalised cross-correlation with the template (FFT-free direct correlation on a 4x
   decimated envelope to find the coarse lag, then full-rate correlation in +/-64 samples for
   the exact lag).
5. A click is found when its correlation peak is >= 0.3 and >= 6x the median absolute
   correlation of its window. Result: `ok` when at least 6 clicks and at least 75 % of the
   clicks are found (6 of the default 8) and their spread
   (max - min lag) is <= 44 samples (1 ms); `latencySamples` = median lag.
6. Failure reasons: `noSignal` (capture peak < -60 dBFS), `tooFewClicks` (fewer than that),
   `inconsistent` (spread > 1 ms), `tooLong` (lag > 0.8 s). `clipped` (capture peak >= -0.1
   dBFS) is a warning, not a failure.

Implemented as a pure function `estimateLatency(capture, clickFrames, template, options)` in the
engine, unit-tested with synthetic signals: exact delays (0, 128, 1,000, 4,410, 20,000 samples),
white noise at -20/-40 dB SNR, a 120 ms echo, missing clicks, clipping, jitter of +/-2 samples.

**Alternatives**: impulse with threshold onset (sensitive to noise and to the transducer's
smearing); MLS/sine-sweep impulse response (more robust, but longer and harder to explain);
tap-along calibration (human asynchrony of 20-50 ms makes it useless for sample accuracy).

**Manual fallback**: slider and number field 0-1000 ms (0.1 ms steps), stored as samples
(`round(ms * 44.1)`, half away from zero). The help topic explains the "clap test": record a few
claps on the metronome and adjust until the take's claps line up with the clicks.

**Estimate before calibration**: `round((ctx.baseLatency + ctx.outputLatency +
track.getSettings().latency) * 44100)`; missing values count as 0; labelled "estimated".

## R-05 Calibration storage and invalidation

**Decision**: backend table `InputCalibrations` keyed by the device **label** (unique), with the
`deviceId` as a hint, plus `outputLatencySamples` (`round((baseLatency + outputLatency) * 44100)`
at measurement time). At panel open the engine reports the current output latency; a difference
> 220 samples (5 ms) shows "Your audio setup changed - calibrate again" (the stored value is
still used until then).

**Rationale**: browser `deviceId`s are per-origin and reset when site data is cleared; labels are
stable. Storing in the backend keeps calibrations across browsers and in the 013 backup.

**Alternatives**: browser storage (lost with site data; not in backups); keyed by `deviceId`
only (lost on clear); keyed by input and output pair (the browser does not expose a stable
output device identity without `setSinkId` permissions).

## R-06 Speed and transpose (012)

**Decision**: record only at speed 1.0 and 0 semitones; listen back at any setting (the take
channel uses the same tempo/pitch stage as the stems). The speed trainer is paused and its
controls disabled while recording.

**Rationale**: at speed `s` the captured audio is `1/s` times longer than the song span it
covers. Aligning it needs a time-stretch of the user's performance (audible artefacts on exactly
the audio the user wants to judge), compensation for SoundTouch's processing delay (which depends
on the tempo), and a stretch step in the 007 renderer for exports - a worker contract change not
allocated to 015. Backlog item "slow-take recording" keeps the option open.

**Alternatives**: store slow takes unaligned and play them only at their recording speed
(confusing; exports at 100 % impossible); stretch in the browser after recording (artefacts,
export still at the wrong length).

## R-07 Upload path and format

**Decision**: the engine encodes the compensated span as 24-bit PCM WAV (canonical 44-byte
header, `WAVE_FORMAT_PCM`, little-endian, 44.1 kHz, 1-2 channels). Float samples are clamped to
[-1, 1) and quantised with `clamp(round(x * 2^23), -2^23, 2^23 - 1)` (the libsndfile scale of
ADR-016, so the worker's render reads exactly these integers). Blazor receives the WAV as an
`IJSStreamReference` (`OpenReadStreamAsync(maxAllowedSize: 256 MiB)`) and the typed API client
streams it as multipart part `audio` with a JSON part `metadata`.

**Rationale**: lossless; trivial to validate in .NET without a codec; Blazor's stream interop
handles hundreds of MB; CORS stays GET-only.

**Sizes**: 1 minute stereo = 15.9 MB, mono = 7.9 MB; 15 minutes stereo = 238 MB (limit 256
MiB). **Spike S-02**: stream a 240 MB WAV through the interop and the API on the reference
machine; target < 15 s for 5 minutes stereo (SC-004).

**Alternatives**: browser FLAC (WASM dependency, e.g. libflac.js; also not needed because the
worker writes FLAC); Float32 WAV (larger, and the integer path keeps the bit-exact guarantee);
direct `fetch` to the API (needs CORS POST).

## R-08 Server-side storage via the 007 renderer

**Decision**: the API writes `assets/<assetId>/capture.wav` = 44-byte header + `startSample *
blockAlign` zero bytes + the uploaded `data` chunk (streamed, no decode), creates the take's
Asset (`origin: recording`, status `queued`) and a worker render job with one input
`{ path: "assets/<assetId>/capture.wav", gain: 1, pan: 0 }`, `masterGain: 1`, output
`{ format: flac, bitDepth: 24, dither: false }`, metadata `{ title: <take name>, comment:
"Recorded with StemStudio" }`. The 007 asset sync downloads `output.flac` as role `audio`
(`audio.flac`) and marks the asset `ready`; the take service then deletes `capture.wav`.

**Bit-exactness** (SC-006): unity gain, pan 0 and master 1 leave the float32 samples unchanged;
float32 represents every 24-bit integer exactly; peak of a 24-bit signal is < 1.0 so no
scaling; no dither; libsndfile writes the same integers. Mono input is duplicated to stereo
(007 data-model 2.2). Verified by a worker regression test (no contract change) and an API
integration test that decodes the FLAC. A take whose samples are all zero is rejected by the API
before the render (the renderer would fail it as "Nothing to export").

**FLAC size of the padding**: digital silence becomes constant subframes (a few bytes per
4,096-sample block and channel), about 20 KB for 4 minutes.

**Alternatives**: see ADR-024 D4.

## R-09 Playback of takes

**Decision**: `loadTake` decodes the FLAC (`decodeAudioData`, context rate 44.1 kHz), copies
`[startSample, startSample + lengthSamples)` into a new `AudioBuffer` (1 or 2 channels as
recorded; a stored stereo copy of a mono take is reduced back to one channel), releases the full
buffer, and schedules the take source at song time `startSample / 44100` against the shared
transport anchor (`start(when + max(0, startTime - offset), max(0, offset - startTime))`).
Right after a recording, `loadTake({ captureKey })` uses the local capture (no download).
With the loop on, the take source is not looped; it is re-scheduled at each pass like the
metronome's look-ahead (scheduled by the transport's pass events), so a loop take plays in every
pass.

**Memory**: decoding a 15-minute padded take needs about 318 MB transiently; normal takes are far
smaller. One take is loaded at a time.

## R-10 Compare and gates

**Decision**: compare is an extra gate factor, not a change to the user's mute/solo flags:
`compareGate(channel) = 0` when (mode `take` and channel is the take's part or a detail channel of
it) or (mode `original` and channel is `take`), else 1; `gate = hidden || mute ? 0 :
(anySolo && !solo ? 0 : 1) * compareGate`. The take is an audible-level channel for `anySolo`.
Changes use ADR-010's swap (one context time + 10 ms ramps). The frontend turns the compare mode
into explicit mutes when it builds an export ("What I hear"), so the backend needs no compare
concept. Shared vectors (`render-vectors.json`) gain take cases so engine and planner agree.

## R-11 Instrument families (MoSCoW principle)

| Family | Typical connection | Mode | Help emphasis |
|---|---|---|---|
| Vocals | microphone (USB or XLR into interface), headphones | mono | headphones (no bleed), pop filter, distance, monitoring off or direct |
| Guitar | interface hi-Z/DI input, amp-simulator or pedal output, or a mic on the amp | mono (stereo for stereo amp sims) | direct monitoring, gain staging, tuning before recording |
| Bass | DI/hi-Z input or amp DI out | mono | same as guitar; low-end check on headphones |
| Keys / piano | line outputs L/R; acoustic piano with one or two mics | stereo | stereo mode, level of the instrument's own volume |
| Drums | two overhead/room mics into the interface, or e-drum line out; one mic for a basic take | stereo or mono | loud headphones (isolating), clipping, mute original drums |
| Acoustic and other (strings, brass, woodwinds, percussion) | microphone | mono | distance, room noise, part "other" |

Acceptance (SC-008): one E2E case per family with the fake capture device and a family-specific
fixture (synthetic, generated by a committed script: sung vowels, plucked string, low pluck,
chord pad stereo, drum hits stereo, bowed tone).

## R-12 Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Acoustic loopback (headphones to mic) is noisy | calibration fails or spreads | chirp correlation, 6 of 8 rule, clear tips, manual fallback, cable recommended |
| Input latency changes between sessions (driver buffer changes) | takes shift by a few ms | output-latency snapshot check; recalibration hint; Expert timing adjustment (US7) |
| Firefox rate mismatch | no recording in Firefox | ADR-024 D8: disabled with reason; Chromium supported |
| Long takes over Blazor stream interop | slow save | 256 MiB limit; progress; local playback while saving; spike S-02 |
| Main-thread jank while recording | capture unaffected (worklet), but UI lag | posts batched per 2,048 frames; meters on the existing frame loop |
| 012 not exposing pass boundaries / processing latency | wrong alignment in loops or with the tempo stage | DELTA requests to 012; Phase 0 checks 012's merged engine; fallback: 015 adds the internal hook in 2.5.0 |
| Feedback with monitoring on speakers | loud howl | off by default, warning, monitoring volume default 0.7, Stop mutes monitoring |
| Disk use | many takes fill the disk | 200 per track, total size shown, 1 GiB free-space rule, 013 storage overview counts takes |

## R-13 Fake microphone in E2E

Chromium full build (`channel: 'chromium'`, new headless; the headless shell has no audio
capture - qa hand-off 009/010), flags `--use-fake-ui-for-media-stream`,
`--use-fake-device-for-media-stream`, `--use-file-for-fake-audio-capture=<fixture.wav>` and the
`microphone` permission granted per context (denied in the permission cases). The fake device
loops the file from the moment capture starts, so the E2E alignment test does not use it:
**digital loopback** (engine option `testInput: 'loopback'`, passed by the Web app only when the
web setting `StemStudio:Recording:TestLoopback` is `true`, which only the E2E compose override
sets; default `false`, never shown in the UI) feeds the engine's own destination mix into its
input through a `MediaStreamAudioDestinationNode`; calibration then measures that path and a recorded take of the
click-track stem must line up within 2 samples (SC-001). Firefox runs only the
"unsupported/permission" smoke cases.

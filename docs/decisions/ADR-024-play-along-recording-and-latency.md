# ADR-024: Play-along recording, latency compensation and take storage

**Status**: Accepted (015 Phase 0 T001, architect, 2026-11-17; proposed 2026-11-16)
**Feature**: 015 Play-along recording (MoSCoW S3)
**Related**: ADR-005 (audio engine), ADR-010 (stem groups), ADR-015 (karaoke clock), ADR-016
(asset library and export rendering), ADR-019 (voice samples, consent, exact-copy guard),
feature 012 (practice tempo, count-in; audio-engine 2.3.0), feature 014 (audio-engine 2.4.0).

**Numbering note**: features 012-016 are specified in parallel. ADR numbers are taken in feature
order (012 -> 021, 013 -> 022, 014 -> 023, 015 -> 024, 016 -> 025) so the parallel branches do not
collide. The CTO may renumber at merge; nothing in the code refers to the number.

## Context

Musicians of every family (vocals, guitar and bass through an interface or an amp-simulator
output, keys, drums with microphones or e-drum line outputs, acoustic instruments) want to record
themselves over the mix, hear the take next to the original part, keep the best of several
attempts and export the result. The hard part is time: a browser adds output latency (mix leaves
the speakers later than it is scheduled) and input latency (the microphone signal reaches the
page later than it was played). Without compensation a take sits 15-80 ms late, which is clearly
audible. The audio engine already schedules everything on `AudioContext.currentTime`
(constitution V); recordings must use the same clock.

Constraints: open source only, no new paid service; recordings never leave the machine; the
worker contract is not allocated a version for 015 (ROADMAP); exports go through 007.

## Decisions

### D1. Capture raw PCM in an AudioWorklet inside the engine's own AudioContext

The audio engine (2.5.0) opens the input with `getUserMedia` (echo cancellation, noise
suppression, automatic gain and voice isolation **off**), connects a
`MediaStreamAudioSourceNode` to an `AudioWorkletNode` (`stemstudio-capture`) in the **same**
44.1 kHz `AudioContext` that plays the stems, and the processor posts Float32 blocks stamped with
the global `currentFrame`. Every captured sample therefore has an exact context frame index, the
same timeline the transport uses.

Rejected: `MediaRecorder` (used by 010 for voice samples): lossy Opus, its own clock and an
unknown start offset, so sample-accurate alignment is impossible. `ScriptProcessorNode`:
deprecated, runs on the main thread, glitches. A separate capture `AudioContext`: two clocks
that drift.

### D2. Latency = one measured round trip, applied as metadata at capture time

`L` (samples) is the round trip from "scheduled at context time T" to "appears in the capture at
frame T + L". It is measured with a **loopback click test**: the engine schedules 8 broadband
clicks (5 ms log chirp) at seeded, irregular intervals directly on the destination (not through
the mix or any tempo processing), captures the input and finds each click by normalised
cross-correlation; the result is the median lag when at least 6 clicks are found and their
spread is at most 1 ms. A **manual** value (0-1000 ms) is the fallback; an **estimate**
(`baseLatency + outputLatency + MediaTrackSettings.latency`) is used, and labelled as such,
until the user calibrates. Calibrations are stored per input device (backend, keyed by device
label) with a snapshot of the output latency; a difference of more than 5 ms at recording time
shows "Calibrate again".

Compensation: a captured frame `f` belongs to song position `pos(f - L - P)`, where `pos` is the
transport mapping of ADR-005 (with loop passes and 012 count-in gaps) and `P` the engine's
processing latency of the stem path (012's tempo stage; 0 when it is bypassed). The engine cuts
each take so that its first sample is exactly song sample `startSample`; nothing is resampled or
stretched. Alignment error is therefore the measurement error of `L` only.

### D3. Record only at 100 % speed and 0 semitones; listen back at any setting

Recording is enabled only when 012's speed is 100 % and transpose is 0 (one click sets both);
the speed trainer is paused while recording. The take channel is processed exactly like a stem
(same 012 tempo/pitch stage), so takes stay in time when the user later listens back slower or
transposed.

Rejected for 015: time-aligning takes recorded at other speeds. It needs an offline SoundTouch
stretch of the take (artefacts on the user's own performance), compensation of SoundTouch's
variable processing delay, and a stretch step in the 007 renderer for exports (worker change).
Recorded in the backlog as "slow-take recording".

### D4. Takes are library assets (origin `recording`), stored as 24-bit FLAC aligned at sample 0

The browser encodes the compensated span as 24-bit PCM WAV (44.1 kHz, 1 or 2 channels) and
streams it through Blazor's JS stream interop to the Web server, which streams it to the API
(`POST /api/tracks/{trackId}/takes`). The API validates the RIFF header, scans peaks, and writes
`assets/<assetId>/capture.wav` **padded with `startSample` frames of digital silence**, then
creates a 007 render job with that single input (gain 1, pan 0, `flac`, 24-bit, `dither: false`).
The existing asset sync stores `assets/<assetId>/audio.flac` and the API deletes `capture.wav`.

Consequences:
- **No worker change**: the input path matches the existing allow-list
  (`assets/<uuid>/<name>.(flac|wav|mp3)`), the raw single-input render is bit-exact (ADR-016 D4:
  no dither, unity gain, centre pan, libsndfile scale), mono is written as two identical channels.
- **Exports are free**: because every take starts at song sample 0, "take + mix" is a normal 007
  mix render with one more input, and "take only" is a raw render with the take's span as range.
- **Storage**: leading silence costs almost nothing in FLAC (constant subframes); a 4-minute
  stereo take is about 35-45 MB, a mono one about half.
- **Playback memory**: the engine decodes the take once and keeps only the span
  (`startSample .. startSample + lengthSamples`) as its buffer; right after recording it plays
  the local capture without a download.
- Takes are hidden from the Library list by default (`listAssets` excludes origin `recording`
  unless asked); the mixer's Takes panel manages them. Deleting a track deletes its takes;
  exports made from takes stay in the library (007 rule).
- Take files are registered in the 010 audio-hash index, so a take can never be used as a voice
  sample (ADR-019 exact-copy guard; a take may contain other people's voices from the speakers).

Rejected: a new worker endpoint or path pattern (no worker version allocated); FLAC encoding in
the browser (a new WASM dependency, and the export path still needs the worker to read the
file); a FLAC encoder in .NET (no maintained permissive library; hand-written codec code);
direct browser upload to the API (would widen CORS from GET to POST for the first time); storing
unpadded takes plus an offset (needs an offset field in the worker's `RenderInput`).

### D5. One take channel strip; compare by a sample-locked gate swap

The engine gets one extra channel `take` with the normal chain (gain x gate -> panner -> meter ->
master), part of the solo/mute gate like any audible-level channel, and `setTakeCompare(mode)`:
`both`, `take` (the take's part stem, or all of its expanded detail channels, gated to 0) or
`original` (the take gated to 0). The swap uses ADR-010's mechanism (one context time, <= 10 ms
ramps). One take plays at a time; switching takes restarts only the take source with the shared
transport anchor. Layering several takes (harmonies, double tracking) is out of scope.

### D6. Loop recording: one take per pass, all passes kept

With the A-B loop on, capture runs continuously and is split at the pass boundaries the transport
reports (loop wraps, plus 012's count-in before a pass). Every complete pass is saved as a take
(`sessionId`, `passIndex`); a final partial pass is kept when it is at least 1 s long and marked
`partial`. Nothing is discarded automatically: the user stars the best pass and uses "Delete the
other passes". Each pass ends `L + P` after its boundary so its compensated tail is complete.

### D7. Privacy and permissions

The microphone is opened only from the recording panel after an explanation, closed when the
panel or the mixer closes, and never used in the background. Captured audio goes only to the
local StemStudio API (same machine, through the Web server); no other origin is contacted
(verified by an E2E network assertion). Recording needs a secure context (localhost or https)
like 010. Permission states (`prompt`, `granted`, `denied`, no device, device busy, device
unplugged) each have a readable message and the next step.

### D8. Browser support

Chromium-based browsers (Chrome, Edge) are the supported recording browsers and are tested
end-to-end (full Chromium build, fake capture device). Firefox is best effort: when it refuses to
connect a microphone whose rate differs from 44.1 kHz, recording is disabled with that reason and
playback keeps working.

## Consequences

- audio-engine 2.5.0 (additive): input, monitoring, calibration, recording, take channel,
  compare; a second Vite entry builds the worklet module.
- backend-api 1.14.0 (additive): takes, calibrations, export and mix additions, asset origin
  `recording`; ui-test-ids 1.14.0; render-vectors gains take cases (next minor).
- No worker-api, generator-api or agent tool catalogue change.
- Risks (research.md R-12): calibration through an acoustic path is noisier than a cable; Chrome
  may change input latency between sessions (hence the per-session estimate check); large takes
  (15-minute stereo, about 240 MB WAV) take several seconds to stream.

## Phase 0 outcome (2026-11-17)

Number check: ADR-024 is free and allocated to 015 (ADR-023 = 014, ADR-025 = 016); no rename.
Checked against `main` e8d6a5d8 (001-013 merged; 014 and 022/023 in progress). Details:
`specs/015-play-along-recording/contracts/audio-engine.md` "Phase 0 outcome" and "Browser
constraints", `contracts/DELTA.md` "Phase 0 decisions".

1. **Engine hooks (D2, D6)**: 012's transport keeps heard/played anchor queues with explicit
   passes and count-in gaps, but prunes them; the 2.5.0 work adds an internal segment log. The
   tempo-stage latency `P` is already part of the **heard** anchors, so the mapping is
   `songSample(f) = heardSampleAt(f - L)`; `P` is not added a second time (it is still reported in
   `CapturedTake.latencySamples` = `L + P`). The stage sits on the master bus, so the take channel
   is processed like the stems (D3). Count-in bars and "every pass" come from 012's practice
   settings.
2. **Secure context (D7)**: measured - `getUserMedia` and `AudioWorklet` exist only on
   `http://localhost` / `127.0.0.1` (or https); on a LAN address over http both are undefined. Recording
   is offered only on the StemStudio computer; from another device `recordingSupport()` reports
   `insecureContext` with the reason. This also keeps FR-017 (audio never crosses the network) and
   needs no change to the LAN residual risk T7 (closed by 018).
3. **Take recipe (D4)**: the asset's provenance carries the take as `Provenance.recording`
   (optional, like 1.8.0's `generation`) instead of a third `Provenance.recipe` variant, so the typed
   `ExportRecipe` readers stay unchanged.
4. **Channel ids (D5)**: `ChannelId` stays unchanged (scores and agent tools use it); the export
   channel lists use the new `ExportChannelId` (= `ChannelId` or `take`). The agent tool catalogue
   stays unchanged.
5. **013 / 022 integration (D4)**: takes are `recordings` in backups and the storage overview
   (013's reserved category; stored provenance value `Recording`, as the 013 fixture expects);
   `RecordingTakes` and `InputCalibrations` join the backup scrub, merge restore and the 013 write
   gate (delete and replace of take files); 022's library filing excludes origin `recording`.
6. **Spikes**: S-01 60 s gap-free capture in Chromium and Firefox (fake devices); the 10-minute
   real-interface run, CPU and the Firefox rate mismatch move to the owner checks (T089). S-02
   browser half 0.2-0.3 s for a 5-minute stereo take; the circuit + API half is measured by the
   frontend lane (US1) and the CTO.

# ADR-021: Practice tempo - time stretch and pitch shift with SoundTouch WASM (feature 012)

- **Status**: Accepted (architect, 012 Phase 0, 2026-11-16, after the spikes P0-1/P0-2; see
  "Phase 0 outcome"). Proposed in the 012 spec the same day.
- **Date**: 2026-11-16
- **Decider**: Architect (autonomous mode), to be confirmed by the CTO; library choice by the owner
  (MoSCoW decisions 2026-11-16)
- **Feature**: `specs/012-practice-tempo/`
- **Builds on**: ADR-005 (audio engine), ADR-010 (stem groups), ADR-015 (karaoke clock), ADR-016
  (export rendering)
- **Numbering note**: 012 takes ADR-021 (first of the 012-016 specs in roadmap order); the
  parallel specs 013-016 take ADR-022 onward.

## Context

Practising needs the song slower (50-120 %) without a pitch change and in another key (+-12
semitones) without a speed change, live in the mixer, with every stem sample-locked. The engine
plays decoded stems with `AudioBufferSourceNode`s on one `AudioContext` (ADR-005); `playbackRate`
alone changes speed and pitch together. The owner chose SoundTouch (LGPL-2.1) as WASM; Rubber Band
(GPL) would force the MIT project to GPL. An `AudioWorkletNode` inside a push graph returns as many
frames as it receives, so it cannot change the duration of a signal.

## Decisions

### D1 Rate on the sources, pitch correction on the summed mix

Every source plays at `playbackRate = r = speed / 100` (the browser resamples; sample lock and native
loops keep working). One stereo SoundTouch stage between the channel sum and the master gain applies
`setPitch(p)` with `p = 2^(st/12) / r` at tempo 1, which restores (or shifts) the pitch without
changing the duration. Quality equals SoundTouch's own tempo change of the same ratio.

### D2 One stage for the whole mix, never per stem

Per-stem WSOLA would splice each stem at different points (breaking the <= 1 ms lock) and cost up to
20x the CPU. Channel meters sit before the stage (they lead the sound by L while engaged; master
meter after it) - accepted and documented.

### D3 Fixed latency, compensated in the anchor

The processor wraps SoundTouch in a FIFO with a constant delay `D = STRETCH_DELAY_FRAMES` (measured
in Phase 0, <= 230 ms). Sources start `L = D / sampleRate` earlier than the transport anchor's
`when`, so the anchor describes the heard time; the metronome and count-in clicks (not stretched,
straight to the destination), the karaoke clock and the position need only the rate factor:
`position(t) = offset + (t - when) * r`.

### D4 On demand, bypass at neutral

Nothing is loaded at 100 % / 0 semitones (2.1.0 graph, bit-exact; mixes from before 012 never load
the module). The first non-neutral setting loads the processor and the WASM (bytes passed in
`processorOptions`; no `SharedArrayBuffer`), engages the stage and - while playing - restarts at the
heard position once. The stage stays engaged until the next restart at neutral settings. Live changes
are `playbackRate.setValueAtTime(r, T - L)` on every source plus `setPitch(p, frame(T - L))` and a
re-anchor at `T = now + L + 20 ms`.

### D5 Explicit loop passes for the trainer and the count-in before every pass

The native loop stays for plain looping. When the speed trainer or the count-in before every pass is
active, each pass is a fresh set of sources `start(T_k - L, A, B - A)` with its own constant rate and
anchor, created ahead by the existing look-ahead timer (the timer only wakes the scheduler; times
come from the audio clock). Trainer steps happen exactly at pass boundaries; count-in gaps sit
between passes.

### D6 Count-in on the song's grid

1-2 bars of clicks (metronome sound/volume) continue the song's beat grid backwards from the first
beat at or after the start, scaled by the speed; unavailable without a reliable grid or a manual BPM.
No audio pre-roll (deferred to 015).

### D7 Packaging, licence and fallback

`soundtouch.wasm` (SoundTouch 2.4.1 unmodified + an LGPL shim exporting a small C ABI, Emscripten
6.0.13 standalone build; 6.0.7 before Phase 0) is a separate, replaceable file loaded at run time (LGPL-2.1 section 6(b));
our processor and engine stay MIT. Binary distributions ship `/third-party/soundtouch/` (licence,
exact source archive, shim, build script, notice). If the WASM build fails in Phase 0, the
JavaScript port `soundtouchjs` (LGPL-2.1) is loaded as a separate module behind the same
`TimeStretchStage` port; the contract does not change.

### D8 Persistence and exports

Practice settings are part of the mix (backend 1.11.0, optional `MixSettings.practice`, defaults for
older mixes, one JSON column). Exports never apply speed or transpose (ADR-016 render plans stay
bit-exact); the export dialog says so.

## Consequences

- Contracts: backend-api 1.11.0 (additive), audio-engine 2.3.0 (additive; neutral settings =
  2.1.0 behaviour), ui-test-ids 1.11.0 (additive). Worker API unchanged.
- New build step (Emscripten in Docker) and two new static files; CI builds the WASM and runs a Node
  test suite against it. No new runtime npm dependency.
- Start of playback and live changes are delayed by `L` (<= 230 ms) while engaged; meters of single
  channels lead the sound by `L`.
- Quality at 50 % is noticeably lower than with commercial stretchers; shown as a hint and in help.
  Revisit only with the owner (an MIT alternative such as Signalsmith Stretch would fit the same
  port).
- Vocals transposed up sound "chipmunk"-like (no formant correction).

## Alternatives rejected

Rubber Band (GPL); pull-based stretch worklet that mixes stems itself (re-implements the mixer,
copies all PCM into the worklet, loses native loops); offline pre-rendering per setting (not live,
double memory); per-stem stages (lock, CPU); exporting slowed audio in the worker (second
implementation, packaging, accidental slowed inputs for 008/009/011).

## Phase 0 outcome (2026-11-16, research.md "Phase 0 results")

The SoundTouch WASM path works; the fallback (D7, soundtouchjs) is not needed. Decisions taken in
Phase 0 (owner unavailable; decided by the architect for the best practising result, recorded here):

- **P1 emsdk 6.0.13 instead of 6.0.7.** 6.0.13 (2026-10-19) is the newest 6.0 patch older than two
  weeks; 7.0.0 (2026-11-06) is too new and a major. Pinned by the multi-arch digest in
  `third_party/soundtouch/soundtouch.lock`; two clean builds are bit-identical.
- **P2 Upstream artefact = the surina.net release tarball** (SHA-256 `e07abf20...6733`); the Codeberg
  tag archive has identical content but is generated on demand, so its hash is informational only.
- **P3 `SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER` is mandatory** (an upstream configuration switch,
  not a source change). Without it every pitch change across factor 1.0 lost about 1,000 frames
  inside SoundTouch, so the "fixed delay" drifted by 4.6 s over 120 extreme jumps; with it the
  output lag stays <= 4,736 frames in every scenario. Upstream calls the quality cost "slight".
- **P4 No WASM SIMD.** `-msimd128` produced bit-identical output at the same speed; the scalar module
  (30 KB, no imports) runs everywhere.
- **P5 `STRETCH_DELAY_FRAMES = 5120`** (116.1 ms at 44.1 kHz), well under the 230 ms limit; SC-001
  keeps its 250 ms (measured 87-151 ms at the stage output in Chromium).
- **P6 Content-offset compensation (refines D3).** WSOLA shifts the audio in time by a mean that
  depends on the pitch factor (+32 ms at p = 0.42, -40 ms at p = 4), so music and the unstretched
  clicks would drift apart by up to ~40 ms at 50 % +12 st. The engine uses the heard latency
  `L(p) = (D + c(p)) / sampleRate` with `c(p) = round(-1100 * log2(p))` frames for source starts and
  re-anchoring; the residual is WSOLA's own per-onset spread (<= +-23 ms), inherent to any
  time-stretcher. Contract: audio-engine.md "Live changes", "Stretch stage".
- **P7 When a pitch change applies (refines D4).** SoundTouch applies `setPitch` also to buffered,
  unprocessed input, so applying it at the input frame made the correction audible up to ~90 ms
  before the matching rate change (a short wrong-pitch "chirp" on every speed change). The processor
  applies it when the midpoint of SoundTouch's buffered input reaches the change frame: wrong-pitch
  time 12-23 ms instead of 70-81 ms.
- **P8 Reactor module.** The module has no imports and exports `_initialize`, which the processor
  calls once (ABI header `third_party/soundtouch/include/stemstudio_soundtouch.h`).

## Implementation notes (audio-engine lane, 2026-11-16)

Owner unavailable; decided by the audio-engine agent for the best result for a musician practising at home.
None changes `types.ts` 2.3.0 or the WASM ABI.

- **E1 Stage position.** The stage sits between the master gain and the master meter (contract graph: sum ->
  stage -> master gain). The chain is linear, so the sound is the same; at neutral settings the graph is literally
  the 2.1.0 one and every pre-012 graph test stays unchanged. Cost: master-fader moves are heard L later while engaged.
- **E2 Reset on pause/stop.** The stage is also reset when playback pauses or stops, so no 66-147 ms of music
  trails after pressing pause.
- **E3 No fade on the engage restart.** Engaging while playing restarts like every seek since 001 (hard stop/start).
- **E4 Gapless passes in source time.** Pass k+1 starts exactly where pass k stops at the sources; its heard anchor
  is `S + L(p_{k+1})`. A trainer step therefore moves the heard pass start by the difference of two content offsets
  (1-2 ms for a 5 % step) instead of overlapping or gapping the audio.
- **E5 Changes while passes play.** Speed changes of the settings (and disabling the trainer or the every-pass
  count-in) apply from the next pass boundary (each pass has one rate, R-10); the transpose applies live as a pitch
  change only (the heard anchor is not shifted by the c(p) difference: <= 2 ms per semitone until the next pass).
- **E6 Restarts.** Enabling the trainer while playing and every loop change while passes play restart at the heard
  position (one gap of startDelay + L).
- **E7 No stage reset between passes, also not after a count-in gap.** The gap carries silence through the stage;
  a reset would cut the end of the previous pass, which is still being heard.
- **E8 FIFO overflow** drops the oldest frames back to D (restores the fixed delay) rather than to D + 1024.
- **E9 Count-in tempo.** With a manual BPM and a beat grid, `b` is the first click of the metronome's constant
  grid at or after A (so the count matches the metronome); with a manual BPM and no grid, `b = A` (data-model 3.3).
  When A lies after the last two grid clicks, the last grid interval is used and `b = A`.
- **E10 Contract text fix requested.** audio-engine.md "Content offset" says input frame n lands near SoundTouch
  output frame `n - c(p)`; the measurement (and `L(p) = (D + c(p)) / sampleRate`) needs `n + c(p)`. The Node suite
  measures `output onset - input onset`: 17.5 ms at p = 0.5 (c = 24.9 ms) and -33.1 ms at p = 2 (c = -24.9 ms).

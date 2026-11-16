# ADR-021: Practice tempo - time stretch and pitch shift with SoundTouch WASM (feature 012)

- **Status**: Proposed (architect, 012 spec, 2026-11-16); becomes Accepted after the Phase 0 spikes
  (tasks.md T001/T002)
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
6.0.7 standalone build) is a separate, replaceable file loaded at run time (LGPL-2.1 section 6(b));
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

# ADR-005: Audio engine design

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: architect agent, on the CTO brief

## Context

SC-002/SC-003 require stems aligned within 1 ms and metronome clicks within 1 ms of the grid;
the constitution forbids timer-driven audible timing.

## Decision

- One `AudioContext`; stems fully decoded to `AudioBuffer`s; graph per stem
  `BufferSource -> Gain (fader x mute/solo gate) -> StereoPanner -> Analyser -> master Gain`.
- All sources start with the same `when = currentTime + 0.1` and the same offset; seek = stop all
  and restart all; looping via `loop/loopStart/loopEnd` on every source.
- Metronome: look-ahead scheduler (25 ms wake-up, 100 ms window), click times derived from the
  audio clock, transport anchor and beat grid (loop-aware), pre-rendered accent/normal buffers,
  own gain node straight to the destination.
- Meters are drawn by the engine with `requestAnimationFrame` into DOM elements registered by id;
  .NET receives only position (<= 10 Hz) and state changes.
- Exposed as an ES module (`createEngine` returns an object used as `IJSObjectReference`); the
  TypeScript contract is `src/audio-engine/src/types.ts`, described in `contracts/audio-engine.md`.

## Consequences

- Sample-exact sync and loops are delegated to the browser's audio thread.
- Memory: decoded PCM is ~42 MB per stem for 4 minutes (float32 stereo); six stems ~250 MB.
  Acceptable for 15-minute max songs on desktop browsers; documented as a limitation.
- Unit tests need an `AudioContext` mock; real-browser behaviour is covered by Playwright.

# ADR-026: Mixer effects, export parity through browser pre-renders, and Web MIDI controllers (feature 017)

- **Status**: Accepted (owner, 2026-11-19, via the Tipoca project site; proposed by the architect, 2026-11-16).
- **Context documents**: `specs/017-effects-controllers/{spec.md, research.md, data-model.md,
  DELTA.md, contracts/}`, ADR-005/ADR-010 (audio engine, gate rule v2), ADR-013 (design system,
  levels), ADR-016 (asset library, server-side export rendering), `docs/MoSCoW.md` C2/C3,
  `docs/ROADMAP.md` (017 after 012; versions allocated)
- **Number note**: ADR-021..025 are expected to be taken by 012-016, which are specified in
  parallel; the CTO renumbers at merge if needed (references are by file name).

## Context

C2 asks for EQ, compressor and reverb per strip plus a practice EQ, saved in mixes; C3 asks for
MIDI controller support (Chromium only) with a mapping screen. Constraints:

1. 007 promises "exports sound like the mixer" and renders in the worker with numpy, matched to
   the engine by shared vectors (ADR-016 D2). Linear gain and pan can be matched that way; a
   compressor cannot (the Web Audio compressor algorithm is implementation-defined).
2. The roadmap allocates no worker-api or render-vectors version to 017.
3. The mix can have up to 28 audible strips (6 standard, up to 22 detail) and must stay smooth.
4. Every instrument counts equally (MoSCoW principle); levels from 004 decide what users see.
5. The UI is Blazor Server: every interop call crosses SignalR; mixer state has one owner (.NET).

## Decisions

### D1 Native Web Audio nodes, no own DSP

EQ = `BiquadFilterNode`s (cookbook formulas fixed by the Web Audio spec), compressor =
`DynamicsCompressorNode` + make-up `GainNode`, reverb = `ConvolverNode`. No AudioWorklet DSP, no
WASM, no new dependency. Rejected: own DSP in a worklet (CPU per strip in JS, code to own),
Python mirrors (two implementations, never exact for a compressor).

### D2 Topology: pre-fader inserts, one post-fader reverb send bus

`sources -> fxIn -> (dry | EQ -> compressor -> make-up -> wet) -> fader x gate -> panner -> meter
-> master`; `meter tap -> send -> reverb bus (pre-delay -> convolver -> return -> meter 'reverb')
-> master`. On/off/bypass cross-fade the dry/wet gains (<= 10 ms). One reverb per mix (Room,
Plate, Hall), not a reverb per strip.

### D3 Generated, versioned impulse responses

IRs are generated deterministically (seeded noise, exponential decay, progressive damping, early
taps) with `irVersion: 1` stored in the mix. No IR files, no licences, reproducible exports.

### D4 CPU budget by construction

Nodes exist only for enabled effects (HP/LP sections only when enabled); wet paths of hidden or
bypassed strips are disconnected after their fade; the reverb bus exists only when used. Budget:
worst case 28 strips x (8 biquads + compressor) + 1 convolver; offline 30 s render <= 10 s on CI;
no dropouts during 5 minutes on the reference machine. The engine reports `renderCapacity` when
the browser offers it, else a cost-unit estimate.

### D5 Exports with effects are pre-rendered in the browser and encoded by the unchanged 007 pipeline

The engine renders the export state in an `OfflineAudioContext` with **the same graph builder** as
playback (including a pre-roll of up to 3 s for reverb/compressor state), uploads a 32-bit float
WAV to `POST /api/tracks/{trackId}/prerenders`, and the UI calls `createAudioExport` with
`effects: apply` and the `prerenderId`. The API moves the file to `assets/{assetId}/prerender.wav`
(an allowed worker input) and runs the existing worker render job with that one input at gain 1,
pan 0, master 1; the worker keeps clip protection, bit depth, dither, MP3 and tags. Parity is by
construction (SC-003). The engine's offline selection gate is tested against the 007
`render-vectors.json` gate cases.

Exports without a browser (agent 011, API clients, or `effects` absent) use `ignore`: exactly the
007 render, and the library says "without effects" when the mix had effects. ADR-016 D2 (worker
renders) stays true for every export; the browser only adds a pre-processed input.

Rejected: effects in the worker (needs worker-api and render-vectors bumps, a second DSP, inexact
compressor); never exporting effects (breaks 007 FR-003 silently).

### D6 Built-in presets as engine data; mixes store concrete values

The preset catalogue (practice EQ Focus/Make room for seven families with drum variants, tone and
dynamics presets) lives in the engine (`getEffectPresets()`), so there is one source and no C#
duplicate. Applying a preset writes concrete values; mixes store values plus an informational
`presetId`. User-saved presets are out of scope.

### D7 Effects are optional members of the mix; old mixes unchanged

`ChannelSettings.effects`, `DetailChannelSettings.effects` and `MixSettings.effects` are optional
(null = off) inside the existing JSON columns plus one nullable column. Mixes saved before 1.16.0
load with effects off and export byte-identical to 007.

### D8 Web MIDI input module in the engine package, user-initiated, no SysEx

`createMidiController` is a separate factory in `src/audio-engine` (engine 2.7.0 is the allocated
contract; it reuses the frame loop; no dependency on the engine instance, so it can move to
`web-interop` later). Access is requested only from "Connect controller" (or silently when the
permission is already granted), with `sysex: false`. Supported: Chromium. Unsupported, insecure
context, denied and no-device each have a reason and a remedy.

### D9 One writer: MIDI -> coalesced events -> .NET -> the same mixer methods

JS parses and maps messages, applies pick-up, coalesces continuous values (latest wins, <= 30 Hz,
buttons immediately) and calls `OnControllerInput(events[])`; .NET runs the same methods as the
on-screen controls, which call the engine. Latency budget < 50 ms p95. Rejected: JS writing to the
engine directly (two writers, UI and saved mix can diverge).

### D10 Mappings are backend profiles per device; strip targets are slots with banks

Profiles keyed by `(deviceName, manufacturer)` in SQLite (survive browser changes, included in 013
backups). Strip targets address slots 1-16 in mixer order with bank next/previous, so one mapping
works for every track. Discrete commands use ids aligned with 014's command registry at merge.

### D11 Levels

Beginner: practice EQ only (and a "has effects" marker); Advanced: all effects, export option,
MIDI learn, Controllers page; Expert: Q/knee/attack/release, numeric entry, gain-reduction meters,
effects load, binding modes. Saved effects and enabled profiles work at every level.

### D12 Out of scope

Insert reverb per strip, master-bus effects, other effect types, user presets, automation, MIDI
output/feedback (motor faders, LEDs), MIDI clock, SysEx, Mackie/HUI, supported MIDI outside
Chromium, effects in browser-less exports.

## Consequences

- + Exports with effects equal what the user hears, without touching the worker contract.
- + No new dependency or licence; IRs are generated.
- - Exports with effects need the mixer page open; up to ~640 MB browser memory for a 15-minute
  render; agent exports cannot include effects (documented, labelled).
- - Effects sound may differ slightly between browser engines (the export is made in the browser
  that plays, so it still matches what the user hears).
- - MIDI works only on Chromium; feedback to devices is not available.
- Follow-ups: agent tools for effects and an `effects` option for agent exports (later catalogue
  version, possibly via the agent's UI-result path), user presets, MIDI output.

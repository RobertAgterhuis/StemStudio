# Contract delta: audio engine 2.9.0 (feature 021)

Status: **binding delta** (021 design, 2026-11-16). Base: audio-engine **2.8.0** (019) on top of
2.5.0 (015 inputs/recording/takes) and 2.7.0 (017 strip effects). Every call valid in 2.8.0 behaves
exactly as before; 2.9.0 is additive (minor). The binding TypeScript lands in
`src/audio-engine/src/types.ts` in Phase 0 (T006); this document is the readable source until then.

## Changelog line

> 2.9.0 (additive, feature 021) - part channels `part-<8 hex>` (`addPart`, `removePart`); amp rig
> per input (`loadNamRuntime`, `loadAmpProfile`, `loadCabinetIr`, `setRig`, `setRigBypass`,
> `getRigStatus`, callback `OnRigStatus`); recorder option `captureWet`; take re-amp
> (`setTakeReamp`). Every 2.8.0 call behaves exactly as before.

## 1. Types (additions)

```ts
export type EngineContractVersion = '2.9.0';

/** 021: generated part channel. */
export type PartChannelId = `part-${string}`;          // runtime-checked: /^part-[0-9a-f]{8}$/
// ChannelId (2.8.0 union incl. 015 take ids) gains PartChannelId.

export interface PartSource {
  channelId: PartChannelId;
  url: string;                    // /api/tracks/{trackId}/parts/{partId}/audio (FLAC, track length)
  settings?: ChannelSettings;     // default volume 1, pan from data-model 2.2, unmuted
}

export type BuiltinAmpId = 'builtin:guitar-clean' | 'builtin:guitar-crunch' | 'builtin:guitar-lead' | 'builtin:bass-drive';
export type BuiltinCabinetId = 'builtin:cab-guitar' | 'builtin:cab-bass';

/** Same shape as backend RigSettings (data-model 3.5); `effects` is the 017 StripEffectsSettings. */
export interface RigSettings {
  version: 1;
  gate: { enabled: boolean; thresholdDb: number; releaseMs: number };
  inputGainDb: number;
  pedal: { profileId: string | null; enabled: boolean; levelDb: number };
  amp: { kind: 'builtin' | 'profile' | 'none'; id: BuiltinAmpId | string | null; levelDb: number;
         tone: { bass: number; mid: number; treble: number }; drive: number };
  cabinet: { kind: 'builtin' | 'ir' | 'none'; id: BuiltinCabinetId | string | null; mix: number };
  outputLevelDb: number;
  effects: StripEffectsSettings;  // 017 type, unchanged
}

export interface AmpProfileInfo {
  profileId: string;
  architecture: 'WaveNet' | 'LSTM' | 'ConvNet' | 'Linear' | 'A2';
  expectedSampleRate: number;     // from the model, 48000 when absent
  resampled: boolean;             // expectedSampleRate !== context.sampleRate
  latencyFrames: number;          // resampler latency at context rate (0 when not resampled)
}

export type RigState = 'off' | 'loading' | 'running' | 'bypassed' | 'overloaded' | 'unavailable';

export interface RigStatus {
  inputId: string;                // 015 input id
  state: RigState;
  reason: string | null;          // e.g. "WebAssembly is not available in this browser"
  chainLatencyMs: number;         // resampler(s); 0 for built-in amps
  roundTripMs: number | null;     // 015 calibration + chainLatencyMs; null before calibration
  loadP50: number;                // fraction of the quantum budget, 0..n (worklet self-measured)
  loadP99: number;
  overloadedQuanta: number;       // count in the last 2 s window
  missing: string[];              // profile/IR ids that were not loaded (fallbacks applied)
}
// DotNetCallbacks gains 'OnRigStatus' (RigStatus), throttled to 4 per second and on every state change.
```

## 2. AudioEngine methods (additions)

| Method | Behaviour |
|---|---|
| `addPart(source: PartSource): Promise<number>` | Fetches and decodes the part (same loader as stems), adds a top-level channel with volume/pan/mute/solo and a meter, starts it sample-locked at the current transport position (same `start(when, offset)` as stems). Returns the duration. Rejects on a duplicate id or a decode error. The gate rule treats parts like collapsed stems (no detail children). |
| `removePart(channelId)` | Stops and disconnects; unknown id is a no-op. |
| `loadNamRuntime(wasmUrl: string): Promise<boolean>` | Once per page: `WebAssembly.compileStreaming(fetch(wasmUrl))`, `audioWorklet.addModule('<engine>/nam-worklet.js')`. Resolves false (never throws) when AudioWorklet or WebAssembly is unavailable; rigs then run built-in amps only and report `unavailable` for captures. |
| `loadAmpProfile(profileId, url): Promise<AmpProfileInfo>` | Fetches the `.nam` JSON text (cached by profile id and ETag), validates it in the worklet (`nam_create`); rejects with the core's message for an unusable model. |
| `loadCabinetIr(irId, url): Promise<void>` | Fetches and decodes the WAV into an `AudioBuffer` for a `ConvolverNode` (`normalize = false`). |
| `setRig(inputId, settings: RigSettings): void` | Builds or updates the chain of that 015 input: `input -> gate (worklet-free DynamicsCompressor-based expander or a tiny gate worklet) -> pedal NAM? -> amp (NAM or built-in) -> cabinet ConvolverNode? -> 017 StripEffects -> output gain`. Model changes cross-fade 20 ms (equal power) between two worklet instances; parameter changes ramp over 10 ms (`setTargetAtTime`). Unloaded/unknown ids -> fallback (`builtin:guitar-clean`, no cabinet) and listed in `missing`. |
| `setRigBypass(inputId, bypass: boolean)` | 10 ms cross-fade between the chain output and the dry input. |
| `clearRig(inputId)` | Removes the chain (input monitored dry as in 015). |
| `getRigStatus(inputId): RigStatus` | Snapshot (also pushed by `OnRigStatus`). |
| `setTakeReamp(takeChannelId, settings: RigSettings \| null): void` | Plays the take's **dry** buffer through a rig instead of its normal file; `null` returns to 015 playback (wet or dry per `playWet`). One re-amp chain at a time (budget). |

Recorder (015): the record options object gains `captureWet?: boolean` (default false). When true
and the input has a rig, the recorder worklet records the post-chain signal in the same quantum as
the dry signal, drops `round(chainLatencyMs x rate / 1000)` frames at the wet start and pads the end,
so both buffers have the same length; the result object gains `wet: Blob | null` (FLAC/WAV encoding
as 015's dry result). "Print" = an offline render of the dry buffer through the rig in an
`OfflineAudioContext` with the same nodes (the NAM worklet supports offline contexts) -> `Blob`.

## 3. NAM worklet (`stemstudio-nam`)

- Files: `src/audio-engine/src/amp/nam-worklet.ts` (bundled to `wwwroot/js/nam-worklet.js`),
  `src/audio-engine/vendor/nam/nam-engine.wasm` (copied to `wwwroot/js/`), `PROVENANCE.md`.
- `processorOptions`: `{ module: WebAssembly.Module, hostRate: number }`; one mono input, one mono
  output (`channelCount: 1`, `channelCountMode: 'explicit'`, `channelInterpretation: 'speakers'`,
  a `ChannelMergerNode`/gain duplicates mono to stereo after the cabinet).
- Messages main -> worklet: `{type: 'load', json: string}`, `{type: 'reset'}`, `{type: 'dispose'}`.
  Worklet -> main: `{type: 'loaded', info: AmpProfileInfo}`, `{type: 'error', message}`,
  `{type: 'load', p50, p99, overloaded}` every 250 ms.
- No allocation in `process()`; input/output scratch buffers live in WASM memory and are allocated
  at `load`. Denormals: the C shim sets flush-to-zero equivalent by adding 1e-20 DC offset removed
  by the cabinet high-pass (WASM has no FTZ flag).
- Load measurement, two parts: (1) at `loadAmpProfile` the engine runs the same WASM module on
  1 s of noise in a dedicated Web Worker (`performance.now()` is available there, not in
  `AudioWorkletGlobalScope`) and reports `loadP50`/`loadP99` as the fraction of the quantum
  budget per quantum; (2) at run time overload is detected from the context's underrun statistics
  where the browser exposes them (Chromium playout statistics; S-03 confirms the API) and otherwise
  from `Date.now()` deltas over 64-quantum windows inside the worklet (coarse, millisecond
  resolution). The status shape above is binding; the measurement method may be refined by S-03.

## 4. Shared vectors and tests

- `render-vectors` 1.1.0 (019) is unchanged: parts are inputs with gain and pan exactly like stems.
- New engine-only vectors `src/audio-engine/test/vectors/nam-linear.json`: a `Linear` NAM model
  (FIR taps) with input and expected output; the WASM build must match within 1e-6 (Node, no
  AudioWorklet). A tiny WaveNet model generated by `tools/nam-wasm/make_test_model.py` (random
  weights, fixed seed) has a stored output hash (regression of the build, not of accuracy).
- Vitest with the mocked `AudioContext` covers graph wiring, cross-fades, fallbacks, status
  throttling, `captureWet` alignment (synthetic latency) and part channels in the gate.

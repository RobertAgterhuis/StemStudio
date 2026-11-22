# ADR-030: Amp and pedal simulation with Neural Amp Modeler in WebAssembly (feature 021, C10)

- **Status**: Accepted (owner, 2026-11-19, via the Tipoca project site; proposed by the architect, 2026-11-16).
- **Numbering**: feature-scoped, see ADR-025.
- **Context documents**: `specs/021-partner-studio-bridge/{spec.md, research.md (R-06..R-11),
  data-model.md 3}`, ADR-005/ADR-010 (audio engine), ADR-009 (weights), the 015 (inputs, latency,
  takes) and 017 (strip effects) designs, constitution 1.3.1.

## Context

MoSCoW C10: amp and pedal simulation for guitar and bass played through an audio interface, with
Neural Amp Modeler (MIT) in an AudioWorklet/WASM, building on 015 input and 017 effects. Monitoring
must be real time with the lowest latency the browser allows; the mixer context runs at 44.1 kHz;
almost all NAM captures are trained at 48 kHz.

## Decisions

### D1 Processing in the browser, in the mixer's AudioContext

The chain runs in the existing engine context (same clock as playback and 015 recording):
`input (015) -> gate -> [pedal NAM] -> [amp NAM | built-in amp] -> cabinet ConvolverNode -> 017
StripEffects -> output level -> monitor bus / recorder`. A server-side path is rejected (latency).

### D2 NAM core compiled by StemStudio, not the npm player package

- NeuralAmpModelerCore (MIT) at a pinned commit, with Eigen (MPL-2.0, unmodified headers),
  nlohmann/json (MIT) and AudioDSPTools (MIT, resampler), compiled with a pinned Emscripten image
  (`emscripten/emsdk:<version>`, `-O3 -msimd128`, no pthreads, `ALLOW_MEMORY_GROWTH`, max 512 MB)
  plus StemStudio's own C shim (`tools/nam-wasm/shim.cpp`, MIT): `nam_create(json, len,
  host_rate)`, `nam_process(handle, in, out, frames)`, `nam_reset`, `nam_latency_frames`,
  `nam_expected_rate`, `nam_destroy`, `nam_last_error`.
- The shim wraps the model in AudioDSPTools' resampling container when the model rate differs from
  the context rate (44.1 kHz) and reports the added latency.
- The built `nam-engine.wasm` is committed in `src/audio-engine/vendor/nam/` with `PROVENANCE.md`
  (commits, emsdk version, command, SHA-256, licences). CI rebuilds it in the container and checks
  exports and golden vectors; app builds and unit tests never need Emscripten.
- Rejected: npm `neural-amp-modeler-wasm` (MIT, but React runtime/peer dependencies and a fast
  release cadence); writing our own NAM inference in TypeScript (slower, re-implements the model
  formats).

### D3 Worklet design and budget

One `AudioWorkletProcessor` `stemstudio-nam` per NAM instance (pedal, amp), mono in/out (the
input is summed to mono; NAM captures are mono), the compiled `WebAssembly.Module` passed in
`processorOptions`, models posted as `ArrayBuffer` (the JSON text), swapped with a 20 ms
equal-power cross-fade between two handles. Budget: `process()` <= 50 % of a 128-frame quantum on
the reference CPU for a standard WaveNet (SC-004, measured by S-03); overload = > 2 % of quanta over
90 % of the budget in 2 s -> `onRigStatus` `overloaded`. No SharedArrayBuffer, so no COOP/COEP.
The page CSP needs `'wasm-unsafe-eval'`.

### D4 Profiles: built-ins, a curated download catalogue, and own files

- Four built-in basic amps and two synthesised cabinets in own TypeScript (Web Audio nodes); they
  need no download and also cover browsers without WebAssembly.
- Catalogue `Amp/catalogue.v1.json` (embedded, read-only, JSON Schema in the 021 contracts). An
  entry is admitted only when: (1) it downloads anonymously over HTTPS from a host in
  `allowedHosts`; (2) a licence is stated **for that file** by its creator; accepted: CC0, CC BY,
  CC BY-SA, MIT, Apache-2.0, GPL (as data), CC BY-NC/-NC-SA (ADR-009 weights exception), and "no
  licence stated" only when no term forbids local personal use; excluded: terms that forbid
  redistribution through third-party apps or require an account/API agreement (e.g. TONE3000 "T3K"
  captures; users may upload those themselves); (3) SHA-256, size, creator, URL and licence are
  recorded in the catalogue and in `docs/THIRD-PARTY-LICENSES.md`. Files are downloaded by the API
  on request into `amp/profiles/`, never committed and never part of an image.
- Own `.nam` (<= 16 MiB, validated) and cabinet IR WAV (<= 2 s, <= 5 MiB) uploads.
- The catalogue is curated in Phase 0 (S-04); target 6-10 entries for guitar and bass; fewer is
  acceptable because the built-ins keep the feature complete.

### D5 Recording dry and wet

When the amp is on, the 015 recorder captures the dry tap and the post-chain wet tap in the same
worklet quantum; the wet tap drops the chain's reported latency frames so both files are aligned and
share 015's latency compensation. The take stores dry (015 file), wet (`wet.flac`) and the rig
snapshot; it plays wet by default; "re-amp" plays the dry file through any rig live; "print" uploads
a new wet file. Mode "record dry, hear amp on playback" is the answer to high monitoring latency.

### D6 Latency is shown, not hidden

The panel shows round trip = 015 calibration + chain latency (resampler) and warns above 30 ms with
tips (constitution V: browser audio on Windows typically 15-40 ms; ASIO is out of reach).

## Consequences

- One committed binary (WASM, a few hundred KB) with provenance; one CI container job.
- New backend tables and endpoints for profiles, IRs and rig presets; one new library folder.
- The amp is a UI-only capability for the 011 agent (it needs the live device).
- Every input family can use the chain; captures target guitar and bass (MoSCoW principle: other
  families get the clean chain with 017 effects).

## Alternatives rejected

| Alternative | Why not |
|---|---|
| Native helper app or VST host | VST cannot run in a browser (MoSCoW W9); native helpers need an installer |
| Second AudioContext at 48 kHz for the input | two clocks drift against playback; recording alignment breaks |
| Mixer context at 48 kHz | stems are 44.1 kHz; resampling every stem in the browser breaks 007 bit-identity |
| Bundling community capture packs | licences per file unknown (GPL as a whole, NC-SA, T3K); ADR-009 forbids committing weights |
| Offline amp rendering in the worker (Python NAM) | needs torch in the worker for a feature that must be live; re-amp in the browser covers it |

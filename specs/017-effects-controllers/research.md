# Research: Mixer Effects and Controllers (017)

Architect, 2026-11-16. Each item: question, options, decision, rationale. Decisions are binding
through ADR-026; numbers marked *(spike)* are verified in Phase 0 task T007 and corrected here.

## R-01 How to implement the effects: native nodes or AudioWorklet DSP

| Option | Parity live/offline | CPU | Code to own | Test |
|---|---|---|---|---|
| Native `BiquadFilterNode`, `DynamicsCompressorNode`, `ConvolverNode`, `GainNode` | same implementation in `AudioContext` and `OfflineAudioContext` | native C++, SIMD, off the main thread | graph wiring only | mocked nodes (Vitest), real in Playwright |
| Own DSP in an `AudioWorklet` (TS/WASM) | same code both sides | JS per sample, one worklet per strip or one big worklet | filters, compressor, reverb | pure-function unit tests |
| Own DSP mirrored in Python (worker render) | by vectors only, never exact for a compressor | n/a | two implementations | vectors |

**Decision**: native nodes. Biquad coefficients follow the Audio EQ Cookbook as required by the
Web Audio specification, so the EQ curve is well-defined; the compressor algorithm is
implementation-defined but identical between real-time and offline contexts of the same browser,
which is all "export as heard" needs (R-03). No DSP code to maintain, no new dependency.

## R-02 Strip topology, reverb as send

Current chain (engine 2.1.0): `sources -> Gain(fader x gate) -> StereoPanner -> Analyser -> master`.

New chain (engine 2.7.0):

```text
sources -> fxIn(Gain 1) -+-> dry Gain ----------------------------+-> Gain(fader x gate) -> Panner -> Analyser -> master
                         '-> [HP x1|2] -> LowShelf -> Mid1 -> Mid2 -> HighShelf -> [LP x1|2] -> Compressor -> Makeup -> wet Gain -'
                                                                                         Analyser -> send Gain -> reverb bus
reverb bus: Delay(pre-delay) -> Convolver(IR) -> return Gain -> return Analyser -> master
```

- Inserts are pre-fader (standard mixer behaviour; the fader rides the processed signal and the
  compressor's response does not depend on the fader position).
- The send taps after the panner (post-fader, post-gate): mute, solo, hidden and fader apply to
  the reverb as well; the reverb keeps the strip's stereo position.
- Dry/wet gain pair: on, off and bypass cross-fade the two gains (`setTargetAtTime`, 3 ms time
  constant, ~10 ms to 95 %), so the chain never clicks. The meter tap stays post-fader as in 001.
- **One reverb bus per mix** instead of a reverb insert per strip: 1 convolver instead of up to 28,
  one shared space (a real room), and the common mixing practice. An insert reverb is out of scope.

## R-03 Export parity: where effects are rendered (FR-009)

007 renders in the worker with numpy (ADR-016 D2), matching the engine's gain/pan by shared
vectors. That works for linear gain and pan; it cannot match a compressor (implementation-defined)
and would need a second EQ and reverb implementation in Python and a worker-api change (not
allocated to 017).

| Option | Exact as heard | Works without browser (agent) | Contract impact | Effort |
|---|---|---|---|---|
| A. Python effects in the worker | no (compressor), EQ/reverb by vectors | yes | worker-api bump, render-vectors bump | L, two DSPs |
| B. Browser pre-render (`OfflineAudioContext`, same graph builder) + 007 worker encodes | **yes, by construction** | no | backend: pre-render upload, two fields on the export request | M |
| C. Effects never exported, label exports "without effects" | no | yes | none | S, breaks 007's promise |

**Decision: B, with C as the honest fallback** when no browser renders (agent, API clients,
`effects` omitted). Flow:

1. The export dialog asks the engine to `renderPrerender(...)`: an `OfflineAudioContext`
   (44.1 kHz, stereo, length = range + pre-roll) built by the **same graph builder** as playback
   from the current state (selection gate, gain, pan, effects, reverb, master).
2. The engine streams the result as a 32-bit float WAV to
   `POST /api/tracks/{trackId}/prerenders` (direct `fetch`, no Blazor interop transfer) and
   returns the `prerenderId`.
3. The UI calls `createAudioExport` with `effects: apply` and the `prerenderId`. The backend moves
   the file into `assets/{assetId}/prerender.wav` (already an allowed worker input,
   `ports.LIBRARY_PATH_PATTERNS`) and starts the **unchanged** worker render job with that single
   input at gain 1, pan 0, master 1, no range. The worker keeps doing clip protection, bit depth,
   dither, MP3 and tags (007).
4. Pan 0 on a stereo input is the identity in the engine's pan law and in the worker's
   `pan_matrix` (007 E-05), gain 1 is exact in float, so the encoded file equals the pre-render up
   to encoding (SC-003).

Selection semantics are shared: "What I hear" uses the live gate (`gate.ts`), "Selected channels"
uses the 007 rule (listed channels at their fader and pan, mute/solo ignored, master applies). The
engine's offline gate function is tested against **every gate case of
`specs/007-export-library/contracts/render-vectors.json`**, the same vectors the backend planner
uses, so selection parity holds without a new vector file.

Pre-roll: the render starts `min(3 s, range start)` earlier and drops those samples, so the reverb
tail and the compressor envelope at the range start equal continuous playback (spec US4-5).

Retry: the pre-render stays in the asset folder until the asset completes (then deleted) or is
deleted; a failed effects export can be retried from it. Orphaned staging pre-renders (uploaded,
never used) are swept after 60 minutes by the existing background service.

Tempo (012): the offline graph uses the same builder, so whatever 012 inserts in the live graph is
available offline; whether an export includes speed/transpose follows 012's export rule (default
original tempo and pitch). Integration item for the CTO at merge.

## R-04 Reverb impulse responses

Options: bundled IR recordings (licence per file, binary assets), algorithmic reverb nodes
(feedback delay networks with `DelayNode` loops: CPU per strip, harder to keep identical), or
**generated IRs** for the `ConvolverNode`.

**Decision**: generate IRs in the engine: stereo, decorrelated seeded noise (xorshift32, fixed
seeds per channel and kind) with an exponential decay per kind and progressive high-frequency
damping (one-pole low-pass whose cutoff falls with time), early reflections as 6 sparse taps.

| Kind | RT60 | Damping start/end | Length |
|---|---|---|---|
| room | 0.7 s | 12 kHz -> 4 kHz | 1.0 s |
| plate | 1.6 s | 14 kHz -> 7 kHz | 2.0 s |
| hall | 2.4 s | 10 kHz -> 2.5 kHz | 3.0 s |

`irVersion: 1` is stored with the reverb settings so a future change of the generator never
changes how a saved mix sounds silently (an old version stays reproducible). The convolver uses
`normalize: false` with a fixed gain per kind so levels are predictable. Generation takes < 30 ms
*(spike)* and the buffer is shared by the live and the offline context.

## R-05 CPU budget (FR-010, SC-005)

- Worst case: 6 standard strips + 22 detail strips (5 split parents) = 28 strips; full EQ with
  24 dB/oct HP and LP = 8 biquads, 1 compressor, make-up gain; one convolver (3 s stereo).
- Native biquads cost a few multiply-adds per sample; the compressor includes a 6 ms look-ahead;
  the convolver uses FFT partitioning on Chromium's audio thread. Estimate on the reference
  machine: < 15 % of one core for the worst case *(spike)*.
- Measures by construction: (1) EQ/compressor nodes are created only when the effect is enabled;
  HP/LP sections only when enabled; (2) the wet path of a **hidden** strip (collapsed parent) and
  of a bypassed strip is disconnected after its cross-fade has finished (silent, so the timer does
  not determine audible timing; constitution V); (3) the reverb bus exists only while any send >
  0 or the return is used.
- Measurement: Playwright (Chromium) renders the worst-case graph for 30 s offline and asserts
  <= 10 s wall time on CI (`effects-budget.spec.ts`); a manual 5-minute playback check on the
  reference machine in the qa walkthrough with `chrome://media-internals` underrun counter. When
  `AudioContext.renderCapacity` (Chromium, experimental) exists, the engine reports it in
  `getEffectsLoad()`; otherwise it reports a node-cost estimate (cost units: biquad 1,
  compressor 4, convolver 40) for the Expert readout.

## R-06 Real-time vs offline determinism

Within one browser engine the same node graph with static parameters renders identically in an
`OfflineAudioContext` (Chromium uses the same kernels). Parameter ramps do not occur in exports
(static settings). Pre-delay uses a `DelayNode` with a static delay. Two offline renders of the
same state are compared sample for sample in E2E (SC-003). Across browsers results can differ
(e.g. the compressor in Firefox); the export is made in the browser the user listens in, so it is
"as heard" by definition. Documented in the technical manual.

## R-07 Practice EQ curves per family

Ranges follow common instrument fundamental + harmonic ranges in mixing references; the curves
are deliberately strong (practice, not polish):

- **Focus** = high-pass at the low end of the family range (24 dB/oct), low-pass at the high end
  (24 dB/oct), +3 dB presence peak; this removes bleed of other instruments outside the range.
- **Make room** = a deep cut of the family's core band on the other strips: one or two peaking
  bands at -15 dB, Q 0.7-1.0, or a low shelf for bass.

Concrete values are the catalogue in data-model 5 and are verified by the offline band-energy
test (SC-001). Detail stems map to families (data-model 5.1) so `drums.kick` focuses like a kick,
not like a full kit; there is one Focus curve per family plus a few detail-specific Focus
variants (kick, snare, hi-hat/cymbals) so drummers are not served worse than others.

## R-08 Web MIDI support and permission (FR-011, FR-016)

- Chromium (Chrome, Edge): supported; since Chrome 124 every `requestMIDIAccess` call (also
  without SysEx) is behind a permission prompt; `navigator.permissions.query({name: 'midi'})`
  reports `granted`/`prompt`/`denied`. Requires a secure context (`localhost` counts).
- Firefox: implemented from 108 behind a site-permission add-on flow; not a supported target,
  may work, untested. Safari: not available.
- **Decision**: feature-detect (`'requestMIDIAccess' in navigator` and `isSecureContext`); call
  `requestMIDIAccess({ sysex: false, software: false })` only from the "Connect controller"
  button (user activation) or automatically when the permission is already `granted`
  (re-connect without a prompt). Never on page load otherwise. Reasons:
  `unsupported`, `insecure-context`, `denied`, `prompt` (not asked yet), `no-device`, `connected`.

## R-09 MIDI message parsing

Web MIDI delivers one complete message per `midimessage` event (running status is expanded by the
browser), but devices still send note-on velocity 0 as note-off, 14-bit controllers as MSB/LSB
pairs (CC n and n+32) and relative encoders. Parser (pure functions, 100 % unit-testable):

- Channel voice messages only: note on/off, CC, pitch bend, program change. System messages
  (clock 0xF8, active sensing 0xFE, SysEx) are ignored.
- 14-bit CC: a binding of kind `cc14` on CC n (0-31) combines MSB n and LSB n+32; values update on
  the LSB or after 20 ms without LSB (MSB-only controllers).
- Relative modes: `relative-twos` (1..63 up, 65..127 down), `relative-signed` (bit 6 = sign,
  64+n down), `relative-offset` (64 = 0, 65 up, 63 down). Step per tick = 1/127 of the range,
  times 4 with Shift (a bound modifier is out of scope; constant step only).
- Learn infers: CC -> `absolute` (or `cc14` when an LSB at n+32 follows within 20 ms), note ->
  `toggle` for strip mute/solo and loop, `trigger` for commands, pitch bend -> `absolute`
  (14-bit, Mackie-style fader).
- Pick-up (soft takeover): an absolute control only takes over once its value crosses (or comes
  within 2 % of) the current target value; afterwards it follows. Default on for faders and knobs.

## R-10 Dispatch path: JS -> .NET -> engine (FR-014, SC-004)

| Option | Single source of state | Latency | Notes |
|---|---|---|---|
| JS applies to the engine directly and notifies .NET | no (two writers) | lowest | UI and saved mix can diverge |
| **JS maps and coalesces; batched `OnControllerInput(events)` to .NET; .NET runs the same mixer methods as the UI** | yes | interop round trip (localhost) + <= 33 ms coalescing | chosen |
| Raw MIDI bytes to .NET | yes | flood of SignalR messages | rejected |

Coalescing: per target the latest value wins; a batch is flushed on the shared animation frame
loop at most every 33 ms; discrete actions (buttons) flush immediately. Budget for SC-004:
<= 33 ms coalescing + ~5 ms interop on localhost + 10 ms ramp = < 50 ms. `setTimeout`/frames do
not determine audible timing in the sense of constitution V (that rule is about scheduling of
sample-accurate events); transport starts still use the engine's look-ahead start.

## R-11 Where mappings are stored

`localStorage` (per browser, lost on browser change, not in backups) vs **backend** (SQLite, part
of the library, included in 013 backups as database rows, survives browser change). Decision:
backend, one profile per device identity `(name, manufacturer)` (Web MIDI port ids are not stable
across sessions). The JS controller receives the active profile from .NET; nothing MIDI-related is
stored in the browser (no `localStorage`).

## R-12 Persisting optional effects in mixes (FR-008)

Mixes store channels as EF Core JSON complex collections (`MixConfiguration`). Adding an optional
nested complex property `Effects` (nullable) to `ChannelSettings`/`DetailChannelSettings` and a
nullable `Effects` complex property on the mix: rows written before 1.16.0 lack the JSON members
and materialise as `null` (= effects off). EF Core 10 supports optional complex types inside JSON
*(spike: Phase 0 T007 checks a 1.15.0 database file loads unchanged; fallback: a value-converted
JSON column `JsonColumn<T>` as used for assets)*. No migration of existing data; a new migration
only adds the mix-level `Effects` column (nullable).

Normalisation: the backend stores exactly what it receives; the UI sends `effects: null` for a
strip whose effects are all off and neutral (so "save again without changes" stores no effects,
US3-2).

## R-13 Fake MIDI device for Playwright

Playwright cannot attach a virtual MIDI port. A test init script (`e2e/fixtures/fake-midi.ts`,
`page.addInitScript`) replaces `navigator.requestMIDIAccess` and `navigator.permissions.query`
for `midi` with a fake `MIDIAccess` (inputs map, `onstatechange`, `MIDIInput.onmidimessage`) and
exposes `window.__fakeMidi = { plug(name, manufacturer), unplug(id), send(id, bytes),
setPermission(state) }`. The engine's MIDI module accepts `requestMIDIAccess` and `permissions`
injections (options), so Vitest uses the same fake. A second init script deletes the API to test
the unsupported path. Events carry `timeStamp` for the latency assertion (SC-004).

## R-14 Location of the MIDI code

The MIDI module lives in the audio-engine package (`src/audio-engine/src/midi/`), exported as a
separate factory `createMidiController` next to `createEngine`, because (1) the roadmap allocates
engine 2.7.0 and no `web-interop` version to 017, (2) it reuses the engine's `FrameLoop` for
coalescing and (3) it is tested with the same Vitest setup. It has no dependency on the engine
instance (the engine never imports it), so it can move to `web-interop` later without a contract
change in .NET (the .NET wrapper `IMidiController` hides the module).

## R-15 Pre-render upload format and limits

- Format: RIFF WAVE, `WAVE_FORMAT_IEEE_FLOAT` (3), 32-bit, 2 channels, 44,100 Hz. Float keeps
  peaks above 0 dBFS, so the worker's clip protection (007 FR-005) works as for any mix.
- Size: 8 bytes per frame; the 007 range limit of 900 s gives 317,520,044 bytes. Limit
  `StemStudio:MaxPrerenderBytes` = 320,000,000 (413 above). Kestrel's request body limit is raised
  for this endpoint only.
- Upload: one `fetch` `POST` whose body is a `Blob` assembled from 1-second WAV chunks (the
  browser can page Blob parts to disk). Progress goes to .NET through `OnRenderProgress`: render
  0..0.8 (from `OfflineAudioContext` `suspend`/`resume` checkpoints every 5 s of audio), upload
  0.8..1.0 (`fetch` reports no upload progress, so 0.8 at start and 1.0 on the response).
- CORS: the web origin may `POST` to `/api/tracks/{trackId}/prerenders` with
  `Content-Type: audio/wav` (preflight allowed for that path only).
- Memory: the `OfflineAudioContext` output for 15 min is ~318 MB plus the WAV Blob; the dialog
  warns above 10 minutes. Segment rendering would bound memory but breaks compressor/reverb
  continuity at segment edges; rejected for 017.

## R-16 Help, levels, a11y

- Help topics follow help-content 1.0.0 (area `mixer`; the Controllers page gets the screen topic
  with route `/controllers`); no new help area, so no help-content version bump.
- Sliders use Radzen sliders with `aria-valuetext` in units ("2.5 kHz", "-6.0 dB", "4:1").
  The EQ curve is drawn by the engine into an SVG element (like meters, no data over interop)
  and is `aria-hidden`; every value is a form control. Gain-reduction meters use the meter
  pattern of 004 (`role="meter"`, `aria-valuenow` written by the engine).
- MIDI learn is keyboard operable: in learn mode Tab moves between mappable controls, Enter
  selects, a polite live region announces "Move a control on your device for Bass volume",
  Escape cancels.

## Dependencies

None added. Web Audio and Web MIDI are browser APIs. Playwright, Vitest, xUnit, bUnit as today.

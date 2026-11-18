# Contract delta: Audio engine JS interop 2.6.0 (feature 016, draft, additive)

Status: **merged** into `audio-engine.md` **2.6.0** (016 Phase 0 T004, 2026-11-18; reconciliation table there; `scoreTake` takes one `TakeScoreRequest`, the worklet file is `audio-engine-pitch-worklet.js`, latency sources are the 015 `LatencyMethod`). Planning text (2026-11-16) below. Base: engine **2.5.0** (015, written in
parallel; on `main` the last full document is 2.1.0 in
[006](../../006-synced-lyrics-karaoke/contracts/audio-engine.md)). In Phase 0 (T004) the
architect merges this delta into `specs/016-instrument-views/contracts/audio-engine.md` **2.6.0**
and into `src/audio-engine/src/types.ts` (`ENGINE_CONTRACT_VERSION = "2.6.0"`). Every 2.5.0 call
behaves exactly as before. Rationale: ADR-033 D3 (timeline views), ADR-032 (pitch tracking),
ADR-031 D5 (chord spelling).

**Changelog line** (for the merged document): 2.6.0 (additive, feature 016) - chord lane, chord
symbols in the karaoke banner, key-region-aware spelling and capo mode (`ChordDisplay.capo`,
`showSounding`, host `data-current-key`), drum hit lanes, singing feedback (pitch-tracker AudioWorklet, pitch
view, unit scoring, take scoring), `getRoundTripLatency`, callbacks `OnSingingUnitScored` and
`OnSingingStateChanged`. All 2.5.0 calls are unchanged.

## Assumed 2.3.0-2.5.0 surface (reconciled in Phase 0 T001)

| Need | Assumed from | Placeholder name used below |
|---|---|---|
| Current playback transpose in semitones | 012 (2.3.0) | internal `transposeSemitones` of the engine's transpose state |
| Playback rate (speed) | 012 (2.3.0) | `transportPosition(t)` already accounts for rate (012 owns the formula) |
| Selected input node and its `MediaStreamTrack` | 015 (2.5.0) | internal input graph (`InputSource`) opened by the 015 call |
| Calibrated round-trip latency in ms | 015 (2.5.0) | internal latency state (`calibrated` or not) |
| A take's audio URL | 015 (2.5.0) / backend 1.14.0 | `takeUrl` argument |

If 012/015 expose different names, only this delta's internal wiring changes; the methods below
keep their names.

## New types (`types.ts`)

```ts
export type ChordQuality = 'maj' | 'min' | 'dim' | 'aug' | 'min6' | 'maj6' | 'min7' | 'minmaj7'
  | 'maj7' | '7' | 'dim7' | 'hdim7' | 'sus2' | 'sus4';
export interface ChordSymbolEvent {
  readonly startMs: number; readonly endMs: number;
  readonly root: number | null; readonly quality: ChordQuality | null; readonly bass: number | null;
}
export interface KeyRegionData { readonly startMs: number; readonly endMs: number; readonly tonic: number; readonly mode: 'major' | 'minor' }
export interface ChordChartData {
  readonly segments: readonly ChordSymbolEvent[];   // contiguous, ordered, <= 4000
  readonly keyTonic: number | null; readonly keyMode: 'major' | 'minor' | null;   // global key (fallback)
  readonly keyRegions?: readonly KeyRegionData[];   // S11: spelling uses the region a chord starts in
}
export interface ChordDisplay { readonly capo: number; readonly showSounding?: boolean }   // capo 0..12 (capo mode, S12)
export interface ChordLaneOptions { readonly barsBehind?: number; readonly barsAhead?: number; readonly reducedMotion?: boolean }
export type DrumPiece = 'kick' | 'snare' | 'toms' | 'hihat' | 'cymbals';
export interface DrumHitEvent { readonly timeMs: number; readonly piece: DrumPiece; readonly strength: number }
export interface HitLaneOptions { readonly lookAheadMs?: number; readonly pieces?: readonly DrumPiece[]; readonly reducedMotion?: boolean }
export interface PitchContour { readonly hopMs: 10; readonly frames: ArrayLike<number> }   // centi-semitones, 0 = unvoiced
export interface SingingUnit { readonly index: number; readonly startMs: number; readonly endMs: number }
export interface SingingFeedbackRequest {
  readonly pitchViewElementId: string;
  readonly reference: PitchContour;
  readonly units: readonly SingingUnit[];        // lyric lines or fallback units (data-model 3.4)
  readonly toleranceCents: number;               // 10..200
  readonly roundTripMs?: number | null;          // null/absent = the 015 calibrated value, else the estimate
  readonly reducedMotion?: boolean;
}
export interface SingingUnitScore { readonly index: number; readonly score: number | null; readonly pass: number }
export interface SingingSummary {
  readonly unitsScored: number; readonly average: number | null;
  readonly best: { readonly index: number; readonly score: number } | null;
  readonly toleranceCents: number; readonly latencySource: 'calibrated' | 'estimated' | 'given';
}
export type SingingState = 'idle' | 'starting' | 'running' | 'stopped' | 'failed';
export interface RoundTripLatency { readonly ms: number; readonly source: 'calibrated' | 'estimated' }
export type DotNetCallbacks = /* 2.5.0 values */ | 'OnSingingUnitScored' | 'OnSingingStateChanged';
```

## New `AudioEngine` methods

| Method | Behaviour |
|---|---|
| `attachChordLane(elementId, chart, options?)` | Engine-owned DOM in the host: `.chord-lane-item` elements (text via `textContent`) with `data-state` = `past`/`current`/`future`, `data-start-ms`; positioned by `style.transform` on a horizontal timeline of `barsBehind` (1) to `barsAhead` (6) bars from the beat grid (or 4 s/12 s without a grid). Host attributes: `data-chord-index`, `data-current-symbol`, `data-next-symbol`, `data-position-ms`. Replaces an attached lane. |
| `detachChordLane()` | Removes the children and the host attributes; no-op when none. |
| `setKaraokeChords(chart \| null)` | Adds `.karaoke-chord` elements above the words of the three karaoke slots: a chord is anchored to the word whose `[start, nextStart)` holds its `startMs` (`data-lead="true"` before the first word of a line), with `data-state` like words; `null` removes them. Allowed before or after `attachKaraoke`; kept across `attachKaraoke` calls. Host gets `data-chord-index`. |
| `setChordDisplay(display)` | Sets the capo (capo mode, S12) for every chord view; symbols are re-spelled from `root`/`quality`/`bass`, the key region the chord starts in (fallback the global key), the engine's transpose and the capo (data-model 2.5, 2.9) on the next frame; with `showSounding` each item also gets a `.chord-sounding` child with the sounding symbol. A transpose change (012) re-spells automatically. The chord lane host additionally gets `data-capo` and `data-current-key` (display of the current region's key at the current transpose, data-model 2.8), so Blazor can show the current key without per-frame interop. |
| `attachHitLanes(elementId, hits, options?)` | Engine-owned `<canvas>` in the host (`role="img"`, `aria-label` set by Blazor on the host); five lanes (data-model 5.2), look-ahead `lookAheadMs` (4000; 2000..8000), only `pieces` drawn when given. Host attributes: `data-position-ms`, `data-next-hit-ms`, `data-next-hit-piece`, `data-visible-hits` (count), `data-look-ahead-ms`. |
| `setHitLaneOptions(options)` | Changes look-ahead or pieces without re-attaching. |
| `detachHitLanes()` | Removes the canvas and attributes. |
| `getRoundTripLatency(): RoundTripLatency` | The value singing feedback would use (015 calibration or estimate, research R-07). |
| `startSingingFeedback(request): Promise<void>` | Loads the `pitch-tracker` worklet module once (`audio-engine-pitch.worklet.js` next to `audio-engine.js`), connects it to the 015 input (rejects with `Error("No input device is open")` when 015 has none), attaches the pitch view canvas to `pitchViewElementId`, and scores units while playing (data-model 3.3-3.4). State callbacks: `starting` -> `running`. Host attributes of the pitch view: `data-state`, `data-singer-cents` (rounded, `""` when unvoiced), `data-target-cents`, `data-unit-index`, `data-last-score`, `data-latency-source`. |
| `setSingingTolerance(cents)` | 10..200; applies to the current and later units. |
| `stopSingingFeedback(): SingingSummary` | Disconnects the worklet, keeps the canvas showing the last window, returns the summary; state `stopped`. Idempotent (returns the last summary). |
| `scoreTake(takeUrl, reference, units, toleranceCents, roundTripMs?): Promise<SingingSummary & { readonly units: readonly SingingUnitScore[] }>` | Fetches and decodes the take, runs the same YIN core (shared pure module) offline over it, aligns with the take's recorded latency compensation (015 takes are already compensated: `roundTripMs` default 0) and scores every unit. Does not need the worklet. |

## Timeline clock and frame loop

- All views use the ADR-015 karaoke clock (`positionMs` from `getOutputTimestamp`, output latency
  subtracted before the loop wrap, 012 rate applied by `transportPosition`); paused/stopped =
  stored position. No accumulation from frame deltas.
- One shared `requestAnimationFrame` loop (the 006 `frame-loop.ts`) draws meters, karaoke,
  banner chords, chord lane, hit lanes and pitch view while attached, playing and visible, plus
  one frame after every attach, seek, pause, stop, loop change, offset/capo/transpose change and
  resize. Budget: <= 4 ms per frame for all views (benchmark test with counted primitives).
- Cursors advance in O(1) per frame and binary-search after seeks, loop wraps and changes.
- Reduced motion (`options.reducedMotion`, default from the media query, followed at runtime): the
  lane, hit lanes and pitch view page instead of scrolling (data-model 3.5, 5.2).

## Pitch tracker worklet (`pitch-tracker`)

- Processor name `stemstudio-pitch-tracker`; parameters per research R-05 (binding); messages
  per data-model 3.2. Pure YIN core in `src/pitch/yin.ts` (no Web Audio types, used by the
  worklet and `scoreTake`), decimator in `src/pitch/decimate.ts`.
- Overrun guard: the main thread counts hops whose `t` lags the context time by more than 50 ms;
  50 such hops within 5 s -> `stopSingingFeedback()` internally, state `failed`, reason "Pitch
  tracking is too slow on this device." (`OnSingingStateChanged('failed', reason)`).
- No microphone audio leaves the engine; the worklet's output is silent (not connected to the
  destination).

## Callbacks to .NET (additions)

| Method (JSInvokable) | Arguments | When |
|---|---|---|
| `OnSingingUnitScored` | `int index, int? score, int pass` | once per finished unit (at most a few per second) |
| `OnSingingStateChanged` | `string state, string? reason` | every `SingingState` change |

## Errors (additions)

`Error("Chord lane host not found: <id>")`, `Error("Hit lane host not found: <id>")`,
`Error("Pitch view host not found: <id>")`; `Error("Invalid chord chart: <reason>")` (segments not
ordered/contiguous, > 4000, root/bass outside 0..11, unknown quality); `Error("Invalid drum hits:
<reason>")` (> 40,000, unknown piece, strength outside 0..1, times decreasing); `Error("Invalid
pitch contour: <reason>")` (hopMs not 10, > 90,000 frames, values outside 0..12700);
`Error("Invalid singing request: <reason>")` (tolerance outside 10..200, units not ordered);
`Error("Capo out of range: <value>")`; `Error("Singing feedback is already running")`;
`Error("No input device is open")`. Validation happens before an attached view is replaced.

## Testing

Vitest with the existing `AudioContext` mock plus a fake `AudioWorkletNode`/`audioWorklet.addModule`,
a fake canvas 2D context that records primitives, the fake `requestAnimationFrame`, stubbed
`getBoundingClientRect`/`ResizeObserver`; the YIN core is tested on generated sines, harmonics and
noise (SC-004 accuracy) and with a benchmark test (`yin.bench.test.ts`, reported, not gating in
CI); shared chord spelling vectors (`chord-spelling-vectors.json`). Coverage >= 90 % lines,
branches, functions, statements.
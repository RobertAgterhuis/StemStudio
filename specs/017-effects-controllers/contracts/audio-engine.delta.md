# Contract delta: audio engine 2.7.0 (feature 017)

Additive minor version on top of the engine contract current when 017 starts (2.6.0 from 016;
2.1.0 is the last merged). Nothing existing changes name, signature or behaviour; a track without
effects renders exactly as in 2.6.0 (the dry path is the identity). Phase 0 (T003) merges this
delta into `contracts/audio-engine.md` and `src/audio-engine/src/types.ts`
(`EngineContractVersion = '2.7.0'`).

## 1. Types (additions to `types.ts`)

```ts
/** Instrument family of a channel (data-model 5.1). */
export type InstrumentFamily = 'vocals' | 'drums' | 'bass' | 'guitar' | 'keys' | 'orchestral' | 'other';

/** High- or low-pass section; slope 12 = one biquad, 24 = two (Butterworth). */
export interface PassBand { readonly enabled: boolean; readonly frequencyHz: number; readonly slope: 12 | 24; }
/** Shelf band. */
export interface ShelfBand { readonly frequencyHz: number; readonly gainDb: number; }
/** Peaking band. */
export interface PeakBand { readonly frequencyHz: number; readonly gainDb: number; readonly q: number; }

/** 6-band parametric EQ; mirrors backend `Eq` (data-model 2.1). */
export interface EqSettings {
  readonly enabled: boolean;
  readonly presetId: string | null;
  readonly highPass: PassBand;   // 20..2000 Hz
  readonly lowShelf: ShelfBand;  // 30..500 Hz, -18..18 dB
  readonly mid1: PeakBand;       // 40..16000 Hz, -18..18 dB, Q 0.3..12
  readonly mid2: PeakBand;
  readonly highShelf: ShelfBand; // 1000..16000 Hz
  readonly lowPass: PassBand;    // 500..20000 Hz
}

/** Compressor; mirrors backend `Compressor`. */
export interface CompressorSettings {
  readonly enabled: boolean;
  readonly presetId: string | null;
  readonly thresholdDb: number; // -60..0
  readonly ratio: number;       // 1..20
  readonly kneeDb: number;      // 0..40
  readonly attackMs: number;    // 0..200
  readonly releaseMs: number;   // 10..1000
  readonly makeupDb: number;    // 0..24
}

/** Effects of one strip; mirrors backend `ChannelEffects`. */
export interface ChannelEffects {
  readonly bypass: boolean;
  readonly eq: EqSettings | null;
  readonly compressor: CompressorSettings | null;
  readonly reverbSend: number; // 0..1, post-fader
}

/** Reverb kind of the shared bus. */
export type ReverbKind = 'room' | 'plate' | 'hall';

/** Shared reverb bus; mirrors backend `MixEffects.reverb`. */
export interface ReverbSettings {
  readonly kind: ReverbKind;
  readonly returnLevel: number; // 0..1
  readonly predelayMs: number;  // 0..200 (integer)
  readonly irVersion: 1;
}

/** Built-in preset (data-model 5). */
export interface EffectPreset {
  readonly id: string;
  readonly name: string;
  readonly scope: 'eq' | 'compressor';
  readonly mode?: 'focus' | 'make-room';
  readonly families: readonly InstrumentFamily[]; // empty = every family
  readonly variantOf?: string;
  readonly eq?: EqSettings;
  readonly compressor?: CompressorSettings;
}

/** Load estimate for the Expert readout. */
export interface EffectsLoad {
  /** `renderCapacity` average load 0..1 when the browser reports it, else null. */
  readonly measured: number | null;
  /** Cost units of active nodes (biquad 1, compressor 4, convolver 40). */
  readonly costUnits: number;
  /** Budget in cost units for the worst case (28 x (8 + 4) + 40 = 376). */
  readonly budgetUnits: number;
}

/** Argument of {@link AudioEngine.renderPrerender}. */
export interface PrerenderRequest {
  /** Track id (canonical lower-case GUID) for the upload URL. */
  readonly trackId: string;
  /** Upload URL, `/api/tracks/{trackId}/prerenders` (absolute or API-relative). */
  readonly uploadUrl: string;
  /** Null = "What I hear" (live gate); a list = "Selected channels" (007 rule). */
  readonly channels: readonly ChannelId[] | null;
  /** Range in seconds, or null for the whole track. */
  readonly range: { readonly startSeconds: number; readonly endSeconds: number } | null;
  /** Abort signal (dialog "Cancel"). */
  readonly signal?: AbortSignal;
}

/** Result of a pre-render upload (body of the backend's 201). */
export interface PrerenderResult {
  readonly prerenderId: string;
  readonly frames: number;
  readonly sizeBytes: number;
}

/** Meter target additions: the reverb return. */
export type MeterTarget = ChannelId | 'master' | 'reverb';
```

`DotNetCallbacks` gains `'OnRenderProgress'` (`OnRenderProgress(double fraction)`, 0..1,
non-decreasing, at most 10 Hz during `renderPrerender`).

## 2. `AudioEngine` methods (additions)

| Method | Behaviour | Errors |
|---|---|---|
| `setChannelEffects(id: ChannelId, effects: ChannelEffects \| null): void` | Validates and clamps like `setChannel`; builds/removes nodes lazily; parameter changes ramp (3 ms constant); on/off/bypass cross-fade dry/wet (<= 10 ms). `null` = all off. Works for hidden channels (applied on expand). | `Unknown stem "<id>"`, `Invalid <field>: <value>` |
| `setReverb(settings: ReverbSettings): void` | Regenerates the IR when the kind changes (deterministic, R-04); return level ramps; pre-delay static. | `Invalid reverb: <reason>` |
| `setEffectsBypassed(bypassed: boolean): void` | Global A/B bypass of every strip and the reverb return (cross-faded); not part of any saved or exported state. | - |
| `getEffectPresets(): readonly EffectPreset[]` | Catalogue version 1 (data-model 5), stable order. | - |
| `familyOf(id: ChannelId): InstrumentFamily` | Data-model 5.1 mapping. | unknown id |
| `attachEqCurve(id: ChannelId, elementId: string): void` | Draws the strip's EQ magnitude response (20 Hz-20 kHz, log, -24..+24 dB) into the `<svg>` element as one `<path>` via `getFrequencyResponse`, redrawn on change (coalesced per frame). Element is `aria-hidden`. | `EQ curve host not found: <id>` |
| `detachEqCurve(id: ChannelId): void` | Removes the drawing. | - |
| `attachReductionMeter(id: ChannelId, elementId: string): void` | Per frame writes `--gr-level` (0..1 of 0..24 dB), `data-gr-db` (rounded, <= 0) and `aria-valuenow` from `DynamicsCompressorNode.reduction`. | no compressor -> `data-gr-db="0"` |
| `detachReductionMeter(id: ChannelId): void` | - | - |
| `getEffectsLoad(): EffectsLoad` | Research R-05. | - |
| `renderPrerender(request: PrerenderRequest): Promise<PrerenderResult>` | Builds an `OfflineAudioContext` (44.1 kHz, 2 ch, pre-roll `min(3 s, start)` + range) with the **same graph builder** as playback from the current state: gate (live rule for `channels = null`, 007 "selected channels" rule otherwise; tested against `render-vectors.json` gate cases), fader, pan, effects, reverb, master; it always renders the saved-state effects and ignores the transient global bypass (the UI turns the effects option off while bypass-all is on); metronome and karaoke never included. Drops the pre-roll, encodes 32-bit float WAV in 1 s chunks, `POST`s it to `uploadUrl` (`Content-Type: audio/wav`), reports progress, resolves with the 201 body. | `AbortError` on cancel; `Error("Prerender upload failed: <status> <title>")`; `Error("No track loaded")` |

Graph builder: one exported internal function `buildStripChain(context, state)` used by the live
mixer and by `renderPrerender` (the parity guarantee, research R-03). Unit tests assert that the
live and the offline graph for the same state have the same node types, order and parameter
values (mocked contexts).

## 3. MIDI controller module (new export `createMidiController`)

Separate factory in the same package, independent of the engine instance (research R-14).

```ts
/** Why MIDI is (not) usable. */
export type MidiAvailability = 'unsupported' | 'insecure-context' | 'prompt' | 'denied' | 'no-device' | 'connected';

/** One input port as shown to the user. */
export interface MidiDevice { readonly id: string; readonly name: string; readonly manufacturer: string; readonly connected: boolean; }

/** Binding as sent by .NET (backend `ControllerBinding`, data-model 3.2). */
export interface MidiBinding {
  readonly id: string;
  readonly control: { readonly kind: 'cc' | 'cc14' | 'note' | 'pitch-bend' | 'program'; readonly channel: number; readonly number: number | null };
  readonly target: { readonly kind: string; readonly slot?: number; readonly command?: string };
  readonly mode: 'absolute' | 'relative-twos' | 'relative-signed' | 'relative-offset' | 'toggle' | 'momentary' | 'trigger';
  readonly invert: boolean;
  readonly pickup: boolean;
}

/** One coalesced controller event delivered to .NET. */
export interface ControllerEvent {
  /** Binding that produced it. */
  readonly bindingId: string;
  /** `value` 0..1 (absolute), `delta` -1..1 (relative), `on`/`off` (boolean), `trigger`. */
  readonly type: 'value' | 'delta' | 'on' | 'off' | 'trigger';
  readonly value: number;
  /** `MIDIMessageEvent.timeStamp` of the last message folded into this event (ms, DOMHighResTimeStamp). */
  readonly timeStamp: number;
}

/** Options (mainly for tests). */
export interface MidiControllerOptions {
  readonly navigator?: Pick<Navigator, 'requestMIDIAccess' | 'permissions'>;
  readonly isSecureContext?: boolean;
  readonly frames?: FrameScheduler;        // coalescing loop (<= 30 Hz flush)
  readonly lsbWindowMs?: number;           // cc14 MSB-only fallback, default 20
  readonly pickupTolerance?: number;       // default 0.02
}

export interface MidiController {
  /** Capability and permission check (`permissions.query`); never prompts. */
  availability(): Promise<MidiAvailability>;
  /** Requests access (`sysex: false`); call only from a user action unless availability is not `prompt`. */
  connect(): Promise<MidiAvailability>;
  /** Current inputs. */
  devices(): readonly MidiDevice[];
  /** Activates bindings for a device (by id); replaces earlier bindings of that device. */
  setBindings(deviceId: string, bindings: readonly MidiBinding[]): void;
  /** Current target values 0..1 per binding id, for pick-up (sent by .NET after UI changes, coalesced). */
  syncValues(values: Readonly<Record<string, number>>): void;
  /** Next channel-voice message on any input is reported via `OnControllerLearned`; cancels after `timeoutMs` (default 10000). */
  startLearn(timeoutMs?: number): void;
  cancelLearn(): void;
  /** Removes listeners and timers; does not close ports the browser owns. */
  dispose(): void;
}

export type CreateMidiController = (dotnetRef: DotNetObjectReference, options?: MidiControllerOptions) => MidiController;
```

.NET callbacks of the MIDI module (`[JSInvokable]` on the controller's .NET object):

| Callback | Argument | When |
|---|---|---|
| `OnControllerInput` | `ControllerEvent[]` | coalesced batch: continuous per binding latest-wins, flushed on the frame loop at most every 33 ms; boolean/trigger events flush immediately |
| `OnControllerLearned` | `{deviceId, control, suggestedMode}` | first message after `startLearn` |
| `OnControllerLearnTimeout` | - | `timeoutMs` elapsed |
| `OnControllerDevicesChanged` | `MidiDevice[]` | `MIDIAccess.onstatechange` |
| `OnControllerAvailabilityChanged` | `MidiAvailability` | permission change (`PermissionStatus.onchange`) or last device removed/added |

Parsing rules: research R-09 (channel voice only; note-on velocity 0 = note-off; cc14 pairing;
relative encodings; system messages ignored). Pick-up: absolute events for a binding with
`pickup` are dropped until the incoming value is within `pickupTolerance` of, or crosses, the last
synced value.

## 4. Version notes

- `ENGINE_CONTRACT_VERSION` = `'2.7.0'`; changelog line: "2.7.0 (additive, feature 017) - strip
  effects (EQ, compressor, reverb send), shared reverb bus and meter `reverb`, global bypass,
  preset catalogue, EQ curve and gain-reduction meters, `renderPrerender` for exports with
  effects, `createMidiController` (Web MIDI input)."
- 015 take strips: `setChannelEffects` accepts every `ChannelId` the engine knows, including the
  take ids 015 defines.

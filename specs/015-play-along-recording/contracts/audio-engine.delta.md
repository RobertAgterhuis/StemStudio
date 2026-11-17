# Contract delta: audio engine 2.4.0 -> 2.5.0 (feature 015)

**Merged** into the full [audio-engine.md](audio-engine.md) 2.5.0 and `types.ts` in Phase 0 (2026-11-17);
those are binding now (this delta is the merge input for the re-merge onto 014's 2.4.0, DELTA.md "Re-merge").

Additive. Every 2.4.0 call behaves exactly as before; without `openInput` and `loadTake` the
graph and the gate are unchanged. The binding TypeScript lands in `src/audio-engine/src/types.ts`
in Phase 0 (T004) on top of 012 (2.3.0) and 014 (2.4.0); `ENGINE_CONTRACT_VERSION = "2.5.0"`.
Design: ADR-024; algorithms: research.md R-03, R-04, R-09, R-10; state and mapping:
data-model.md 3.

## 1. Types (binding names and shapes)

```ts
/** Channel ids gain the literal 'take' (the one take channel strip). */
type ChannelId = StemName | `${ParentStemName}.${DetailStemName}` | 'take';
/** Meters may also be attached to the open input. */
type MeterTarget = ChannelId | 'master' | 'input';

type RecordingPart = 'none' | 'vocals' | 'guitar' | 'bass' | 'piano' | 'drums' | 'other';
type InputChannelMode = 'mono-1' | 'mono-2' | 'stereo';
type LatencyMethod = 'loopback' | 'manual' | 'estimated';
type RecordingState = 'closed' | 'open' | 'countIn' | 'recording' | 'finishing';
type TakeCompareMode = 'both' | 'take' | 'original';
type PermissionStateValue = 'granted' | 'prompt' | 'denied' | 'unknown';

interface RecordingSupport {
  readonly supported: boolean;
  /** Phase 0 addition: 'insecureContext' | 'noMediaDevices' | 'noAudioWorklet' | 'rateMismatch'; null when supported. */
  readonly reasonCode: RecordingUnsupportedReason | null;
  /** Readable reason when not supported (insecure context, no AudioWorklet, no getUserMedia). */
  readonly reason: string | null;
  readonly permission: PermissionStateValue;   // navigator.permissions 'microphone', 'unknown' when not queryable
}

interface InputDevice {
  readonly deviceId: string;
  /** Empty until permission is granted (browser rule). */
  readonly label: string;
  readonly groupId: string;
}

interface OpenInputRequest {
  /** null = the browser default input. */
  readonly deviceId: string | null;
  readonly mode: InputChannelMode;
}

interface InputInfo {
  readonly deviceId: string;
  readonly label: string;
  /** Channels the device delivers (1 or 2). 'mono-2' and 'stereo' need 2. */
  readonly channelCount: 1 | 2;
  readonly mode: InputChannelMode;
  /** Device rate reported by the track (the context stays at 44100). */
  readonly deviceSampleRate: number | null;
  /** round((baseLatency + outputLatency + track latency) * 44100). */
  readonly estimatedLatencySamples: number;
  /** round((baseLatency + outputLatency) * 44100), compared with a stored calibration. */
  readonly outputLatencySamples: number;
}

interface MonitoringSettings { readonly enabled: boolean; readonly volume: number }   // volume 0..1

interface CalibrationOptions {
  /** Number of clicks, 6..12, default 8. */
  readonly clicks?: number;
  /** PRNG seed for the click gaps (tests); default random. */
  readonly seed?: number;
}

interface CalibrationResult {
  readonly ok: boolean;
  readonly latencySamples: number | null;
  readonly latencyMilliseconds: number | null;
  readonly detectedClicks: number;
  readonly spreadSamples: number | null;
  readonly outputLatencySamples: number;
  readonly inputPeakDbfs: number;
  readonly reason: 'noSignal' | 'tooFewClicks' | 'inconsistent' | 'tooLong' | null;
  readonly warnings: readonly 'clipped'[];
}

interface RecordOptions {
  /** L + P is computed by the engine: this is L (measured, manual or estimated), 0..44100. */
  readonly latencySamples: number;
  readonly latencyMethod: LatencyMethod;
  readonly part: RecordingPart;
  /** Gate the part's stem (and its detail channels) to 0 while recording. */
  readonly muteOriginal: boolean;
  /** 0..2 bars; uses 012's count-in (and its "before every loop pass" setting). */
  readonly countInBars: number;
  /** 1..50, default 50. */
  readonly maxPasses?: number;
  /** Seconds of capture per session, default 1800. */
  readonly maxSeconds?: number;
}

interface CapturedTake {
  readonly captureKey: string;            // engine-local id
  readonly sessionId: string;             // UUID, shared by the passes of one Record press
  readonly passIndex: number | null;      // 1-based; null without loop
  readonly partial: boolean;
  readonly startSample: number;
  readonly lengthSamples: number;
  readonly channels: 1 | 2;
  readonly inputMode: InputChannelMode;
  readonly latencySamples: number;        // L + P as applied
  readonly latencyMethod: LatencyMethod;
  readonly peakDbfs: number;
  readonly clippedSamples: number;
  readonly dropouts: number;
  readonly recordedAt: string;            // ISO 8601 UTC
}

type TakeSource =
  | { readonly captureKey: string }                       // local capture, right after recording
  | { readonly url: string; readonly startSample: number; readonly lengthSamples: number; readonly channels: 1 | 2 };

interface LoadTakeRequest {
  readonly takeId: string;                // backend id (or the captureKey before it is saved)
  readonly source: TakeSource;
  readonly part: RecordingPart;
  /** Playback-only timing adjustment in samples (US7), -8820..8820, default 0. */
  readonly adjustmentSamples?: number;
}

interface EngineOptions {
  // ...2.4.0 options...
  /** E2E only: 'loopback' feeds the engine's output into its input (research R-13). */
  readonly testInput?: 'loopback';
  /** Test override of navigator.mediaDevices. */
  readonly mediaDevices?: Pick<MediaDevices, 'getUserMedia' | 'enumerateDevices' | 'addEventListener' | 'removeEventListener'>;
  /** Worklet module URL; default './js/audio-engine-capture-worklet.js'. */
  readonly captureWorkletUrl?: string;
}
```

## 2. AudioEngine methods (added)

| Method | Behaviour |
|---|---|
| `recordingSupport(): Promise<RecordingSupport>` | No side effects; never opens the microphone |
| `listInputDevices(): Promise<InputDevice[]>` | `enumerateDevices()` filtered to `audioinput`; labels empty before permission |
| `openInput(request): Promise<InputInfo>` | `getUserMedia` with the constraints of research R-02; replaces an open input; adds the capture worklet (once per context); attaches `'input'` meters. Rejects with `Error` whose message starts with a code: `permissionDenied:`, `notFound:`, `busy:` (NotReadableError), `modeUnavailable:` ('mono-2'/'stereo' on a 1-channel device), `unsupported:` (incl. Firefox rate mismatch), followed by a readable sentence |
| `closeInput(): void` | Stops every track of the stream, disconnects the nodes; allowed in `open` only (throws `Error("Stop the recording first")` otherwise) |
| `setMonitoring(settings): void` | Input -> GainNode(volume) -> destination, bypassing master and the take channel; volume ramps <= 10 ms; forced off during calibration |
| `calibrateLatency(options?): Promise<CalibrationResult>` | Needs `open`, transport not playing (else rejects `Error("Pause playback to calibrate")`); research R-04; never throws for a failed measurement (`ok: false` + reason) |
| `startRecording(options): Promise<void>` | Needs `open`, a loaded track, speed 1 and transpose 0 (else rejects `recordingSpeed:`), no speed trainer (`speedTrainer:`); starts capture, then the transport from the current position with 012's count-in; disables seek, loop, speed, transpose and track changes until `stopRecording` (they throw `Error("Stop the recording first")`); the take channel is gated to 0 while recording |
| `stopRecording(): Promise<CapturedTake[]>` | Stops the transport, waits `L + P` + one batch (state `finishing`), returns every take of the session (also reported by `OnTakesCaptured`) |
| `captureWav(captureKey): Blob` | 24-bit PCM WAV of the take (research R-07); Blazor reads it as `IJSStreamReference` |
| `releaseCapture(captureKey): void` | Frees the local audio (unknown key: no-op) |
| `loadTake(request): Promise<void>` | Decodes/slices (R-09), creates the `take` channel (fader x gate -> panner -> meter -> master) if needed, schedules it sample-locked with the shared transport anchor; replaces a loaded take; rejects with the stem-loader messages on fetch/decode errors |
| `unloadTake(): void` | Removes the take buffer; the `take` channel stays silent; compare resets to `both` |
| `setTakeAdjustment(samples): void` | Playback-only shift of the loaded take, re-scheduled at `ctx.currentTime + 0.01` |
| `setTakeCompare(mode): void` | R-10 gate factor, swapped at one context time with <= 10 ms ramps; with part `none`, `original` = take gated |

`setChannel('take', settings)` and `attachMeter('take' | 'input', elementId)` work like for stems;
before `loadTake` they store the settings (meter decays at -60 dB).

## 3. Callbacks to .NET (added)

| Method (JSInvokable) | Arguments | When |
|---|---|---|
| `OnRecordingStateChanged` | `string state` | every `RecordingState` change |
| `OnRecordingProgress` | `{ elapsedSeconds, passIndex | null, countInBeatsLeft | null }` | at most 4 Hz while counting in or recording |
| `OnTakesCaptured` | `CapturedTake[]` | when a session ends (also returned by `stopRecording`) |
| `OnInputLost` | `string reason` | the device was unplugged or the track ended; a running recording stops and keeps a partial take |
| `OnInputDevicesChanged` | - | `devicechange` event (UI re-lists) |

## 4. Graph additions

```text
input:  MediaStreamSource -> [ChannelSplitter/Merger per mode] -> AudioWorkletNode(capture) -> Gain(0) -> destination
                                                                 -> Analyser(input meter)
                                                                 -> Gain(monitor volume) -> destination   (monitoring)
take:   AudioBufferSource -> Gain(fader x gate x compareGate) -> StereoPanner -> Analyser(meter) -> [012 tempo stage, as stems] -> master
clicks: calibration clicks -> destination (directly, like the metronome)
```

## 5. Requirements on 012 / 014 (internal, not public API)

1. The transport exposes `segments(): readonly TransportSegment[]` (or an equivalent listener)
   with the context frame and song sample of every play start, loop pass start and the end of
   each count-in gap (data-model 3.2).
2. The tempo stage exposes `processingLatencySamples(): number` (0 when bypassed at 1.0 / 0 st).
3. The tempo stage accepts additional channels (the `take`) and processes them identically to
   the stems.
4. Count-in is startable with an explicit bar count (`countInBars`), and its "before every loop
   pass" option is honoured during recording.

If the merged 012/014 engine lacks one of these, Phase 0 (T004) adds the missing internal hook
in the 2.5.0 work package (audio-engine lane task T014) without changing the 2.3.0/2.4.0 public
API.

## 6. Packaging

Vite builds a second entry `src/recording/capture-worklet.ts` to
`wwwroot/js/audio-engine-capture-worklet.js` (+ map; build artefact, not committed). The
worklet processor name is `stemstudio-capture`. No new npm dependency.

## 7. Testing

Vitest with the existing `AudioContext` mock extended by `audioWorklet.addModule`,
`AudioWorkletNode` (a fake port the test drives with stamped blocks), `createMediaStreamSource`,
`createMediaStreamDestination`, `getUserMedia`/`enumerateDevices` fakes and
`permissions.query`. Pure units (`estimateLatency`, `mapCaptureToTakes`, `encodeWav24`,
`sliceTake`, `compareGate`) are tested without the mock. Coverage >= 90 % lines, branches,
functions and statements.

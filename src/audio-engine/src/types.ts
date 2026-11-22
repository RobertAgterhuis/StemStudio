/**
 * Binding TypeScript contract of the StemStudio audio engine (contracts/audio-engine.md).
 *
 * Value ranges match the Mix DTO of the backend API: volumes 0..1, pan -1..1, times in seconds.
 * Changing anything here is a contract change (ADR + version bump of {@link ENGINE_CONTRACT_VERSION}).
 *
 * @packageDocumentation
 */

/** Semantic version of this contract; exported at runtime by `index.ts`. */
export type EngineContractVersion = '2.6.0';

/** Stem names delivered by the backend (`htdemucs_6s`; `htdemucs` yields vocals, drums, bass, other). */
export type StemName = 'vocals' | 'drums' | 'bass' | 'guitar' | 'piano' | 'other';

/** Standard stems that can be split into detail stems (`bass` is never split). */
export type ParentStemName = 'vocals' | 'drums' | 'guitar' | 'piano' | 'other';

/** Detail stem names (closed set); `rest` is the last stem of every split parent. */
export type DetailStemName =
  | 'kick'
  | 'snare'
  | 'toms'
  | 'hihat'
  | 'cymbals'
  | 'lead'
  | 'backing'
  | 'strings'
  | 'brass'
  | 'woodwinds'
  | 'keys'
  | 'synth'
  | 'acoustic-guitar'
  | 'electric-guitar'
  | 'percussion'
  | 'rest';

/**
 * Channel address: a standard stem, `<parent>.<detail>` for a detail stem (e.g. `drums.snare`), or `take`, the one
 * take channel strip (contract 2.5.0).
 */
export type ChannelId = StemName | `${ParentStemName}.${DetailStemName}` | 'take';

/** Meter target name: a channel, the master bus or the open input (`input`, contract 2.5.0). */
export type MeterTarget = ChannelId | 'master' | 'input';

/** Transport state reported to .NET via {@link DotNetCallbacks.OnPlaybackStateChanged}. */
export type PlaybackState = 'stopped' | 'playing' | 'paused';

/**
 * Minimal surface of a Blazor `DotNetObjectReference` as seen from JavaScript.
 * The .NET side implements the methods named in {@link DotNetCallbacks} with `[JSInvokable]`.
 */
export interface DotNetObjectReference {
  /** Invokes a `[JSInvokable]` instance method on the .NET object. */
  invokeMethodAsync(methodName: DotNetCallbacks, ...args: unknown[]): Promise<unknown>;
}

/**
 * Names of the `[JSInvokable]` .NET methods the engine calls.
 *
 * - `OnPositionChanged(double seconds)`: at most 10 Hz while playing, once after seek/stop.
 * - `OnPlaybackStateChanged(string state)`: on every {@link PlaybackState} change, including
 *   reaching the end of the track (state `stopped`, position 0).
 * - `OnPracticeChanged(PracticeStatus status)` (2.3.0): after every applied practice change, stretch
 *   state change, trainer pass start, count-in beat and underrun burst; at most 10 Hz.
 * - `OnLoopPass(LoopPassInfo pass)` (2.4.0, feature 014): once per completed pass of an enabled loop, when the
 *   wrap is heard; at most one call per pass.
 * - `OnRecordingStateChanged(string state)` (2.5.0): on every {@link RecordingState} change.
 * - `OnRecordingProgress(RecordingProgress progress)` (2.5.0): at most 4 Hz while counting in or recording.
 * - `OnTakesCaptured(CapturedTake[] takes)` (2.5.0): when a recording session ends (also returned by
 *   {@link AudioEngine.stopRecording}).
 * - `OnInputLost(string reason)` (2.5.0): the input device was unplugged or its track ended; a running recording
 *   stops and keeps a partial take.
 * - `OnInputDevicesChanged()` (2.5.0): a `devicechange` event; the UI lists the inputs again.
 * - `OnSingingUnitScored(int index, int? score, int pass)` (2.6.0, feature 016): once per finished singing unit.
 * - `OnSingingStateChanged(string state, string? reason)` (2.6.0): on every {@link SingingState} change.
 */
export type DotNetCallbacks =
  | 'OnPositionChanged'
  | 'OnPlaybackStateChanged'
  | 'OnPracticeChanged'
  | 'OnLoopPass'
  | 'OnRecordingStateChanged'
  | 'OnRecordingProgress'
  | 'OnTakesCaptured'
  | 'OnInputLost'
  | 'OnInputDevicesChanged'
  | 'OnSingingUnitScored'
  | 'OnSingingStateChanged';

/** One stem to load: its name and an absolute or API-relative URL of the FLAC file. */
export interface StemSource {
  /** Stem name; unique within a track. */
  readonly name: StemName;
  /** URL served by the API (`GET /api/tracks/{id}/stems/{name}`, `audio/flac`, CORS enabled). */
  readonly url: string;
}

/** Beat grid of a track, identical to `BeatGridDto` of the backend API. */
export interface BeatGrid {
  /** Global tempo estimate (BPM); 0 when detection failed. */
  readonly bpm: number;
  /** Beat times in seconds, ascending. */
  readonly beats: readonly number[];
  /** Times of beat 1 of each bar in seconds; a subset of {@link BeatGrid.beats}. */
  readonly downbeats: readonly number[];
  /** Beats per bar detected/assumed (default 4). */
  readonly beatsPerBar: number;
  /** Detection confidence 0..1. */
  readonly confidence: number;
  /** False when the grid should not be trusted (metronome starts disabled). */
  readonly reliable: boolean;
}

/** Argument of {@link AudioEngine.loadTrack}. */
export interface LoadTrackRequest {
  /** All stems of the track; fetched in parallel and decoded with `decodeAudioData`. */
  readonly stems: readonly StemSource[];
  /** Beat grid used by the metronome; null when the track has none. */
  readonly beatGrid: BeatGrid | null;
  /**
   * Detail stem groups of split parents (contract 2.0.0); default none. Their files are fetched
   * lazily on the first {@link AudioEngine.setExpanded} of the parent.
   */
  readonly groups?: readonly DetailGroup[];
}

/** One detail stem file of a group. */
export interface DetailStemSource {
  /** Detail stem name. */
  readonly name: DetailStemName;
  /** Absolute or API-relative URL of the FLAC file (`/api/tracks/{id}/detail-stems/{parent}/{name}`). */
  readonly url: string;
}

/** The detail stems of one split parent, in display order with `rest` last. */
export interface DetailGroup {
  /** The parent stem whose audio the group replaces when expanded. */
  readonly parent: ParentStemName;
  /** Detail stems; together they sum to the parent. */
  readonly stems: readonly DetailStemSource[];
}

/** Per-channel mixer state; mirrors `ChannelSettingsDto` without the stem name. */
export interface ChannelSettings {
  /** Fader gain 0..1 (linear). */
  readonly volume: number;
  /** Stereo position -1 (left) .. 1 (right). */
  readonly pan: number;
  /** Channel muted (also wins over solo). */
  readonly mute: boolean;
  /** Channel soloed; when any channel is soloed only soloed, unmuted channels are audible. */
  readonly solo: boolean;
}

/** A-B loop region; mirrors `LoopRegionDto`. */
export interface LoopRegion {
  /** Loop start (A) in seconds, >= 0. */
  readonly start: number;
  /** Loop end (B) in seconds, > start and <= duration. */
  readonly end: number;
  /** Looping active. */
  readonly enabled: boolean;
}

/** Metronome state; mirrors `MetronomeSettingsDto`. */
export interface MetronomeSettings {
  /** Click on/off. */
  readonly enabled: boolean;
  /** Click gain 0..1, independent of the master fader. */
  readonly volume: number;
  /** Constant manual tempo 30..300 BPM, or null to follow the detected beats. */
  readonly bpmOverride: number | null;
  /** Shift of the whole grid in milliseconds, -2000..2000 (positive = later). */
  readonly offsetMs: number;
  /** Beats per bar 1..12; beat 1 of each bar gets the accent click. */
  readonly beatsPerBar: number;
}

/** One word of the karaoke banner; times in integer milliseconds from the start of the track (contract 2.1.0). */
export interface KaraokeWord {
  /** Text, set with `textContent` (never parsed as HTML); 1..100 characters. */
  readonly text: string;
  /** The ball lands on the word at this time. */
  readonly startMs: number;
  /** End of the word, > `startMs`. */
  readonly endMs: number;
}

/** One line of the karaoke banner; its words lie inside `startMs..endMs`. */
export interface KaraokeLine {
  /** Start of the line (= start of its first word or earlier). */
  readonly startMs: number;
  /** End of the line, > `startMs`. */
  readonly endMs: number;
  /** Words in order (1..40); starts never decrease. */
  readonly words: readonly KaraokeWord[];
}

/** Timed lyrics to draw: the render subset of the backend `Lyrics` DTO (word- or line-synced only). */
export interface KaraokeLyrics {
  /** Lines in order of `startMs` (<= 400); empty = banner state `hidden`. */
  readonly lines: readonly KaraokeLine[];
}

/** Options of {@link AudioEngine.attachKaraoke}. */
export interface KaraokeOptions {
  /** User offset -5000..5000 ms; positive = the lyrics come later. */
  readonly offsetMs: number;
  /** No ball, the current word is highlighted instead; default `matchMedia('(prefers-reduced-motion: reduce)')`. */
  readonly reducedMotion?: boolean;
  /** Gaps between lines longer than this show the countdown; default 5000 ms. */
  readonly countdownGapMs?: number;
}

/** State of the banner, written to the host's `data-state` attribute. */
export type KaraokeState = 'hidden' | 'countdown' | 'singing' | 'ended';

/** Practice tempo of the whole mix (contract 2.3.0); mirrors `PracticeSettingsDto` / backend `PracticeSettings` (integers). */
export interface PracticeSettings {
  /** Playback speed 50..120 % of the original tempo, pitch kept; default 100. */
  readonly speedPercent: number;
  /** Pitch shift -12..12 semitones, speed kept; default 0. */
  readonly transposeSemitones: number;
  /** Speed trainer on the enabled A-B loop. */
  readonly trainer: SpeedTrainerSettings;
  /** Count-in clicks before playback (and optionally before every loop pass). */
  readonly countIn: CountInSettings;
}

/** Speed trainer settings (contract 2.3.0); mirrors `SpeedTrainerSettingsDto`. */
export interface SpeedTrainerSettings {
  /** Trainer on; effective only with an enabled loop of at least 0.5 s; default false. */
  readonly enabled: boolean;
  /** Speed of pass 1, 50..119 %; default 70. */
  readonly startPercent: number;
  /** Increase per step, 1..20 %; default 5. */
  readonly stepPercent: number;
  /** Final speed 51..120 %, greater than `startPercent`; default 100. */
  readonly targetPercent: number;
  /** Completed passes per speed, 1..8; default 1. */
  readonly passesPerStep: number;
}

/** Count-in settings (contract 2.3.0); mirrors `CountInSettingsDto`. */
export interface CountInSettings {
  /** Counted bars: 0 (off), 1 or 2. */
  readonly bars: number;
  /** Also count in before every pass of an enabled loop; default false. */
  readonly everyPass: boolean;
}

/** Where the time-stretch stage is: not inserted, being loaded, in the graph, or not usable. */
export type StretchState = 'bypass' | 'loading' | 'active' | 'unavailable';

/** Speed trainer state: disabled or no usable loop, enabled and not playing, playing passes, target reached. */
export type TrainerState = 'off' | 'waiting' | 'running' | 'target';

/** Snapshot sent with `OnPracticeChanged` and returned by {@link AudioEngine.getPracticeStatus} (contract 2.3.0). */
export interface PracticeStatus {
  /** Speed heard now (the trainer's speed while it runs). */
  readonly speedPercent: number;
  /** Transpose heard now. */
  readonly transposeSemitones: number;
  /** State of the time-stretch stage. */
  readonly stretch: StretchState;
  /** Reason text; set only when `stretch` is `unavailable`. */
  readonly stretchReason: string | null;
  /** Heard latency L(p) of the stage in ms while engaged, else 0 (audio-engine.md "Stretch stage"). */
  readonly latencyMs: number;
  /** Stretch FIFO underruns (frames output as silence) since `loadTrack`; Expert diagnostics. */
  readonly underruns: number;
  /** Speed trainer progress. */
  readonly trainer: {
    /** Trainer state. */
    readonly state: TrainerState;
    /** Current pass (1-based), 0 when not running. */
    readonly pass: number;
    /** Speed of the current pass in percent. */
    readonly speedPercent: number;
  };
  /** Count-in progress. */
  readonly countIn: {
    /** False without a usable tempo (no reliable grid and no manual BPM). */
    readonly available: boolean;
    /** A count-in is running now. */
    readonly active: boolean;
    /** Beats left N..1 while active, 0 otherwise. */
    readonly beatsRemaining: number;
    /** Bars x beats per bar of the metronome settings. */
    readonly beatsTotal: number;
  };
}

/** One page of a score view (contract 2.4.0); mirrors `ScoreViewPage` of backend-api 1.13.0. */
export interface ScoreViewPage {
  /** 1-based page number; the host has a page container with `data-score-page="<page>"`. */
  readonly page: number;
  /** API-relative URL of the SVG page (used as `<img src>` by Blazor, never fetched by the engine). */
  readonly url: string;
  /** Page width in CSS pixels. */
  readonly widthPx: number;
  /** Page height in CSS pixels. */
  readonly heightPx: number;
}

/** One bar of a score view (contract 2.4.0): box in page fractions 0..1, times in track seconds; mirrors `ScoreBar`. */
export interface ScoreViewBar {
  /** 0-based index in document order. */
  readonly index: number;
  /** Printed bar number (0 for a pickup). */
  readonly number: number;
  /** Page of the bar (one of `ScoreViewMap.pages`). */
  readonly page: number;
  /** Left edge as a fraction of the page width. */
  readonly x: number;
  /** Top edge as a fraction of the page height. */
  readonly y: number;
  /** Width as a fraction of the page width (> 0). */
  readonly width: number;
  /** Height as a fraction of the page height (> 0). */
  readonly height: number;
  /** Start in track seconds. */
  readonly startSeconds: number;
  /** End in track seconds; equals the next bar's start (within 1 ms). */
  readonly endSeconds: number;
}

/** Argument of {@link AudioEngine.attachScoreFollower} (contract 2.4.0): the render subset of the backend `ScoreView`. */
export interface ScoreViewMap {
  /** 1..200 pages with unique page numbers. */
  readonly pages: readonly ScoreViewPage[];
  /** 1..2000 bars, ascending and contiguous (`bars[i].endSeconds === bars[i + 1].startSeconds` within 1 ms). */
  readonly bars: readonly ScoreViewBar[];
}

/** Options of the score follower (contract 2.4.0). */
export interface ScoreFollowOptions {
  /** Auto-scroll on or off; the highlight is always drawn. */
  readonly follow: boolean;
  /** Jump instead of smooth scrolling; default `matchMedia('(prefers-reduced-motion: reduce)')`. */
  readonly reducedMotion?: boolean;
  /** Pause of auto-scroll after a manual scroll (wheel, touch, scroll keys) in ms, 0..60000; default 4000. */
  readonly scrollPauseMs?: number;
}

/** Argument of the `.NET` callback `OnLoopPass` (contract 2.4.0). */
export interface LoopPassInfo {
  /** Loop start in seconds of the pass that just completed. */
  readonly start: number;
  /** Loop end in seconds of the pass that just completed. */
  readonly end: number;
  /** 1-based number of completed passes since looping was (re-)enabled, the region changed or a seek. */
  readonly pass: number;
  /** Playback speed of that pass in percent (the trainer's speed while it runs; 100 at normal speed). */
  readonly speedPercent: number;
}

/** Loading options of the time-stretch stage (contract 2.3.0). */
export interface TimeStretchOptions {
  /** URL of the AudioWorklet processor module; default `./js/time-stretch-processor.js`. */
  readonly processorUrl?: string;
  /** URL of the SoundTouch module (WASM ABI v1); default `./js/soundtouch/soundtouch.wasm`. */
  readonly wasmUrl?: string;
}

/** The time-stretch stage as the engine sees it; implemented by the worklet wrapper and by test fakes (contract 2.3.0). */
export interface TimeStretchStage {
  /** Node the summed mix connects to. */
  readonly input: AudioNode;
  /** Node connected to the master gain. */
  readonly output: AudioNode;
  /** Fixed FIFO delay D in frames (`STRETCH_DELAY_FRAMES`), constant for the stage's lifetime. */
  readonly latencyFrames: number;
  /**
   * Pitch factor applied to the audio that enters the stage from context frame `atFrame` on (the processor applies it
   * when the midpoint of SoundTouch's buffered input reaches that frame; audio-engine.md "Stretch stage").
   */
  setPitch(pitchFactor: number, atFrame: number): void;
  /** Drops buffered audio and refills the fixed delay with silence (seek, restart). */
  reset(): void;
  /** Underrun counter (frames output as silence because the FIFO was empty). */
  underruns(): number;
  /** Disconnects the node and frees the SoundTouch instance. */
  dispose(): void;
}

/** Part a take is recorded for (contract 2.5.0); `none` = no original part (compare `original` gates the take). */
export type RecordingPart = 'none' | 'vocals' | 'guitar' | 'bass' | 'piano' | 'drums' | 'other';

/** Input channel mode: channel 1 or channel 2 of the device as mono, or both as stereo (contract 2.5.0). */
export type InputChannelMode = 'mono-1' | 'mono-2' | 'stereo';

/** How the latency `L` of a take was obtained (contract 2.5.0). */
export type LatencyMethod = 'loopback' | 'manual' | 'estimated';

/** Recording state machine (data-model 3.1, contract 2.5.0). */
export type RecordingState = 'closed' | 'open' | 'countIn' | 'recording' | 'finishing';

/** Compare mode of the take strip (research R-10, contract 2.5.0). */
export type TakeCompareMode = 'both' | 'take' | 'original';

/** Microphone permission as reported by `navigator.permissions` (`unknown` when not queryable). */
export type PermissionStateValue = 'granted' | 'prompt' | 'denied' | 'unknown';

/**
 * Why recording is not available in this page (contract 2.5.0, ADR-024 D7/D8):
 * `insecureContext` (not https and not localhost, e.g. a LAN address over http), `noMediaDevices`
 * (no `navigator.mediaDevices.getUserMedia`), `noAudioWorklet` (no `AudioWorklet`), `rateMismatch`
 * (the browser cannot connect a device whose rate differs from 44.1 kHz, Firefox).
 */
export type RecordingUnsupportedReason = 'insecureContext' | 'noMediaDevices' | 'noAudioWorklet' | 'rateMismatch';

/** Result of {@link AudioEngine.recordingSupport} (contract 2.5.0). */
export interface RecordingSupport {
  /** True when recording can be offered in this page. */
  readonly supported: boolean;
  /** Machine-readable reason when not supported; null when supported. */
  readonly reasonCode: RecordingUnsupportedReason | null;
  /** Readable reason when not supported (one sentence with the next step); null when supported. */
  readonly reason: string | null;
  /** Microphone permission (`navigator.permissions.query({ name: 'microphone' })`). */
  readonly permission: PermissionStateValue;
}

/** One audio input device (`enumerateDevices`, kind `audioinput`). */
export interface InputDevice {
  /** Browser device id (opaque, per origin). */
  readonly deviceId: string;
  /** Device label; empty until the permission is granted (browser rule). */
  readonly label: string;
  /** Group id (devices of one physical interface share it). */
  readonly groupId: string;
}

/** Argument of {@link AudioEngine.openInput}. */
export interface OpenInputRequest {
  /** Device to open; null = the browser default input. */
  readonly deviceId: string | null;
  /** Channel mode; `mono-2` and `stereo` need a 2-channel device. */
  readonly mode: InputChannelMode;
}

/** The open input (result of {@link AudioEngine.openInput}). */
export interface InputInfo {
  /** Device id of the open track. */
  readonly deviceId: string;
  /** Device label (non-empty once open). */
  readonly label: string;
  /** Channels the device delivers (1 or 2). */
  readonly channelCount: 1 | 2;
  /** Applied channel mode. */
  readonly mode: InputChannelMode;
  /** Device rate reported by the track; the context stays at 44,100 Hz. Null when not reported. */
  readonly deviceSampleRate: number | null;
  /** `round((baseLatency + outputLatency + track latency) * 44100)`: the estimate used until calibrated. */
  readonly estimatedLatencySamples: number;
  /** `round((baseLatency + outputLatency) * 44100)`: compared with the snapshot of a stored calibration. */
  readonly outputLatencySamples: number;
}

/** Direct monitoring of the input (contract 2.5.0). */
export interface MonitoringSettings {
  /** Input audible on the output (bypasses master and the take channel). */
  readonly enabled: boolean;
  /** Monitor gain 0..1 (linear). */
  readonly volume: number;
}

/** Options of {@link AudioEngine.calibrateLatency}. */
export interface CalibrationOptions {
  /** Number of clicks, 6..12; default 8. */
  readonly clicks?: number;
  /** PRNG seed of the click gaps (tests); default random. */
  readonly seed?: number;
}

/** Why a calibration failed (research R-04). */
export type CalibrationFailure = 'noSignal' | 'tooFewClicks' | 'inconsistent' | 'tooLong';

/** Result of {@link AudioEngine.calibrateLatency} (data-model 3.4). */
export interface CalibrationResult {
  /** True when a latency was measured. */
  readonly ok: boolean;
  /** Measured round trip `L` in samples (44.1 kHz); null when not ok. */
  readonly latencySamples: number | null;
  /** `L` in milliseconds rounded to 0.1 ms; null when not ok. */
  readonly latencyMilliseconds: number | null;
  /** Clicks found (0..clicks). */
  readonly detectedClicks: number;
  /** Spread (max - min lag) of the found clicks in samples; null when fewer than 2 were found. */
  readonly spreadSamples: number | null;
  /** Output latency snapshot `round((baseLatency + outputLatency) * 44100)` at measurement time. */
  readonly outputLatencySamples: number;
  /** Highest input peak during the measurement (dBFS, -Infinity reported as -120). */
  readonly inputPeakDbfs: number;
  /** Failure reason; null when ok. */
  readonly reason: CalibrationFailure | null;
  /** Warnings that do not fail the measurement. */
  readonly warnings: readonly 'clipped'[];
}

/** Argument of {@link AudioEngine.startRecording}. */
export interface RecordOptions {
  /** Latency `L` in samples (measured, manual or estimated), 0..44100; the engine adds its own processing latency. */
  readonly latencySamples: number;
  /** Where `L` comes from. */
  readonly latencyMethod: LatencyMethod;
  /** Part the take is recorded for. */
  readonly part: RecordingPart;
  /** Gate the part's stem (and its detail channels) to 0 while recording. */
  readonly muteOriginal: boolean;
  /** Count-in bars 0..2 (012 count-in; its "before every loop pass" setting is honoured). */
  readonly countInBars: number;
  /** Loop passes per session 1..50; default 50. */
  readonly maxPasses?: number;
  /** Seconds of capture per session 1..1800; default 1800. */
  readonly maxSeconds?: number;
}

/** Progress of a running recording (`OnRecordingProgress`, at most 4 Hz). */
export interface RecordingProgress {
  /** Seconds captured in this session. */
  readonly elapsedSeconds: number;
  /** 1-based loop pass being recorded; null without loop. */
  readonly passIndex: number | null;
  /** Count-in beats left; null when not counting in. */
  readonly countInBeatsLeft: number | null;
}

/** One captured take (data-model 3.3); the audio stays in the engine until {@link AudioEngine.releaseCapture}. */
export interface CapturedTake {
  /** Engine-local id of the capture. */
  readonly captureKey: string;
  /** UUID shared by the passes of one Record press. */
  readonly sessionId: string;
  /** 1-based loop pass; null without loop. */
  readonly passIndex: number | null;
  /** True for a final pass shorter than the loop (kept when >= 1 s). */
  readonly partial: boolean;
  /** Song sample (44.1 kHz) of the take's first sample. */
  readonly startSample: number;
  /** Length in samples (frames). */
  readonly lengthSamples: number;
  /** Channels of the take (1 for `mono-*`, 2 for `stereo`). */
  readonly channels: 1 | 2;
  /** Channel mode the take was recorded with. */
  readonly inputMode: InputChannelMode;
  /** Total compensation applied in samples (`L` plus the engine's processing latency). */
  readonly latencySamples: number;
  /** Where `L` came from. */
  readonly latencyMethod: LatencyMethod;
  /** Peak of the take (dBFS). */
  readonly peakDbfs: number;
  /** Samples at or above full scale. */
  readonly clippedSamples: number;
  /** Frames filled with zeros because the worklet's stamps had a gap. */
  readonly dropouts: number;
  /** Start of the recording (ISO 8601 UTC). */
  readonly recordedAt: string;
}

/** Audio of a take to load: the local capture right after recording, or the saved FLAC. */
export type TakeSource =
  | { readonly captureKey: string }
  | { readonly url: string; readonly startSample: number; readonly lengthSamples: number; readonly channels: 1 | 2 };

/** Argument of {@link AudioEngine.loadTake}. */
export interface LoadTakeRequest {
  /** Backend take id, or the capture key before the take is saved. */
  readonly takeId: string;
  /** Where the audio comes from. */
  readonly source: TakeSource;
  /** Part of the take (compare gate). */
  readonly part: RecordingPart;
  /** Playback-only timing adjustment in samples, -8820..8820; default 0. */
  readonly adjustmentSamples?: number;
}

// ---------------------------------------------------------------- instrument views (contract 2.6.0, feature 016)

/** Chord quality of the `full` vocabulary (data-model 2.1); `simple` charts use `maj` / `min`. */
export type ChordQuality =
  | 'maj'
  | 'min'
  | 'dim'
  | 'aug'
  | 'min6'
  | 'maj6'
  | 'min7'
  | 'minmaj7'
  | 'maj7'
  | '7'
  | 'dim7'
  | 'hdim7'
  | 'sus2'
  | 'sus4';

/** Mode of a key. */
export type KeyMode = 'major' | 'minor';

/** One chord of a chart; `root`/`quality` null = no chord (`N.C.`). Pitch classes C = 0 ... B = 11. */
export interface ChordSymbolEvent {
  /** Start in track milliseconds. */
  readonly startMs: number;
  /** End in track milliseconds (> start; segments are contiguous). */
  readonly endMs: number;
  /** Root pitch class 0..11, or null for no chord. */
  readonly root: number | null;
  /** Quality, or null for no chord. */
  readonly quality: ChordQuality | null;
  /** Bass pitch class when it is not the root (slash chord), else null. */
  readonly bass: number | null;
}

/** A key region (scope addition S11); spelling uses the region a chord starts in. */
export interface KeyRegionData {
  /** Start in track milliseconds. */
  readonly startMs: number;
  /** End in track milliseconds. */
  readonly endMs: number;
  /** Tonic pitch class 0..11. */
  readonly tonic: number;
  /** Mode. */
  readonly mode: KeyMode;
}

/** Argument of {@link AudioEngine.attachChordLane} and {@link AudioEngine.setKaraokeChords}. */
export interface ChordChartData {
  /** Contiguous, ordered segments (at most 4000). */
  readonly segments: readonly ChordSymbolEvent[];
  /** Global key tonic (fallback of the regions), or null. */
  readonly keyTonic: number | null;
  /** Global key mode, or null. */
  readonly keyMode: KeyMode | null;
  /** Key regions (at most 50); absent = the global key everywhere. */
  readonly keyRegions?: readonly KeyRegionData[];
}

/** Argument of {@link AudioEngine.setChordDisplay} (capo mode, S12); the transpose comes from the 012 practice state. */
export interface ChordDisplay {
  /** Capo 0..12; chords are spelled as the shapes played with the capo. */
  readonly capo: number;
  /** Also show the sounding symbol in a `.chord-sounding` child (Advanced). */
  readonly showSounding?: boolean;
}

/** Options of {@link AudioEngine.attachChordLane}. */
export interface ChordLaneOptions {
  /** Bars shown before the current one; default 1. */
  readonly barsBehind?: number;
  /** Bars shown after the current one; default 6. */
  readonly barsAhead?: number;
  /** Page instead of scrolling; default from `prefers-reduced-motion`. */
  readonly reducedMotion?: boolean;
}

/** Drum piece of the hit lanes. */
export type DrumPiece = 'kick' | 'snare' | 'toms' | 'hihat' | 'cymbals';

/** One drum hit (data-model 5.1). */
export interface DrumHitEvent {
  /** Track milliseconds. */
  readonly timeMs: number;
  /** Piece. */
  readonly piece: DrumPiece;
  /** Strength 0..1 (mark height 40-100 % of the lane). */
  readonly strength: number;
}

/** Options of {@link AudioEngine.attachHitLanes} and {@link AudioEngine.setHitLaneOptions}. */
export interface HitLaneOptions {
  /** Look-ahead 2000..8000 ms; default 4000. */
  readonly lookAheadMs?: number;
  /** Lanes to draw; default all five. */
  readonly pieces?: readonly DrumPiece[];
  /** Page instead of scrolling; default from `prefers-reduced-motion`. */
  readonly reducedMotion?: boolean;
}

/** Reference melody: 10 ms frames in centi-semitones (MIDI x 100), 0 = unvoiced; at most 90,000 frames. */
export interface PitchContour {
  /** Always 10. */
  readonly hopMs: 10;
  /** Frames (values 0..12700). */
  readonly frames: ArrayLike<number>;
}

/** One scored unit: a lyric line, a 014 section, a 4-bar block or a 10 s block (data-model 3.4). */
export interface SingingUnit {
  /** 0-based index (ordered by start). */
  readonly index: number;
  /** Start in track milliseconds. */
  readonly startMs: number;
  /** End in track milliseconds. */
  readonly endMs: number;
}

/** Argument of {@link AudioEngine.startSingingFeedback}. */
export interface SingingFeedbackRequest {
  /** Host of the pitch view canvas. */
  readonly pitchViewElementId: string;
  /** Reference melody of the lead vocal. */
  readonly reference: PitchContour;
  /** Units in time order. */
  readonly units: readonly SingingUnit[];
  /** Tolerance 10..200 cents (level defaults 100 / 50 / 25). */
  readonly toleranceCents: number;
  /**
   * Round trip in ms from the 015 calibration Blazor holds (`/api/recording/calibrations`); null or absent = the
   * estimate of the open 015 input (`InputInfo.estimatedLatencySamples`).
   */
  readonly roundTripMs?: number | null;
  /** How `roundTripMs` was obtained (`loopback` or `manual`); ignored without `roundTripMs`. */
  readonly latencyMethod?: LatencyMethod;
  /** Page instead of scrolling; default from `prefers-reduced-motion`. */
  readonly reducedMotion?: boolean;
}

/** Score of one finished unit (`OnSingingUnitScored`). */
export interface SingingUnitScore {
  /** Unit index. */
  readonly index: number;
  /** 0..100, or null with fewer than 20 voiced reference frames. */
  readonly score: number | null;
  /** 1-based loop pass. */
  readonly pass: number;
}

/** Summary on stop or track end (never sent to the server, FR-047). */
export interface SingingSummary {
  /** Units with a score. */
  readonly unitsScored: number;
  /** Rounded mean of the scored units, or null. */
  readonly average: number | null;
  /** Best unit, or null. */
  readonly best: { readonly index: number; readonly score: number } | null;
  /** Tolerance in cents at the end. */
  readonly toleranceCents: number;
  /** Where the round trip came from (015 `LatencyMethod`). */
  readonly latencySource: LatencyMethod;
}

/** State of singing feedback (`OnSingingStateChanged`). */
export type SingingState = 'idle' | 'starting' | 'running' | 'stopped' | 'failed';

/** Result of {@link AudioEngine.getRoundTripLatency}. */
export interface RoundTripLatency {
  /** Round trip in milliseconds. */
  readonly ms: number;
  /** `estimated` until a 015 calibration is given in the request. */
  readonly source: LatencyMethod;
}

/** Argument of {@link AudioEngine.scoreTake} (a 015 take, already latency-compensated). */
export interface TakeScoreRequest {
  /** Audio URL of the take (`GET /api/takes/{takeId}/audio`). */
  readonly takeUrl: string;
  /** Song sample (44.1 kHz) of the take's first sample (`RecordingTake.startSample`). */
  readonly startSample: number;
  /** Reference melody. */
  readonly reference: PitchContour;
  /** Units in time order. */
  readonly units: readonly SingingUnit[];
  /** Tolerance 10..200 cents. */
  readonly toleranceCents: number;
  /**
   * 012 transpose the take was recorded at (integer -12..12, default 0): the reference target moves by 100 cents per
   * semitone, as in the live scorer. Added at review 2026-11-22; optional, backwards compatible.
   */
  readonly transposeSemitones?: number;
}

/** Result of {@link AudioEngine.scoreTake}. */
export interface TakeScoreResult extends SingingSummary {
  /** Score of every unit the take covers. */
  readonly units: readonly SingingUnitScore[];
}

/** Optional construction parameters (mainly for tests). */
export interface EngineOptions {
  /** Factory for the single AudioContext; defaults to `new AudioContext({ sampleRate: 44100 })`. */
  readonly audioContextFactory?: () => AudioContext;
  /** `fetch` implementation used to download stems; defaults to `globalThis.fetch`. */
  readonly fetch?: typeof fetch;
  /** Start delay for sample-locked starts in seconds; default 0.1. */
  readonly startDelaySeconds?: number;
  /** Metronome look-ahead window in seconds; default 0.1. */
  readonly scheduleAheadSeconds?: number;
  /** Scheduler wake-up interval in milliseconds (wakes the scheduler only); default 25. */
  readonly schedulerIntervalMs?: number;
  /** Loading options of the time-stretch stage (contract 2.3.0). */
  readonly timeStretch?: TimeStretchOptions;
  /** Test seam (contract 2.3.0): replaces the AudioWorklet + WASM stage, e.g. with a Vitest fake. */
  readonly timeStretchFactory?: (context: AudioContext) => Promise<TimeStretchStage>;
  /**
   * E2E only (contract 2.5.0): `loopback` feeds the engine's own output into its input through a
   * `MediaStreamAudioDestinationNode` instead of opening a microphone (research R-13).
   */
  readonly testInput?: 'loopback';
  /** Test seam (contract 2.5.0): replaces `navigator.mediaDevices`. */
  readonly mediaDevices?: Pick<MediaDevices, 'getUserMedia' | 'enumerateDevices' | 'addEventListener' | 'removeEventListener'>;
  /** Capture worklet module URL (contract 2.5.0); default `./js/audio-engine-capture-worklet.js`. */
  readonly captureWorkletUrl?: string;
  /** Pitch tracker worklet module URL (contract 2.6.0); default `./js/audio-engine-pitch-worklet.js`. */
  readonly pitchWorkletUrl?: string;
}

/**
 * Engine instance returned by {@link CreateEngine}. Blazor holds it as an `IJSObjectReference`
 * and calls its methods by name (`InvokeAsync<double>("loadTrack", request)` etc.).
 * All methods reject with an `Error` (surfaced as `JSException` in .NET) on invalid input.
 */
export interface AudioEngine {
  /**
   * Downloads and decodes all stems, replacing any loaded track (playback stops).
   * @returns Track duration in seconds (longest stem).
   */
  loadTrack(request: LoadTrackRequest): Promise<number>;
  /** Resumes the AudioContext (user gesture) and starts all stems at the same `when` and offset. */
  play(): Promise<void>;
  /** Stops sources and remembers the position. */
  pause(): void;
  /** Stops sources and resets the position to 0 (also when looping). */
  stop(): void;
  /** Jumps to `seconds` (clamped to 0..duration); keeps playing if playing. */
  seek(seconds: number): void;
  /** Current position in seconds, derived from `AudioContext.currentTime`. */
  getPosition(): number;
  /** Current transport state. */
  getState(): PlaybackState;
  /** Applies channel settings (standard or detail channel); gain changes are ramped (<= 10 ms). */
  setChannel(name: ChannelId, settings: ChannelSettings): void;
  /**
   * Expands (detail stems replace the parent's audio) or collapses a parent with a group. The first
   * expansion fetches and decodes the group; the swap is a gate change at one context time.
   * Rejects for a parent without a group or when the group cannot be loaded (parent stays collapsed).
   */
  setExpanded(parent: ParentStemName, expanded: boolean): Promise<void>;
  /** Sets the master gain 0..1. */
  setMaster(volume: number): void;
  /** Sets or clears looping; rejects B <= A or a region outside the track. */
  setLoop(loop: LoopRegion): void;
  /** Applies metronome settings; takes effect at the next scheduler tick. */
  setMetronome(settings: MetronomeSettings): void;
  /**
   * Binds a peak meter (post-fader, dBFS -60..0) to a DOM element by id. On every animation frame
   * the engine sets CSS custom property `--meter-level` (0..1), `data-peak-db` (rounded dB) and
   * `data-clip` (`"true"` for 1.5 s after a sample >= 0 dBFS) on that element.
   */
  attachMeter(target: MeterTarget, elementId: string): void;
  /** Unbinds a meter. */
  detachMeter(target: MeterTarget): void;
  /**
   * Builds the karaoke banner inside the (empty) element `elementId` and draws it on the shared
   * animation frame against the audio clock (contract 2.1.0). Replaces an attached karaoke.
   * Throws `Error("Karaoke host not found: <id>")` or `Error("Invalid karaoke lyrics: <reason>")`.
   */
  attachKaraoke(elementId: string, lyrics: KaraokeLyrics, options: KaraokeOptions): void;
  /** Changes the karaoke offset (-5000..5000 ms); takes effect on the next frame. */
  setKaraokeOffset(offsetMs: number): void;
  /** Removes the karaoke DOM and stops drawing it; no-op when none is attached. */
  detachKaraoke(): void;
  /**
   * Validates and applies practice settings (contract 2.3.0); resolves when they are audible (the stretch stage loaded
   * when needed). Rejects with `Error("Invalid practice settings: <field> <reason>")` without changing anything, or with
   * `Error("Time stretch unavailable: <reason>")` (speed 100 %, 0 semitones and trainer off kept; count-in applied).
   */
  setPractice(settings: PracticeSettings): Promise<void>;
  /** Current practice snapshot (contract 2.3.0). */
  getPracticeStatus(): PracticeStatus;
  /**
   * Binds the score follower to the host element `elementId` (contract 2.4.0). The host holds one child per page with
   * `data-score-page="<n>"` (Blazor renders the `<img>` inside); the engine adds and owns one `.score-highlight` per page
   * container and draws it on the shared animation frame against the heard position. Replaces an attached follower.
   * Throws `Error("Score host not found: <id>")` or `Error("Invalid score view: <reason>")` (an attached follower stays).
   */
  attachScoreFollower(elementId: string, view: ScoreViewMap, options: ScoreFollowOptions): void;
  /**
   * Changes `follow`, `reducedMotion` and `scrollPauseMs`; takes effect on the next frame (contract 2.4.0).
   * Throws `Error("Invalid score follow options: <reason>")`.
   */
  setScoreFollowOptions(options: ScoreFollowOptions): void;
  /** Removes the highlight elements and host attributes; no-op when none is attached (contract 2.4.0). */
  detachScoreFollower(): void;
  /** Reports whether recording can be offered (contract 2.5.0); no side effects, never opens the microphone. */
  recordingSupport(): Promise<RecordingSupport>;
  /** Lists the audio inputs (`enumerateDevices`, kind `audioinput`); labels are empty before the permission. */
  listInputDevices(): Promise<InputDevice[]>;
  /**
   * Opens (or replaces) the input with all voice processing off (research R-02), adds the capture worklet once per
   * context and feeds the `input` meter. Rejects with an `Error` whose message starts with a code and a colon:
   * `permissionDenied:`, `notFound:`, `busy:`, `modeUnavailable:` or `unsupported:`, followed by a readable sentence.
   */
  openInput(request: OpenInputRequest): Promise<InputInfo>;
  /** Stops every track of the input stream and disconnects it; throws `Error("Stop the recording first")` unless `open`. */
  closeInput(): void;
  /** Direct monitoring (input -> gain -> destination); ramps <= 10 ms; forced off during a calibration. */
  setMonitoring(settings: MonitoringSettings): void;
  /**
   * Loopback click test (research R-04); needs an open input and a paused transport (else rejects with
   * `Error("Pause playback to calibrate")`). A failed measurement resolves with `ok: false` and a reason.
   */
  calibrateLatency(options?: CalibrationOptions): Promise<CalibrationResult>;
  /**
   * Starts capture, then the transport from the current position with the 012 count-in. Rejects with
   * `recordingSpeed:` (speed not 100 % or transpose not 0), `speedTrainer:` (trainer on), or when no input is open or no
   * track is loaded. Until {@link AudioEngine.stopRecording}, seek, loop, speed, transpose and track changes throw
   * `Error("Stop the recording first")`.
   */
  startRecording(options: RecordOptions): Promise<void>;
  /** Stops the transport, finishes the compensated tail (state `finishing`) and returns every take of the session. */
  stopRecording(): Promise<CapturedTake[]>;
  /** 24-bit PCM WAV of a captured take (research R-07); throws for an unknown key. */
  captureWav(captureKey: string): Blob;
  /** Frees the local audio of a capture; an unknown key is a no-op. */
  releaseCapture(captureKey: string): void;
  /**
   * Loads a take into the `take` channel (fader x gate -> panner -> meter -> master), sample-locked with the transport
   * anchor; replaces a loaded take. Rejects with the stem-loader messages on fetch or decode errors.
   */
  loadTake(request: LoadTakeRequest): Promise<void>;
  /** Removes the take buffer; the `take` channel stays silent and compare returns to `both`. */
  unloadTake(): void;
  /** Playback-only shift of the loaded take (-8820..8820 samples), rescheduled at `currentTime + 0.01`. */
  setTakeAdjustment(samples: number): void;
  /** Compare gate (research R-10), swapped at one context time with ramps <= 10 ms. */
  setTakeCompare(mode: TakeCompareMode): void;
  /**
   * Chord lane (2.6.0, feature 016): engine-owned `.chord-lane-item` children of the host on a bar timeline; host
   * attributes `data-chord-index`, `data-current-symbol`, `data-next-symbol`, `data-position-ms`, `data-capo`,
   * `data-current-key`. Replaces an attached lane; validates before replacing (`Invalid chord chart: ...`).
   */
  attachChordLane(elementId: string, chart: ChordChartData, options?: ChordLaneOptions): void;
  /** Removes the lane's children and host attributes; no-op when none. */
  detachChordLane(): void;
  /** Chord symbols above the karaoke words (`.karaoke-chord`); null removes them; kept across {@link AudioEngine.attachKaraoke}. */
  setKaraokeChords(chart: ChordChartData | null): void;
  /** Capo and sounding display for every chord view; re-spelled on the next frame (throws `Capo out of range: <n>`). */
  setChordDisplay(display: ChordDisplay): void;
  /** Drum hit lanes on an engine-owned canvas in the host (data-model 5.2); validates first (`Invalid drum hits: ...`). */
  attachHitLanes(elementId: string, hits: readonly DrumHitEvent[], options?: HitLaneOptions): void;
  /** Changes look-ahead or pieces without re-attaching. */
  setHitLaneOptions(options: HitLaneOptions): void;
  /** Removes the canvas and host attributes; no-op when none. */
  detachHitLanes(): void;
  /** The round trip singing feedback would use (the given 015 calibration or the open input's estimate). */
  getRoundTripLatency(): RoundTripLatency;
  /**
   * Loads the `pitch-tracker` worklet once, connects it to the open 015 input (rejects `No input device is open`,
   * `Singing feedback is already running`), draws the pitch view and scores units while playing.
   */
  startSingingFeedback(request: SingingFeedbackRequest): Promise<void>;
  /** Tolerance 10..200 cents for the current and later units. */
  setSingingTolerance(cents: number): void;
  /** Disconnects the worklet and returns the summary; idempotent (returns the last summary). */
  stopSingingFeedback(): SingingSummary;
  /** Scores a saved 015 take offline with the same YIN core (no worklet needed). */
  scoreTake(request: TakeScoreRequest): Promise<TakeScoreResult>;
  /** Stops playback, cancels timers/animation frames and closes the AudioContext. */
  dispose(): Promise<void>;
}

/** Signature of the exported `createEngine` function. */
export type CreateEngine = (dotnetRef: DotNetObjectReference, options?: EngineOptions) => AudioEngine;

/**
 * Binding TypeScript contract of the StemStudio audio engine (contracts/audio-engine.md).
 *
 * Value ranges match the Mix DTO of the backend API: volumes 0..1, pan -1..1, times in seconds.
 * Changing anything here is a contract change (ADR + version bump of {@link ENGINE_CONTRACT_VERSION}).
 *
 * @packageDocumentation
 */

/** Semantic version of this contract; exported at runtime by `index.ts`. */
export type EngineContractVersion = '2.3.0';

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

/** Channel address: a standard stem, or `<parent>.<detail>` for a detail stem (e.g. `drums.snare`). */
export type ChannelId = StemName | `${ParentStemName}.${DetailStemName}`;

/** Meter target name: a channel or the master bus. */
export type MeterTarget = ChannelId | 'master';

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
 */
export type DotNetCallbacks = 'OnPositionChanged' | 'OnPlaybackStateChanged' | 'OnPracticeChanged';

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
  /** Stops playback, cancels timers/animation frames and closes the AudioContext. */
  dispose(): Promise<void>;
}

/** Signature of the exported `createEngine` function. */
export type CreateEngine = (dotnetRef: DotNetObjectReference, options?: EngineOptions) => AudioEngine;

/**
 * Binding TypeScript contract of the StemStudio audio engine (contracts/audio-engine.md).
 *
 * Value ranges match the Mix DTO of the backend API: volumes 0..1, pan -1..1, times in seconds.
 * Changing anything here is a contract change (ADR + version bump of {@link ENGINE_CONTRACT_VERSION}).
 *
 * @packageDocumentation
 */

/** Semantic version of this contract; exported at runtime by `index.ts`. */
export type EngineContractVersion = '1.0.0';

/** Stem names delivered by the backend (`htdemucs_6s`; `htdemucs` yields vocals, drums, bass, other). */
export type StemName = 'vocals' | 'drums' | 'bass' | 'guitar' | 'piano' | 'other';

/** Meter target name: a stem or the master bus. */
export type MeterTarget = StemName | 'master';

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
 */
export type DotNetCallbacks = 'OnPositionChanged' | 'OnPlaybackStateChanged';

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
  /** Stops sources and resets the position to 0 (or to loop start when looping). */
  stop(): void;
  /** Jumps to `seconds` (clamped to 0..duration); keeps playing if playing. */
  seek(seconds: number): void;
  /** Current position in seconds, derived from `AudioContext.currentTime`. */
  getPosition(): number;
  /** Current transport state. */
  getState(): PlaybackState;
  /** Applies channel settings; gain changes are ramped (<= 10 ms) to avoid clicks. */
  setChannel(name: StemName, settings: ChannelSettings): void;
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
  /** Stops playback, cancels timers/animation frames and closes the AudioContext. */
  dispose(): Promise<void>;
}

/** Signature of the exported `createEngine` function. */
export type CreateEngine = (dotnetRef: DotNetObjectReference, options?: EngineOptions) => AudioEngine;

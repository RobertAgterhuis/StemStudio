/**
 * Composition root: one AudioContext shared by transport, mixer, stem groups, meters, metronome and karaoke, exposed
 * as the {@link AudioEngine} object Blazor holds as an `IJSObjectReference`.
 *
 * @packageDocumentation
 */
import { DotNetNotifier, PracticeNotifier, loopPassNotifier } from './callbacks';
import { clampChecked } from './channel';
import { FrameLoop } from './frame-loop';
import { Karaoke } from './karaoke/index';
import { KaraokeClock, heardTime } from './karaoke/clock';
import { SWAP_DELAY_SECONDS, StemGroups, groupChannels, validateGroups } from './groups';
import { setLoopChecked } from './loop';
import { LoopPassTracker } from './loop-pass';
import { Meters } from './meters';
import { ClickPlayer } from './metronome/clicks';
import { MetronomeScheduler } from './metronome/scheduler';
import { DEFAULT_METRONOME, validateMetronomeSettings } from './metronome/settings';
import { Mixer } from './mixer';
import { PracticeController } from './practice/controller';
import { PendingRecording, type RecordingSurface } from './recording/pending';
import type { TempoSource } from './practice/count-ins';
import { loadStems, type NamedSource } from './stem-loader';
import { createWorkletStageFactory } from './stretch/worklet-stage';
import { Transport } from './transport';
import type {
  AudioEngine,
  BeatGrid,
  CalibrationOptions,
  CalibrationResult,
  CapturedTake,
  ChannelId,
  ChannelSettings,
  DotNetObjectReference,
  EngineOptions,
  InputDevice,
  InputInfo,
  KaraokeLyrics,
  KaraokeOptions,
  LoadTakeRequest,
  LoadTrackRequest,
  LoopRegion,
  MeterTarget,
  MetronomeSettings,
  MonitoringSettings,
  OpenInputRequest,
  ParentStemName,
  PlaybackState,
  PracticeSettings,
  PracticeStatus,
  ScoreFollowOptions,
  ScoreViewMap,
  RecordOptions,
  RecordingSupport,
  TakeCompareMode,
} from './types';

/** Default delay of the shared `when` (s). */
export const DEFAULT_START_DELAY_SECONDS = 0.1;
/** Default metronome look-ahead window (s). */
export const DEFAULT_SCHEDULE_AHEAD_SECONDS = 0.1;
/** Default scheduler wake-up interval (ms). */
export const DEFAULT_SCHEDULER_INTERVAL_MS = 25;

interface Parts {
  readonly context: AudioContext;
  readonly mixer: Mixer;
  readonly transport: Transport;
  readonly notifier: DotNetNotifier;
  readonly frames: FrameLoop;
  readonly meters: Meters;
  readonly metronome: MetronomeScheduler;
  readonly clicks: ClickPlayer;
  readonly karaoke: Karaoke;
  readonly passes: LoopPassTracker;
}

function createParts(ref: DotNetObjectReference, options: EngineOptions): Parts {
  const context = (options.audioContextFactory ?? (() => new AudioContext({ sampleRate: 44100 })))();
  const mixer = new Mixer(context);
  const transport = new Transport({
    context,
    startDelaySeconds: options.startDelaySeconds ?? DEFAULT_START_DELAY_SECONDS,
    output: (id) => mixer.input(id),
  });
  const notifier = createNotifier(ref, transport);
  const frames = new FrameLoop();
  const meters = new Meters({ source: (target) => mixer.analyser(target), loop: frames });
  const clicks = new ClickPlayer(context);
  const passes = createLoopPasses({ context, transport, frames }, ref);
  const metronome = createMetronome({ context, transport, clicks, onTick: passes.observe }, options);
  const karaoke = createKaraoke(context, transport, frames);
  return { context, mixer, transport, notifier, frames, meters, metronome, clicks, karaoke, passes };
}

function createLoopPasses(
  { context, transport, frames }: { context: AudioContext; transport: Transport; frames: FrameLoop },
  ref: DotNetObjectReference,
): LoopPassTracker {
  const now = (): number => heardTime(context, performance.now());
  const passes = new LoopPassTracker({ transport, frames, now, report: loopPassNotifier(ref) });
  transport.addListener(passes);
  return passes;
}

function createNotifier(ref: DotNetObjectReference, transport: Transport): DotNetNotifier {
  const notifier = new DotNetNotifier(ref, () => transport.position());
  transport.addListener(notifier);
  return notifier;
}

function createKaraoke(context: AudioContext, transport: Transport, loop: FrameLoop): Karaoke {
  const clock = new KaraokeClock({ context, transport });
  const karaoke = new Karaoke({ loop, clock, playing: () => transport.state === 'playing' });
  transport.addListener(karaoke);
  return karaoke;
}

interface MetronomeParts {
  readonly context: AudioContext;
  readonly transport: Transport;
  readonly clicks: ClickPlayer;
  readonly onTick: () => void;
}

function createMetronome({ context, transport, clicks, onTick }: MetronomeParts, options: EngineOptions): MetronomeScheduler {
  const metronome = new MetronomeScheduler({
    context,
    clock: transport,
    output: clicks,
    onTick,
    aheadSeconds: options.scheduleAheadSeconds ?? DEFAULT_SCHEDULE_AHEAD_SECONDS,
    intervalMs: options.schedulerIntervalMs ?? DEFAULT_SCHEDULER_INTERVAL_MS,
  });
  transport.addListener(metronome);
  return metronome;
}

function createGroups(parts: Parts, load: GroupLoader): StemGroups {
  return new StemGroups({
    load,
    swapTime: () => parts.transport.startTimeAfter(SWAP_DELAY_SECONDS),
    attach: (buffers, at) => {
      parts.transport.addBuffers(buffers, at);
    },
    swap: (parent, expanded, at) => {
      parts.mixer.setExpanded(parent, expanded, at);
    },
  });
}

/**
 * Message of the 2.4.0 score follower stubs until the audio-engine lane implements them (014 T081); the arguments are
 * accepted so the signatures type-check.
 */
function scoreFollowerPending(args: readonly unknown[]): string {
  return `Score follower is not implemented yet (contract 2.4.0; ${String(args.length)} arguments)`;
}

/** Fetch-and-decode function handed to {@link StemGroups} (rejects after disposal). */
type GroupLoader = <N extends string>(sources: readonly NamedSource<N>[]) => Promise<ReadonlyMap<N, AudioBuffer>>;

/** The StemStudio audio engine (contract `types.ts`, behaviour `contracts/audio-engine.md`). */
export class Engine implements AudioEngine {
  private readonly parts: Parts;
  private readonly groups: StemGroups;
  private readonly fetchFn: typeof fetch;
  private readonly practice: PracticeController;
  private readonly practiceNotifier: PracticeNotifier;
  private readonly recording: RecordingSurface = new PendingRecording();
  private metronome: MetronomeSettings = DEFAULT_METRONOME;
  private grid: BeatGrid | null = null;
  private disposed = false;

  /**
   * @param ref - .NET object receiving the callbacks.
   * @param options - Test overrides (context factory, fetch, timing).
   */
  public constructor(ref: DotNetObjectReference, options: EngineOptions = {}) {
    this.parts = createParts(ref, options);
    this.fetchFn = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
    this.groups = createGroups(this.parts, this.load);
    this.practiceNotifier = new PracticeNotifier(ref, () => this.practice.status());
    this.practice = this.createPractice(options);
  }

  /** Validated metronome settings. */
  public get metronomeSettings(): MetronomeSettings {
    return this.metronome;
  }

  /** Beat grid of the loaded track. */
  public get beatGrid(): BeatGrid | null {
    return this.grid;
  }

  /** {@inheritDoc AudioEngine.loadTrack} */
  public async loadTrack(request: LoadTrackRequest): Promise<number> {
    this.alive();
    const groups = request.groups ?? [];
    validateGroups(request.stems, groups);
    const buffers = await this.load(request.stems);
    this.parts.transport.load(buffers);
    this.groups.reset(groups);
    this.parts.mixer.build([...buffers.keys(), ...groupChannels(groups)]);
    this.grid = request.beatGrid;
    this.parts.metronome.configure(this.metronome, this.grid);
    this.practice.onTrackLoaded();
    this.parts.passes.reset();
    return this.parts.transport.duration;
  }

  /** {@inheritDoc AudioEngine.play} */
  public async play(): Promise<void> {
    this.alive();
    await this.practice.play();
  }

  /** {@inheritDoc AudioEngine.pause} */
  public pause(): void {
    this.alive();
    this.parts.transport.pause();
  }

  /** {@inheritDoc AudioEngine.stop} */
  public stop(): void {
    this.alive();
    this.parts.transport.stop();
    this.parts.passes.reset();
  }

  /** {@inheritDoc AudioEngine.seek} */
  public seek(seconds: number): void {
    this.alive();
    const max = this.parts.transport.duration;
    this.parts.transport.seek(clampChecked(seconds, { min: 0, max, name: 'position' }));
    this.parts.passes.reset();
  }

  /** {@inheritDoc AudioEngine.getPosition} */
  public getPosition(): number {
    this.alive();
    return this.parts.transport.position();
  }

  /** {@inheritDoc AudioEngine.getState} */
  public getState(): PlaybackState {
    this.alive();
    return this.parts.transport.state;
  }

  /** {@inheritDoc AudioEngine.setChannel} */
  public setChannel(id: ChannelId, settings: ChannelSettings): void {
    this.alive();
    this.parts.mixer.setChannel(id, settings);
  }

  /** {@inheritDoc AudioEngine.setExpanded} */
  public async setExpanded(parent: ParentStemName, expanded: boolean): Promise<void> {
    this.alive();
    await this.groups.setExpanded(parent, expanded);
  }

  /** {@inheritDoc AudioEngine.setMaster} */
  public setMaster(volume: number): void {
    this.alive();
    this.parts.mixer.setMaster(volume);
  }

  /** {@inheritDoc AudioEngine.setLoop} */
  public setLoop(loop: LoopRegion): void {
    this.alive();
    setLoopChecked(this.parts.transport, loop);
    this.practice.onLoopChanged();
    this.parts.passes.onLoopChanged();
    this.parts.karaoke.redraw();
  }

  /** {@inheritDoc AudioEngine.setMetronome} */
  public setMetronome(settings: MetronomeSettings): void {
    this.alive();
    this.metronome = validateMetronomeSettings(settings);
    this.parts.metronome.configure(this.metronome, this.grid);
  }

  /** {@inheritDoc AudioEngine.attachMeter} */
  public attachMeter(target: MeterTarget, elementId: string): void {
    this.alive();
    this.parts.meters.attach(target, elementId);
  }

  /** {@inheritDoc AudioEngine.detachMeter} */
  public detachMeter(target: MeterTarget): void {
    this.alive();
    this.parts.meters.detach(target);
  }

  /** {@inheritDoc AudioEngine.attachKaraoke} */
  public attachKaraoke(elementId: string, lyrics: KaraokeLyrics, options: KaraokeOptions): void {
    this.alive();
    this.parts.karaoke.attach(elementId, lyrics, options);
  }

  /** {@inheritDoc AudioEngine.setKaraokeOffset} */
  public setKaraokeOffset(offsetMs: number): void {
    this.alive();
    this.parts.karaoke.setOffset(offsetMs);
  }

  /** {@inheritDoc AudioEngine.detachKaraoke} */
  public detachKaraoke(): void {
    this.alive();
    this.parts.karaoke.detach();
  }

  /** {@inheritDoc AudioEngine.setPractice} */
  public async setPractice(settings: PracticeSettings): Promise<void> {
    await Promise.resolve();
    this.alive();
    await this.practice.apply(settings);
  }

  /** {@inheritDoc AudioEngine.getPracticeStatus} */
  public getPracticeStatus(): PracticeStatus {
    this.alive();
    return this.practice.status();
  }

  /** {@inheritDoc AudioEngine.attachScoreFollower} */
  public attachScoreFollower(elementId: string, view: ScoreViewMap, options: ScoreFollowOptions): void {
    this.alive();
    throw new Error(scoreFollowerPending([elementId, view, options]));
  }

  /** {@inheritDoc AudioEngine.setScoreFollowOptions} */
  public setScoreFollowOptions(options: ScoreFollowOptions): void {
    this.alive();
    throw new Error(scoreFollowerPending([options]));
  }

  /** {@inheritDoc AudioEngine.detachScoreFollower} */
  public detachScoreFollower(): void {
    this.alive();
    throw new Error(scoreFollowerPending([]));
  }

  /** {@inheritDoc AudioEngine.recordingSupport} */
  public recordingSupport(): Promise<RecordingSupport> {
    this.alive();
    return this.recording.recordingSupport();
  }

  /** {@inheritDoc AudioEngine.listInputDevices} */
  public listInputDevices(): Promise<InputDevice[]> {
    this.alive();
    return this.recording.listInputDevices();
  }

  /** {@inheritDoc AudioEngine.openInput} */
  public openInput(request: OpenInputRequest): Promise<InputInfo> {
    this.alive();
    return this.recording.openInput(request);
  }

  /** {@inheritDoc AudioEngine.closeInput} */
  public closeInput(): void {
    this.alive();
    this.recording.closeInput();
  }

  /** {@inheritDoc AudioEngine.setMonitoring} */
  public setMonitoring(settings: MonitoringSettings): void {
    this.alive();
    this.recording.setMonitoring(settings);
  }

  /** {@inheritDoc AudioEngine.calibrateLatency} */
  public calibrateLatency(options?: CalibrationOptions): Promise<CalibrationResult> {
    this.alive();
    return this.recording.calibrateLatency(options);
  }

  /** {@inheritDoc AudioEngine.startRecording} */
  public startRecording(options: RecordOptions): Promise<void> {
    this.alive();
    return this.recording.startRecording(options);
  }

  /** {@inheritDoc AudioEngine.stopRecording} */
  public stopRecording(): Promise<CapturedTake[]> {
    this.alive();
    return this.recording.stopRecording();
  }

  /** {@inheritDoc AudioEngine.captureWav} */
  public captureWav(captureKey: string): Blob {
    this.alive();
    return this.recording.captureWav(captureKey);
  }

  /** {@inheritDoc AudioEngine.releaseCapture} */
  public releaseCapture(captureKey: string): void {
    this.alive();
    this.recording.releaseCapture(captureKey);
  }

  /** {@inheritDoc AudioEngine.loadTake} */
  public loadTake(request: LoadTakeRequest): Promise<void> {
    this.alive();
    return this.recording.loadTake(request);
  }

  /** {@inheritDoc AudioEngine.unloadTake} */
  public unloadTake(): void {
    this.alive();
    this.recording.unloadTake();
  }

  /** {@inheritDoc AudioEngine.setTakeAdjustment} */
  public setTakeAdjustment(samples: number): void {
    this.alive();
    this.recording.setTakeAdjustment(samples);
  }

  /** {@inheritDoc AudioEngine.setTakeCompare} */
  public setTakeCompare(mode: TakeCompareMode): void {
    this.alive();
    this.recording.setTakeCompare(mode);
  }

  /** {@inheritDoc AudioEngine.dispose} */
  public async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    const { transport, mixer, notifier, meters, karaoke, frames, metronome, context, passes } = this.parts;
    [transport, this.groups, mixer, notifier, meters, passes].forEach((part) => {
      part.dispose();
    });
    karaoke.detach();
    [frames, metronome, this.practice, this.practiceNotifier].forEach((part) => {
      part.dispose();
    });
    await context.close();
  }

  private createPractice(options: EngineOptions): PracticeController {
    const { context, transport, mixer } = this.parts;
    const factory = options.timeStretchFactory ?? createWorkletStageFactory({ ...options.timeStretch, fetch: this.fetchFn });
    const changed = (): void => {
      this.practiceNotifier.changed();
    };
    const startDelay = options.startDelaySeconds ?? DEFAULT_START_DELAY_SECONDS;
    const tempoSource = (): TempoSource => ({ grid: this.grid, metronome: this.metronome });
    const practice = new PracticeController({ context, transport, mixer, factory, changed, clicks: this.parts.clicks, startDelay, tempoSource });
    transport.addListener(practice);
    transport.setRestartHook(() => {
      practice.onRestart();
    });
    return practice;
  }

  /** Fetches and decodes files; rejects when the engine was disposed meanwhile. */
  private readonly load: GroupLoader = async (sources) => {
    const buffers = await loadStems(this.parts.context, this.fetchFn, sources);
    this.alive();
    return buffers;
  };

  private alive(): void {
    if (this.disposed) {
      throw new Error('Audio engine has been disposed');
    }
  }
}

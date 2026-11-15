/**
 * Composition root: one AudioContext shared by transport, mixer, meters and metronome, exposed
 * as the {@link AudioEngine} object Blazor holds as an `IJSObjectReference`.
 *
 * @packageDocumentation
 */
import { DotNetNotifier } from './callbacks';
import { clampChecked } from './channel';
import { setLoopChecked } from './loop';
import { Meters } from './meters';
import { ClickPlayer } from './metronome/clicks';
import { MetronomeScheduler } from './metronome/scheduler';
import { DEFAULT_METRONOME, validateMetronomeSettings } from './metronome/settings';
import { Mixer } from './mixer';
import { loadStems } from './stem-loader';
import { Transport } from './transport';
import type {
  AudioEngine,
  BeatGrid,
  ChannelSettings,
  DotNetObjectReference,
  EngineOptions,
  LoadTrackRequest,
  LoopRegion,
  MeterTarget,
  MetronomeSettings,
  PlaybackState,
  StemName,
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
  readonly meters: Meters;
  readonly metronome: MetronomeScheduler;
}

function createParts(ref: DotNetObjectReference, options: EngineOptions): Parts {
  const context = (options.audioContextFactory ?? (() => new AudioContext({ sampleRate: 44100 })))();
  const mixer = new Mixer(context);
  const transport = new Transport({
    context,
    startDelaySeconds: options.startDelaySeconds ?? DEFAULT_START_DELAY_SECONDS,
    output: (name) => mixer.input(name),
  });
  const notifier = new DotNetNotifier(ref, () => transport.position());
  transport.addListener(notifier);
  const meters = new Meters({ source: (target) => mixer.analyser(target) });
  const metronome = createMetronome(context, transport, options);
  return { context, mixer, transport, notifier, meters, metronome };
}

function createMetronome(context: AudioContext, transport: Transport, options: EngineOptions): MetronomeScheduler {
  const metronome = new MetronomeScheduler({
    context,
    clock: transport,
    output: new ClickPlayer(context),
    aheadSeconds: options.scheduleAheadSeconds ?? DEFAULT_SCHEDULE_AHEAD_SECONDS,
    intervalMs: options.schedulerIntervalMs ?? DEFAULT_SCHEDULER_INTERVAL_MS,
  });
  transport.addListener(metronome);
  return metronome;
}

/** The StemStudio audio engine (contract `types.ts`, behaviour `contracts/audio-engine.md`). */
export class Engine implements AudioEngine {
  private readonly parts: Parts;
  private readonly fetchFn: typeof fetch;
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
    const buffers = await loadStems(this.parts.context, this.fetchFn, request.stems);
    this.alive();
    this.parts.transport.load(buffers);
    this.parts.mixer.build([...buffers.keys()]);
    this.grid = request.beatGrid;
    this.parts.metronome.configure(this.metronome, this.grid);
    return this.parts.transport.duration;
  }

  /** {@inheritDoc AudioEngine.play} */
  public async play(): Promise<void> {
    this.alive();
    await this.parts.transport.play();
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
  }

  /** {@inheritDoc AudioEngine.seek} */
  public seek(seconds: number): void {
    this.alive();
    const max = this.parts.transport.duration;
    this.parts.transport.seek(clampChecked(seconds, { min: 0, max, name: 'position' }));
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
  public setChannel(name: StemName, settings: ChannelSettings): void {
    this.alive();
    this.parts.mixer.setChannel(name, settings);
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

  /** {@inheritDoc AudioEngine.dispose} */
  public async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    this.parts.transport.dispose();
    this.parts.notifier.dispose();
    this.parts.meters.dispose();
    this.parts.metronome.dispose();
    await this.parts.context.close();
  }

  private alive(): void {
    if (this.disposed) {
      throw new Error('Audio engine has been disposed');
    }
  }
}

/**
 * Composition root: one AudioContext shared by transport, mixer, stem groups, meters and metronome, exposed
 * as the {@link AudioEngine} object Blazor holds as an `IJSObjectReference`.
 *
 * @packageDocumentation
 */
import { DotNetNotifier } from './callbacks';
import { clampChecked } from './channel';
import { SWAP_DELAY_SECONDS, StemGroups, groupChannels, validateGroups } from './groups';
import { setLoopChecked } from './loop';
import { Meters } from './meters';
import { ClickPlayer } from './metronome/clicks';
import { MetronomeScheduler } from './metronome/scheduler';
import { DEFAULT_METRONOME, validateMetronomeSettings } from './metronome/settings';
import { Mixer } from './mixer';
import { loadStems, type NamedSource } from './stem-loader';
import { Transport } from './transport';
import type {
  AudioEngine,
  BeatGrid,
  ChannelId,
  ChannelSettings,
  DotNetObjectReference,
  EngineOptions,
  KaraokeLyrics,
  KaraokeOptions,
  LoadTrackRequest,
  LoopRegion,
  MeterTarget,
  MetronomeSettings,
  ParentStemName,
  PlaybackState,
} from './types';

/** Message of the karaoke methods until the karaoke module exists (contract 2.1.0 skeleton). */
export const KARAOKE_NOT_IMPLEMENTED = 'Karaoke is not implemented yet';

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
    output: (id) => mixer.input(id),
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

/** Fetch-and-decode function handed to {@link StemGroups} (rejects after disposal). */
type GroupLoader = <N extends string>(sources: readonly NamedSource<N>[]) => Promise<ReadonlyMap<N, AudioBuffer>>;

/** The StemStudio audio engine (contract `types.ts`, behaviour `contracts/audio-engine.md`). */
export class Engine implements AudioEngine {
  private readonly parts: Parts;
  private readonly groups: StemGroups;
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
    this.groups = createGroups(this.parts, this.load);
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
    this.karaokeStub([elementId, lyrics, options]);
  }

  /** {@inheritDoc AudioEngine.setKaraokeOffset} */
  public setKaraokeOffset(offsetMs: number): void {
    this.karaokeStub([offsetMs]);
  }

  /** {@inheritDoc AudioEngine.detachKaraoke} */
  public detachKaraoke(): void {
    this.karaokeStub([]);
  }

  /** {@inheritDoc AudioEngine.dispose} */
  public async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    this.parts.transport.dispose();
    this.groups.dispose();
    this.parts.mixer.dispose();
    this.parts.notifier.dispose();
    this.parts.meters.dispose();
    this.parts.metronome.dispose();
    await this.parts.context.close();
  }

  /** Fetches and decodes files; rejects when the engine was disposed meanwhile. */
  private readonly load: GroupLoader = async (sources) => {
    const buffers = await loadStems(this.parts.context, this.fetchFn, sources);
    this.alive();
    return buffers;
  };

  /** Contract stub of the 2.1.0 karaoke methods (006 T026/T036 replace it); the arguments are ignored. */
  private karaokeStub(args: readonly unknown[]): never {
    this.alive();
    throw new Error(`${KARAOKE_NOT_IMPLEMENTED} (${String(args.length)} argument(s))`);
  }

  private alive(): void {
    if (this.disposed) {
      throw new Error('Audio engine has been disposed');
    }
  }
}

/**
 * Mixer: one {@link Channel} per standard or detail stem into
 * `master Gain -> master Analyser -> destination`, gated by the audible-level rule (`gate.ts`).
 *
 * @packageDocumentation
 */
import { Channel, checkedVolume, rampTo } from './channel';
import { anyAudibleSolo, isHidden, levelGate } from './gate';
import type { ChannelId, ChannelSettings, MeterTarget, ParentStemName } from './types';

/** Extra gate factor per channel (compare and recording, contract 2.5.0); absent = 1. */
export type ExtraGate = (id: ChannelId) => number;

const TAKE: ChannelId = 'take';

/** Channels of the loaded track plus the master bus. */
export class Mixer {
  private readonly master: GainNode;
  private readonly masterAnalyser: AnalyserNode;
  private channels = new Map<ChannelId, Channel>();
  private readonly expanded = new Set<ParentStemName>();
  private extraGate: ExtraGate | null = null;

  /** @param context - The engine's AudioContext. */
  public constructor(private readonly context: BaseAudioContext) {
    this.master = context.createGain();
    this.masterAnalyser = context.createAnalyser();
    this.master.connect(this.masterAnalyser).connect(context.destination);
  }

  /**
   * Replaces (disconnects) all existing channels; every parent starts collapsed, so detail
   * channels start silent.
   *
   * @param ids - Standard and detail channels of the new track.
   */
  public build(ids: readonly ChannelId[]): void {
    this.dispose();
    this.channels = new Map(ids.map((id) => [id, new Channel(this.context, this.master)]));
    this.channels.forEach((channel, id) => {
      channel.initGate(isHidden(id, this.expanded) ? 0 : 1);
    });
  }

  /**
   * @param id - Channel id.
   * @returns The node the channel's sources connect to.
   * @throws Error for an unknown channel.
   */
  public input(id: ChannelId): AudioNode {
    return this.channel(id).input;
  }

  /**
   * Applies channel settings and re-evaluates the gate of every channel.
   *
   * @param id - Channel id (standard stem or `<parent>.<detail>`).
   * @param settings - Volume, pan, mute, solo.
   * @throws Error for an unknown channel or a non-finite value.
   */
  public setChannel(id: ChannelId, settings: ChannelSettings): void {
    if (id === TAKE) {
      this.ensureTake();
    }
    this.channel(id).configure(settings);
    this.applyGates(this.context.currentTime);
  }

  /**
   * Expands or collapses a parent: a pure gate change of every channel at one context time.
   *
   * @param parent - Parent stem (the caller checks that it has a group).
   * @param expanded - New state.
   * @param at - Context time at which all ramps start.
   */
  public setExpanded(parent: ParentStemName, expanded: boolean, at: number): void {
    if (expanded) {
      this.expanded.add(parent);
    } else {
      this.expanded.delete(parent);
    }
    this.applyGates(at);
  }

  /**
   * @param parent - Parent stem.
   * @returns Whether its detail channels currently replace it.
   */
  public isExpanded(parent: ParentStemName): boolean {
    return this.expanded.has(parent);
  }

  /**
   * Inserts the time-stretch stage between the master gain and the master meter, or removes it (contract 2.3.0;
   * without a stage the graph is exactly the 2.1.0 one).
   *
   * @param stage - Stage input/output, or null for the direct connection.
   */
  public setStage(stage: { readonly input: AudioNode; readonly output: AudioNode } | null): void {
    this.master.disconnect();
    if (stage === null) {
      this.master.connect(this.masterAnalyser);
      return;
    }
    stage.output.disconnect();
    this.master.connect(stage.input);
    stage.output.connect(this.masterAnalyser);
  }

  /**
   * Creates the `take` channel (fader x gate -> panner -> meter -> master) when it does not exist yet (contract 2.5.0);
   * without it the graph is exactly the 2.4.0 one. A new track ({@link Mixer.build}) removes it.
   */
  public ensureTake(): void {
    if (this.channels.has(TAKE)) {
      return;
    }
    const channel = new Channel(this.context, this.master);
    this.channels.set(TAKE, channel);
    channel.initGate(0);
    this.applyGates(this.context.currentTime);
  }

  /**
   * @param gate - Extra gate factor per channel (compare, recording), or null for none.
   * @param at - Context time at which all ramps start (one swap, ADR-010).
   */
  public setExtraGate(gate: ExtraGate | null, at: number): void {
    this.extraGate = gate;
    this.applyGates(at);
  }

  /** @returns The post-master node feeding the destination (the E2E loopback input taps it, contract 2.5.0). */
  public tap(): AudioNode {
    return this.masterAnalyser;
  }

  /** @param volume - Master gain 0..1 (clamped). */
  public setMaster(volume: number): void {
    rampTo(this.master.gain, checkedVolume(volume), this.context.currentTime);
  }

  /**
   * @param target - Channel id, `"master"` or `"input"` (the input tap belongs to the recording input, contract 2.5.0).
   * @returns The meter tap of the target, or undefined when the channel is not loaded (always for `"input"`); the
   *   `take` channel is created on first use (contract 2.5.0: a meter can be attached before `loadTake`).
   */
  public analyser(target: MeterTarget): AnalyserNode | undefined {
    if (target === 'input') {
      return undefined;
    }
    if (target === TAKE) {
      this.ensureTake();
    }
    return target === 'master' ? this.masterAnalyser : this.channels.get(target)?.analyser;
  }

  /**
   * @param target - Channel id or `"master"`.
   * @returns Whether the target exists in the current track (the `take` channel only once created).
   */
  public has(target: MeterTarget): boolean {
    return target === TAKE ? this.channels.has(TAKE) : this.analyser(target) !== undefined;
  }

  /** Disconnects every channel and collapses all parents (the master bus stays). */
  public dispose(): void {
    this.channels.forEach((channel) => {
      channel.dispose();
    });
    this.channels = new Map();
    this.expanded.clear();
  }

  private applyGates(at: number): void {
    const views = [...this.channels].map(([id, channel]) => ({
      id,
      channel,
      settings: channel.settings,
      hidden: isHidden(id, this.expanded),
    }));
    const anySolo = anyAudibleSolo(views);
    const extra = this.extraGate ?? ((): number => 1);
    views.forEach((view) => {
      view.channel.apply(levelGate(view, anySolo) * extra(view.id), at);
    });
  }

  private channel(id: ChannelId): Channel {
    const channel = this.channels.get(id);
    if (channel === undefined) {
      throw new Error(`Unknown stem "${id}"`);
    }
    return channel;
  }
}

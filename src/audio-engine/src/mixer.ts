/**
 * Mixer: one {@link Channel} per stem into `master Gain -> master Analyser -> destination`.
 *
 * @packageDocumentation
 */
import { Channel, checkedVolume, rampTo } from './channel';
import type { ChannelSettings, MeterTarget, StemName } from './types';

/** Channels of the loaded track plus the master bus. */
export class Mixer {
  private readonly master: GainNode;
  private readonly masterAnalyser: AnalyserNode;
  private channels = new Map<StemName, Channel>();

  /** @param context - The engine's AudioContext. */
  public constructor(private readonly context: BaseAudioContext) {
    this.master = context.createGain();
    this.masterAnalyser = context.createAnalyser();
    this.master.connect(this.masterAnalyser).connect(context.destination);
  }

  /** @param names - Stems of the new track; replaces (disconnects) all existing channels. */
  public build(names: readonly StemName[]): void {
    this.channels.forEach((channel) => {
      channel.dispose();
    });
    this.channels = new Map(names.map((name) => [name, new Channel(this.context, this.master)]));
  }

  /**
   * @param name - Stem name.
   * @returns The node the stem's sources connect to.
   * @throws Error for an unknown stem.
   */
  public input(name: StemName): AudioNode {
    return this.channel(name).input;
  }

  /**
   * Applies channel settings and re-evaluates the solo gate of every channel.
   *
   * @param name - Stem name.
   * @param settings - Volume, pan, mute, solo.
   * @throws Error for an unknown stem or a non-finite value.
   */
  public setChannel(name: StemName, settings: ChannelSettings): void {
    this.channel(name).configure(settings);
    const anySolo = [...this.channels.values()].some((c) => c.settings.solo);
    this.channels.forEach((channel) => {
      channel.apply(anySolo);
    });
  }

  /** @param volume - Master gain 0..1 (clamped). */
  public setMaster(volume: number): void {
    rampTo(this.master.gain, checkedVolume(volume), this.context.currentTime);
  }

  /**
   * @param target - Stem name or `"master"`.
   * @returns The meter tap of the target, or undefined when the stem is not loaded.
   */
  public analyser(target: MeterTarget): AnalyserNode | undefined {
    // Detail channel ids (`<parent>.<detail>`, contract 2.0.0) are not loaded until groups exist.
    const channels: ReadonlyMap<string, Channel> = this.channels;
    return target === 'master' ? this.masterAnalyser : channels.get(target)?.analyser;
  }

  /**
   * @param target - Stem name or `"master"`.
   * @returns Whether the target exists in the current track.
   */
  public has(target: MeterTarget): boolean {
    return this.analyser(target) !== undefined;
  }

  private channel(name: StemName): Channel {
    const channel = this.channels.get(name);
    if (channel === undefined) {
      throw new Error(`Unknown stem "${name}"`);
    }
    return channel;
  }
}

/**
 * One mixer channel: `Gain (fader x gate) -> StereoPanner -> Analyser (post-fader meter tap)`.
 *
 * @packageDocumentation
 */
import type { ChannelSettings } from './types';

/** Time constant of gain/pan ramps (s); three constants (~95 %) fit in 10 ms (SC-004). */
export const RAMP_TIME_CONSTANT = 0.003;

/** Settings of a freshly built channel. */
export const DEFAULT_CHANNEL: ChannelSettings = { volume: 1, pan: 0, mute: false, solo: false };

/**
 * Mute/solo gate of a channel.
 *
 * @param settings - Channel settings.
 * @param anySolo - Whether any channel of the mix is soloed.
 * @returns 0 (silent) or 1 (audible).
 */
export function gateOf(settings: ChannelSettings, anySolo: boolean): number {
  if (settings.mute) {
    return 0;
  }
  return anySolo && !settings.solo ? 0 : 1;
}

/**
 * Smoothly moves a parameter to `value` (no zipper noise).
 *
 * @param param - Parameter to automate.
 * @param value - Target value.
 * @param now - Current context time.
 */
export function rampTo(param: AudioParam, value: number, now: number): void {
  param.cancelScheduledValues(now);
  param.setTargetAtTime(value, now, RAMP_TIME_CONSTANT);
}

/**
 * Validates a number and clamps it into a range.
 *
 * @param value - Input value.
 * @param range - Inclusive bounds and the name used in the error message.
 * @returns The clamped value.
 * @throws Error when the value is not a finite number.
 */
export function clampChecked(value: number, range: { min: number; max: number; name: string }): number {
  if (!Number.isFinite(value)) {
    throw new Error(`Invalid ${range.name}: ${String(value)}`);
  }
  return Math.min(Math.max(value, range.min), range.max);
}

/**
 * @param value - Linear gain.
 * @returns The gain validated and clamped to 0..1.
 */
export function checkedVolume(value: number): number {
  return clampChecked(value, { min: 0, max: 1, name: 'volume' });
}

/** Audio nodes and settings of one stem channel. */
export class Channel {
  /** Node the stem sources connect to (fader x gate). */
  public readonly input: GainNode;
  /** Post-fader, post-pan meter tap. */
  public readonly analyser: AnalyserNode;
  private readonly panner: StereoPannerNode;
  private current: ChannelSettings = DEFAULT_CHANNEL;

  /**
   * @param context - Owning context.
   * @param output - Bus the channel feeds (master gain).
   */
  public constructor(
    private readonly context: BaseAudioContext,
    output: AudioNode,
  ) {
    this.input = context.createGain();
    this.panner = context.createStereoPanner();
    this.analyser = context.createAnalyser();
    this.input.connect(this.panner).connect(this.analyser).connect(output);
  }

  /** Current (validated) settings. */
  public get settings(): ChannelSettings {
    return this.current;
  }

  /** @param settings - New settings; validated and clamped, applied by {@link Channel.apply}. */
  public configure(settings: ChannelSettings): void {
    this.current = {
      ...settings,
      volume: checkedVolume(settings.volume),
      pan: clampChecked(settings.pan, { min: -1, max: 1, name: 'pan' }),
    };
  }

  /** @param anySolo - Whether any channel is soloed; ramps gain and pan to the new values. */
  public apply(anySolo: boolean): void {
    const now = this.context.currentTime;
    rampTo(this.input.gain, this.current.volume * gateOf(this.current, anySolo), now);
    rampTo(this.panner.pan, this.current.pan, now);
  }

  /** Disconnects all nodes of the channel. */
  public dispose(): void {
    this.input.disconnect();
    this.panner.disconnect();
    this.analyser.disconnect();
  }
}

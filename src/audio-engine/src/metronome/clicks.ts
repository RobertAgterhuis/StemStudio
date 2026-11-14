/**
 * Pre-rendered click sounds and their output, which bypasses the master fader.
 *
 * @packageDocumentation
 */
import { checkedVolume, rampTo } from '../channel';

/** A click tone: a decaying sine burst. */
export interface ClickTone {
  /** Pitch (Hz). */
  readonly frequency: number;
  /** Peak amplitude 0..1. */
  readonly amplitude: number;
}

/** Beat 1 of a bar: higher and louder. */
export const ACCENT_TONE: ClickTone = { frequency: 1600, amplitude: 1 };
/** Other beats. */
export const NORMAL_TONE: ClickTone = { frequency: 1000, amplitude: 0.6 };

const CLICK_SECONDS = 0.03;
const DECAY_SECONDS = 0.006;

/**
 * Renders a mono click buffer.
 *
 * @param context - Context providing the sample rate and buffers.
 * @param tone - Pitch and amplitude.
 * @returns The click buffer.
 */
export function renderClick(context: BaseAudioContext, tone: ClickTone): AudioBuffer {
  const rate = context.sampleRate;
  const buffer = context.createBuffer(1, Math.round(CLICK_SECONDS * rate), rate);
  const data = buffer.getChannelData(0);
  for (let i = 0; i < data.length; i++) {
    const t = i / rate;
    data[i] = tone.amplitude * Math.sin(2 * Math.PI * tone.frequency * t) * Math.exp(-t / DECAY_SECONDS);
  }
  return buffer;
}

/** Where the scheduler sends clicks. */
export interface ClickOutput {
  /** Schedules one click at an exact context time; returns its (cancellable) source. */
  play(time: number, accent: boolean): AudioScheduledSourceNode;
  /** Sets the click volume 0..1 immediately (ramped, no zipper noise). */
  setVolume(volume: number): void;
}

/** Plays the two pre-rendered clicks through its own gain node straight to the destination. */
export class ClickPlayer implements ClickOutput {
  /** Click volume node, connected to `context.destination` (not to the master). */
  public readonly output: GainNode;
  private readonly accent: AudioBuffer;
  private readonly normal: AudioBuffer;

  /** @param context - The engine's AudioContext. */
  public constructor(private readonly context: BaseAudioContext) {
    this.output = context.createGain();
    this.output.connect(context.destination);
    this.accent = renderClick(context, ACCENT_TONE);
    this.normal = renderClick(context, NORMAL_TONE);
  }

  /** {@inheritDoc ClickOutput.play} */
  public play(time: number, accent: boolean): AudioBufferSourceNode {
    const source = this.context.createBufferSource();
    source.buffer = accent ? this.accent : this.normal;
    source.connect(this.output);
    source.onended = () => {
      source.disconnect();
    };
    source.start(time);
    return source;
  }

  /** {@inheritDoc ClickOutput.setVolume} */
  public setVolume(volume: number): void {
    rampTo(this.output.gain, checkedVolume(volume), this.context.currentTime);
  }
}

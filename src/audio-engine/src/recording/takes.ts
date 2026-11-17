/**
 * Captured takes kept in the engine until released (data-model 3.3): the audio per capture key, its 24-bit WAV for
 * the upload (`captureWav`), and the description of a mapped take as a {@link CapturedTake}.
 *
 * @packageDocumentation
 */
import type { CapturedTake, InputChannelMode, LatencyMethod } from '../types';
import { peakDbfs } from './calibration';
import type { MappedTake } from './mapping';
import { encodeWav24 } from './wav';

/** Smallest absolute float sample that quantises to full scale (`2^23 - 1`). */
const FULL_SCALE = (2 ** 23 - 1) / 2 ** 23;

/** Local audio of captured takes by capture key. */
export class CaptureStore {
  private readonly audio = new Map<string, Float32Array[]>();
  private readonly starts = new Map<string, number>();

  /**
   * @param key - Capture key.
   * @param channels - Take audio per channel.
   * @param startSample - Song sample of the first frame.
   */
  public put(key: string, channels: Float32Array[], startSample = 0): void {
    this.audio.set(key, channels);
    this.starts.set(key, startSample);
  }

  /**
   * @param key - Capture key.
   * @returns The audio, or undefined for an unknown key.
   */
  public samples(key: string): readonly Float32Array[] | undefined {
    return this.audio.get(key);
  }

  /**
   * @param key - Capture key.
   * @returns Song sample of the capture's first frame (0 for an unknown key).
   */
  public startOf(key: string): number {
    return this.starts.get(key) ?? 0;
  }

  /**
   * @param key - Capture key.
   * @returns The take as a 24-bit PCM WAV `Blob` (research R-07).
   * @throws Error `Unknown capture "<key>"`.
   */
  public wav(key: string): Blob {
    const channels = this.audio.get(key);
    if (channels === undefined) {
      throw new Error(`Unknown capture "${key}"`);
    }
    return new Blob([encodeWav24(channels)], { type: 'audio/wav' });
  }

  /** @param key - Capture key; unknown keys are ignored. */
  public release(key: string): void {
    this.audio.delete(key);
    this.starts.delete(key);
  }

  /** Frees every capture. */
  public clear(): void {
    this.audio.clear();
    this.starts.clear();
  }
}

/** What a session adds to every take of it. */
export interface SessionInfo {
  readonly sessionId: string;
  readonly inputMode: InputChannelMode;
  /** `L + P` as applied. */
  readonly latencySamples: number;
  readonly latencyMethod: LatencyMethod;
  readonly recordedAt: string;
}

function levels(channels: readonly Float32Array[]): { peak: number; clipped: number } {
  let peak = 0;
  let clipped = 0;
  channels.forEach((channel) => {
    channel.forEach((x) => {
      const magnitude = Math.abs(x);
      peak = Math.max(peak, magnitude);
      clipped += magnitude >= FULL_SCALE ? 1 : 0;
    });
  });
  return { peak, clipped };
}

/**
 * @param take - A mapped take.
 * @param session - The session.
 * @param captureKey - Key of its audio in the {@link CaptureStore}.
 * @returns The take as reported to .NET.
 */
export function describeTake(take: MappedTake, session: SessionInfo, captureKey: string): CapturedTake {
  const { peak, clipped } = levels(take.channels);
  return {
    captureKey,
    sessionId: session.sessionId,
    passIndex: take.passIndex,
    partial: take.partial,
    startSample: take.startSample,
    lengthSamples: take.lengthSamples,
    channels: take.channels.length >= 2 ? 2 : 1,
    inputMode: session.inputMode,
    latencySamples: session.latencySamples,
    latencyMethod: session.latencyMethod,
    peakDbfs: Math.round(peakDbfs(peak) * 100) / 100,
    clippedSamples: clipped,
    dropouts: take.dropouts,
    recordedAt: session.recordedAt,
  };
}

/**
 * Re-encodes a loaded take for a new start (US7, data-model 2.4): the start is clamped at song sample 0, so a shift
 * before the song start drops the leading samples.
 *
 * @param buffer - The loaded take.
 * @param shiftedStart - `startSample + adjustment` (may be negative).
 * @returns The 24-bit WAV of the span that starts at `max(0, shiftedStart)`.
 */
export function shiftedWav(buffer: AudioBuffer, shiftedStart: number): Blob {
  const drop = Math.max(0, -shiftedStart);
  const channels = Array.from({ length: buffer.numberOfChannels }, (_, c) => buffer.getChannelData(c).subarray(drop));
  return new Blob([encodeWav24(channels)], { type: 'audio/wav' });
}

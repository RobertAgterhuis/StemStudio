/**
 * Stereo FIFO with a fixed delay (audio-engine.md 2.3.0 "Stretch stage"): prefilled with D frames of silence, it
 * plays SoundTouch output frame `m` at stage output frame `m + D` whatever SoundTouch's momentary output count is.
 * Runs inside `process()` of the AudioWorklet, so nothing here allocates after construction.
 *
 * @packageDocumentation
 */
import { STRETCH_OVERFLOW_FRAMES } from './constants';

/** Largest number of frames the processor writes in one call (sizes the ring; larger writes still work). */
export const MAX_WRITE_FRAMES = 8192;

/**
 * Interleaves two channels into `target` (left, right, left, right, ...).
 *
 * @param left - Left channel.
 * @param right - Right channel, same length.
 * @param target - At least `2 * left.length` values.
 */
export function interleave(left: Float32Array, right: Float32Array, target: Float32Array): void {
  for (let i = 0; i < left.length; i++) {
    target[2 * i] = sample(left, i);
    target[2 * i + 1] = sample(right, i);
  }
}

/**
 * @param data - Samples.
 * @param index - Index inside `data` (callers stay in range).
 * @returns The sample.
 */
function sample(data: Float32Array, index: number): number {
  return Number(data[index]);
}

/** Ring buffer of interleaved stereo frames with a fixed initial delay and underrun/overflow counting. */
export class DelayFifo {
  private readonly ring: Float32Array;
  private readonly capacity: number;
  private readIndex = 0;
  private count = 0;
  private lost = 0;

  /**
   * @param delayFrames - Fixed delay D (frames of silence after construction and every reset).
   * @param overflowFrames - Growth above D that is tolerated before the surplus is dropped.
   */
  public constructor(
    private readonly delayFrames: number,
    private readonly overflowFrames = STRETCH_OVERFLOW_FRAMES,
  ) {
    this.capacity = delayFrames + overflowFrames + MAX_WRITE_FRAMES;
    this.ring = new Float32Array(this.capacity * 2);
    this.reset();
  }

  /** Buffered frames. */
  public get size(): number {
    return this.count;
  }

  /** Frames output as silence because the FIFO was empty, plus frames dropped on overflow. */
  public get underruns(): number {
    return this.lost;
  }

  /**
   * Appends frames; when the FIFO then holds more than D + overflow frames the oldest are dropped back to D.
   *
   * @param interleaved - Interleaved stereo samples.
   * @param frames - Number of frames of `interleaved` to append.
   */
  public write(interleaved: Float32Array, frames: number): void {
    for (let i = 0; i < frames; i++) {
      this.push(sample(interleaved, 2 * i), sample(interleaved, 2 * i + 1));
    }
    if (this.count > this.delayFrames + this.overflowFrames) {
      this.drop(this.count - this.delayFrames);
    }
  }

  /**
   * Removes `left.length` frames; missing frames are silence and counted as underruns.
   *
   * @param left - Receives the left channel.
   * @param right - Receives the right channel (same length).
   */
  public read(left: Float32Array, right: Float32Array): void {
    for (let i = 0; i < left.length; i++) {
      if (this.count === 0) {
        left[i] = 0;
        right[i] = 0;
        this.lost += 1;
        continue;
      }
      left[i] = sample(this.ring, 2 * this.readIndex);
      right[i] = sample(this.ring, 2 * this.readIndex + 1);
      this.drop(1, false);
    }
  }

  /** Drops everything and refills D frames of silence; the underrun counter is kept. */
  public reset(): void {
    this.readIndex = 0;
    this.count = 0;
    for (let i = 0; i < this.delayFrames; i++) {
      this.push(0, 0);
    }
  }

  private push(left: number, right: number): void {
    if (this.count === this.capacity) {
      this.drop(1);
    }
    const index = (this.readIndex + this.count) % this.capacity;
    this.ring[2 * index] = left;
    this.ring[2 * index + 1] = right;
    this.count += 1;
  }

  private drop(frames: number, counted = true): void {
    this.readIndex = (this.readIndex + frames) % this.capacity;
    this.count -= frames;
    this.lost += counted ? frames : 0;
  }
}

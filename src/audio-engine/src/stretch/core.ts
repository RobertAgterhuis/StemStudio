/**
 * Per-quantum work of the time-stretch processor once SoundTouch is ready: feed the input block, apply pitch changes
 * by the midpoint rule (ADR-021 P7), drain SoundTouch into the fixed-delay FIFO and play the FIFO.
 *
 * @packageDocumentation
 */
import type { SoundTouchStream } from './abi';
import { RENDER_QUANTUM_FRAMES } from './constants';
import { DelayFifo, interleave } from './fifo';

/** A pitch factor that applies to the stage input from context frame `atFrame` on. */
export interface PitchChange {
  /** Pitch factor. */
  readonly value: number;
  /** Context sample frame of the stage input. */
  readonly atFrame: number;
}

/** Pending pitch changes in frame order; a newer change replaces every pending one at or after its frame. */
export class PitchQueue {
  private items: PitchChange[] = [];

  /** @param change - New change. */
  public add(change: PitchChange): void {
    this.items = this.items.filter((item) => item.atFrame < change.atFrame);
    this.items.push(change);
  }

  /** @returns The earliest pending change. */
  public next(): PitchChange | undefined {
    return this.items[0];
  }

  /** Removes the earliest pending change. */
  public shift(): void {
    this.items.shift();
  }
}

/** One input channel and one output channel pair of a render quantum. */
export interface StereoBlock {
  /** Left channel (128 frames). */
  readonly left: Float32Array;
  /** Right channel (128 frames). */
  readonly right: Float32Array;
}

/** SoundTouch + FIFO + pitch timing of one processor. */
export class StretchCore {
  private readonly fifo: DelayFifo;
  private first: number | null = null;
  private inFrames = 0;
  private outFrames = 0;

  /**
   * @param stream - Ready SoundTouch stream.
   * @param queue - Pending pitch changes (shared with the message handler).
   * @param delayFrames - Fixed delay D.
   */
  public constructor(
    private readonly stream: SoundTouchStream,
    private readonly queue: PitchQueue,
    delayFrames: number,
  ) {
    this.fifo = new DelayFifo(delayFrames);
  }

  /** Underruns plus dropped frames of the FIFO. */
  public get underruns(): number {
    return this.fifo.underruns;
  }

  /**
   * Processes one render quantum.
   *
   * @param input - Input block.
   * @param output - Output block (filled).
   * @param frame - Context frame of the block (`currentFrame`).
   */
  public process(input: StereoBlock, output: StereoBlock, frame: number): void {
    this.first ??= frame;
    interleave(input.left, input.right, this.stream.block);
    const from = this.applyDue(this.first);
    this.put(from, RENDER_QUANTUM_FRAMES);
    this.outFrames += this.stream.drainInto(this.fifo);
    this.fifo.read(output.left, output.right);
  }

  /** Clears SoundTouch and the FIFO (D frames of silence) and restarts the frame counters. */
  public reset(): void {
    this.stream.clear();
    this.fifo.reset();
    this.first = null;
    this.inFrames = 0;
    this.outFrames = 0;
  }

  /** Destroys the SoundTouch instance. */
  public dispose(): void {
    this.stream.destroy();
  }

  /** Applies every change whose frame the buffered-input midpoint reaches inside this block; returns frames put. */
  private applyDue(first: number): number {
    let from = 0;
    for (let change = this.queue.next(); change !== undefined; change = this.queue.next()) {
      const missing = Math.max(0, Math.ceil(2 * (change.atFrame - first) - this.outFrames - this.inFrames));
      if (from + missing > RENDER_QUANTUM_FRAMES) {
        return from;
      }
      this.put(from, from + missing);
      from += missing;
      this.stream.setPitch(change.value);
      this.queue.shift();
    }
    return from;
  }

  private put(from: number, to: number): void {
    this.stream.put(from, to);
    this.inFrames += to - from;
  }
}

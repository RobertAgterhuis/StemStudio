/**
 * Captured input of one recording session: the worklet's stamped batches kept as blocks on the context frame
 * timeline (research R-01). Missing frames between two batches are a gap: they read as zeros and count as one
 * dropout (data-model 3.2); frames overlapping already captured ones are ignored.
 *
 * @packageDocumentation
 */

/** One stored batch. */
interface Block {
  /** Context frame of the first sample. */
  readonly frame: number;
  /** Frames in the block. */
  readonly length: number;
  /** Samples per channel. */
  readonly data: readonly Float32Array[];
}

/** A missing range of context frames `[from, to)`. */
interface Gap {
  readonly from: number;
  readonly to: number;
}

/** Stamped capture blocks of one session (1 or 2 channels). */
export class CaptureBuffer {
  private readonly blocks: Block[] = [];
  private readonly missing: Gap[] = [];
  private end = 0;

  /** @param channels - Channels of every block. */
  public constructor(public readonly channels: 1 | 2) {}

  /** Context frame of the first captured sample (0 while empty). */
  public get firstFrame(): number {
    return this.blocks[0]?.frame ?? 0;
  }

  /** Context frame after the last captured sample (0 while empty). */
  public get endFrame(): number {
    return this.end;
  }

  /** Number of gaps between batches. */
  public get gaps(): number {
    return this.missing.length;
  }

  /** Whether nothing was captured. */
  public get empty(): boolean {
    return this.blocks.length === 0;
  }

  /**
   * @param frame - Context frame of `data[c][0]`.
   * @param data - One array per channel, equal lengths.
   */
  public append(frame: number, data: readonly Float32Array[]): void {
    const skip = this.overlap(frame);
    const length = Math.min(...data.map((channel) => channel.length)) - skip;
    if (length <= 0) {
      return;
    }
    if (!this.empty && frame > this.end) {
      this.missing.push({ from: this.end, to: frame });
    }
    this.blocks.push({ frame: frame + skip, length, data: data.map((channel) => channel.subarray(skip, skip + length)) });
    this.end = frame + skip + length;
  }

  /** Frames at the start of a batch at `frame` that were already captured. */
  private overlap(frame: number): number {
    return this.empty ? 0 : Math.max(0, this.end - frame);
  }

  /**
   * @param from - First context frame.
   * @param length - Frames to read.
   * @returns One new array per channel; zeros where nothing was captured.
   */
  public read(from: number, length: number): Float32Array[] {
    const out = Array.from({ length: this.channels }, () => new Float32Array(length));
    const to = from + length;
    this.blocks
      .filter((block) => block.frame < to && block.frame + block.length > from)
      .forEach((block) => {
        copyOverlap(block, out, { from, to });
      });
    return out;
  }

  /**
   * @param from - First context frame.
   * @param to - Frame after the range.
   * @returns Number of gaps that overlap `[from, to)`.
   */
  public gapsIn(from: number, to: number): number {
    return this.missing.filter((gap) => gap.from < to && gap.to > from).length;
  }
}

function copyOverlap(block: Block, out: Float32Array[], range: { from: number; to: number }): void {
  const start = Math.max(range.from, block.frame);
  const stop = Math.min(range.to, block.frame + block.length);
  block.data.forEach((source, c) => {
    out[c]?.set(source.subarray(start - block.frame, stop - block.frame), start - range.from);
  });
}

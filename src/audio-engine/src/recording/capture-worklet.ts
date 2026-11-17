/**
 * AudioWorklet processor `stemstudio-capture` (contract 2.5.0, research R-01), built as its own Vite entry
 * `audio-engine-capture-worklet.js` (`vite.capture.config.ts`).
 *
 * Between a `{ type: 'start' }` and a `{ type: 'stop' }` message it copies every 128-frame render quantum of its input
 * (the selected channels: 1 for `mono-*`, 2 for `stereo`, `processorOptions.channels`) and posts a batch every 2,048
 * frames as `{ type: 'block', frame, channels: Float32Array[] }`, `frame` being the global `currentFrame` of the
 * batch's first frame (the context frame timeline). `stop` flushes the partial batch. Missing input channels are
 * written as zeros. The processor always returns true so the node stays alive.
 *
 * @packageDocumentation
 */

/** Registered processor name. */
export const CAPTURE_PROCESSOR_NAME = 'stemstudio-capture';

/** Frames per posted batch (16 render quanta). */
export const CAPTURE_BATCH_FRAMES = 2048;

/** The processor as the scope sees it. */
export interface CaptureProcessor {
  /** Message port to the `AudioWorkletNode`. */
  readonly port: MessagePort;
  /** Handles one render quantum; true keeps the processor alive. */
  process(inputs: Float32Array[][], outputs?: Float32Array[][]): boolean;
}

/** The parts of `AudioWorkletGlobalScope` the capture processor uses. */
export interface CaptureScope {
  /** Base class providing `port`. */
  readonly AudioWorkletProcessor: new () => { readonly port: MessagePort };
  /** Frame of the current render quantum on the context timeline. */
  readonly currentFrame: number;
  /** Registers a processor class under a name. */
  registerProcessor(name: string, processor: new (options?: AudioWorkletNodeOptions) => CaptureProcessor): void;
}

/** One batch being filled. */
interface Batch {
  readonly frame: number;
  readonly channels: Float32Array[];
  filled: number;
}

function channelCountOf(options: AudioWorkletNodeOptions | undefined): 1 | 2 {
  const requested = (options?.processorOptions as { channels?: unknown } | undefined)?.channels;
  return requested === 2 ? 2 : 1;
}

function quantumLength(input: readonly Float32Array[]): number {
  return input[0]?.length ?? 128;
}

function copyInto(target: Float32Array, source: Float32Array | undefined, at: number): void {
  if (source !== undefined) {
    target.set(source.subarray(0, CAPTURE_BATCH_FRAMES - at), at);
  }
}

/** Posts one batch: its first frame and one array per channel. */
export type BatchSink = (frame: number, channels: Float32Array[]) => void;

/** Collects render quanta into stamped batches between `start` and `stop` (the processor's logic). */
export class CaptureBatcher {
  private armed = false;
  private batch: Batch | null = null;

  /**
   * @param channels - Channels to copy.
   * @param frame - Reads the current render quantum's frame (`currentFrame`).
   * @param sink - Receives each full (or flushed) batch.
   */
  public constructor(
    private readonly channels: 1 | 2,
    private readonly frame: () => number,
    private readonly sink: BatchSink,
  ) {}

  /** @param type - `start` arms, `stop` disarms and flushes; anything else is ignored. */
  public command(type: unknown): void {
    if (type === 'start') {
      this.armed = true;
    } else if (type === 'stop') {
      this.armed = false;
      this.flush();
    }
  }

  /** @param input - Channels of one render quantum (missing channels count as silence). */
  public push(input: readonly Float32Array[]): void {
    if (!this.armed) {
      return;
    }
    const batch = (this.batch ??= this.open());
    batch.channels.forEach((target, c) => {
      copyInto(target, input[c], batch.filled);
    });
    batch.filled += quantumLength(input);
    if (batch.filled >= CAPTURE_BATCH_FRAMES) {
      this.flush();
    }
  }

  private open(): Batch {
    const channels = Array.from({ length: this.channels }, () => new Float32Array(CAPTURE_BATCH_FRAMES));
    return { frame: this.frame(), channels, filled: 0 };
  }

  private flush(): void {
    const batch = this.batch;
    this.batch = null;
    if (batch !== null && batch.filled > 0) {
      this.sink(batch.frame, batch.channels.map((channel) => channel.slice(0, Math.min(batch.filled, CAPTURE_BATCH_FRAMES))));
    }
  }
}

/**
 * Registers the capture processor in a worklet scope (or a test fake).
 *
 * @param scope - The worklet scope.
 */
export function registerCaptureProcessor(scope: CaptureScope): void {
  class StemStudioCaptureProcessor extends scope.AudioWorkletProcessor implements CaptureProcessor {
    private readonly batcher: CaptureBatcher;

    public constructor(options?: AudioWorkletNodeOptions) {
      super();
      const post: BatchSink = (frame, channels) => {
        this.port.postMessage({ type: 'block', frame, channels }, channels.map((channel) => channel.buffer));
      };
      this.batcher = new CaptureBatcher(channelCountOf(options), () => scope.currentFrame, post);
      this.port.onmessage = (event: MessageEvent): void => {
        this.batcher.command((event.data as { type?: unknown } | null)?.type);
      };
    }

    public process(inputs: Float32Array[][]): boolean {
      this.batcher.push(inputs[0] ?? []);
      return true;
    }
  }
  scope.registerProcessor(CAPTURE_PROCESSOR_NAME, StemStudioCaptureProcessor);
}

const globalScope = globalThis as unknown as Partial<CaptureScope>;
if (typeof globalScope.registerProcessor === 'function') {
  registerCaptureProcessor(globalThis as unknown as CaptureScope);
}

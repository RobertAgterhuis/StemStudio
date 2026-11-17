/**
 * Main-thread side of the capture (contract 2.5.0, research R-01): loads the `stemstudio-capture` module once per
 * context, connects `input -> AudioWorkletNode -> Gain(0) -> destination` (the zero gain keeps the browser pulling the
 * node), and collects the posted batches of one session into a {@link CaptureBuffer} up to the session limit.
 *
 * @packageDocumentation
 */
import { CaptureBuffer } from './capture-buffer';
import { CAPTURE_PROCESSOR_NAME } from './capture-worklet';
import type { MediaEnvironment } from './environment';

/** Default URL of the capture worklet module. */
export const DEFAULT_CAPTURE_WORKLET_URL = './js/audio-engine-capture-worklet.js';
/** Longest capture of one session (s, FR-010). */
export const MAX_CAPTURE_SECONDS = 1800;

/** Construction parameters of {@link CaptureReceiver}. */
export interface CaptureReceiverOptions {
  readonly context: AudioContext;
  readonly env: MediaEnvironment;
  /** Module URL; default {@link DEFAULT_CAPTURE_WORKLET_URL}. */
  readonly workletUrl?: string;
  /** The session reached its limit (called once). */
  readonly onLimit: () => void;
  /** The processor failed (`processorError`). */
  readonly onError?: (reason: string) => void;
}

interface BlockMessage {
  readonly type?: unknown;
  readonly frame?: unknown;
  readonly channels?: unknown;
}

const modules = new WeakMap<BaseAudioContext, Promise<void>>();

/** Adds the module once per context; a failed load is forgotten so the next call retries. */
function loadModule(context: AudioContext, url: string): Promise<void> {
  let loading = modules.get(context);
  if (loading === undefined) {
    loading = context.audioWorklet.addModule(url).catch((error: unknown) => {
      modules.delete(context);
      throw new Error(`unsupported: The capture worklet could not be loaded (${String(error)}).`, { cause: error });
    });
    modules.set(context, loading);
  }
  return loading;
}

function isBlock(message: BlockMessage | null): message is { type: 'block'; frame: number; channels: Float32Array[] } {
  return message?.type === 'block' && typeof message.frame === 'number' && Array.isArray(message.channels);
}

/** The capture node and the audio of the running session. */
export class CaptureReceiver {
  private node: AudioWorkletNode | null = null;
  private keepAlive: GainNode | null = null;
  private buffer: CaptureBuffer | null = null;
  private channels: 1 | 2 = 1;
  private limitFrames = Infinity;
  private limited = false;

  /** @param options - Context, environment, module URL and callbacks. */
  public constructor(private readonly options: CaptureReceiverOptions) {}

  /** Frames the session spans, gaps included (0 without one). */
  public get capturedFrames(): number {
    const buffer = this.buffer;
    return buffer === null || buffer.empty ? 0 : buffer.endFrame - buffer.firstFrame;
  }

  /** Context frame after the last captured sample (0 without a session). */
  public get endFrame(): number {
    return this.buffer?.endFrame ?? 0;
  }

  /**
   * Connects the capture node to the selected-channel node of the input (replacing an earlier node).
   *
   * @param input - Selected-channel node.
   * @param channels - Channels to capture.
   */
  public async connect(input: AudioNode, channels: 1 | 2): Promise<void> {
    const { context, env } = this.options;
    await loadModule(context, this.options.workletUrl ?? DEFAULT_CAPTURE_WORKLET_URL);
    this.disconnect();
    const node = env.createWorkletNode(context, CAPTURE_PROCESSOR_NAME, {
      numberOfInputs: 1,
      numberOfOutputs: 1,
      outputChannelCount: [1],
      channelCount: channels,
      channelCountMode: 'explicit',
      channelInterpretation: 'discrete',
      processorOptions: { channels },
    });
    node.port.onmessage = (event: MessageEvent): void => {
      this.receive(event.data as BlockMessage | null);
    };
    node.onprocessorerror = (): void => {
      this.options.onError?.('processorError');
    };
    const keepAlive = context.createGain();
    keepAlive.gain.value = 0;
    input.connect(node);
    node.connect(keepAlive).connect(context.destination);
    this.node = node;
    this.keepAlive = keepAlive;
    this.channels = channels;
  }

  /**
   * Starts a session.
   *
   * @param maxSeconds - Session limit in seconds.
   */
  public start(maxSeconds: number): void {
    this.buffer = new CaptureBuffer(this.channels);
    this.limitFrames = Math.round(maxSeconds * this.options.context.sampleRate);
    this.limited = false;
    this.node?.port.postMessage({ type: 'start' });
  }

  /** Stops posting; the flushed tail still arrives until {@link CaptureReceiver.take}. */
  public stop(): void {
    this.node?.port.postMessage({ type: 'stop' });
  }

  /** @returns The session's audio (an empty buffer without a session); the receiver forgets it. */
  public take(): CaptureBuffer {
    const buffer = this.buffer ?? new CaptureBuffer(this.channels);
    this.buffer = null;
    return buffer;
  }

  /** Drops the session's audio. */
  public release(): void {
    this.buffer = null;
  }

  /** Disconnects the capture node (no-op without one). */
  public disconnect(): void {
    [this.node, this.keepAlive].forEach((node) => node?.disconnect());
    if (this.node !== null) {
      this.node.port.onmessage = null;
    }
    this.node = null;
    this.keepAlive = null;
  }

  private receive(message: BlockMessage | null): void {
    if (isBlock(message)) {
      this.accept(message);
    }
  }

  /** Appends a batch, cut at the session limit. */
  private accept(block: { frame: number; channels: Float32Array[] }): void {
    const buffer = this.limited ? null : this.buffer;
    if (buffer === null) {
      return;
    }
    const limitEnd = (buffer.empty ? block.frame : buffer.firstFrame) + this.limitFrames;
    const allowed = Math.max(0, limitEnd - block.frame);
    buffer.append(block.frame, block.channels.map((channel) => channel.subarray(0, allowed)));
    if (buffer.endFrame >= limitEnd) {
      this.limited = true;
      this.options.onLimit();
    }
  }
}

/**
 * The real {@link TimeStretchStage}: an `AudioWorkletNode` running `stemstudio-time-stretch` with the bytes of
 * `soundtouch.wasm` (audio-engine.md 2.3.0 "Loading and engagement"). The processor module and the WASM bytes are
 * loaded once per engine and only when the first non-neutral `setPractice` needs the stage.
 *
 * @packageDocumentation
 */
import type { TimeStretchOptions, TimeStretchStage } from '../types';
import { STRETCH_DELAY_FRAMES, STRETCH_PROCESSOR_NAME } from './constants';

/** Default URL of the processor module. */
export const DEFAULT_PROCESSOR_URL = './js/time-stretch-processor.js';
/** Default URL of the SoundTouch module. */
export const DEFAULT_WASM_URL = './js/soundtouch/soundtouch.wasm';
/** Prefix of every load failure (contract 2.3.0 "Validation and errors"). */
export const UNAVAILABLE_PREFIX = 'Time stretch unavailable: ';

/** A stage that reports failures after it became ready (processor error); the engine sets the handler. */
export interface FailureAware {
  /** Called with a reason when the processor fails after `ready`. */
  onfailure: ((reason: string) => void) | null;
}

/** Creates the worklet node; a test seam (default `new AudioWorkletNode`). */
export type WorkletNodeFactory = (context: AudioContext, name: string, options: AudioWorkletNodeOptions) => AudioWorkletNode;

/** Construction parameters of {@link createWorkletStageFactory}. */
export interface WorkletStageOptions extends TimeStretchOptions {
  /** `fetch` used for the WASM bytes. */
  readonly fetch: typeof fetch;
  /** Worklet node factory; default `new AudioWorkletNode(...)`. */
  readonly createNode?: WorkletNodeFactory;
}

/** Stage over one worklet node; port messages of the processor protocol. */
class WorkletStage implements TimeStretchStage, FailureAware {
  public onfailure: ((reason: string) => void) | null = null;
  private count = 0;

  public constructor(
    private readonly node: AudioWorkletNode,
    public readonly latencyFrames: number,
  ) {}

  public get input(): AudioNode {
    return this.node;
  }

  public get output(): AudioNode {
    return this.node;
  }

  public setPitch(pitchFactor: number, atFrame: number): void {
    this.node.port.postMessage({ type: 'pitch', value: pitchFactor, atFrame });
  }

  public reset(): void {
    this.node.port.postMessage({ type: 'reset' });
  }

  public underruns(): number {
    return this.count;
  }

  public dispose(): void {
    this.node.port.postMessage({ type: 'dispose' });
    this.node.disconnect();
  }

  /** Handles a processor message after `ready`. */
  public receive(message: ProcessorMessage): void {
    if (message.type === 'stats') {
      this.count = Number(message.underruns);
    } else if (message.type === 'error') {
      this.fail(String(message.message));
    }
  }

  public fail(reason: string): void {
    this.onfailure?.(reason);
  }
}

interface ProcessorMessage {
  readonly type?: unknown;
  readonly latencyFrames?: unknown;
  readonly underruns?: unknown;
  readonly message?: unknown;
}

/** Caches a successful promise; a failed one is dropped so the next call retries. */
function once<T>(load: () => Promise<T>): () => Promise<T> {
  let cached: Promise<T> | null = null;
  return () => {
    cached ??= load().catch((error: unknown) => {
      cached = null;
      throw error;
    });
    return cached;
  };
}

async function fetchBytes(fetchFn: typeof fetch, url: string): Promise<ArrayBuffer> {
  const response = await fetchFn(url);
  if (!response.ok) {
    throw new Error(`${url} returned HTTP ${String(response.status)}`);
  }
  return response.arrayBuffer();
}

function reasonOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

const defaultNode: WorkletNodeFactory = (context, name, options) => new AudioWorkletNode(context, name, options);

/** Port handshake: resolves the stage on `ready`, rejects on an earlier error; later messages go to the stage. */
class Handshake {
  private stage: WorkletStage | null = null;

  public constructor(
    private readonly node: AudioWorkletNode,
    private readonly settle: { resolve: (stage: WorkletStage) => void; reject: (error: Error) => void },
  ) {
    node.port.onmessage = (event: MessageEvent) => {
      this.message((event.data ?? {}) as ProcessorMessage);
    };
    node.onprocessorerror = () => {
      this.fail('processor error');
    };
  }

  private message(message: ProcessorMessage): void {
    if (this.stage !== null) {
      this.stage.receive(message);
    } else if (message.type === 'ready') {
      this.stage = new WorkletStage(this.node, Number(message.latencyFrames));
      this.settle.resolve(this.stage);
    } else if (message.type === 'error') {
      this.fail(String(message.message));
    }
  }

  private fail(reason: string): void {
    if (this.stage === null) {
      this.node.disconnect();
      this.settle.reject(new Error(reason));
    } else {
      this.stage.fail(reason);
    }
  }
}

function awaitReady(node: AudioWorkletNode): Promise<WorkletStage> {
  return new Promise((resolve, reject) => {
    new Handshake(node, { resolve, reject });
  });
}

/**
 * @param options - URLs, `fetch` and the node factory.
 * @returns The engine's stage factory: loads the processor module and the WASM bytes once (retried after a failure)
 *   and creates one ready stage per call; rejects with `Time stretch unavailable: <reason>`.
 */
export function createWorkletStageFactory(options: WorkletStageOptions): (context: AudioContext) => Promise<TimeStretchStage> {
  const processorUrl = options.processorUrl ?? DEFAULT_PROCESSOR_URL;
  const wasmUrl = options.wasmUrl ?? DEFAULT_WASM_URL;
  const createNode = options.createNode ?? defaultNode;
  const bytes = once(() => fetchBytes(options.fetch, wasmUrl));
  let module: (() => Promise<void>) | null = null;
  return async (context) => {
    try {
      const worklet = (context as Partial<Pick<AudioContext, 'audioWorklet'>>).audioWorklet;
      if (worklet === undefined) {
        throw new Error('AudioWorklet is not supported');
      }
      module ??= once(() => worklet.addModule(processorUrl));
      const [, wasmBytes] = await Promise.all([module(), bytes()]);
      const node = createNode(context, STRETCH_PROCESSOR_NAME, nodeOptions(wasmBytes));
      return await awaitReady(node);
    } catch (error: unknown) {
      throw new Error(UNAVAILABLE_PREFIX + reasonOf(error), { cause: error });
    }
  };
}

function nodeOptions(wasmBytes: ArrayBuffer): AudioWorkletNodeOptions {
  return {
    numberOfInputs: 1,
    numberOfOutputs: 1,
    outputChannelCount: [2],
    channelCount: 2,
    channelCountMode: 'explicit',
    processorOptions: { wasmBytes, delayFrames: STRETCH_DELAY_FRAMES },
  };
}

/**
 * AudioWorklet processor `stemstudio-time-stretch` (audio-engine.md 2.3.0 "Stretch stage"): one stereo SoundTouch
 * instance on the summed mix behind a fixed delay D. Built as the second Vite entry `time-stretch-processor.js`
 * (MIT); SoundTouch itself (LGPL-2.1) is the separate `soundtouch.wasm` whose bytes arrive in
 * `processorOptions.wasmBytes`.
 *
 * Messages (JSON on `port`): main -> processor `{type: 'pitch', value, atFrame}`, `{type: 'reset'}`,
 * `{type: 'dispose'}`; processor -> main `{type: 'ready', latencyFrames}`, `{type: 'error', message}`,
 * `{type: 'stats', underruns}` (at most 4 Hz). Until `ready` the processor outputs silence.
 *
 * @packageDocumentation
 */
import { SoundTouchStream, instantiateSoundTouch, type AbiLoader, type SoundTouchExports } from './abi';
import { RENDER_QUANTUM_FRAMES, STRETCH_DELAY_FRAMES, STRETCH_PROCESSOR_NAME } from './constants';
import { PitchQueue, StretchCore, type StereoBlock } from './core';

/** The processor as the scope sees it. */
export interface StretchProcessor {
  /** Message port to the `AudioWorkletNode`. */
  readonly port: MessagePort;
  /** Renders one quantum; false once disposed. */
  process(inputs: Float32Array[][], outputs: Float32Array[][]): boolean;
}

/** Constructor registered with `registerProcessor`. */
export type ProcessorConstructor = new (options: { readonly processorOptions?: unknown }) => StretchProcessor;

/** The parts of `AudioWorkletGlobalScope` the processor uses. */
export interface WorkletScope {
  /** Base class providing `port`. */
  readonly AudioWorkletProcessor: new () => { readonly port: MessagePort };
  /** Registers a processor class under a name. */
  registerProcessor(name: string, processor: ProcessorConstructor): void;
  /** Context frame of the current render quantum. */
  readonly currentFrame: number;
  /** Context sample rate. */
  readonly sampleRate: number;
}

/** `processorOptions` of the node (created by `worklet-stage.ts`). */
export interface StretchProcessorOptions {
  /** Bytes of `soundtouch.wasm`. */
  readonly wasmBytes: BufferSource;
  /** Fixed delay D; default {@link STRETCH_DELAY_FRAMES}. */
  readonly delayFrames?: number;
}

/** A message of the main thread (fields of every type; unused ones are absent). */
interface StageMessage {
  readonly type?: unknown;
  readonly value?: unknown;
  readonly atFrame?: unknown;
}

const SILENCE = new Float32Array(RENDER_QUANTUM_FRAMES);
const SCRATCH = new Float32Array(RENDER_QUANTUM_FRAMES);

/** Loading, messages, statistics and lifetime of one processor instance. */
class ProcessorControl {
  private readonly queue = new PitchQueue();
  private readonly delay: number;
  private core: StretchCore | null = null;
  private disposed = false;
  private postedUnderruns = 0;
  private statsFrame = -Infinity;
  private readonly handlers = new Map<string, (message: StageMessage) => void>([
    ['pitch', (m) => {
      this.queue.add({ value: Number(m.value), atFrame: Number(m.atFrame) });
    }],
    ['reset', () => this.core?.reset()],
    ['dispose', () => {
      this.dispose();
    }],
  ]);

  public constructor(
    private readonly scope: WorkletScope,
    private readonly port: MessagePort,
    options: StretchProcessorOptions,
  ) {
    this.delay = options.delayFrames ?? STRETCH_DELAY_FRAMES;
    port.onmessage = (event: MessageEvent) => {
      this.receive(event.data);
    };
  }

  public load(loader: AbiLoader, bytes: BufferSource): void {
    loader(bytes)
      .then((abi) => {
        this.start(abi);
      })
      .catch((error: unknown) => {
        this.port.postMessage({ type: 'error', message: error instanceof Error ? error.message : String(error) });
      });
  }

  public process(inputs: Float32Array[][], outputs: Float32Array[][]): boolean {
    const output = outputBlock(outputs);
    if (this.disposed || this.core === null) {
      output.left.fill(0);
      output.right.fill(0);
      return !this.disposed;
    }
    this.core.process(inputBlock(inputs), output, this.scope.currentFrame);
    this.postStats(this.core.underruns);
    return true;
  }

  private start(abi: SoundTouchExports): void {
    if (this.disposed) {
      return;
    }
    const stream = SoundTouchStream.create(abi, this.scope.sampleRate);
    this.core = new StretchCore(stream, this.queue, this.delay);
    this.port.postMessage({ type: 'ready', latencyFrames: this.delay });
  }

  private receive(data: unknown): void {
    const message = (data ?? {}) as StageMessage;
    this.handlers.get(String(message.type))?.(message);
  }

  private dispose(): void {
    if (!this.disposed) {
      this.disposed = true;
      this.core?.dispose();
    }
  }

  private postStats(underruns: number): void {
    const frame = this.scope.currentFrame;
    if (underruns !== this.postedUnderruns && frame - this.statsFrame >= this.scope.sampleRate / 4) {
      this.postedUnderruns = underruns;
      this.statsFrame = frame;
      this.port.postMessage({ type: 'stats', underruns });
    }
  }
}

function inputBlock(inputs: Float32Array[][]): StereoBlock {
  const channels = inputs[0] ?? [];
  const left = channels[0] ?? SILENCE;
  return { left, right: channels[1] ?? left };
}

function outputBlock(outputs: Float32Array[][]): StereoBlock {
  const channels = outputs[0] ?? [];
  return { left: channels[0] ?? SCRATCH, right: channels[1] ?? SCRATCH };
}

/**
 * Defines the processor class on `scope` and registers it as `stemstudio-time-stretch`.
 *
 * @param scope - The AudioWorkletGlobalScope (or a test fake).
 * @param loader - Instantiates the WASM bytes; default {@link instantiateSoundTouch}.
 */
export function registerTimeStretchProcessor(scope: WorkletScope, loader: AbiLoader = instantiateSoundTouch): void {
  class TimeStretchProcessor extends scope.AudioWorkletProcessor implements StretchProcessor {
    private readonly control: ProcessorControl;

    public constructor(options: { readonly processorOptions?: unknown }) {
      super();
      const processorOptions = options.processorOptions as StretchProcessorOptions;
      this.control = new ProcessorControl(scope, this.port, processorOptions);
      this.control.load(loader, processorOptions.wasmBytes);
    }

    public process(inputs: Float32Array[][], outputs: Float32Array[][]): boolean {
      return this.control.process(inputs, outputs);
    }
  }
  scope.registerProcessor(STRETCH_PROCESSOR_NAME, TimeStretchProcessor);
}

const globalScope = globalThis as unknown as Partial<WorkletScope>;
if (typeof globalScope.registerProcessor === 'function') {
  registerTimeStretchProcessor(globalThis as unknown as WorkletScope);
}

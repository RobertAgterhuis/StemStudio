/**
 * AudioWorklet processor `stemstudio-capture` (contract 2.5.0, research R-01), built as its own Vite entry
 * `audio-engine-capture-worklet.js` (`vite.capture.config.ts`).
 *
 * Final behaviour (audio-engine lane): copy each 128-frame render quantum of the selected input channel(s) and post a
 * batch every 2,048 frames as `{ type: 'block', frame, channels: Float32Array[] }`, where `frame` is the global
 * `currentFrame` of the batch's first frame. Phase 0 skeleton: the processor registers, keeps the node alive and posts
 * nothing.
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
  process(inputs: Float32Array[][]): boolean;
}

/** The parts of `AudioWorkletGlobalScope` the capture processor uses. */
export interface CaptureScope {
  /** Base class providing `port`. */
  readonly AudioWorkletProcessor: new () => { readonly port: MessagePort };
  /** Registers a processor class under a name. */
  registerProcessor(name: string, processor: new () => CaptureProcessor): void;
}

/**
 * Registers the capture processor in a worklet scope (or a test fake).
 *
 * @param scope - The worklet scope.
 */
export function registerCaptureProcessor(scope: CaptureScope): void {
  class StemStudioCaptureProcessor extends scope.AudioWorkletProcessor implements CaptureProcessor {
    public process(): boolean {
      return true;
    }
  }
  scope.registerProcessor(CAPTURE_PROCESSOR_NAME, StemStudioCaptureProcessor);
}

const globalScope = globalThis as unknown as Partial<CaptureScope>;
if (typeof globalScope.registerProcessor === 'function') {
  registerCaptureProcessor(globalThis as unknown as CaptureScope);
}

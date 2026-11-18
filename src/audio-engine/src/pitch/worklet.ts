/**
 * AudioWorklet processor `stemstudio-pitch-tracker` (contract 2.6.0, feature 016), built as its own Vite entry
 * `audio-engine-pitch-worklet.js` (`vite.pitch.config.ts`). Architect skeleton (T004): the processor is registered
 * and silent; the audio-engine lane adds the YIN tracking of research R-05 and the messages of data-model 3.2 (T100).
 *
 * @packageDocumentation
 */

/** Registered processor name. */
export const PITCH_PROCESSOR_NAME = 'stemstudio-pitch-tracker';

/** The processor as the scope sees it. */
export interface PitchProcessor {
  /** Handles one render quantum; true keeps the processor alive. */
  process(inputs: Float32Array[][]): boolean;
}

/** The parts of `AudioWorkletGlobalScope` the pitch processor uses. */
export interface PitchScope {
  /** Registers a processor class under a name. */
  registerProcessor(name: string, processor: new () => PitchProcessor): void;
}

/** Skeleton processor: consumes its input and posts nothing yet. */
class StemStudioPitchTracker implements PitchProcessor {
  /**
   * Keeps the node alive.
   *
   * @param inputs - The render quantum's inputs (read from T100 on).
   * @returns Always true.
   */
  public process(inputs: Float32Array[][]): boolean {
    return inputs.length >= 0;
  }
}

/**
 * Registers the processor in a worklet scope.
 *
 * @param scope - The `AudioWorkletGlobalScope` (or a test fake).
 */
export function registerPitchProcessor(scope: PitchScope): void {
  scope.registerProcessor(PITCH_PROCESSOR_NAME, StemStudioPitchTracker);
}

const globalScope = globalThis as unknown as Partial<PitchScope>;
if (typeof globalScope.registerProcessor === 'function') {
  registerPitchProcessor(globalThis as unknown as PitchScope);
}

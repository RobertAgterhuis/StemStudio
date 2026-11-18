/**
 * AudioWorklet processor `stemstudio-pitch-tracker` (contract 2.6.0, feature 016), built as its own Vite entry
 * `audio-engine-pitch-worklet.js` (`vite.pitch.config.ts`). It runs the shared YIN core (research R-05) on the first
 * channel of its input and posts `{ t, cents, clarity }` per 10 ms hop (data-model 3.2), `t` being the context time of
 * the window centre: `blockEnd - (decimated samples after the window + 256) / 16000`. `{ type: 'stop' }` ends the
 * processor. It has no output: no microphone audio leaves the engine.
 *
 * @packageDocumentation
 */
import { DECIMATED_RATE } from './decimate';
import { PitchTracker, YIN, type PitchFrame } from './yin';

/** Registered processor name. */
export const PITCH_PROCESSOR_NAME = 'stemstudio-pitch-tracker';

/** The processor as the scope sees it. */
export interface PitchProcessor {
  /** Handles one render quantum; false ends the processor. */
  process(inputs: Float32Array[][]): boolean;
}

/** The parts of `AudioWorkletGlobalScope` the pitch processor uses. */
export interface PitchScope {
  /** Base class providing `port`. */
  readonly AudioWorkletProcessor: new () => { readonly port: MessagePort };
  /** Context time of the current render quantum (s). */
  readonly currentTime: number;
  /** Context sample rate (Hz). */
  readonly sampleRate: number;
  /** Registers a processor class under a name. */
  registerProcessor(name: string, processor: new () => PitchProcessor): void;
}

/** Half a YIN window in seconds at 16 kHz (window centre). */
const HALF_WINDOW_SECONDS = YIN.window / 2 / DECIMATED_RATE;

/**
 * Registers the processor in a worklet scope.
 *
 * @param scope - The `AudioWorkletGlobalScope` (or a test fake).
 */
export function registerPitchProcessor(scope: PitchScope): void {
  class StemStudioPitchTracker extends scope.AudioWorkletProcessor implements PitchProcessor {
    private readonly tracker = new PitchTracker(scope.sampleRate);
    private readonly frames: PitchFrame[] = [];
    private running = true;

    public constructor() {
      super();
      this.port.onmessage = (event: MessageEvent): void => {
        if ((event.data as { type?: unknown } | null)?.type === 'stop') {
          this.running = false;
        }
      };
    }

    public process(inputs: Float32Array[][]): boolean {
      const channel = inputs[0]?.[0];
      if (this.running && channel !== undefined) {
        this.tracker.process(channel, (frame) => this.frames.push(frame));
        this.post(scope.currentTime + channel.length / scope.sampleRate);
      }
      return this.running;
    }

    private post(blockEnd: number): void {
      this.frames.splice(0).forEach((frame) => {
        const t = blockEnd - (this.tracker.samples - frame.endSample) / DECIMATED_RATE - HALF_WINDOW_SECONDS;
        this.port.postMessage({ t, cents: frame.cents, clarity: frame.clarity });
      });
    }
  }
  scope.registerProcessor(PITCH_PROCESSOR_NAME, StemStudioPitchTracker);
}

const globalScope = globalThis as unknown as Partial<PitchScope>;
if (typeof globalScope.registerProcessor === 'function') {
  registerPitchProcessor(globalThis as unknown as PitchScope);
}

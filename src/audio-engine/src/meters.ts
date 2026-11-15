/**
 * Post-fader peak meters drawn by the engine on the shared animation-frame loop into DOM
 * elements registered by id (no meter data crosses the .NET interop boundary).
 *
 * @packageDocumentation
 */
import { FrameLoop, type FrameScheduler } from './frame-loop';
import type { MeterTarget } from './types';

export { browserFrames, type FrameScheduler } from './frame-loop';

/** Lowest displayed level (dBFS). */
export const METER_FLOOR_DB = -60;
/** Fall-back speed of the displayed level after a peak. */
export const DECAY_DB_PER_SECOND = 20;
/** How long `data-clip` stays `"true"` after a sample with `|x| >= 1.0` (ms). */
export const CLIP_HOLD_MS = 1500;

/** Construction parameters of {@link Meters}. */
export interface MetersOptions {
  /** Meter tap of a target, undefined when the target does not exist (any more). */
  readonly source: (target: MeterTarget) => AnalyserNode | undefined;
  /** Shared frame loop (the engine's); defaults to a private loop over `frames`. */
  readonly loop?: FrameLoop;
  /** Frame scheduler of the private loop; defaults to `browserFrames`. */
  readonly frames?: FrameScheduler;
  /** Document holding the meter elements; defaults to `globalThis.document`. */
  readonly document?: Document;
}

interface MeterState {
  elementId: string;
  db: number;
  clipUntil: number;
}

/**
 * Largest absolute sample.
 *
 * @param samples - Time-domain samples.
 * @returns Peak magnitude (0 for an empty block).
 */
export function peakOf(samples: Float32Array): number {
  let peak = 0;
  for (const sample of samples) {
    peak = Math.max(peak, Math.abs(sample));
  }
  return peak;
}

/**
 * @param peak - Peak magnitude.
 * @returns `20*log10(peak)` clamped to {@link METER_FLOOR_DB}..0.
 */
export function peakToDb(peak: number): number {
  const db = peak > 0 ? 20 * Math.log10(peak) : METER_FLOOR_DB;
  return Math.min(0, Math.max(METER_FLOOR_DB, db));
}

/** Registry of meters, drawn on every frame of the (shared) frame loop while any is attached. */
export class Meters {
  private readonly meters = new Map<MeterTarget, MeterState>();
  private readonly loop: FrameLoop;
  private readonly doc: Document;
  private lastFrame: number | undefined;
  private buffer = new Float32Array(0);

  /** @param options - Analyser lookup, frame loop (or scheduler) and document. */
  public constructor(private readonly options: MetersOptions) {
    this.doc = options.document ?? globalThis.document;
    this.loop = options.loop ?? new FrameLoop(privateLoopOptions(options, this.doc));
  }

  /**
   * Binds a meter to an element id and starts the frame loop if needed.
   *
   * @param target - Channel id (stem or `<parent>.<detail>`) or `"master"`.
   * @param elementId - Id of the element receiving `--meter-level`, `data-peak-db`, `data-clip`.
   * @throws Error when the target has no analyser.
   */
  public attach(target: MeterTarget, elementId: string): void {
    if (this.options.source(target) === undefined) {
      throw new Error(`Unknown meter target "${target}"`);
    }
    this.meters.set(target, { elementId, db: METER_FLOOR_DB, clipUntil: Number.NEGATIVE_INFINITY });
    this.loop.add(this.frame);
  }

  /** @param target - Meter to unbind; the frame loop stops when none is left. */
  public detach(target: MeterTarget): void {
    this.meters.delete(target);
    if (this.meters.size === 0) {
      this.stopLoop();
    }
  }

  /** Unbinds all meters and leaves the frame loop (a private loop is disposed). */
  public dispose(): void {
    this.meters.clear();
    this.stopLoop();
    if (this.options.loop === undefined) {
      this.loop.dispose();
    }
  }

  private readonly frame = (timestamp: number): void => {
    const seconds = (timestamp - (this.lastFrame ?? timestamp)) / 1000;
    this.lastFrame = timestamp;
    this.meters.forEach((state, target) => {
      this.update(target, state, { timestamp, seconds });
    });
  };

  private update(target: MeterTarget, state: MeterState, time: { timestamp: number; seconds: number }): void {
    const analyser = this.options.source(target);
    if (analyser === undefined) {
      return;
    }
    const peak = this.read(analyser);
    state.db = Math.max(peakToDb(peak), state.db - DECAY_DB_PER_SECOND * time.seconds, METER_FLOOR_DB);
    state.clipUntil = peak >= 1 ? time.timestamp + CLIP_HOLD_MS : state.clipUntil;
    const element = this.doc.getElementById(state.elementId);
    if (element !== null) {
      render(element, state, time.timestamp);
    }
  }

  private read(analyser: AnalyserNode): number {
    if (this.buffer.length !== analyser.fftSize) {
      this.buffer = new Float32Array(analyser.fftSize);
    }
    analyser.getFloatTimeDomainData(this.buffer);
    return peakOf(this.buffer);
  }

  private stopLoop(): void {
    this.loop.remove(this.frame);
    this.lastFrame = undefined;
  }
}

function privateLoopOptions(options: MetersOptions, doc: Document): { frames?: FrameScheduler; document: Document } {
  return options.frames === undefined ? { document: doc } : { frames: options.frames, document: doc };
}

function render(element: HTMLElement, state: MeterState, timestamp: number): void {
  element.style.setProperty('--meter-level', String((state.db - METER_FLOOR_DB) / -METER_FLOOR_DB));
  element.dataset.peakDb = String(Math.round(state.db));
  element.dataset.clip = String(timestamp < state.clipUntil);
}

/**
 * Post-fader peak meters drawn by the engine with `requestAnimationFrame` into DOM elements
 * registered by id (no meter data crosses the .NET interop boundary).
 *
 * @packageDocumentation
 */
import type { MeterTarget } from './types';

/** Lowest displayed level (dBFS). */
export const METER_FLOOR_DB = -60;
/** Fall-back speed of the displayed level after a peak. */
export const DECAY_DB_PER_SECOND = 20;
/** How long `data-clip` stays `"true"` after a sample with `|x| >= 1.0` (ms). */
export const CLIP_HOLD_MS = 1500;

/** Animation-frame scheduling, injectable for tests. */
export interface FrameScheduler {
  /** Schedules `callback` for the next frame; returns a handle. */
  request(callback: FrameRequestCallback): number;
  /** Cancels a scheduled frame. */
  cancel(handle: number): void;
}

/** The browser's `requestAnimationFrame` / `cancelAnimationFrame`. */
export const browserFrames: FrameScheduler = {
  request(callback) {
    return requestAnimationFrame(callback);
  },
  cancel(handle) {
    cancelAnimationFrame(handle);
  },
};

/** Construction parameters of {@link Meters}. */
export interface MetersOptions {
  /** Meter tap of a target, undefined when the target does not exist (any more). */
  readonly source: (target: MeterTarget) => AnalyserNode | undefined;
  /** Frame scheduler; defaults to {@link browserFrames}. */
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

/** Registry of meters plus the frame loop that updates them. */
export class Meters {
  private readonly meters = new Map<MeterTarget, MeterState>();
  private readonly frames: FrameScheduler;
  private readonly doc: Document;
  private handle: number | undefined;
  private lastFrame: number | undefined;
  private buffer = new Float32Array(0);

  /** @param options - Analyser lookup, frame scheduler and document. */
  public constructor(private readonly options: MetersOptions) {
    this.frames = options.frames ?? browserFrames;
    this.doc = options.document ?? globalThis.document;
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
    this.handle ??= this.frames.request(this.frame);
  }

  /** @param target - Meter to unbind; the frame loop stops when none is left. */
  public detach(target: MeterTarget): void {
    this.meters.delete(target);
    if (this.meters.size === 0) {
      this.stopLoop();
    }
  }

  /** Unbinds all meters and stops the frame loop. */
  public dispose(): void {
    this.meters.clear();
    this.stopLoop();
  }

  private readonly frame = (timestamp: number): void => {
    const seconds = (timestamp - (this.lastFrame ?? timestamp)) / 1000;
    this.lastFrame = timestamp;
    this.meters.forEach((state, target) => {
      this.update(target, state, { timestamp, seconds });
    });
    this.handle = this.frames.request(this.frame);
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
    if (this.handle !== undefined) {
      this.frames.cancel(this.handle);
    }
    this.handle = undefined;
    this.lastFrame = undefined;
  }
}

function render(element: HTMLElement, state: MeterState, timestamp: number): void {
  element.style.setProperty('--meter-level', String((state.db - METER_FLOOR_DB) / -METER_FLOOR_DB));
  element.dataset.peakDb = String(Math.round(state.db));
  element.dataset.clip = String(timestamp < state.clipUntil);
}

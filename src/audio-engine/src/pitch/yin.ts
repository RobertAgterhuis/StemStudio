/**
 * YIN pitch core (de Cheveigné & Kawahara 2002; research R-05 parameters, binding): 16 kHz input, window 512 samples
 * (32 ms), hop 160 (10 ms), lags 14..246 (65-1143 Hz), absolute threshold 0.15 on the cumulative-mean-normalised
 * difference, parabolic interpolation; voiced when the window RMS is at least -45 dBFS and the minimum normalised
 * difference is at most 0.25. Pure code: no Web Audio types, shared by the pitch worklet and `scoreTake`.
 *
 * @packageDocumentation
 */
import { DECIMATED_RATE, Decimator } from './decimate';

/** The binding parameters. */
export const YIN = {
  rate: DECIMATED_RATE,
  window: 512,
  hop: 160,
  tauMin: 14,
  tauMax: 246,
  threshold: 0.15,
  voicedDb: -45,
  maxDifference: 0.25,
} as const;

/** Lowest window RMS of a voiced frame (linear). */
const MIN_RMS = 10 ** (YIN.voicedDb / 20);
/** Highest pitch value of a contour (centi-semitones). */
const MAX_CENTS = 12_700;
/** Samples integrated per lag. */
const INTEGRATION = YIN.window - YIN.tauMax;

/** One analysed window. */
export interface PitchEstimate {
  /** Centi-semitones (MIDI x 100), or null when unvoiced. */
  readonly cents: number | null;
  /** 1 - the normalised difference at the chosen lag (0..1); 0 for a window below the level gate. */
  readonly clarity: number;
}

/** One frame of the tracker. */
export interface PitchFrame extends PitchEstimate {
  /** Decimated (16 kHz) samples consumed when the frame's window ended; its centre is 256 samples earlier. */
  readonly endSample: number;
}

/**
 * @param hz - Frequency.
 * @returns Centi-semitones (MIDI x 100): `1200 log2(hz / 440) + 6900`.
 */
export function hzToCents(hz: number): number {
  return 1200 * Math.log2(hz / 440) + 6900;
}

function rms(window: Float32Array): number {
  let sum = 0;
  for (const v of window) {
    sum += v * v;
  }
  return Math.sqrt(sum / window.length);
}

/** Cumulative-mean-normalised difference d'(tau) for tau 0..tauMax into `out`. */
function normalisedDifference(window: Float32Array, out: Float64Array): void {
  let running = 0;
  out[0] = 1;
  for (let tau = 1; tau <= YIN.tauMax; tau += 1) {
    let sum = 0;
    for (let j = 0; j < INTEGRATION; j += 1) {
      const delta = Number(window[j]) - Number(window[j + tau]);
      sum += delta * delta;
    }
    running += sum;
    out[tau] = running === 0 ? 1 : (sum * tau) / running;
  }
}

/** The first lag under the threshold, walked down to its local minimum; else the global minimum. */
function chooseLag(d: Float64Array): number {
  const first: number = YIN.tauMin;
  let best = first;
  for (let tau = first; tau <= YIN.tauMax; tau += 1) {
    if (Number(d[tau]) < YIN.threshold) {
      return localMinimum(d, tau);
    }
    best = Number(d[tau]) < Number(d[best]) ? tau : best;
  }
  return best;
}

function localMinimum(d: Float64Array, start: number): number {
  let tau = start;
  while (tau < YIN.tauMax && Number(d[tau + 1]) < Number(d[tau])) {
    tau += 1;
  }
  return tau;
}

/**
 * Parabolic interpolation of the lag around `tau`, only inside a valley (`d[tau]` at most both neighbours) and by at
 * most one lag (exported for tests).
 *
 * @param d - Normalised difference.
 * @param tau - Chosen lag.
 * @returns The refined lag.
 */
export function refineLag(d: Float64Array, tau: number): number {
  if (tau <= 1 || tau >= YIN.tauMax) {
    return tau;
  }
  const [a, b, c] = [Number(d[tau - 1]), Number(d[tau]), Number(d[tau + 1])];
  const denominator = a - 2 * b + c;
  // on a slope (e.g. at tauMin) the parabola's vertex lies outside the bracket: keep the lag
  return isValley(a, b, c) && denominator !== 0 ? tau + Math.min(Math.max((a - c) / (2 * denominator), -1), 1) : tau;
}

function isValley(a: number, b: number, c: number): boolean {
  return b <= a && b <= c;
}

/**
 * @param tau - Refined lag in 16 kHz samples.
 * @returns Its pitch in centi-semitones, or null when not finite or outside 0..12700 (exported for tests).
 */
export function lagToCents(tau: number): number | null {
  const cents = tau > 0 ? hzToCents(YIN.rate / tau) : Number.NaN;
  return Number.isFinite(cents) && cents >= 0 && cents <= MAX_CENTS ? cents : null;
}

/**
 * Analyses one 512-sample window at 16 kHz.
 *
 * @param window - The window.
 * @param scratch - Reusable buffer of `tauMax + 1` values.
 * @returns The estimate.
 */
export function analyseWindow(window: Float32Array, scratch: Float64Array): PitchEstimate {
  if (rms(window) < MIN_RMS) {
    return { cents: null, clarity: 0 };
  }
  normalisedDifference(window, scratch);
  const tau = chooseLag(scratch);
  const difference = Number(scratch[tau]);
  const clarity = Math.min(Math.max(1 - difference, 0), 1);
  return { cents: difference <= YIN.maxDifference ? lagToCents(refineLag(scratch, tau)) : null, clarity };
}

/** Streams input at any rate >= 16 kHz and posts one {@link PitchFrame} per hop once the first window is full. */
export class PitchTracker {
  private readonly decimator: Decimator;
  private readonly ring = new Float32Array(YIN.window);
  private readonly window = new Float32Array(YIN.window);
  private readonly scratch = new Float64Array(YIN.tauMax + 1);
  private count = 0;

  /** @param rate - Input sample rate (Hz). */
  public constructor(rate: number) {
    this.decimator = new Decimator(rate);
  }

  /** Decimated samples consumed so far. */
  public get samples(): number {
    return this.count;
  }

  /**
   * @param input - Next input samples (mono, any block size).
   * @param emit - Receives every finished frame in order.
   */
  public process(input: Float32Array, emit: (frame: PitchFrame) => void): void {
    this.decimator.process(input, (sample) => {
      this.ring[this.count % YIN.window] = sample;
      this.count += 1;
      if (this.count >= YIN.window && (this.count - YIN.window) % YIN.hop === 0) {
        emit({ ...analyseWindow(this.ordered(), this.scratch), endSample: this.count });
      }
    });
  }

  /** The last window in time order. */
  private ordered(): Float32Array {
    const split = this.count % YIN.window;
    this.window.set(this.ring.subarray(split));
    this.window.set(this.ring.subarray(0, split), YIN.window - split);
    return this.window;
  }
}

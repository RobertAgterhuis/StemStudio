/**
 * Decimation of the pitch input to 16 kHz (research R-05, binding): a 31-tap windowed-sinc (Blackman) low-pass with a
 * fractional-phase resampler, so any context rate (44.1, 48, 96 kHz) works. Rates of 64 kHz and more are first halved
 * with a 31-tap low-pass (the single stage's transition band would be too wide there). Pure code: no Web Audio types,
 * shared by the pitch worklet and `scoreTake`.
 *
 * @packageDocumentation
 */

/** Output rate (Hz). */
export const DECIMATED_RATE = 16_000;
/** Filter taps. */
export const TAPS = 31;
/** Pass-band edge of the resampler (Hz). */
const CUTOFF_HZ = 7000;
/** Fractional phases of the polyphase table. */
const PHASES = 64;
/** Half the taps. */
const HALF = (TAPS - 1) / 2;
/** Ring buffer size (power of two, > taps + the largest step). */
const RING = 64;
/** Rates from which a half-band stage runs first. */
const HALVE_FROM = 64_000;

function blackman(t: number): number {
  const x = (2 * Math.PI * t) / (TAPS + 1);
  return 0.42 + 0.5 * Math.cos(x) + 0.08 * Math.cos(2 * x);
}

function sinc(x: number): number {
  return x === 0 ? 1 : Math.sin(Math.PI * x) / (Math.PI * x);
}

/**
 * @param cutoff - Cut-off in cycles per input sample.
 * @param frac - Fractional delay 0..1 of the output point after the base sample.
 * @returns The normalised taps.
 */
function kernel(cutoff: number, frac: number): Float64Array {
  const taps = new Float64Array(TAPS);
  for (let k = 0; k < TAPS; k += 1) {
    const t = k - HALF - frac;
    taps[k] = 2 * cutoff * sinc(2 * cutoff * t) * blackman(t);
  }
  const sum = taps.reduce((s, v) => s + v, 0);
  return taps.map((v) => v / sum);
}

/** A ring of the last input samples and their count. */
class History {
  private readonly ring = new Float64Array(RING);
  private count = 0;

  /** Samples received. */
  public get length(): number {
    return this.count;
  }

  public push(sample: number): void {
    this.ring[this.count & (RING - 1)] = sample;
    this.count += 1;
  }

  /** Dot product of the taps with the samples centred on input index `base`. */
  public convolve(base: number, taps: Float64Array, offset: number): number {
    let sum = 0;
    for (let k = 0; k < TAPS; k += 1) {
      sum += Number(this.ring[(base - HALF + k) & (RING - 1)]) * Number(taps[offset + k]);
    }
    return sum;
  }
}

/** Integer decimation by two with a 31-tap low-pass at 0.2 cycles per sample. */
class Halver {
  private readonly history = new History();
  private readonly taps = kernel(0.2, 0);

  public push(sample: number, emit: (sample: number) => void): void {
    this.history.push(sample);
    const base = this.history.length - 1 - HALF;
    if (base >= 0 && base % 2 === 0) {
      emit(this.history.convolve(base, this.taps, 0));
    }
  }
}

/** Fractional-phase resampler to {@link DECIMATED_RATE}. */
class Resampler {
  private readonly history = new History();
  private readonly table: Float64Array;
  private readonly step: number;
  private produced = 0;

  public constructor(rate: number) {
    this.step = rate / DECIMATED_RATE;
    const cutoff = Math.min(CUTOFF_HZ / rate, 0.5);
    this.table = new Float64Array(PHASES * TAPS);
    for (let p = 0; p < PHASES; p += 1) {
      this.table.set(kernel(cutoff, p / PHASES), p * TAPS);
    }
  }

  public push(sample: number, emit: (sample: number) => void): void {
    this.history.push(sample);
    for (;;) {
      const centre = this.produced * this.step;
      const phase = Math.round((centre - Math.floor(centre)) * PHASES);
      const base = Math.floor(centre) + Math.floor(phase / PHASES);
      if (base + HALF > this.history.length - 1) {
        return;
      }
      emit(this.history.convolve(base, this.table, (phase % PHASES) * TAPS));
      this.produced += 1;
    }
  }
}

/** Streams any input rate >= 16 kHz down to 16 kHz. */
export class Decimator {
  private readonly halver: Halver | null;
  private readonly resampler: Resampler;

  /**
   * @param rate - Input sample rate (Hz).
   * @throws Error `Pitch input rate must be at least 16000 Hz`.
   */
  public constructor(rate: number) {
    if (!(rate >= DECIMATED_RATE)) {
      throw new Error(`Pitch input rate must be at least ${String(DECIMATED_RATE)} Hz`);
    }
    this.halver = rate >= HALVE_FROM ? new Halver() : null;
    this.resampler = new Resampler(this.halver === null ? rate : rate / 2);
  }

  /**
   * @param input - Next input samples (any block size).
   * @param emit - Receives every 16 kHz output sample in order.
   */
  public process(input: Float32Array, emit: (sample: number) => void): void {
    const into = (sample: number): void => {
      this.resampler.push(sample, emit);
    };
    for (const sample of input) {
      if (this.halver === null) {
        into(sample);
      } else {
        this.halver.push(sample, into);
      }
    }
  }
}

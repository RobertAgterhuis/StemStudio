/**
 * Loopback latency calibration (research R-04, FR-007): a seeded, irregular schedule of chirp clicks is played on the
 * destination and captured through the input; the latency `L` is the median lag between each scheduled click and its
 * arrival. Pure functions; the engine plays the clicks and records the capture.
 *
 * Search: the normalised cross-correlation of every click window is evaluated on a 4x decimated lag grid and summed
 * over the clicks (one coarse lag for the whole schedule, so an echo or the neighbouring click cannot win), then each
 * click is refined at full rate within +-64 samples of that lag. FFT-free, direct correlation.
 *
 * @packageDocumentation
 */
import type { CalibrationFailure, CalibrationOptions, CalibrationResult } from '../types';

/** Length of the chirp click (s). */
export const CHIRP_SECONDS = 0.005;
/** Capture continues this long after the last click (s); it is also the lag search range. */
export const CALIBRATION_TAIL_SECONDS = 1;
/** Longest accepted latency (s). */
export const MAX_LATENCY_SECONDS = 0.8;
/** Silence floor of reported peaks (dBFS). */
export const SILENCE_DBFS = -120;

const LEAD_IN_SECONDS = 0.5;
const MIN_GAP_SECONDS = 0.45;
const MAX_GAP_SECONDS = 0.75;
const CHIRP_FROM_HZ = 500;
const CHIRP_TO_HZ = 8000;
const CHIRP_PEAK = 10 ** (-6 / 20);
const COARSE_STEP = 4;
const REFINE_RADIUS = 64;
const MIN_CORRELATION = 0.3;
const MIN_PEAK_TO_MEDIAN = 6;
const MIN_FOUND_SHARE = 0.75;
const MIN_FOUND = 6;
const MAX_SPREAD_SAMPLES = 44;
const NO_SIGNAL_DBFS = -60;
const CLIPPED_DBFS = -0.1;
const DEFAULT_CLICKS = 8;

/** Result of {@link estimateLatency}: a {@link CalibrationResult} without the output-latency snapshot. */
export type LatencyEstimate = Omit<CalibrationResult, 'outputLatencySamples'>;

/** Validated {@link CalibrationOptions}. */
export interface CalibrationPlan {
  readonly clicks: number;
  /** PRNG seed, or null for a random one. */
  readonly seed: number | null;
}

/**
 * @param options - Requested options.
 * @returns Clicks (default 8) and seed.
 * @throws Error `Invalid calibration options: ...` for clicks outside 6..12 or a non-integer seed.
 */
export function validateCalibrationOptions(options: CalibrationOptions | undefined): CalibrationPlan {
  const { clicks = DEFAULT_CLICKS, seed = null } = options ?? {};
  return { clicks: checkedClicks(clicks), seed: checkedSeed(seed) };
}

function checkedClicks(clicks: number): number {
  if (!Number.isInteger(clicks) || clicks < 6 || clicks > 12) {
    throw new Error(`Invalid calibration options: clicks must be an integer 6..12 (got ${String(clicks)})`);
  }
  return clicks;
}

function checkedSeed(seed: number | null): number | null {
  if (seed !== null && !Number.isInteger(seed)) {
    throw new Error(`Invalid calibration options: seed must be an integer (got ${String(seed)})`);
  }
  return seed;
}

/** @returns `values[index]`, 0 outside the array. */
function valueAt(values: ArrayLike<number>, index: number): number {
  return values[index] ?? 0;
}

/**
 * @param seed - Integer seed.
 * @returns A deterministic PRNG (mulberry32) returning values in [0, 1).
 */
export function seededRandom(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * @param clicks - Number of clicks.
 * @param seed - PRNG seed.
 * @param sampleRate - Context rate.
 * @returns Click positions in frames after the capture start: a 0.5 s lead-in, then gaps of 0.45..0.75 s.
 */
export function clickSchedule(clicks: number, seed: number, sampleRate: number): number[] {
  const random = seededRandom(seed);
  const schedule: number[] = [];
  let at = LEAD_IN_SECONDS;
  for (let k = 0; k < clicks; k++) {
    schedule.push(Math.round(at * sampleRate));
    at += MIN_GAP_SECONDS + random() * (MAX_GAP_SECONDS - MIN_GAP_SECONDS);
  }
  return schedule;
}

/**
 * @param sampleRate - Context rate.
 * @returns The 5 ms logarithmic chirp 500 Hz -> 8 kHz, Hann-windowed, peak -6 dBFS.
 */
export function chirpTemplate(sampleRate: number): Float32Array {
  const length = Math.round(CHIRP_SECONDS * sampleRate);
  const ratio = CHIRP_TO_HZ / CHIRP_FROM_HZ;
  const raw = Array.from({ length }, (_, i) => {
    const t = i / sampleRate;
    const phase = ((2 * Math.PI * CHIRP_FROM_HZ * CHIRP_SECONDS) / Math.log(ratio)) * (ratio ** (t / CHIRP_SECONDS) - 1);
    const hann = 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (length - 1));
    return Math.sin(phase) * hann;
  });
  const peak = Math.max(...raw.map(Math.abs));
  return Float32Array.from(raw, (x) => (x / peak) * CHIRP_PEAK);
}

/**
 * @param peak - Linear peak.
 * @returns `20 log10(peak)`, floored at {@link SILENCE_DBFS}.
 */
export function peakDbfs(peak: number): number {
  return Math.max(SILENCE_DBFS, 20 * Math.log10(peak));
}

/** Normalised cross-correlation of the template with the capture at one position. */
class Correlator {
  private readonly energy: Float64Array;
  private readonly norm: number;

  public constructor(
    private readonly capture: Float32Array,
    private readonly template: Float32Array,
  ) {
    this.energy = new Float64Array(capture.length + 1);
    capture.forEach((x, i) => {
      this.energy[i + 1] = valueAt(this.energy, i) + x * x;
    });
    this.norm = Math.sqrt(template.reduce((sum, x) => sum + x * x, 0));
  }

  /** @returns The correlation at capture position `at` (0 outside the capture or for silence). */
  public at(at: number): number {
    const power = this.power(at);
    if (power <= 1e-12) {
      return 0;
    }
    let dot = 0;
    this.template.forEach((t, i) => {
      dot += t * valueAt(this.capture, at + i);
    });
    return dot / (this.norm * Math.sqrt(power));
  }

  /** Capture energy under the template at `at`; 0 when the template does not fit. */
  private power(at: number): number {
    const end = at + this.template.length;
    const inside = at >= 0 && end <= this.capture.length;
    return inside ? valueAt(this.energy, end) - valueAt(this.energy, at) : 0;
  }
}

function median(values: readonly number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = sorted.length >> 1;
  return sorted.length % 2 === 1 ? valueAt(sorted, mid) : (valueAt(sorted, mid - 1) + valueAt(sorted, mid)) / 2;
}

/** One click: the refined lag and whether it counts as found. */
interface ClickHit {
  readonly lag: number;
  readonly found: boolean;
}

/** Coarse correlation rows (one per click) on the decimated lag grid. */
function coarseRows(correlator: Correlator, clicks: readonly number[], range: number): number[][] {
  const steps = Math.ceil(range / COARSE_STEP);
  return clicks.map((click) => Array.from({ length: steps }, (_, s) => correlator.at(click + s * COARSE_STEP)));
}

/** Coarse lag of the whole schedule: the maximum of the rows summed over the clicks. */
function coarseLag(rows: readonly (readonly number[])[]): number {
  const sums = (rows[0] ?? []).map((_, s) => rows.reduce((sum, row) => sum + valueAt(row, s), 0));
  const best = sums.reduce((bestIndex, value, s) => (value > valueAt(sums, bestIndex) ? s : bestIndex), 0);
  return best * COARSE_STEP;
}

/** Refines one click around the coarse lag and applies the detection thresholds. */
function refine(correlator: Correlator, click: number, row: readonly number[], around: number): ClickHit {
  let lag = Math.max(0, around - REFINE_RADIUS);
  let peak = -Infinity;
  for (let candidate = lag; candidate <= around + REFINE_RADIUS; candidate++) {
    const value = correlator.at(click + candidate);
    if (value > peak) {
      peak = value;
      lag = candidate;
    }
  }
  const floor = MIN_PEAK_TO_MEDIAN * median(row.map(Math.abs));
  return { lag, found: peak >= MIN_CORRELATION && peak >= floor };
}

function failure(reason: CalibrationFailure, base: Pick<LatencyEstimate, 'inputPeakDbfs' | 'warnings'>, hits: readonly ClickHit[], spread: number | null): LatencyEstimate {
  return { ok: false, latencySamples: null, latencyMilliseconds: null, detectedClicks: hits.length, spreadSamples: spread, reason, ...base };
}

/** Thresholds on the found clicks: count, length, spread. */
function judge(found: readonly ClickHit[], clicks: number, base: Pick<LatencyEstimate, 'inputPeakDbfs' | 'warnings'>, sampleRate: number): LatencyEstimate {
  if (found.length < Math.max(MIN_FOUND, Math.ceil(MIN_FOUND_SHARE * clicks))) {
    return failure('tooFewClicks', base, found, null);
  }
  const lags = found.map((hit) => hit.lag);
  const spread = Math.max(...lags) - Math.min(...lags);
  const latency = Math.round(median(lags));
  if (latency > MAX_LATENCY_SECONDS * sampleRate) {
    return failure('tooLong', base, found, spread);
  }
  if (spread > MAX_SPREAD_SAMPLES) {
    return failure('inconsistent', base, found, spread);
  }
  return { ok: true, latencySamples: latency, latencyMilliseconds: (latency * 1000) / sampleRate, detectedClicks: found.length, spreadSamples: spread, reason: null, ...base };
}

/**
 * Estimates the loopback latency; never throws for a failed measurement.
 *
 * @param capture - Captured input from the capture start (mono).
 * @param clicks - Scheduled click positions in frames after the capture start ({@link clickSchedule}).
 * @param template - The played click ({@link chirpTemplate}).
 * @param sampleRate - Context rate.
 * @returns The estimate, `ok: false` with a reason when the measurement failed.
 */
export function estimateLatency(capture: Float32Array, clicks: readonly number[], template: Float32Array, sampleRate: number): LatencyEstimate {
  const peak = capture.reduce((max, x) => Math.max(max, Math.abs(x)), 0);
  const inputPeakDbfs = peakDbfs(peak);
  const base = { inputPeakDbfs, warnings: inputPeakDbfs >= CLIPPED_DBFS ? (['clipped'] as const) : [] };
  if (inputPeakDbfs < NO_SIGNAL_DBFS) {
    return failure('noSignal', base, [], null);
  }
  const correlator = new Correlator(capture, template);
  const rows = coarseRows(correlator, clicks, Math.round(CALIBRATION_TAIL_SECONDS * sampleRate));
  const around = coarseLag(rows);
  const hits = clicks.map((click, k) => refine(correlator, click, rows[k] ?? [], around));
  return judge(
    hits.filter((hit) => hit.found),
    clicks.length,
    base,
    sampleRate,
  );
}

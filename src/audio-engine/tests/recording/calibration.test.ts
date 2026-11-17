import { describe, expect, it } from 'vitest';
import {
  CALIBRATION_TAIL_SECONDS,
  CHIRP_SECONDS,
  MAX_LATENCY_SECONDS,
  chirpTemplate,
  clickSchedule,
  estimateLatency,
  peakDbfs,
  seededRandom,
  validateCalibrationOptions,
} from '../../src/recording/calibration';

/** Loopback latency calibration (research R-04, FR-007). */
const SR = 44100;
const TEMPLATE = chirpTemplate(SR);
const SCHEDULE = clickSchedule(8, 42, SR);

interface Signal {
  /** Latency of every click (samples), or one per click (jitter); null = click missing. */
  readonly delays: number | readonly (number | null)[];
  /** White noise RMS (linear). */
  readonly noise?: number;
  /** Echo: delay (samples) and gain. */
  readonly echo?: { readonly delay: number; readonly gain: number };
  /** Gain of the captured clicks. */
  readonly gain?: number;
}

/** Synthetic loopback capture of {@link SCHEDULE}. */
function capture(signal: Signal, schedule: readonly number[] = SCHEDULE): Float32Array {
  const last = schedule.at(-1) ?? 0;
  const out = new Float32Array(last + Math.round(CALIBRATION_TAIL_SECONDS * SR));
  const random = seededRandom(7);
  if (signal.noise !== undefined) {
    for (let i = 0; i < out.length; i++) {
      // Sum of uniforms: approximately Gaussian with RMS `noise`.
      out[i] = (random() + random() + random() - 1.5) * 2 * signal.noise;
    }
  }
  schedule.forEach((click, k) => {
    const delay = typeof signal.delays === 'number' ? signal.delays : signal.delays[k];
    if (delay === null || delay === undefined) {
      return;
    }
    add(out, click + delay, signal.gain ?? 1);
    if (signal.echo !== undefined) {
      add(out, click + delay + signal.echo.delay, (signal.gain ?? 1) * signal.echo.gain);
    }
  });
  return out;
}

function add(out: Float32Array, at: number, gain: number): void {
  TEMPLATE.forEach((x, i) => {
    if (at + i < out.length) {
      out[at + i] = (out[at + i] ?? 0) + x * gain;
    }
  });
}

function estimate(signal: Signal, clicks: readonly number[] = SCHEDULE): ReturnType<typeof estimateLatency> {
  return estimateLatency(capture(signal, clicks), clicks, TEMPLATE, SR);
}

describe('click schedule', () => {
  it('is seeded: 8 clicks after a 0.5 s lead-in, gaps 0.45..0.75 s, irregular', () => {
    expect(clickSchedule(8, 42, SR)).toEqual(SCHEDULE);
    expect(clickSchedule(8, 43, SR)).not.toEqual(SCHEDULE);
    expect(SCHEDULE).toHaveLength(8);
    expect(SCHEDULE[0]).toBe(0.5 * SR);
    const gaps = SCHEDULE.slice(1).map((c, i) => c - (SCHEDULE[i] ?? 0));
    gaps.forEach((gap) => {
      expect(gap).toBeGreaterThanOrEqual(0.45 * SR);
      expect(gap).toBeLessThanOrEqual(0.75 * SR);
    });
    expect(new Set(gaps).size).toBe(gaps.length);
    expect(clickSchedule(12, 1, SR)).toHaveLength(12);
  });

  it('seededRandom is a deterministic PRNG in [0, 1)', () => {
    const a = seededRandom(5);
    const b = seededRandom(5);
    const values = Array.from({ length: 100 }, () => a());
    expect(values).toEqual(Array.from({ length: 100 }, () => b()));
    expect(values.every((v) => v >= 0 && v < 1)).toBe(true);
  });
});

describe('chirp template', () => {
  it('is 5 ms, Hann-windowed (zero at both ends), peak -6 dBFS', () => {
    expect(CHIRP_SECONDS).toBe(0.005);
    expect(TEMPLATE).toHaveLength(Math.round(0.005 * SR));
    const peak = Math.max(...TEMPLATE.map(Math.abs));
    expect(peak).toBeCloseTo(10 ** (-6 / 20), 6);
    expect([TEMPLATE[0], TEMPLATE.at(-1)].map((x) => Math.abs(x ?? 1) < 1e-3)).toEqual([true, true]);
  });

  it('sweeps upwards: more zero crossings in the second half (500 Hz -> 8 kHz)', () => {
    const crossings = (part: Float32Array): number => part.slice(1).filter((x, i) => Math.sign(x) !== Math.sign(part[i] ?? 0)).length;
    const half = TEMPLATE.length >> 1;
    expect(crossings(TEMPLATE.slice(half))).toBeGreaterThan(2 * crossings(TEMPLATE.slice(0, half)));
  });
});

describe('estimateLatency', () => {
  it.each([0, 128, 1000, 4410, 20000])('finds an exact delay of %i samples', (delay) => {
    expect(estimate({ delays: delay })).toEqual({
      ok: true,
      latencySamples: delay,
      latencyMilliseconds: (delay * 1000) / SR,
      detectedClicks: 8,
      spreadSamples: 0,
      inputPeakDbfs: expect.closeTo(-6, 1) as unknown as number,
      reason: null,
      warnings: [],
    });
  });

  it.each([
    [-20, 0.1],
    [-40, 0.01],
  ])('is robust against white noise at %i dBFS', (_db, rms) => {
    const result = estimate({ delays: 1000, noise: rms });
    expect([result.ok, result.latencySamples, result.detectedClicks]).toEqual([true, 1000, 8]);
  });

  it('ignores a 120 ms echo', () => {
    const result = estimate({ delays: 2000, echo: { delay: Math.round(0.12 * SR), gain: 0.6 } });
    expect([result.ok, result.latencySamples]).toEqual([true, 2000]);
  });

  it('takes the median of jittered clicks (+-2 samples) and reports the spread', () => {
    const result = estimate({ delays: [1000, 1002, 998, 1001, 999, 1000, 1002, 998] });
    expect([result.ok, result.latencySamples, result.spreadSamples]).toEqual([true, 1000, 4]);
  });

  it('accepts 6 of 8 clicks and fails with 5 of 8 (tooFewClicks)', () => {
    const six = estimate({ delays: [500, null, 500, 500, null, 500, 500, 500] });
    const five = estimate({ delays: [500, null, 500, null, 500, null, 500, 500] });
    expect([six.ok, six.detectedClicks, six.latencySamples]).toEqual([true, 6, 500]);
    expect(five).toMatchObject({ ok: false, reason: 'tooFewClicks', detectedClicks: 5, latencySamples: null, latencyMilliseconds: null });
  });

  it('needs 75 % of 12 clicks (9)', () => {
    const twelve = clickSchedule(12, 3, SR);
    const nine = estimate({ delays: [700, 700, null, 700, 700, null, 700, 700, null, 700, 700, 700] }, twelve);
    const eight = estimate({ delays: [700, null, null, 700, 700, null, 700, 700, null, 700, 700, 700] }, twelve);
    expect([nine.ok, eight.ok, eight.reason]).toEqual([true, false, 'tooFewClicks']);
  });

  it('fails with noSignal below -60 dBFS', () => {
    const result = estimate({ delays: 1000, gain: 0.0015 });
    expect(result).toMatchObject({ ok: false, reason: 'noSignal', detectedClicks: 0, latencySamples: null, spreadSamples: null });
    expect(result.inputPeakDbfs).toBeLessThan(-60);
  });

  it('fails with inconsistent when the found lags spread more than 1 ms (44 samples)', () => {
    const result = estimate({ delays: [1000, 1000, 1000, 1000, 1060, 1060, 1060, 1060] });
    expect(result).toMatchObject({ ok: false, reason: 'inconsistent', spreadSamples: 60, latencySamples: null });
  });

  it('fails with tooLong for a latency over 0.8 s', () => {
    expect(MAX_LATENCY_SECONDS).toBe(0.8);
    const result = estimate({ delays: Math.round(0.85 * SR) });
    expect(result).toMatchObject({ ok: false, reason: 'tooLong', latencySamples: null });
  });

  it('warns about clipping (peak >= -0.1 dBFS) without failing', () => {
    const result = estimate({ delays: 300, gain: 2.1 });
    expect([result.ok, result.latencySamples, result.warnings]).toEqual([true, 300, ['clipped']]);
  });

  it('never throws for a silent or empty capture', () => {
    expect(estimateLatency(new Float32Array(0), SCHEDULE, TEMPLATE, SR)).toMatchObject({ ok: false, reason: 'noSignal', inputPeakDbfs: -120 });
  });
});

describe('helpers', () => {
  it('peakDbfs floors silence at -120 dBFS', () => {
    expect([peakDbfs(1), peakDbfs(0.5), peakDbfs(0), peakDbfs(1e-9)].map((v) => Math.round(v * 10) / 10)).toEqual([0, -6, -120, -120]);
  });

  it('validates the options: clicks 6..12 (default 8), seed an integer', () => {
    expect(validateCalibrationOptions(undefined)).toEqual({ clicks: 8, seed: null });
    expect(validateCalibrationOptions({ clicks: 12, seed: 3 })).toEqual({ clicks: 12, seed: 3 });
    expect(() => validateCalibrationOptions({ clicks: 5 })).toThrow('Invalid calibration options: clicks must be an integer 6..12 (got 5)');
    expect(() => validateCalibrationOptions({ clicks: 8.5 })).toThrow('clicks must be an integer 6..12');
    expect(() => validateCalibrationOptions({ seed: 1.5 })).toThrow('Invalid calibration options: seed must be an integer (got 1.5)');
  });
});

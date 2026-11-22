import { describe, expect, it } from 'vitest';
import { DECIMATED_RATE, Decimator } from '../../src/pitch/decimate';
import { tone } from './signals';

function decimate(input: Float32Array, rate: number, block = 128): number[] {
  const decimator = new Decimator(rate);
  const out: number[] = [];
  for (let i = 0; i < input.length; i += block) {
    decimator.process(input.subarray(i, i + block), (sample) => out.push(sample));
  }
  return out;
}

function rms(values: readonly number[]): number {
  return Math.sqrt(values.reduce((s, v) => s + v * v, 0) / values.length);
}

function zeroCrossings(values: readonly number[]): number {
  return values.slice(1).filter((v, i) => Number(values[i]) < 0 && v >= 0).length;
}

describe('Decimator (31-tap windowed sinc, fractional phase, to 16 kHz)', () => {
  it.each([44_100, 48_000, 96_000, 16_000])('keeps the rate ratio and a 1 kHz tone at %i Hz', (rate) => {
    const out = decimate(tone(rate, 1, [[1000, 0.5]]), rate);
    expect(DECIMATED_RATE).toBe(16_000);
    expect(Math.abs(out.length - 16_000)).toBeLessThanOrEqual(20);
    const steady = out.slice(100);
    expect(rms(steady)).toBeCloseTo(0.5 / Math.SQRT2, 2);
    expect(Math.abs(zeroCrossings(steady) - 1000 * (steady.length / 16_000))).toBeLessThanOrEqual(2);
  });

  it.each([44_100, 48_000, 96_000])('attenuates a 12 kHz tone by more than 20 dB at %i Hz', (rate) => {
    const out = decimate(tone(rate, 0.5, [[12_000, 0.5]]), rate).slice(100);
    expect(20 * Math.log10(rms(out) / (0.5 / Math.SQRT2))).toBeLessThan(-20);
  });

  it.each([176_400, 192_000])('halves %i Hz twice, so it filters like 44.1/48 kHz (review L1)', (rate) => {
    const level = (input: Float32Array, at: number): number => 20 * Math.log10(rms(decimate(input, at).slice(100)) / (0.5 / Math.SQRT2));
    expect(level(tone(rate, 0.5, [[12_000, 0.5]]), rate)).toBeLessThan(-60);
    expect(level(tone(rate, 0.5, [[9000, 0.5]]), rate)).toBeLessThan(-20);
    const kept = decimate(tone(rate, 1, [[1000, 0.5]]), rate);
    expect(Math.abs(kept.length - 16_000)).toBeLessThanOrEqual(20);
    expect(rms(kept.slice(100))).toBeCloseTo(0.5 / Math.SQRT2, 2);
  });

  it('gives the same output for any block size', () => {
    const input = tone(48_000, 0.2, [[440, 0.3], [1500, 0.1]]);
    const a = decimate(input, 48_000, 128);
    const b = decimate(input, 48_000, 37);
    expect(b).toHaveLength(a.length);
    expect(Math.max(...a.map((v, i) => Math.abs(v - (b[i] ?? 0))))).toBeLessThan(1e-6);
  });

  it('rejects a rate below 16 kHz', () => {
    expect(() => new Decimator(8000)).toThrow('Pitch input rate must be at least 16000 Hz');
  });
});

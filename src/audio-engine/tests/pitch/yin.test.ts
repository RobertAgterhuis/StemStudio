import { describe, expect, it } from 'vitest';
import { PitchTracker, YIN, hzToCents, type PitchFrame } from '../../src/pitch/yin';
import { centsOf, median, noise, tone } from './signals';

function track(input: Float32Array, rate: number): PitchFrame[] {
  const tracker = new PitchTracker(rate);
  const frames: PitchFrame[] = [];
  for (let i = 0; i < input.length; i += 128) {
    tracker.process(input.subarray(i, i + 128), (frame) => frames.push(frame));
  }
  return frames;
}

function stats(frames: readonly PitchFrame[], expected: number): { error: number; voiced: number } {
  const voiced = frames.filter((f) => f.cents !== null);
  return { error: median(voiced.map((f) => Math.abs((f.cents ?? 0) - expected))), voiced: voiced.length / frames.length };
}

const RATES = [44_100, 48_000, 96_000] as const;
const FREQUENCIES = [80, 110, 220, 330, 440, 660, 880, 1000] as const;

describe('YIN parameters (research R-05, binding)', () => {
  it('uses window 512, hop 160, tau 14..246, threshold 0.15, -45 dBFS, max difference 0.25', () => {
    expect(YIN).toEqual({ rate: 16_000, window: 512, hop: 160, tauMin: 14, tauMax: 246, threshold: 0.15, voicedDb: -45, maxDifference: 0.25 });
  });

  it('converts Hz to centi-semitones (MIDI x 100)', () => {
    expect(hzToCents(440)).toBeCloseTo(6900, 9);
    expect(hzToCents(220)).toBeCloseTo(5700, 9);
  });
});

describe('PitchTracker accuracy (SC-004)', () => {
  it.each(RATES.flatMap((rate) => FREQUENCIES.map((f) => [rate, f] as const)))('sine at %i Hz input, %i Hz: median error <= 20 cents, >= 95 %% voiced', (rate, f) => {
    const frames = track(tone(rate, 1, [[f, 0.4]]), rate).slice(5);
    const { error, voiced } = stats(frames, centsOf(f));
    expect(error).toBeLessThanOrEqual(20);
    expect(voiced).toBeGreaterThanOrEqual(0.95);
  });

  it.each(RATES.flatMap((rate) => [110, 196, 440].map((f) => [rate, f] as const)))('harmonic tone at %i Hz input, %i Hz fundamental', (rate, f) => {
    const frames = track(tone(rate, 1, [[f, 0.3], [2 * f, 0.2], [3 * f, 0.1]]), rate).slice(5);
    const { error, voiced } = stats(frames, centsOf(f));
    expect(error).toBeLessThanOrEqual(20);
    expect(voiced).toBeGreaterThanOrEqual(0.95);
  });

  it('posts one frame per 10 ms hop once the first window is full', () => {
    const frames = track(tone(48_000, 1, [[220, 0.4]]), 48_000);
    expect(frames.length).toBeGreaterThanOrEqual(96);
    expect(frames.length).toBeLessThanOrEqual(100);
    expect(frames.map((f) => f.endSample).slice(0, 3)).toEqual([512, 672, 832]);
  });

  it('reports a clarity in 0..1, high for a clean tone', () => {
    const frames = track(tone(44_100, 0.5, [[440, 0.4]]), 44_100).slice(5);
    expect(Math.min(...frames.map((f) => f.clarity))).toBeGreaterThan(0.85);
    expect(Math.max(...frames.map((f) => f.clarity))).toBeLessThanOrEqual(1);
  });
});

describe('PitchTracker voicing', () => {
  it.each(RATES)('silence at %i Hz is unvoiced', (rate) => {
    const frames = track(new Float32Array(rate), rate);
    expect(frames.every((f) => f.cents === null && f.clarity === 0)).toBe(true);
  });

  it.each(RATES)('white noise at %i Hz is (almost) always unvoiced', (rate) => {
    const frames = track(noise(rate, 1, 0.3), rate);
    expect(frames.filter((f) => f.cents !== null).length / frames.length).toBeLessThanOrEqual(0.05);
  });

  it('a tone below -45 dBFS RMS is unvoiced', () => {
    const frames = track(tone(48_000, 0.5, [[220, 0.005]]), 48_000);
    expect(frames.every((f) => f.cents === null)).toBe(true);
  });
});

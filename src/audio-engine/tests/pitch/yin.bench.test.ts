import { describe, expect, it } from 'vitest';
import { PitchTracker } from '../../src/pitch/yin';
import { tone } from './signals';

/** SC-004 benchmark (reported, not gating): ms per 128-frame block of the decimator + YIN core. */
describe('YIN core benchmark', () => {
  it.each([44_100, 48_000, 96_000])('reports average and worst ms per block at %i Hz', (rate) => {
    const input = tone(rate, 5, [[220, 0.3], [440, 0.2]]);
    const tracker = new PitchTracker(rate);
    let worst = 0;
    let total = 0;
    let blocks = 0;
    for (let i = 0; i + 128 <= input.length; i += 128) {
      const start = performance.now();
      tracker.process(input.subarray(i, i + 128), () => undefined);
      const ms = performance.now() - start;
      worst = Math.max(worst, ms);
      total += ms;
      blocks += 1;
    }
    console.info(`[yin.bench] ${String(rate)} Hz: ${(total / blocks).toFixed(4)} ms avg, ${worst.toFixed(3)} ms worst per 128-frame block`);
    expect(Number.isFinite(total / blocks)).toBe(true);
  });
});

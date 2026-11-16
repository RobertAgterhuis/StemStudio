import type { Page } from '@playwright/test';
import { accents, heardClicks, installClickProbeNow, musicStartTimes, readClicks, spacings } from '../support/click-probe';
import { installOutputProbeNow, outputDominant, outputRmsDb, timeFrequencyStep, withinRatio } from '../support/output-probe';
import { beatIntervalAt, distinctValues, firstBeatAtOrAfter, heardLatencySeconds, pitchFactor, positionRate } from '../support/practice';
import { expect, test } from '../support/test';

// Feature 012 T012: self-tests of the output probe, the click probe and the practice maths on a
// blank page with known oscillators and buffers (like audio-check.spec.ts). Needs no stack; runs
// in both browsers with `npm test` and `npm run test:practice`.

interface ToneWindow {
  __probeTone?: { ctx: AudioContext; osc: OscillatorNode; out: GainNode };
}

/** A blank page with two buttons whose clicks (user gestures) start and step a 440 Hz tone. */
async function tonePage(page: Page): Promise<void> {
  await page.goto('about:blank');
  await installOutputProbeNow(page);
  await page.evaluate(() => {
    const button = (id: string, onClick: () => void): void => {
      const el = document.createElement('button');
      el.dataset.testid = id;
      el.textContent = id;
      el.addEventListener('click', onClick);
      document.body.append(el);
    };
    button('probe-start', () => {
      const ctx = new AudioContext({ sampleRate: 44_100 });
      const osc = new OscillatorNode(ctx, { frequency: 440 });
      const out = new GainNode(ctx, { gain: 0.25 });
      osc.connect(out).connect(ctx.destination);
      osc.start();
      (window as ToneWindow).__probeTone = { ctx, osc, out };
    });
    button('probe-step', () => {
      const tone = (window as ToneWindow).__probeTone;
      tone?.osc.frequency.setValueAtTime(660, tone.ctx.currentTime + 0.1);
    });
  });
  await page.getByTestId('probe-start').click();
  await expect.poll(() => page.evaluate(() => (window as ToneWindow).__probeTone?.ctx.state)).toBe('running');
}

test.describe('output probe', { tag: ['@practice', '@self-test'] }, () => {
  test('reads a 440 Hz oscillator within 0.2 % and its level', async ({ page }) => {
    await tonePage(page);
    await expect.poll(async () => withinRatio((await outputDominant(page)).hz, 440, 0.002), { timeout: 5_000 }).toBe(true);
    // A 0.25 sine has an RMS of 0.177 = -15.05 dBFS.
    await expect.poll(() => outputRmsDb(page)).toBeGreaterThan(-16);
    expect(await outputRmsDb(page)).toBeLessThan(-14);
  });

  test('times a frequency step scheduled 100 ms ahead (the fast window adds up to 23 ms)', async ({ page }) => {
    await tonePage(page);
    await expect.poll(async () => withinRatio((await outputDominant(page, 'fast')).hz, 440, 0.02), { timeout: 5_000 }).toBe(true);
    const ms = await timeFrequencyStep(page.getByTestId('probe-step'), { action: { kind: 'click' }, fromHz: 440, toHz: 660 });
    expect(ms).toBeGreaterThanOrEqual(90);
    expect(ms).toBeLessThan(200);
    await expect.poll(async () => withinRatio((await outputDominant(page)).hz, 660, 0.002), { timeout: 5_000 }).toBe(true);
  });

  test('a node disconnected from the destination is no longer heard', async ({ page }) => {
    await tonePage(page);
    await expect.poll(() => outputRmsDb(page), { timeout: 5_000 }).toBeGreaterThan(-16);
    await page.evaluate(() => {
      const tone = (window as ToneWindow).__probeTone;
      tone?.out.disconnect(tone.ctx.destination);
    });
    await expect.poll(() => outputRmsDb(page), { timeout: 5_000 }).toBeLessThan(-100);
  });
});

function expectTimes(actual: readonly number[], expected: readonly number[]): void {
  expect(actual).toHaveLength(expected.length);
  expected.forEach((t, i) => {
    expect(actual[i]).toBeCloseTo(t, 6);
  });
}

test.describe('click probe', { tag: ['@practice', '@self-test'] }, () => {
  test('records planned click times, accents, cancellations and music starts', async ({ page }) => {
    await page.goto('about:blank');
    await installClickProbeNow(page);
    const base = await page.evaluate(() => {
      const ctx = new AudioContext({ sampleRate: 44_100 });
      const buffer = (seconds: number, peak: number): AudioBuffer => {
        const b = new AudioBuffer({ length: Math.round(seconds * 44_100), numberOfChannels: 1, sampleRate: 44_100 });
        b.getChannelData(0)[0] = peak;
        return b;
      };
      const [accent, normal, music] = [buffer(0.05, 0.9), buffer(0.05, 0.5), buffer(2, 0.1)];
      const t0 = ctx.currentTime + 1;
      [accent, normal, normal, normal].forEach((b, k) => {
        new AudioBufferSourceNode(ctx, { buffer: b }).start(t0 + k * 0.5);
      });
      const cancelled = new AudioBufferSourceNode(ctx, { buffer: normal });
      cancelled.start(t0 + 2);
      cancelled.stop(t0 + 1);
      for (const delay of [2, 2.0004, 4]) new AudioBufferSourceNode(ctx, { buffer: music, playbackRate: 0.5 }).start(t0 + delay, 1);
      return t0;
    });
    const recording = await readClicks(page);
    const clicks = heardClicks(recording);
    expectTimes(clicks.map((c) => c.when - base), [0, 0.5, 1, 1.5]);
    expect(recording.clicks.filter((c) => c.cancelled)).toHaveLength(1);
    expect(accents(clicks)).toEqual([true, false, false, false]);
    expect(spacings(clicks).every((s) => Math.abs(s - 0.5) < 1e-6)).toBe(true);
    expectTimes(musicStartTimes(recording).map((t) => t - base), [2, 4]);
    expect(recording.music[0]).toMatchObject({ offset: 1, rate: 0.5 });
  });
});

test.describe('practice maths', { tag: ['@practice', '@self-test'] }, () => {
  test('pitch factor and heard latency follow audio-engine.md 2.3.0', () => {
    expect(pitchFactor(50, 0)).toBe(2);
    expect(pitchFactor(100, 12)).toBe(2);
    expect(heardLatencySeconds(100, 0)).toBeCloseTo(5120 / 44_100, 9);
    expect(heardLatencySeconds(50, 0)).toBeCloseTo((5120 - 1100) / 44_100, 9);
    expect(heardLatencySeconds(100, -12)).toBeCloseTo((5120 + 1100) / 44_100, 9);
  });

  test('the position rate is the least-squares slope of the readings', () => {
    const samples = [0, 1000, 2000, 3000, 4000].map((at, i) => ({ at, seconds: 0.5 * (at / 1000) + (i % 2 === 0 ? 0.01 : -0.01) }));
    expect(positionRate(samples)).toBeCloseTo(0.5, 2);
    expect(positionRate(samples.slice(0, 2))).toBeNaN();
  });

  test('grid helpers find the next beat and its interval; distinct values collapse repeats', () => {
    const beats = [0.5, 1, 1.5, 2.01];
    expect(firstBeatAtOrAfter(beats, 0)).toBe(0.5);
    expect(firstBeatAtOrAfter(beats, 1.75)).toBe(2.01);
    expect(firstBeatAtOrAfter(beats, 3)).toBeNaN();
    expect(beatIntervalAt(beats, 1.5)).toBeCloseTo(0.51, 9);
    expect(beatIntervalAt(beats, 2.01)).toBeNaN();
    const samples = ['60', '60', '70', null, '80', '80'].map((v, at) => ({ at, values: { 'data-speed': v } }));
    expect(distinctValues(samples, 'data-speed')).toEqual(['60', '70', '80']);
  });
});

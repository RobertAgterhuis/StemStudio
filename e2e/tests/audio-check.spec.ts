import { readFileSync } from 'node:fs';
import { dbDifference, mixRms, samplesEqual, wavFacts, type MixInput } from '../support/audio-check';
import { expect, test } from '../support/feature-test';
import { fixturePath } from '../support/fixtures';
import { panMatrix, type PanMatrix } from '../support/render-vectors';

// Feature 007: self-test of the engine-free audio oracle the export specs rely on (T041, T047,
// T058). Needs no stack; runs on the generated 001 fixtures in both browsers.
const identity: PanMatrix = [
  [1, 0],
  [0, 1],
];
const click = (): Buffer => readFileSync(fixturePath('click_120bpm_4-4_12s.wav'));
const music = (): Buffer => readFileSync(fixturePath('music_100bpm_20s.flac'));

function input(bytes: Buffer, gain: number, matrix: PanMatrix): MixInput {
  return { base64: bytes.toString('base64'), gain, matrix };
}

test.describe('audio oracle', { tag: '@export' }, () => {
  test('reads the WAV header facts of a fixture', () => {
    expect(wavFacts(click())).toEqual({ sampleRate: 44_100, channels: 2, bitsPerSample: 16, frames: 529_200, durationSeconds: 12 });
  });

  test('refuses a file that is not RIFF/WAVE', () => {
    expect(() => wavFacts(music())).toThrow('not a RIFF/WAVE file');
  });

  test('equal files decode to equal samples, different files do not', async ({ scratch }) => {
    expect(await samplesEqual(scratch, music(), music())).toBe(true);
    expect(await samplesEqual(scratch, music(), click())).toBe(false);
  });

  test('half gain lowers the RMS by 6.02 dB on both channels', async ({ scratch }) => {
    const full = await mixRms(scratch, [input(music(), 1, identity)], 1);
    const half = await mixRms(scratch, [input(music(), 1, identity)], 0.5);
    expect(dbDifference(half[0], full[0])).toBeCloseTo(-6.0206, 2);
    expect(dbDifference(half[1], full[1])).toBeCloseTo(-6.0206, 2);
  });

  test('hard left folds the right channel into the left one', async ({ scratch }) => {
    const [left, right] = await mixRms(scratch, [input(music(), 1, panMatrix(-1))], 1);
    const summed = await mixRms(scratch, [input(music(), 1, identity), input(music(), 1, identity)], 1);
    expect(right).toBe(0);
    expect(left).toBeGreaterThan(summed[0] / 2);
  });

  test('two equal inputs at gain 0.5 equal one input at gain 1', async ({ scratch }) => {
    const one = await mixRms(scratch, [input(music(), 1, identity)], 1);
    const two = await mixRms(scratch, [input(music(), 0.5, identity), input(music(), 0.5, identity)], 1);
    expect(Math.abs(dbDifference(two[0], one[0]))).toBeLessThan(0.001);
  });
});

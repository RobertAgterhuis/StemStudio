import type { Page } from '@playwright/test';
import { requireChromium } from '../support/feature-skips';
import { applyPan, readPanVectors } from '../support/render-vectors';
import { expect, test } from '../support/test';

// Feature 007 T048 (research.md E-05, SC-003): the browser's StereoPannerNode is the fourth
// consumer of render-vectors.json. A constant stereo signal (L = 1, R = 0.5) rendered through
// the node at every pan of the vectors must give the vector matrix's output to 1e-6. Needs no
// stack: the page stays at about:blank.
const input: readonly [number, number] = [1, 0.5];
const tolerance = 1e-6;

/** Renders 256 frames of the constant input through a StereoPannerNode; returns frame 128. */
async function renderThroughPanner(page: Page, pan: number): Promise<[number, number]> {
  return page.evaluate(
    async ({ value, signal }) => {
      const context = new OfflineAudioContext(2, 256, 44_100);
      const buffer = context.createBuffer(2, 256, 44_100);
      buffer.getChannelData(0).fill(signal[0]);
      buffer.getChannelData(1).fill(signal[1]);
      const source = new AudioBufferSourceNode(context, { buffer });
      const panner = new StereoPannerNode(context, { pan: value });
      source.connect(panner).connect(context.destination);
      source.start();
      const out = await context.startRendering();
      return [out.getChannelData(0)[128] ?? Number.NaN, out.getChannelData(1)[128] ?? Number.NaN] as [number, number];
    },
    { value: pan, signal: input },
  );
}

test.describe('StereoPannerNode matches the shared pan vectors', { tag: '@export' }, () => {
  test.beforeEach(({ browserName }) => {
    requireChromium(browserName);
  });

  for (const vector of readPanVectors()) {
    test(`pan ${String(vector.pan)}`, async ({ page }) => {
      const [left, right] = await renderThroughPanner(page, vector.pan);
      const [expectedLeft, expectedRight] = applyPan(vector.matrix, input[0], input[1]);
      expect(Math.abs(left - expectedLeft)).toBeLessThanOrEqual(tolerance);
      expect(Math.abs(right - expectedRight)).toBeLessThanOrEqual(tolerance);
    });
  }

  test('the vectors cover hard left, centre and hard right', () => {
    const pans = readPanVectors().map((vector) => vector.pan);
    expect(pans).toEqual(expect.arrayContaining([-1, 0, 1]));
  });
});

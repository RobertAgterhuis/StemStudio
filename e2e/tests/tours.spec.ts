import { ensureCancelledPhase } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { levelState, readStoredPreferences, tourVersions } from '../support/preferences';
import { openScreen } from '../support/screens';
import { readTourDefinition, stepRange, tourCases } from '../support/tours';
import { expect, test } from '../support/ux-test';

// T057 / FR-027, quickstart V-T1..V-T4: offered never forced, keyboard-operable to the end,
// Escape dismisses, steps without a visible target are skipped, progress is versioned.
for (const { id, level, screen } of tourCases) {
  test.describe(`${id} at ${level}`, () => {
    test.use({ storageState: levelState(level, { toursDone: false }) });

    test('is offered but not started, runs keyboard-only to the end, and is not offered again', { tag: '@ux' }, async ({
      page,
      tour,
      uxData,
    }) => {
      await openScreen(page, screen, uxData);
      await expect(tour.offer(id)).toBeVisible();
      await expect(tour.popover).toHaveCount(0);

      await tour.startFromOfferWithKeyboard(id);
      const steps = await tour.steps();
      expect(steps).toBeLessThanOrEqual(readTourDefinition(id).steps.length);
      expect(await tour.finishWithKeyboard()).toEqual(stepRange(steps));

      await expect
        .poll(async () => (await readStoredPreferences(page))?.help.tours[id])
        .toMatchObject({ version: tourVersions[id], status: 'completed' });
      await expect(tour.offer(id)).toHaveCount(0);
      await page.reload();
      await openScreen(page, screen, uxData);
      await expect(tour.offer(id)).toHaveCount(0);
    });
  });
}

test.describe('import-basics interrupted', () => {
  test.use({ storageState: levelState('beginner', { toursDone: false }) });

  test('traps focus, and Escape midway closes it, returns focus and records dismissed', { tag: '@ux' }, async ({
    page,
    shell,
    tour,
    uxData,
  }) => {
    await openScreen(page, 'import', uxData);
    await tour.startFromOfferWithKeyboard('import-basics');
    await page.keyboard.press('ArrowRight');
    await expect(tour.popover).toHaveAttribute('data-step', '2');
    expect(await tour.focusStaysInsideOnTab(4)).toBe(true);

    await page.keyboard.press('Escape');
    await expect(tour.popover).toBeHidden();
    await expect.poll(() => shell.focusIsOnAnElement()).toBe(true);
    await expect.poll(async () => (await readStoredPreferences(page))?.help.tours['import-basics']).toMatchObject({ status: 'dismissed' });
    await page.reload();
    await openScreen(page, 'import', uxData);
    await expect(tour.offer('import-basics')).toHaveCount(0);
  });
});

test.describe('mixer-advanced and missing targets', () => {
  test.use({ storageState: levelState('advanced', { toursDone: false }) });

  test('shows every step on a song with detail stems', { tag: '@ux' }, async ({ page, tour, uxData }) => {
    await openScreen(page, 'mixer', uxData);
    await tour.startFromOfferWithKeyboard('mixer-advanced');
    expect(await tour.steps()).toBe(readTourDefinition('mixer-advanced').steps.length);
  });

  test('skips the step whose target is missing on a song without detail stems', { tag: '@ux' }, async ({
    api,
    mixer,
    tour,
  }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 60_000);
    // Own unsplit copy: the shared click track is not reliably plain (support/detail.ts).
    const plain = await ensureCancelledPhase(api, `tour-unsplit-${testInfo.project.name}`);
    await mixer.goto(plain.id);
    await mixer.waitForReady(['vocals', 'drums']);
    await expect(mixer.expand('drums')).toHaveCount(0);
    await tour.startFromOfferWithKeyboard('mixer-advanced');
    const steps = await tour.steps();
    expect(steps).toBeLessThan(readTourDefinition('mixer-advanced').steps.length);
    expect(await tour.finishWithKeyboard()).toEqual(stepRange(steps));
  });
});

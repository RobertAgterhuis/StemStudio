import type { Page } from '@playwright/test';
import { axeViolations } from '../support/axe';
import { fillAndCommit, setToggle } from '../support/controls';
import { cellState, matrixThemes } from '../support/visual';
import { expect, test } from '../support/ux-test';

// Feature 012 polish (T066; FR-017, SC-001 of 004): the practice states that exist only while
// something runs are scanned in both themes at Advanced (the static panel is part of the screen
// matrix as `mixer-practice`): a running count-in, a running speed trainer and the "time stretch
// unavailable" notice. The count-in indicator must not animate with reduced motion (the project
// runs with `reducedMotion: 'reduce'`). Seeded click track (reliable grid, 120 BPM).

async function openPlainMixer(page: Page, trackId: string): Promise<void> {
  await page.goto(`/tracks/${trackId}/mixer`);
  await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
}

/** Names of the running CSS animations of the indicator and its dots. */
async function indicatorAnimations(page: Page): Promise<string[]> {
  return page.getByTestId('countin-indicator').evaluate((el) =>
    [el, ...el.querySelectorAll('[data-testid^="countin-dot-"]')].map((node) => getComputedStyle(node).animationName),
  );
}

for (const theme of matrixThemes) {
  test.describe(`practice states / ${theme}`, { tag: '@practice' }, () => {
    test.use({ storageState: cellState('advanced', theme) });

    test('a running count-in has no violations and does not animate with reduced motion', async ({ page, uxData }) => {
      await openPlainMixer(page, uxData.plainTrack.id);
      await setToggle(page.getByTestId('countin-toggle'), true);
      await page.getByTestId('countin-bars').selectOption('2');
      await page.getByTestId('transport-play').click();
      // The screen-reader announcer is a separate element (`[data-countin-announcer]`, frontend lane).
      await expect(page.getByTestId('countin-indicator')).toBeVisible();
      // Read the animations first: the 2-bar count-in lasts 4 s and the scan may outlast it.
      expect((await indicatorAnimations(page)).filter((name) => name !== 'none')).toEqual([]);
      expect(await axeViolations(page)).toEqual([]);
    });

    test('a running speed trainer has no violations', async ({ page, uxData }) => {
      await openPlainMixer(page, uxData.plainTrack.id);
      await fillAndCommit(page.getByTestId('loop-start'), '2');
      await fillAndCommit(page.getByTestId('loop-end'), '6');
      await setToggle(page.getByTestId('loop-toggle'), true);
      await setToggle(page.getByTestId('trainer-toggle'), true);
      await page.getByTestId('transport-play').click();
      await expect(page.getByTestId('trainer-status')).toHaveAttribute('data-state', 'running', { timeout: 15_000 });
      expect(await axeViolations(page)).toEqual([]);
    });

    test('the "time stretch unavailable" notice has no violations', async ({ page, uxData }) => {
      await page.route('**/soundtouch.wasm', (route) => route.fulfill({ status: 404, body: '' }));
      await openPlainMixer(page, uxData.plainTrack.id);
      await page.getByTestId('practice-speed-down').click();
      await expect(page.getByTestId('practice-unavailable')).toBeVisible({ timeout: 15_000 });
      expect(await axeViolations(page)).toEqual([]);
    });
  });
}

import { axeViolations } from '../support/axe';
import { preferencesRecord, storageStateFor, type Level, type TourId } from '../support/preferences';
import { openScreen, screenIds } from '../support/screens';
import { expect, test } from '../support/ux-test';
import { cellState, matrixLevels, matrixThemes } from '../support/visual';

// T052 / SC-001: axe (wcag2a, wcag2aa, wcag21a, wcag21aa, wcag22aa) reports 0 violations on every
// screen x level x theme, the help panel, the palette, a tour step and the welcome panel
// (TEST-STRATEGY.md 10.3).
const mixerTour: Readonly<Record<Level, TourId>> = {
  beginner: 'mixer-basics',
  advanced: 'mixer-advanced',
  expert: 'expert-keyboard',
};

for (const theme of matrixThemes) {
  for (const level of matrixLevels) {
    test.describe(`${level} / ${theme}`, () => {
      test.use({ storageState: cellState(level, theme) });

      for (const screen of screenIds) {
        test(`${screen} has no WCAG A/AA violations`, async ({ page, uxData }) => {
          await openScreen(page, screen, uxData);
          expect(await axeViolations(page)).toEqual([]);
        });
      }

      test('the open help panel has no violations', async ({ page, uxData, help }) => {
        await openScreen(page, 'mixer', uxData);
        await help.open('button');
        expect(await axeViolations(page)).toEqual([]);
      });

      test('the open command palette has no violations', async ({ page, uxData }) => {
        await openScreen(page, 'import', uxData);
        await page.getByTestId('command-palette-open').click();
        await expect(page.getByTestId('command-palette')).toBeVisible();
        expect(await axeViolations(page)).toEqual([]);
      });

      test(`a step of the ${mixerTour[level]} tour has no violations`, async ({ page, uxData, help, tour }) => {
        await openScreen(page, 'mixer', uxData);
        await help.open('button');
        await help.byId(`help-tour-start-${mixerTour[level]}`).click();
        await expect(tour.popover).toHaveAttribute('data-tour-id', mixerTour[level]);
        expect(await axeViolations(page)).toEqual([]);
      });
    });
  }

  test.describe(`welcome / ${theme}`, () => {
    test.use({ storageState: storageStateFor(preferencesRecord({ theme })) });

    test('the welcome panel has no violations', async ({ page, uxData }) => {
      await openScreen(page, 'import', uxData);
      await expect(page.getByTestId('welcome-panel')).toBeVisible();
      expect(await axeViolations(page)).toEqual([]);
    });
  });
}

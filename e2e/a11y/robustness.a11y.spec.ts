import { axeViolations } from '../support/axe';
import {
  computedColours,
  desktopViewport,
  horizontalOverflow,
  reflowScreens,
  reflowViewport,
  textLineCounts,
} from '../support/robustness';
import { openScreen } from '../support/screens';
import { expect, test } from '../support/ux-test';
import { cellState, matrixThemes } from '../support/visual';

// Robustness checks suggested in HO:frontend-004-polish (T063): every screen reflows at 320 CSS px
// without horizontal page scrolling (WCAG 1.4.10), and in forced-colours mode the filled controls
// keep a label distinct from their fill while axe still reports 0 violations (WCAG 1.4.11).
for (const theme of matrixThemes) {
  test.describe(`320 px reflow / ${theme}`, () => {
    test.use({ storageState: cellState('advanced', theme), viewport: reflowViewport });

    for (const screen of reflowScreens) {
      test(`${screen} has no horizontal overflow at 320 px`, async ({ page, uxData }) => {
        await openScreen(page, screen, uxData);
        expect(await horizontalOverflow(page)).toBe(0);
      });
    }
  });

  test.describe(`library columns / ${theme}`, () => {
    test.use({ storageState: cellState('advanced', theme), viewport: desktopViewport });

    test('library headings and status words do not break at desktop width', async ({ page, uxData }) => {
      await openScreen(page, 'library', uxData);
      const table = page.locator('.ss-asset-table');
      // The last heading is screen-reader only (clipped to 1 px), so it is not measured.
      const headings = await textLineCounts(table.locator('th:not(:has(.ss-sr-only))'));
      const statuses = await textLineCounts(table.locator('.ss-asset-status'));
      expect(headings).toHaveLength(4);
      expect(statuses.length).toBeGreaterThan(0);
      expect({ headings, statuses }).toEqual({ headings: [1, 1, 1, 1], statuses: statuses.map(() => 1) });
    });
  });

  test.describe(`forced colours / ${theme}`, () => {
    test.use({ storageState: cellState('advanced', theme) });

    test.beforeEach(async ({ page }) => {
      await page.emulateMedia({ forcedColors: 'active' });
    });

    test('the checked level segment keeps a visible label and state', async ({ page, uxData }) => {
      await openScreen(page, 'import', uxData);
      await expect(page.getByTestId('level-advanced')).toHaveAttribute('aria-checked', 'true');
      const checked = await computedColours(page.getByTestId('level-advanced'));
      const unchecked = await computedColours(page.getByTestId('level-beginner'));
      expect(checked.color).not.toBe(checked.background);
      expect(checked.background).not.toBe(unchecked.background);
    });

    test('the primary import button keeps a visible label', async ({ page, uxData }) => {
      await openScreen(page, 'import', uxData);
      const colours = await computedColours(page.getByTestId('import-url-submit'));
      expect(colours.color).not.toBe(colours.background);
    });

    for (const screen of ['import', 'track', 'mixer'] as const) {
      test(`${screen} has no WCAG A/AA violations in forced colours`, async ({ page, uxData }) => {
        await openScreen(page, screen, uxData);
        expect(await axeViolations(page)).toEqual([]);
      });
    }
  });
}

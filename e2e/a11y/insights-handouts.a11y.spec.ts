import { HandoutMenu } from '../pages/handout-menu';
import { QualityPanel } from '../pages/quality-panel';
import { StemStudioApi } from '../support/api';
import { axeViolations, type ViolationSummary } from '../support/axe';
import { apiContextOptions, fakeJobTimeoutMs } from '../support/env';
import { seedSetlist } from '../support/handout-seed';
import { useInsightsApi } from '../support/insights-suite';
import { expect, test } from '../support/test';
import { cellState, matrixThemes } from '../support/visual';

// Feature 020 T101 (FR-016, WCAG 2.2 AA): axe-core on every new area in both themes at Expert (every control rendered):
// the quality panel with tips, the mixer badges, the handout menu and dialog, the practice notes, the setlist handout
// menu. FR-016 asks for no serious or critical violation; the spec reports all and fails on those two. The keyboard
// paths reach and operate every new control without a mouse (menu: WAI-ARIA menu button pattern).
const severe = new Set(['serious', 'critical']);
const suite = useInsightsApi(3 * fakeJobTimeoutMs + 120_000);

function serious(violations: readonly ViolationSummary[]): ViolationSummary[] {
  return violations.filter((v) => severe.has(v.impact));
}

for (const theme of matrixThemes) {
  test.describe(`insights and handouts / ${theme}`, { tag: '@insights' }, () => {
    test.use({ storageState: cellState('expert', theme) });

    test('track page: quality panel with tips, handout menu and dialog, practice notes', async ({ api, page }) => {
      const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
      const report = await suite.insights().ensureQuality(track.id);
      const panel = new QualityPanel(page);
      const menu = new HandoutMenu(page);
      await page.goto(`/tracks/${track.id}`);
      await panel.waitForReport(report);
      await expect(page.getByTestId('practice-notes')).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);

      await menu.menu().click();
      await expect(menu.family('bass')).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);

      await menu.customise().click();
      await expect(menu.dialog).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);
    });

    test('mixer: quality badges on the channel strips', async ({ api, page, mixer }) => {
      const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
      await suite.insights().ensureQuality(track.id);
      await mixer.goto(track.id);
      await expect(page.getByTestId('quality-badge-vocals')).toBeVisible({ timeout: 30_000 });
      expect(serious(await axeViolations(page))).toEqual([]);
    });

    test('setlist page: setlist handout menu', async ({ page, playwright }) => {
      const context = await playwright.request.newContext(apiContextOptions);
      const setlist = await seedSetlist(new StemStudioApi(context), context);
      const menu = new HandoutMenu(page);
      await page.goto(`/setlists/${setlist.id}`);
      await menu.menu('setlist').click();
      await expect(menu.family('overview', 'setlist')).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);
      await context.dispose();
    });
  });
}

test.describe('keyboard only (FR-016)', { tag: '@insights' }, () => {
  test.use({ storageState: cellState('expert', 'light') });

  test('"Check again", the handout menu and the practice notes work without a mouse', async ({ api, page }) => {
    const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
    const insights = suite.insights();
    const before = await insights.ensureQuality(track.id);
    const panel = new QualityPanel(page);
    const menu = new HandoutMenu(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(before);

    await panel.check.focus();
    await page.keyboard.press('Enter');
    await insights.waitForQuality(track.id, 60_000, before);

    // WAI-ARIA menu button: Enter opens the menu and focuses an item; arrows move; Escape closes and returns focus.
    await menu.menu().focus();
    await page.keyboard.press('Enter');
    await expect(menu.menu()).toHaveAttribute('aria-expanded', 'true');
    await expect(page.locator('[role="menuitem"]:focus')).toHaveCount(1);
    await page.keyboard.press('Escape');
    await expect(menu.menu()).toBeFocused();

    const text = page.getByTestId('practice-notes-text');
    await text.focus();
    await page.keyboard.type('Keyboard note: count in twice.');
    await page.getByTestId('practice-notes-save').focus();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('practice-notes-saved')).toBeVisible();
    await expect(page.getByTestId('practice-notes-saved')).toHaveAttribute('aria-live', /polite|assertive/u);
  });
});

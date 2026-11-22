import { InstrumentViews } from '../pages/instrument-views';
import { axeViolations, type ViolationSummary } from '../support/axe';
import { ensureDetailCompleted, fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';
import { cellState, matrixThemes } from '../support/visual';

// Feature 016 T136 (FR-061..FR-064, SC-008): axe-core (WCAG 2.0-2.2 A/AA) on every new area of the instrument views, in
// both themes at Expert (every control rendered): the "Practise as" menu, the chord lane and key with capo mode and its
// suggestions, the drum trainer after Drumless, the singing panel (no microphone in this project: the reason is shown),
// the part view, the track page with the chord sheet and key regions, the chord edit dialog and the sheet music dialog
// with the tablature and part fields. SC-008 asks for no serious or critical violation; the spec reports all of them
// and fails on those two impacts.
const severe = new Set(['serious', 'critical']);

/** The violations SC-008 forbids. */
function serious(violations: readonly ViolationSummary[]): ViolationSummary[] {
  return violations.filter((v) => severe.has(v.impact));
}

test.beforeEach(() => {
  test.setTimeout(fakeJobTimeoutMs + 120_000);
});

for (const theme of matrixThemes) {
  test.describe(`instrument views / ${theme}`, { tag: '@instrument' }, () => {
    test.use({ storageState: cellState('expert', theme) });

    test('mixer: Practise as menu, chord lane, key and capo mode', async ({ api, mixer, page }) => {
      const track = await ensureDetailCompleted(api);
      const ui = new InstrumentViews(page);
      await mixer.goto(track.id);
      await mixer.waitForReady(fakeStems);
      await ui.practiseAs.click();
      expect(serious(await axeViolations(page))).toEqual([]);

      await ui.byId('practise-as-guitar').click();
      await ui.enableCapoMode();
      await expect(ui.byId('capo-suggestions')).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);
    });

    test('mixer: drum trainer, singing panel and part view', async ({ api, mixer, page }) => {
      const track = await ensureDetailCompleted(api);
      const ui = new InstrumentViews(page);
      await mixer.goto(track.id);
      await mixer.waitForReady(fakeStems);

      for (const family of ['drums', 'vocals', 'orchestral'] as const) {
        await ui.practiseAsFamily(family);
        expect(serious(await axeViolations(page)), family).toEqual([]);
      }
      await ui.drumless.click();
      await expect(ui.drumTrainer).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);
    });

    test('track page: chord sheet, key regions, chord edit and sheet music dialogs', async ({ api, trackPage, scoreDialog, page }) => {
      const track = await ensureDetailCompleted(api);
      const ui = new InstrumentViews(page);
      await trackPage.goto(track.id);
      await expect(ui.chordSheet).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);

      await ui.chordEdits.first().click();
      await expect(ui.byId('chord-edit-dialog')).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);
      await ui.byId('chord-edit-cancel').click();

      await scoreDialog.openForStem('guitar');
      await page.getByTestId('score-profile').selectOption('guitar');
      await expect(page.getByTestId('score-tab-layout')).toBeVisible();
      expect(serious(await axeViolations(page))).toEqual([]);
    });
  });
}

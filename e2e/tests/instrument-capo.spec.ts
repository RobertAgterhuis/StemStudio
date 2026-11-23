import type { Page } from '@playwright/test';
import { InstrumentViews } from '../pages/instrument-views';
import { PracticePanel } from '../pages/practice-panel';
import type { StemStudioApi } from '../support/api';
import { rankCapos } from '../support/capo-rule';
import { fakeJobTimeoutMs } from '../support/env';
import { fixturePath } from '../support/fixtures';
import { chordTrack, sharpSymbol, useInstrumentApi } from '../support/instrument-views';
import { expect, test } from '../support/test';

// Feature 016 US8 (T129; FR-072..FR-078, SC-006, SC-011, quickstart V13): capo mode for the guitar family. Data-model
// 2.7: the capo case uses a 012 transpose of +3 on the fake chords (C, G, Am, F), so the song sounds in E-flat; the
// shortcut is capo 3 (original shapes) and the suggestion order follows research R-14, computed here from the track's
// own chart (support/capo-rule.ts). Capo mode exists only for the guitar family.
const fixture = 'music_100bpm_20s.flac';
const chordsMs = 60_000;
const transpose = 3;
const expectedCycle = ['C', 'G', 'Am', 'F'];

const suite = useInstrumentApi(fakeJobTimeoutMs + 2 * chordsMs);

/** The fixture track and its ready chord chart. */
function trackWithChords(api: StemStudioApi): ReturnType<typeof chordTrack> {
  return chordTrack(api, suite.views(), fixturePath(fixture), fakeJobTimeoutMs);
}

/** "Practise as Guitar", transpose +3, capo mode on, then the shortcut (capo 3). */
async function capoThree(page: Page, ui: InstrumentViews): Promise<void> {
  await ui.practiseAsFamily('guitar');
  await new PracticePanel(page).setTranspose(transpose);
  await ui.enableCapoMode();
  await expect(ui.capoShortcut).toContainText('Capo 3');
  await ui.capoShortcut.click();
  await expect(ui.capoBadge).toHaveText('Capo 3');
}

test.describe('capo mode for the guitar (016 US8)', { tag: '@instrument' }, () => {
  test('the shortcut gives capo 3 and the lane shows the original shapes', async ({ api, mixer, page }) => {
    const { track, chart } = await trackWithChords(api);
    const ui = new InstrumentViews(page);
    await mixer.goto(track.id);
    await mixer.waitForReady(['guitar']);

    await capoThree(page, ui);

    await expect(ui.chordLane).toHaveAttribute('data-capo', '3');
    const chords = chart.segments.filter((s) => s.root !== null).slice(0, 4);
    for (const chord of chords) {
      await mixer.seek((chord.startMs + chord.endMs) / 2000);
      await expect(ui.chordLane).toHaveAttribute('data-current-symbol', sharpSymbol(chord));
    }
    expect(chords.map(sharpSymbol)).toEqual(expectedCycle);
  });

  test('the suggestion order follows the R-14 rule', async ({ api, mixer, page }) => {
    const { track, chart } = await trackWithChords(api);
    const expected = rankCapos(chart, transpose).map((s) => s.capo);
    const ui = new InstrumentViews(page);
    await mixer.goto(track.id);
    await mixer.waitForReady(['guitar']);

    await ui.practiseAsFamily('guitar');
    await new PracticePanel(page).setTranspose(transpose);
    await ui.enableCapoMode();

    expect((await suite.views().capo(track.id, transpose)).suggestions.map((s) => s.capo)).toEqual(expected);
    await expect(ui.capoSuggestions).toHaveCount(expected.length);
    for (const [n, capo] of expected.entries()) {
      await expect(ui.capoSuggestion(n)).toHaveAttribute('data-capo', String(capo));
    }
  });

  test('the chord sheet and the tablature dialog take the capo', async ({ api, mixer, trackPage, scoreDialog, page }) => {
    const { track } = await trackWithChords(api);
    const ui = new InstrumentViews(page);
    await mixer.goto(track.id);
    await mixer.waitForReady(['guitar']);
    await capoThree(page, ui);

    await trackPage.goto(track.id);
    const sheet = await suite.views().sheet(track.id, transpose, 3);
    const symbols = sheet.rows.flatMap((r) => r.chords.map((c) => c.symbol));
    expect(symbols.length).toBeGreaterThan(0);
    expect(expectedCycle).toEqual(expect.arrayContaining(symbols.slice(0, 4)));
    await expect(ui.capoBadge).toHaveText('Capo 3');
    // The track page has its own display transpose (chord-sheet-transpose, 0 by default); the capo comes from capo mode.
    await page.getByTestId('chord-sheet-transpose').fill(String(transpose));
    await page.getByTestId('chord-sheet-transpose').press('Tab');
    const first = sheet.rows.findIndex((r) => r.chords.length > 0);
    await expect(ui.sheetChord(first, 0)).toHaveText(symbols[0] ?? '');

    await scoreDialog.openForStem('guitar');
    await page.getByTestId('score-profile').selectOption('guitar');
    await expect(page.getByTestId('score-tab-capo')).toHaveValue('3');
  });
});

test.describe('no capo outside the guitar family (016 US8)', { tag: '@instrument' }, () => {
  for (const family of ['bass', 'keys'] as const) {
    test(`${family}: no capo mode`, async ({ api, mixer, page }) => {
      const { track } = await trackWithChords(api);
      const ui = new InstrumentViews(page);
      await mixer.goto(track.id);
      await mixer.waitForReady(['guitar']);

      await ui.practiseAsFamily(family);

      await expect(ui.view).toBeVisible();
      await expect(ui.capoMode).toHaveCount(0);
      await expect(ui.capoBadge).toHaveCount(0);
    });
  }
});

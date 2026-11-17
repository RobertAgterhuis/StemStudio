import type { Page } from '@playwright/test';
import { axeViolations, type ViolationSummary } from '../support/axe';
import { browseSeedTimeoutMs, browseTrack, ensureBrowseSet } from '../support/library-seed';
import { expect, test } from '../support/library-test';
import { levelState, type DensityPreference } from '../support/preferences';
import { matrixThemes } from '../support/visual';

// Feature 022 (T054; SC-008, WCAG 2.2 AA): axe on the root, an artist, an album, a song folder,
// the search results and the library info dialog, in both themes and both densities, at Beginner
// (fewest controls) and Advanced (every control). SC-008 fails on serious or critical findings.
const densities: readonly DensityPreference[] = ['comfortable', 'compact'];

/** Serious and critical findings only (SC-008). */
async function seriousFindings(page: Page): Promise<ViolationSummary[]> {
  return (await axeViolations(page)).filter((violation) => violation.impact === 'serious' || violation.impact === 'critical');
}

for (const theme of matrixThemes) {
  for (const density of densities) {
    for (const level of ['beginner', 'advanced'] as const) {
      test.describe(`library folders / ${level} / ${theme} / ${density}`, { tag: '@library' }, () => {
        test.use({ storageState: levelState(level, { theme, density, motion: 'reduced' }) });

        test('root, artist, album, song folder, search results and the library info dialog', async ({ seedClients, folders, librarySearch, libraryInfo, page }, testInfo) => {
          test.setTimeout(browseSeedTimeoutMs + 120_000);
          const { placement } = browseTrack(await ensureBrowseSet(seedClients, testInfo), 'nora-harbour').item;
          await folders.gotoRoot();
          expect(await seriousFindings(page), 'root').toEqual([]);
          for (const [id, level] of [[placement.artist.folderId, 'artist'], [placement.album.folderId, 'album'], [placement.song.folderId, 'song']] as const) {
            await folders.gotoFolder(id);
            await expect(folders.view).toHaveAttribute('data-level', level);
            expect(await seriousFindings(page), level).toEqual([]);
          }
          await libraryInfo.openForTrack();
          expect(await seriousFindings(page), 'library info dialog').toEqual([]);
          await page.keyboard.press('Escape');
          await expect(libraryInfo.dialog).toBeHidden();
          await librarySearch.search('nora');
          await expect(librarySearch.results).toBeVisible();
          expect(await seriousFindings(page), 'search results').toEqual([]);
        });
      });
    }
  }
}

import { browseSeedTimeoutMs, browseTrack, ensureBrowseSet } from '../support/library-seed';
import { expect, test } from '../support/library-test';
import { cellState, matrixThemes, requireVisualContainer, shotName } from '../support/visual';

// Feature 022 (T054; SC-005): the folder art of the tiles, element screenshots (the names carry
// the run salt, the art does not: own pictures of tests/fixtures/artwork). Container only; the
// baselines are generated in the CI visual job (`npm run test:visual -- --update-snapshots`) at
// integration. The placeholder's tone follows the folder key (salted), so the placeholder and the
// broken-image fallback are asserted functionally in library-browse.spec.ts.
// The file name sorts after screens.spec.ts on purpose: the browse set adds a dozen tracks and
// artists, which would otherwise appear in the tracks, library-root and library screenshots.
requireVisualContainer();

const artCases = [
  ['cover', 'meters'],
  ['mosaic-2', 'duo-1'],
  ['mosaic-3', 'nora-harbour'],
  ['mosaic-4', 'four-1'],
] as const;

for (const theme of matrixThemes) {
  test.describe(`library art / ${theme}`, { tag: '@library' }, () => {
    test.use({ storageState: cellState('advanced', theme) });

    test('artist tiles: cover and mosaics of 2, 3 and 4 album covers', async ({ seedClients, folders, page }, testInfo) => {
      test.setTimeout(browseSeedTimeoutMs + 60_000);
      const set = await ensureBrowseSet(seedClients, testInfo);
      await folders.gotoRoot();
      for (const [name, key] of artCases) {
        const art = folders.tileArt(browseTrack(set, key).item.placement.artist.folderId);
        await art.scrollIntoViewIfNeeded();
        await expect(art.locator('img').first()).toBeVisible();
        await page.evaluate(() => document.fonts.ready.then(() => undefined));
        await expect(art).toHaveScreenshot(shotName(['library-art', name, theme]));
      }
    });
  });
}

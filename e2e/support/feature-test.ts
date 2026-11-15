/**
 * The `test` object of the feature 005 and 007 specs: the suite's `test` (support/test.ts)
 * extended with the artwork/band-info and library clients and page objects, plus a blank
 * scratch page for decoding audio. Kept separate from support/test.ts so the 004 fixtures stay
 * untouched until the 004 merge (see the qa 005/007 hand-off).
 *
 * @packageDocumentation
 */
import type { Page } from '@playwright/test';
import { ArtworkView } from '../pages/artwork-view';
import { BandInfoPanel } from '../pages/band-info-panel';
import { ExportDialog } from '../pages/export-dialog';
import { LibraryPage } from '../pages/library-page';
import { AssetsApi } from './assets-api';
import { apiBaseUrl } from './env';
import { MetadataApi } from './metadata-api';
import { test as suite } from './test';

/** Fixtures added by features 005 and 007. */
export interface FeatureFixtures {
  readonly metadataApi: MetadataApi;
  readonly assetsApi: AssetsApi;
  readonly artwork: ArtworkView;
  readonly bandInfo: BandInfoPanel;
  readonly exportDialog: ExportDialog;
  readonly library: LibraryPage;
  /** A second page at `about:blank` in the same context, for `decodeAudioData`. */
  readonly scratch: Page;
}

/** Extended Playwright test. */
export const test = suite.extend<FeatureFixtures>({
  metadataApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new MetadataApi(context));
    await context.dispose();
  },
  assetsApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new AssetsApi(context));
    await context.dispose();
  },
  artwork: async ({ page }, use) => {
    await use(new ArtworkView(page));
  },
  bandInfo: async ({ page }, use) => {
    await use(new BandInfoPanel(page));
  },
  exportDialog: async ({ page }, use) => {
    await use(new ExportDialog(page));
  },
  library: async ({ page }, use) => {
    await use(new LibraryPage(page));
  },
  scratch: async ({ context }, use) => {
    const page = await context.newPage();
    await use(page);
    await page.close();
  },
});

export { expect } from '@playwright/test';

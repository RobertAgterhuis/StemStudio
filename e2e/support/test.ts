/**
 * The suite's `test` object: Playwright's base test extended with the API clients and one page
 * object per screen. Specs import `test` and `expect` from here (feature 004 specs from
 * `ux-test.ts`, which extends this one).
 *
 * Fixtures are created lazily, so the 005/007 members (metadata and assets clients, artwork,
 * band info, export dialog, library page, scratch page) cost nothing in the 001-004 specs.
 *
 * @packageDocumentation
 */
import { test as base, type Page } from '@playwright/test';
import { ArtworkView } from '../pages/artwork-view';
import { BandInfoPanel } from '../pages/band-info-panel';
import { DetailSection } from '../pages/detail-section';
import { ExportDialog } from '../pages/export-dialog';
import { ImportPage } from '../pages/import-page';
import { LibraryPage } from '../pages/library-page';
import { MixLibraryPage } from '../pages/mix-library-page';
import { MixerPage } from '../pages/mixer-page';
import { TrackDetailPage } from '../pages/track-detail-page';
import { TrackListPage } from '../pages/track-list-page';
import { StemStudioApi } from './api';
import { AssetsApi } from './assets-api';
import { apiBaseUrl } from './env';
import { MetadataApi } from './metadata-api';

/** Fixtures available to every spec. */
export interface SuiteFixtures {
  readonly api: StemStudioApi;
  readonly importPage: ImportPage;
  readonly trackPage: TrackDetailPage;
  readonly trackList: TrackListPage;
  readonly mixer: MixerPage;
  readonly mixLibrary: MixLibraryPage;
  /** Detail-phase section of the track detail page (feature 002). */
  readonly detail: DetailSection;
  /** Metadata, artwork and band-info routes (feature 005). */
  readonly metadataApi: MetadataApi;
  /** Library routes `/api/assets/**` (feature 007). */
  readonly assetsApi: AssetsApi;
  /** Covers, thumbnails and own-picture actions (feature 005). */
  readonly artwork: ArtworkView;
  /** Band info panel and correction dialog (feature 005). */
  readonly bandInfo: BandInfoPanel;
  /** Export triggers and the export dialog (feature 007). */
  readonly exportDialog: ExportDialog;
  /** The library page `/library` (feature 007). */
  readonly library: LibraryPage;
  /** A second page at `about:blank` in the same context, for `decodeAudioData`. */
  readonly scratch: Page;
}

/** Extended Playwright test. */
export const test = base.extend<SuiteFixtures>({
  api: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new StemStudioApi(context));
    await context.dispose();
  },
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
  importPage: async ({ page }, use) => {
    await use(new ImportPage(page));
  },
  trackPage: async ({ page }, use) => {
    await use(new TrackDetailPage(page));
  },
  trackList: async ({ page }, use) => {
    await use(new TrackListPage(page));
  },
  mixer: async ({ page }, use) => {
    await use(new MixerPage(page));
  },
  mixLibrary: async ({ page }, use) => {
    await use(new MixLibraryPage(page));
  },
  detail: async ({ page }, use) => {
    await use(new DetailSection(page));
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

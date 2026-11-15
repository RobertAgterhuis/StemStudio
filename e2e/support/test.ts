/**
 * The suite's `test` object: Playwright's base test extended with the API client and one page
 * object per screen. Specs import `test` and `expect` from here.
 *
 * @packageDocumentation
 */
import { test as base } from '@playwright/test';
import { DetailSection } from '../pages/detail-section';
import { ImportPage } from '../pages/import-page';
import { MixLibraryPage } from '../pages/mix-library-page';
import { MixerPage } from '../pages/mixer-page';
import { TrackDetailPage } from '../pages/track-detail-page';
import { TrackListPage } from '../pages/track-list-page';
import { StemStudioApi } from './api';
import { apiBaseUrl } from './env';

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
}

/** Extended Playwright test. */
export const test = base.extend<SuiteFixtures>({
  api: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new StemStudioApi(context));
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
});

export { expect } from '@playwright/test';

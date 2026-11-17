/**
 * The `test` object of the feature 022 specs: the 009/010 `test` (support/generative-test.ts, every
 * fixture of 001-010, created lazily) extended with the library client and the folder, search and
 * library info page objects.
 *
 * @packageDocumentation
 */
import { LibraryFoldersPage } from '../pages/library-folders-page';
import { LibraryInfoDialog } from '../pages/library-info-dialog';
import { LibrarySearch } from '../pages/library-search';
import { apiBaseUrl } from './env';
import { LibraryApi } from './library-api';
import type { LibrarySeedClients } from './library-seed';
import { test as base } from './generative-test';

/** Fixtures added by feature 022. */
export interface LibraryFixtures {
  readonly libraryApi: LibraryApi;
  readonly folders: LibraryFoldersPage;
  readonly librarySearch: LibrarySearch;
  readonly libraryInfo: LibraryInfoDialog;
  /** The clients `library-seed.ts` needs, bundled. */
  readonly seedClients: LibrarySeedClients;
}

/** Extended Playwright test. */
export const test = base.extend<LibraryFixtures>({
  libraryApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new LibraryApi(context));
    await context.dispose();
  },
  folders: async ({ page }, use) => use(new LibraryFoldersPage(page)),
  librarySearch: async ({ page }, use) => use(new LibrarySearch(page)),
  libraryInfo: async ({ page }, use) => use(new LibraryInfoDialog(page)),
  seedClients: async ({ api, assetsApi, libraryApi, metadataApi }, use) => use({ api, assetsApi, libraryApi, metadataApi }),
});

export { expect } from '@playwright/test';

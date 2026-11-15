/**
 * The `test` object of the feature 004 specs: the suite's base test plus the shared data
 * ({@link UxData}, seeded once per worker) and page objects for the shell, help and tours.
 *
 * @packageDocumentation
 */
import { AppShell } from '../pages/app-shell';
import { HelpPanel } from '../pages/help-panel';
import { TourPopover } from '../pages/tour-popover';
import { StemStudioApi } from './api';
import { apiBaseUrl, detailPhaseTimeoutMs, fakeJobTimeoutMs } from './env';
import { test as base } from './test';
import { seedUxData, type UxData } from './ux-data';

/** Per-test fixtures added for feature 004. */
export interface UxFixtures {
  readonly shell: AppShell;
  readonly help: HelpPanel;
  readonly tour: TourPopover;
}

/** Per-worker fixtures added for feature 004. */
export interface UxWorkerFixtures {
  readonly uxData: UxData;
}

/** Extended Playwright test for feature 004 specs. */
export const test = base.extend<UxFixtures, UxWorkerFixtures>({
  uxData: [
    async ({ playwright }, use) => {
      const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
      await use(await seedUxData(new StemStudioApi(context)));
      await context.dispose();
    },
    { scope: 'worker', timeout: 2 * (fakeJobTimeoutMs + detailPhaseTimeoutMs) },
  ],
  shell: async ({ page }, use) => {
    await use(new AppShell(page));
  },
  help: async ({ page }, use) => {
    await use(new HelpPanel(page));
  },
  tour: async ({ page }, use) => {
    await use(new TourPopover(page));
  },
});

export { expect } from '@playwright/test';

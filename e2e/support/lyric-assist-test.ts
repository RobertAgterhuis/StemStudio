/**
 * The `test` object of the feature 023 specs: the 011 agent `test` (support/agent-test.ts, every
 * fixture of 001-011, created lazily) extended with the lyric assistant client and pane.
 *
 * @packageDocumentation
 */
import { LyricAssistPane } from '../pages/lyric-assist-pane';
import { test as agent } from './agent-test';
import { apiBaseUrl } from './env';
import { LyricAssistApi } from './lyric-assist-api';

/** Fixtures added by feature 023. */
export interface LyricAssistFixtures {
  readonly lyricAssistApi: LyricAssistApi;
  readonly assistPane: LyricAssistPane;
}

/** Extended Playwright test. */
export const test = agent.extend<LyricAssistFixtures>({
  lyricAssistApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new LyricAssistApi(context));
    await context.dispose();
  },
  assistPane: async ({ page }, use) => use(new LyricAssistPane(page)),
});

export { expect } from '@playwright/test';

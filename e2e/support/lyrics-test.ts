/**
 * The `test` object of the feature 006 and 008 specs: the suite's `test` (support/test.ts, which
 * holds the 005/007 fixtures since they were folded in) extended with the lyrics and sheet music
 * clients and page objects.
 *
 * @packageDocumentation
 */
import { KaraokeBanner } from '../pages/karaoke-banner';
import { LyricsSection } from '../pages/lyrics-section';
import { ScoreDialog } from '../pages/score-dialog';
import { ScoreItems } from '../pages/score-items';
import { apiBaseUrl } from './env';
import { test as feature } from './test';
import { LyricsApi } from './lyrics-api';
import { ScoreApi } from './score-api';

/** Fixtures added by features 006 and 008. */
export interface LyricsScoreFixtures {
  readonly lyricsApi: LyricsApi;
  readonly scoreApi: ScoreApi;
  readonly karaoke: KaraokeBanner;
  readonly lyrics: LyricsSection;
  readonly scoreDialog: ScoreDialog;
  readonly scoreItems: ScoreItems;
}

/** Extended Playwright test. */
export const test = feature.extend<LyricsScoreFixtures>({
  lyricsApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new LyricsApi(context));
    await context.dispose();
  },
  scoreApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new ScoreApi(context));
    await context.dispose();
  },
  karaoke: async ({ page }, use) => {
    await use(new KaraokeBanner(page));
  },
  lyrics: async ({ page }, use) => {
    await use(new LyricsSection(page));
  },
  scoreDialog: async ({ page }, use) => {
    await use(new ScoreDialog(page));
  },
  scoreItems: async ({ page }, use) => {
    await use(new ScoreItems(page));
  },
});

export { expect } from '@playwright/test';

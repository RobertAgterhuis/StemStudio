/**
 * The `test` object of the feature 009 and 010 specs: the feature 006/008 `test`
 * (support/lyrics-test.ts) extended with the generation and voice clients and page objects.
 * The generation ids of the track pages live in `pages/track-detail-page.ts` and
 * `pages/track-list-page.ts` (folded in after the UI merge, TEST-STRATEGY 16.6).
 *
 * @packageDocumentation
 */
import { GenerationStatusBanner } from '../pages/generation-status';
import { GenerationsList } from '../pages/generations-list';
import { LyricsListPage } from '../pages/lyrics-list-page';
import { RestyleDialog } from '../pages/restyle-dialog';
import { SongPage } from '../pages/song-page';
import { StyleEditorPage } from '../pages/style-editor-page';
import { StyleLibraryPage } from '../pages/style-library-page';
import { UseMyVoiceDialog } from '../pages/use-my-voice-dialog';
import { VoiceProfilesPage } from '../pages/voice-profiles-page';
import { apiBaseUrl } from './env';
import { GenerationApi } from './generation-api';
import { test as lyricsScore } from './lyrics-test';
import { VoiceApi } from './voice-api';

/** Fixtures added by features 009 and 010. */
export interface GenerativeFixtures {
  readonly generationApi: GenerationApi;
  readonly voiceApi: VoiceApi;
  readonly status: GenerationStatusBanner;
  readonly restyle: RestyleDialog;
  readonly generations: GenerationsList;
  readonly styles: StyleLibraryPage;
  readonly styleEditor: StyleEditorPage;
  readonly song: SongPage;
  readonly lyricsList: LyricsListPage;
  readonly voices: VoiceProfilesPage;
  readonly useMyVoice: UseMyVoiceDialog;
}

/** Extended Playwright test. */
export const test = lyricsScore.extend<GenerativeFixtures>({
  generationApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new GenerationApi(context));
    await context.dispose();
  },
  voiceApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new VoiceApi(context));
    await context.dispose();
  },
  status: async ({ page }, use) => use(new GenerationStatusBanner(page)),
  restyle: async ({ page }, use) => use(new RestyleDialog(page)),
  generations: async ({ page }, use) => use(new GenerationsList(page)),
  styles: async ({ page }, use) => use(new StyleLibraryPage(page)),
  styleEditor: async ({ page }, use) => use(new StyleEditorPage(page)),
  song: async ({ page }, use) => use(new SongPage(page)),
  lyricsList: async ({ page }, use) => use(new LyricsListPage(page)),
  voices: async ({ page }, use) => use(new VoiceProfilesPage(page)),
  useMyVoice: async ({ page }, use) => use(new UseMyVoiceDialog(page)),
});

export { expect } from '@playwright/test';

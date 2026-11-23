/**
 * `test` of the feature 015 recording specs: the suite fixtures plus the recording panel page object, the app shell (for
 * the experience level) and the recording API client. Specs set the fake capture input with `test.use({ launchOptions })`.
 *
 * @packageDocumentation
 */
import { AppShell } from '../pages/app-shell';
import { RecordingPanel } from '../pages/recording-panel';
import { apiContextOptions } from './env';
import { RecordingApi } from './recording-api';
import { test as suite } from './test';

/** Fixtures added for feature 015. */
export interface RecordingFixtures {
  /** Recording panel, take strip, calibration dialog and Takes panel. */
  readonly recording: RecordingPanel;
  /** The app shell (level switch). */
  readonly shell: AppShell;
  /** `/api/tracks/{id}/takes`, `/api/takes/**`, `/api/recording/calibrations`. */
  readonly recordingApi: RecordingApi;
}

/** The recording test. */
export const test = suite.extend<RecordingFixtures>({
  recording: async ({ page }, use) => {
    await use(new RecordingPanel(page));
  },
  shell: async ({ page }, use) => {
    await use(new AppShell(page));
  },
  recordingApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext(apiContextOptions);
    await use(new RecordingApi(context));
    await context.dispose();
  },
});

export { expect } from '@playwright/test';

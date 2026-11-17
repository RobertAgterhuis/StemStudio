/**
 * `test` of the feature 015 recording specs: the suite fixtures plus the recording panel page object and the recording
 * API client. Specs set the fake capture input with `test.use({ launchOptions })` per instrument family.
 *
 * @packageDocumentation
 */
import { RecordingPanel } from '../pages/recording-panel';
import { apiBaseUrl } from './env';
import { RecordingApi } from './recording-api';
import { test as suite } from './test';

/** Fixtures added for feature 015. */
export interface RecordingFixtures {
  /** Recording panel, take strip and Takes panel. */
  readonly recording: RecordingPanel;
  /** `/api/tracks/{id}/takes`, `/api/takes/**`, `/api/recording/calibrations`. */
  readonly recordingApi: RecordingApi;
}

/** The recording test. */
export const test = suite.extend<RecordingFixtures>({
  recording: async ({ page }, use) => {
    await use(new RecordingPanel(page));
  },
  recordingApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new RecordingApi(context));
    await context.dispose();
  },
});

export { expect } from '@playwright/test';

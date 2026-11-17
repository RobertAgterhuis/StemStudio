import { defineConfig, devices } from '@playwright/test';
import { webBaseUrl } from './support/env';
import { advancedState } from './support/preferences';

// One id per suite run, inherited by every worker process (see support/env.ts runId()).
process.env.E2E_RUN_ID ??= Date.now().toString(36);

/** Desktop Chrome at a fixed size and scale, so screenshots and scans are comparable. */
const fixedChrome = { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 };

/**
 * StemStudio E2E configuration (T022): Chromium and Firefox, retries 0, one worker because both
 * projects share one stack whose worker processes one job at a time.
 *
 * Feature 004 (T011): every context starts at level Advanced with the welcome dismissed and no
 * tour offers (`advancedState`, ui-test-ids 1.3.0 R7), so the 1.2.0 specs run unchanged; new specs
 * override `storageState`. Projects `visual` (container only), `a11y` and `lighthouse-setup` run
 * the design-quality suites (`npm run test:visual`, `test:a11y`, `test:lighthouse`); `npm test`
 * selects only `chromium` and `firefox`.
 *
 * Feature 015 (T010): project `recording` runs the `@recording` specs (`npm run test:recording`) in
 * the full Chromium build (`channel: 'chromium'`, fake media devices, microphone permission); each
 * spec sets its own fake capture WAV (`support/fake-microphone.ts`, `tests/fixtures/recording/`,
 * generated before the run). The default `chromium` project never runs `@recording`.
 */
export default defineConfig({
  testDir: './tests',
  retries: 0,
  workers: 1,
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  timeout: 180_000,
  expect: {
    timeout: 10_000,
    // SC-005: 0.1 % of pixels; animations off, caret hidden, CSS pixels.
    toHaveScreenshot: { maxDiffPixelRatio: 0.001, animations: 'disabled', caret: 'hide', scale: 'css' },
  },
  // Baselines are rendered only in the Playwright Linux container, so the path has no platform.
  snapshotPathTemplate: '{testDir}/__screenshots__/{testFilePath}/{arg}{ext}',
  outputDir: 'test-results',
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
    ['junit', { outputFile: 'test-results/junit.xml' }],
  ],
  use: {
    baseURL: webBaseUrl,
    storageState: advancedState,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', grepInvert: /@recording/, use: { ...devices['Desktop Chrome'] } },
    {
      name: 'recording',
      grep: /@recording/,
      use: {
        ...devices['Desktop Chrome'],
        channel: 'chromium',
        permissions: ['microphone'],
        launchOptions: { args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] },
      },
    },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'visual', testDir: './visual', use: { ...fixedChrome, reducedMotion: 'reduce' } },
    { name: 'a11y', testDir: './a11y', use: { ...fixedChrome, reducedMotion: 'reduce' } },
    { name: 'lighthouse-setup', testDir: './lighthouse', testMatch: '*.setup.ts', use: { ...devices['Desktop Chrome'] } },
  ],
});

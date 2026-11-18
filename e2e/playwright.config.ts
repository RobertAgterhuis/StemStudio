import path from 'node:path';
import { defineConfig, devices, type Project } from '@playwright/test';
import { webBaseUrl } from './support/env';
import { advancedState } from './support/preferences';

// One id per suite run, inherited by every worker process (see support/env.ts runId()).
process.env.E2E_RUN_ID ??= Date.now().toString(36);

/** The qa singing fixtures (`tests/fixtures/generate_instrument_fixtures.py`, git-ignored, generated before the run). */
const instrumentAudio = path.resolve(__dirname, '..', 'tests', 'fixtures', 'audio');

/**
 * A full-Chromium project whose microphone plays `wav` (feature 016, T011/T107): runs only the specs tagged
 * `@<name>` (each singing spec tags the fixture it needs), with fake media devices and microphone permission.
 */
function fakeMicProject(name: string, wav: string): Project {
  return {
    name,
    grep: new RegExp(`@${name}`),
    use: {
      ...devices['Desktop Chrome'],
      channel: 'chromium',
      permissions: ['microphone'],
      launchOptions: {
        args: [
          '--use-fake-device-for-media-stream',
          '--use-fake-ui-for-media-stream',
          `--use-file-for-fake-audio-capture=${path.join(instrumentAudio, wav)}`,
        ],
      },
    },
  };
}

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
 *
 * Feature 016 (T011, T144): projects `fake-mic-on-pitch`, `fake-mic-semitone` and `fake-mic-silence` feed the qa
 * fixtures `sing-on-pitch.wav`, `sing-semitone-off.wav` and `silence.wav` as the microphone and run only specs tagged
 * `@fake-mic-on-pitch` / `@fake-mic-semitone` / `@fake-mic-silence` (every such spec also carries `@fake-mic`);
 * `npm run test:fake-mic` runs the three, `npm run test:instrument` the other `@instrument` specs. `npm test`
 * (and `test:chromium` / `test:firefox`, the CI matrix) excludes both tags; CI runs them as their own passes.
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
    { name: 'chromium', grepInvert: /@recording|@fake-mic/, use: { ...devices['Desktop Chrome'] } },
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
    { name: 'firefox', grepInvert: /@fake-mic/, use: { ...devices['Desktop Firefox'] } },
    fakeMicProject('fake-mic-on-pitch', 'sing-on-pitch.wav'),
    fakeMicProject('fake-mic-semitone', 'sing-semitone-off.wav'),
    fakeMicProject('fake-mic-silence', 'silence.wav'),
    { name: 'visual', testDir: './visual', use: { ...fixedChrome, reducedMotion: 'reduce' } },
    { name: 'a11y', testDir: './a11y', use: { ...fixedChrome, reducedMotion: 'reduce' } },
    { name: 'lighthouse-setup', testDir: './lighthouse', testMatch: '*.setup.ts', use: { ...devices['Desktop Chrome'] } },
  ],
});

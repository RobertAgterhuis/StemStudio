import { defineConfig, devices } from '@playwright/test';
import { webBaseUrl } from './support/env';

// One id per suite run, inherited by every worker process (see support/env.ts runId()).
process.env.E2E_RUN_ID ??= Date.now().toString(36);

/**
 * StemStudio E2E configuration (T022): Chromium and Firefox, retries 0, one worker because both
 * projects share one stack whose worker processes one job at a time.
 */
export default defineConfig({
  testDir: './tests',
  retries: 0,
  workers: 1,
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  timeout: 180_000,
  expect: { timeout: 10_000 },
  outputDir: 'test-results',
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
    ['junit', { outputFile: 'test-results/junit.xml' }],
  ],
  use: {
    baseURL: webBaseUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
  ],
});

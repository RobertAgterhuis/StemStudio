/**
 * Fake microphone for the voice-profile recorder (feature 010, `voice-record-*`).
 *
 * Chromium plays a WAV file as the microphone (`--use-fake-device-for-media-stream
 * --use-file-for-fake-audio-capture=<file>`, the own-voice fixture); permission is granted per
 * context (`permissions: ['microphone']`), so a context without it sees a denied prompt. Firefox
 * gets its built-in fake stream (a steady tone, also voiced) with the permission prompt disabled.
 *
 * @packageDocumentation
 */
import type { LaunchOptions } from '@playwright/test';

/** Browser name of a Playwright project. */
export type BrowserName = 'chromium' | 'firefox' | 'webkit';

/** Launch options that turn `file` into the microphone of the browser. */
export function fakeMicrophoneLaunchOptions(browserName: BrowserName, file: string): LaunchOptions {
  if (browserName === 'firefox') {
    return {
      firefoxUserPrefs: { 'media.navigator.streams.fake': true, 'media.navigator.permission.disabled': true },
    };
  }
  return { args: ['--use-fake-device-for-media-stream', `--use-file-for-fake-audio-capture=${file}`] };
}

/** Context permissions that let the page use the fake microphone (Chromium only; Firefox needs none). */
export function microphonePermissions(browserName: BrowserName): string[] {
  return browserName === 'chromium' ? ['microphone'] : [];
}

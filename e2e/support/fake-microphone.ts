/**
 * Fake microphone for the voice-profile recorder (feature 010, `voice-record-*`).
 *
 * Chromium plays a WAV file as the microphone (`--use-fake-device-for-media-stream
 * --use-file-for-fake-audio-capture=<file>`, the own-voice fixture); permission is granted per
 * context (`permissions: ['microphone']`), so a context without it sees a denied prompt. The E2E
 * suite runs Chromium only (Firefox dropped, owner decision 2026-11-23).
 *
 * @packageDocumentation
 */
import type { LaunchOptions } from '@playwright/test';

/** Browser name of a Playwright project. */
export type BrowserName = 'chromium' | 'firefox' | 'webkit';

/** Launch options that turn `file` into the microphone of the browser. */
export function fakeMicrophoneLaunchOptions(browserName: BrowserName, file: string): LaunchOptions {
  // The default headless shell has no audio capture (getUserMedia: NotSupportedError, even for the
  // fake device); the full Chromium build in new headless mode has it.
  return { channel: 'chromium', args: ['--use-fake-device-for-media-stream', `--use-file-for-fake-audio-capture=${file}`] };
}

/** Context permissions that let the page use the fake microphone (Chromium; other browsers get none). */
export function microphonePermissions(browserName: BrowserName): string[] {
  return browserName === 'chromium' ? ['microphone'] : [];
}

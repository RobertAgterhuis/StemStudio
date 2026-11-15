/**
 * Run conditions of the feature 005 and 007 specs (called inside a test or a `beforeEach`), so
 * a spec only runs on the stack and in the browser it asserts.
 *
 * @packageDocumentation
 */
import { test } from '@playwright/test';
import { fakeOnlineMode, modeSkipReason, onlineAllowed, type FakeOnlineMode } from './online';

/** Skips unless the stack's `STEMSTUDIO_FAKE_ONLINE` is one of `modes`. */
export function requireOnlineMode(...modes: FakeOnlineMode[]): void {
  test.skip(!modes.includes(fakeOnlineMode), modeSkipReason(modes));
}

/** Skips unless `E2E_ONLINE=1` allows real YouTube/Spotify imports (`@online`). */
export function requireNetwork(): void {
  test.skip(!onlineAllowed, 'needs E2E_ONLINE=1 and internet access (real YouTube/Spotify import)');
}

/** The value of an environment variable; skips the test when it is unset or empty. */
export function requireEnv(name: string): string {
  const value = process.env[name] ?? '';
  test.skip(value === '', `set ${name}`);
  return value;
}

/** Skips unless `E2E_PERF=1` (the `@perf` cases seed 100 tracks; reference machine only). */
export function requirePerf(): void {
  test.skip(process.env.E2E_PERF !== '1', 'needs E2E_PERF=1 (seeds 100 tracks; timing is for the reference machine)');
}

/** Skips in every browser but Chromium (the reference browser of research.md E-05). */
export function requireChromium(browserName: string): void {
  test.skip(browserName !== 'chromium', 'Chromium is the reference browser of this check');
}

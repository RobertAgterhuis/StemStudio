/**
 * Checks on plain `<audio controls>` elements (take players, voice samples): they are not driven
 * by the mixer engine, so "plays" means the browser loaded and decoded enough of the file.
 *
 * @packageDocumentation
 */
import { expect, type Locator } from '@playwright/test';

const haveCurrentData = 2;

/** `readyState` of an audio element (0 when the element is not an `HTMLMediaElement`). */
export async function readyState(audio: Locator): Promise<number> {
  return audio.evaluate((element) => (element instanceof HTMLMediaElement ? element.readyState : 0));
}

/** Asks the element to load and waits until `readyState >= HAVE_CURRENT_DATA` (2). */
export async function expectPlayable(audio: Locator, timeoutMs = 20_000): Promise<void> {
  await audio.evaluate((element) => {
    if (element instanceof HTMLMediaElement && element.readyState < 2) {
      element.load();
    }
  });
  await expect.poll(() => readyState(audio), { timeout: timeoutMs }).toBeGreaterThanOrEqual(haveCurrentData);
}

/** `duration` of an audio element in seconds, once its metadata is loaded. */
export async function mediaDuration(audio: Locator): Promise<number> {
  await expect.poll(() => readyState(audio)).toBeGreaterThanOrEqual(1);
  return audio.evaluate((element) => (element instanceof HTMLMediaElement ? element.duration : Number.NaN));
}

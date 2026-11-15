/**
 * Level visibility rules R7-R9 of ui-test-ids 1.3.0 as id lists, plus helpers to assert them and
 * to time a level switch (SC-010).
 *
 * @packageDocumentation
 */
import { expect, type Page } from '@playwright/test';
import type { Level } from './preferences';

/** R8: the only 1.2.0 mixer ids that Beginner does not render, for the given channel keys. */
export function beginnerHiddenMixerIds(keys: readonly string[]): string[] {
  return [
    ...keys.map((key) => `pan-${key}`),
    'loop-start',
    'loop-end',
    'metronome-bpm',
    'metronome-offset',
    'metronome-beats-per-bar',
    'metronome-reset',
  ];
}

/** Mixer ids rendered at every level (a sample of every 1.2.0 control kind plus presets). */
export function everyLevelMixerIds(keys: readonly string[]): string[] {
  const perChannel = ['strip', 'fader', 'mute', 'solo', 'meter'].flatMap((kind) => keys.map((key) => `${kind}-${key}`));
  return [
    ...perChannel,
    'meter-master',
    'master-fader',
    'transport-play',
    'transport-seek',
    'transport-position',
    'loop-toggle',
    'loop-set-a',
    'loop-set-b',
    'metronome-toggle',
    'metronome-volume',
    'mix-save',
    'mix-save-as',
    'mixer-presets',
  ];
}

/** R9: Expert-only mixer ids. */
export function expertMixerIds(keys: readonly string[]): string[] {
  const perChannel = ['fader-value', 'pan-value', 'meter-db'].flatMap((kind) => keys.map((key) => `${kind}-${key}`));
  return [...perChannel, 'meter-db-master'];
}

/** Asserts that every id is rendered exactly once. */
export async function expectRendered(page: Page, ids: readonly string[]): Promise<void> {
  for (const id of ids) {
    await expect(page.getByTestId(id), id).toHaveCount(1);
  }
}

/** Asserts that no id is rendered. */
export async function expectNotRendered(page: Page, ids: readonly string[]): Promise<void> {
  for (const id of ids) {
    await expect(page.getByTestId(id), id).toHaveCount(0);
  }
}

/**
 * Clicks `level-<level>` inside the page and returns the milliseconds until `app-shell` carries
 * the new `data-level` (measured in the page, so Playwright's IPC is not counted; SC-010).
 */
export async function timedLevelSwitch(page: Page, level: Level): Promise<number> {
  return page.evaluate(
    (wanted) =>
      new Promise<number>((resolve, reject) => {
        const shell = document.querySelector('[data-testid="app-shell"]');
        const button = document.querySelector<HTMLElement>(`[data-testid="level-${wanted}"]`);
        if (shell === null || button === null) {
          reject(new Error('app-shell or level button missing'));
          return;
        }
        const start = performance.now();
        const observer = new MutationObserver(() => {
          if (shell.getAttribute('data-level') === wanted) {
            observer.disconnect();
            resolve(performance.now() - start);
          }
        });
        observer.observe(shell, { attributes: true, attributeFilter: ['data-level'] });
        button.click();
      }),
    level,
  );
}

/** Seconds of the first `m:ss(.s)` time in a text such as "0:05.3 / 0:20". */
export function positionSeconds(text: string): number {
  const match = /(\d+):(\d{2}(?:\.\d+)?)/.exec(text);
  return match === null ? Number.NaN : Number(match[1]) * 60 + Number(match[2]);
}

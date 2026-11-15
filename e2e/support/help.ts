/**
 * Expectations of the help suite (quickstart V-H1..V-H5, SC-009).
 *
 * @packageDocumentation
 */
import { expect, type Page } from '@playwright/test';
import type { HelpPanel } from '../pages/help-panel';

/** The 20 reference queries of quickstart V-H3: query -> expected first topic. */
export const referenceQueries: Readonly<Record<string, string>> = {
  pan: 'mixer-pan',
  solo: 'mixer-mute-solo',
  mute: 'mixer-mute-solo',
  loop: 'mixer-loop',
  'a-b': 'mixer-loop',
  metronome: 'mixer-metronome',
  click: 'mixer-metronome',
  bpm: 'bpm',
  tempo: 'bpm',
  downbeat: 'downbeat',
  'detail stems': 'detail-stems',
  kick: 'detail-outcomes',
  youtube: 'import-url',
  spotify: 'import-url',
  upload: 'import-file',
  gpu: 'processing-device',
  save: 'mixer-save',
  'delete track': 'track-delete',
  shortcuts: 'shortcuts',
  copyright: 'legal-notice',
};

/** Runs every reference query and returns the ones whose first result is wrong (query -> got). */
export async function wrongFirstResults(help: HelpPanel): Promise<Record<string, string>> {
  const wrong: Record<string, string> = {};
  for (const [query, expected] of Object.entries(referenceQueries)) {
    await help.search(query);
    const got = await firstResultFor(help, expected);
    if (got !== expected) wrong[query] = got;
  }
  return wrong;
}

/**
 * First result once the list shows `expected` first, or whatever is first after 3 s. A stale list
 * of the previous query can already contain `expected` further down, so the first position is
 * polled rather than the presence of `expected`.
 */
async function firstResultFor(help: HelpPanel, expected: string): Promise<string> {
  try {
    await expect.poll(() => help.firstResultTopic(), { timeout: 3_000 }).toBe(expected);
  } catch {
    // Settled on another topic: report whatever is first.
  }
  return help.firstResultTopic();
}

/** Moves focus to the document body (no marked control focused, shortcuts active). */
export async function blurActiveElement(page: Page): Promise<void> {
  await page.evaluate(() => {
    const active = document.activeElement;
    if (active instanceof HTMLElement) active.blur();
  });
}

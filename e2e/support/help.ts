/**
 * Expectations of the help suite (quickstart V-H1..V-H5, SC-009), plus the reference queries
 * and markers of the help topics added by features 005 and 007 (007 quickstart X10).
 *
 * @packageDocumentation
 */
import { expect, type Locator, type Page } from '@playwright/test';
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

/**
 * Reference queries of the topics added by feature 005 (ui-test-ids 1.4.0: `artwork`,
 * `band-info`, `online-lookups`): query -> expected first topic. Kept apart from the 20 of
 * V-H3 so SC-009 stays as specified.
 */
export const artworkReferenceQueries: Readonly<Record<string, string>> = {
  cover: 'artwork',
  artwork: 'artwork',
  'band information': 'band-info',
  wikipedia: 'band-info',
  'online lookups': 'online-lookups',
  'open data': 'online-lookups',
};

/**
 * Reference queries of the topics added by feature 007 (ui-test-ids 1.5.0: `library`,
 * `export-dialog`, `export-formats`, `export-range`, `export-clipping`).
 */
export const exportReferenceQueries: Readonly<Record<string, string>> = {
  'save to library': 'library',
  'what I hear': 'export-dialog',
  'selected channels': 'export-dialog',
  flac: 'export-formats',
  'bit depth': 'export-formats',
  'export range': 'export-range',
  clipping: 'export-clipping',
};

/**
 * Runs every reference query and returns the ones whose first result is wrong (query -> got).
 * @param queries Query -> expected first topic; defaults to the 20 of V-H3.
 */
export async function wrongFirstResults(
  help: HelpPanel,
  queries: Readonly<Record<string, string>> = referenceQueries,
): Promise<Record<string, string>> {
  const wrong: Record<string, string> = {};
  for (const [query, expected] of Object.entries(queries)) {
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

/**
 * Asserts that `scope` or an element inside it carries the help marker `data-help-id="<topicId>"`
 * (ui-test-ids "Help markers": markers are validated by the help tests, never used to select a
 * control).
 */
export async function expectHelpMarker(scope: Locator, topicId: string): Promise<void> {
  const marked = (): Promise<boolean> =>
    scope.evaluate((el, id) => {
      const selector = `[data-help-id="${id}"]`;
      return el.matches(selector) || el.querySelector(selector) !== null;
    }, topicId);
  await expect.poll(marked, `data-help-id="${topicId}"`).toBe(true);
}

/** Moves focus to the document body (no marked control focused, shortcuts active). */
export async function blurActiveElement(page: Page): Promise<void> {
  await page.evaluate(() => {
    const active = document.activeElement;
    if (active instanceof HTMLElement) active.blur();
  });
}

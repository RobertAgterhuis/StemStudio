/**
 * Expectations of the help suite (quickstart V-H1..V-H5, SC-009), plus the reference queries
 * and markers of the help topics added by features 005, 007, 006 and 008 (007 quickstart X10).
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
  // "Save to library" is the label of the export buttons, so it finds the dialog topic.
  'save to library': 'export-dialog',
  download: 'library',
  'what I hear': 'export-dialog',
  'selected channels': 'export-dialog',
  flac: 'export-formats',
  'bit depth': 'export-formats',
  'export range': 'export-range',
  clipping: 'export-clipping',
};

/**
 * Reference queries of the topics added by feature 006 (ui-test-ids 1.6.0: `karaoke`,
 * `karaoke-offset`, `lyrics-sources`, `lyrics-align`).
 */
export const lyricsReferenceQueries: Readonly<Record<string, string>> = {
  karaoke: 'karaoke',
  'sing along': 'karaoke',
  'lyrics offset': 'karaoke-offset',
  lrclib: 'lyrics-sources',
  'align lyrics': 'lyrics-align',
  whisper: 'lyrics-align',
};

/**
 * Reference queries of the topics added by feature 008 (ui-test-ids 1.7.0: `sheet-music`,
 * `sheet-music-profiles`, `sheet-music-key-tempo`, `sheet-music-drums`, `sheet-music-piano`).
 */
export const scoreReferenceQueries: Readonly<Record<string, string>> = {
  'sheet music': 'sheet-music',
  musicxml: 'sheet-music',
  clef: 'sheet-music-profiles',
  'key signature': 'sheet-music-key-tempo',
  'drum notation': 'sheet-music-drums',
  'grand staff': 'sheet-music-piano',
};

/**
 * Reference queries of the 15 help topics of features 009 and 010 (ui-test-ids 1.8.0/1.9.0;
 * TEST-STRATEGY 16.6 item 2) plus the extra screen topic `styles-new` of `/styles/new`.
 */
export const generativeReferenceQueries: Readonly<Record<string, string>> = {
  'graphics card': 'generation-gpu-required',
  'style library': 'styles-library',
  'edit style': 'styles-editor',
  'new style': 'styles-new',
  restyle: 'restyle-options',
  strength: 'restyle-strength',
  'ai label': 'generation-ai-label',
  'personal use': 'generation-personal-use',
  generations: 'generation-limits',
  'lyrics format': 'lyrics-format',
  'add sections': 'lyrics-sections',
  'new song': 'song-options',
  language: 'song-language',
  'voice profile': 'voice-profiles',
  consent: 'voice-consent',
  'use my voice': 'voice-conversion',
};

/** Screen topic per 009/010 page (help opened with the button). */
export const generativeScreenTopics: Readonly<Record<string, string>> = {
  '/styles': 'styles-library',
  '/styles/new': 'styles-new',
  '/generations': 'generation-limits',
  '/songs/new': 'song-options',
  '/voices': 'voice-profiles',
};

/** Help markers per 009/010 page that need no generator (the restyle dialog's are in generative.spec.ts). */
export const generativePageMarkers: Readonly<Record<string, readonly string[]>> = {
  '/styles': ['styles-library', 'generation-gpu-required'],
  '/styles/new': ['styles-editor'],
  '/generations': ['generation-limits', 'generation-gpu-required'],
  '/songs/new': ['song-options', 'song-language', 'lyrics-format', 'lyrics-sections', 'generation-personal-use'],
};

/** Screen topic per 011 page (help opened with the button; ui-test-ids 1.10.0). */
export const agentScreenTopics: Readonly<Record<string, string>> = {
  '/settings': 'agent-backends',
};

/** Help markers per 011 page (`agent-backends` on `settings-agent-backend`). */
export const agentPageMarkers: Readonly<Record<string, readonly string[]>> = {
  '/settings': ['agent-backends'],
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

/**
 * Reference queries of the help topics added by feature 012 (ui-test-ids 1.11.0 "Help markers":
 * `practice-speed`, `practice-transpose`, `practice-count-in`, `practice-speed-trainer`,
 * `practice-sound-quality`).
 */
export const practiceReferenceQueries: Readonly<Record<string, string>> = {
  'slow down': 'practice-speed',
  transpose: 'practice-transpose',
  'count-in': 'practice-count-in',
  'speed trainer': 'practice-speed-trainer',
  'sound quality': 'practice-sound-quality',
};

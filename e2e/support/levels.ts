/**
 * Level visibility rules R7-R9 of ui-test-ids 1.3.0, with the Beginner exceptions of 1.4.0
 * (feature 005) and 1.5.0 (feature 007), as id lists, plus helpers to assert them and to time a
 * level switch (SC-010).
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

// Feature 005 (ui-test-ids 1.4.0) and 007 (1.5.0): R7 holds for every new id (rendered at
// Advanced and Expert); the contracts' "Beginner" columns list the exceptions below. Ids that
// exist only in some state (`artwork-reset`, `artwork-error`, `export-mp3-bitrate`,
// `export-range-loop`, `export-include-detail`) are only in the hidden lists: absent at Beginner
// in every state.

/** 1.4.0 + 1.5.0 track detail ids rendered at every level, for the given standard stems. */
export function everyLevelTrackFeatureIds(stems: readonly string[]): string[] {
  return ['track-cover', 'metadata-status', 'band-info-open', ...stems.map((stem) => `export-stem-${stem}`)];
}

/** 1.4.0 + 1.5.0 track detail ids that Beginner does not render. */
export const beginnerHiddenTrackFeatureIds: readonly string[] = [
  'artwork-upload-input',
  'artwork-reset',
  'artwork-error',
  'export-all-stems',
];

/** 1.4.0 + 1.5.0 track detail ids rendered at Advanced on a track with the automatic picture. */
export const advancedTrackFeatureIds: readonly string[] = ['artwork-upload-input', 'export-all-stems'];

/** 1.4.0 + 1.5.0 mixer ids rendered at every level. */
export const everyLevelMixerFeatureIds: readonly string[] = ['mixer-cover', 'band-info-open', 'mixer-export'];

/** Band info facts that Beginner does not render (1.4.0; status `found`). */
export const beginnerHiddenBandInfoIds: readonly string[] = [
  'band-info-country',
  'band-info-genres',
  'band-info-members',
  'band-info-website',
  'band-info-sources',
];

/** Export dialog ids rendered at every level in mode `mix` (Beginner: the format only, FLAC preselected). */
export const everyLevelMixDialogIds: readonly string[] = [
  'export-source-audible',
  'export-format',
  'export-name',
  'export-submit',
  'export-cancel',
];

/** Export dialog ids that Beginner does not render (1.5.0); `export-channel-*` is matched by prefix. */
export const beginnerHiddenDialogIds: readonly string[] = [
  'export-source-selected',
  'export-bit-depth',
  'export-mp3-bitrate',
  'export-range-loop',
  'export-range-start',
  'export-range-end',
  'export-include-detail',
];

/** Export dialog ids rendered at Advanced in mode `mix` with the default FLAC and no loop region. */
export const advancedMixDialogIds: readonly string[] = [
  'export-source-selected',
  'export-bit-depth',
  'export-range-start',
  'export-range-end',
];

/** Library ids rendered at every level. */
export const everyLevelLibraryIds: readonly string[] = ['library-total-size'];

/** Library filters that Beginner does not render (1.5.0). */
export const beginnerHiddenLibraryIds: readonly string[] = ['library-filter-track', 'library-filter-kind', 'library-search'];

/** Asserts that no element's `data-testid` starts with `prefix` (per-key ids such as `export-channel-`). */
export async function expectNoIdWithPrefix(page: Page, prefix: string): Promise<void> {
  await expect(page.locator(`[data-testid^="${prefix}"]`), `${prefix}*`).toHaveCount(0);
}

// Features 009 (ui-test-ids 1.8.0) and 010 (1.9.0): R7 again; the Beginner columns list the ids
// below as "not rendered" (the defaults apply). `restyle-stem-<stem>` is per stem of the track.

/** 1.8.0 restyle dialog ids rendered at every level. */
export const everyLevelRestyleIds: readonly string[] = [
  'restyle-style-select',
  'restyle-style-description',
  'restyle-personal-use-notice',
  'restyle-start',
  'restyle-cancel',
];

/** 1.8.0 restyle dialog ids that Beginner does not render, for the given stems of the track. */
export function beginnerHiddenRestyleIds(stems: readonly string[]): string[] {
  return [
    'restyle-mode-range',
    'restyle-mode-inplace',
    'restyle-range-start',
    'restyle-range-end',
    ...stems.map((stem) => `restyle-stem-${stem}`),
    'restyle-strength',
    'restyle-takes',
  ];
}

/** 1.9.0 song page ids rendered at every level (the Advanced options are listed below). */
export const everyLevelSongIds: readonly string[] = [
  'song-lyrics-select',
  'song-lyrics-text',
  'song-sections-propose',
  'song-title-input',
  'song-language-select',
  'song-style-select',
  'song-lead-voice',
  'song-instrumental',
  'song-duration',
  'song-generate',
  'song-personal-use-notice',
];

/** 1.9.0 song page ids that Beginner does not render. */
export const beginnerHiddenSongIds: readonly string[] = [
  'song-free-caption',
  'song-delivery-input',
  'song-bpm',
  'song-key',
  'song-time-signature',
  'song-takes',
];

/** 1.9.0 "Use my voice" dialog ids rendered at every level. */
export const everyLevelUseMyVoiceIds: readonly string[] = [
  'take-voice-profile-select',
  'take-voice-convert-start',
  'take-voice-convert-cancel',
];

/** 1.9.0 "Use my voice" dialog ids that Beginner does not render. */
export const beginnerHiddenUseMyVoiceIds: readonly string[] = ['take-voice-pitch-shift', 'take-voice-auto-shift'];

/** 1.8.0/1.9.0 rail links, rendered at every level. */
export const generativeNavIds: readonly string[] = ['nav-generations', 'nav-styles', 'nav-songs', 'nav-lyrics', 'nav-voices'];

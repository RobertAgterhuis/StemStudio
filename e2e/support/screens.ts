/**
 * The screens of the feature 004 matrices (SC-001, SC-005): how to open each one with the seeded
 * data and when it is ready to be scanned or captured.
 *
 * @packageDocumentation
 */
import { expect, test, type Page } from '@playwright/test';
import type { UxData } from './ux-data';
import { ensureLibraryItem } from './ux-library';

/** Terminal `BandInfo.status` values (no skeleton left on screen). */
const settledMetadata = /^(found|notFound|failed|disabled)$/;

/**
 * Screen states covered by the visual and accessibility matrices. `band-info`, `export-dialog`
 * and `library` are the 005/007 states (ui-test-ids 1.4.0/1.5.0); the covers and thumbnails of
 * 005 appear on the 004 screens themselves. `style-library`, `song`, `voices` (with the consent
 * panel open) and `restyle-dialog` are the 009/010 states (1.8.0/1.9.0); the restyle dialog opens
 * only with a ready generator, so it skips on the default stack (TEST-STRATEGY 16.6 item 7).
 */
export type ScreenId =
  | 'import'
  | 'tracks'
  | 'track'
  | 'track-no-detail'
  | 'mixer'
  | 'mixer-expanded'
  | 'mixes'
  | 'band-info'
  | 'export-dialog'
  | 'library'
  | 'style-library'
  | 'song'
  | 'voices'
  | 'restyle-dialog';

/** Every screen of the matrices, in report order. */
export const screenIds: readonly ScreenId[] = [
  'import',
  'tracks',
  'track',
  'track-no-detail',
  'mixer',
  'mixer-expanded',
  'mixes',
  'band-info',
  'export-dialog',
  'library',
  'style-library',
  'song',
  'voices',
  'restyle-dialog',
];

/** Help screen topic per screen (design-spec.md 7; V-H1). */
export const screenTopics: Readonly<Record<ScreenId, string>> = {
  import: 'import-screen',
  tracks: 'tracks-library',
  track: 'track-screen',
  'track-no-detail': 'track-screen',
  mixer: 'mixer-screen',
  'mixer-expanded': 'mixer-screen',
  mixes: 'mixes-library',
  'band-info': 'track-screen',
  'export-dialog': 'mixer-screen',
  library: 'library',
  'style-library': 'styles-library',
  song: 'song-options',
  voices: 'voice-profiles',
  'restyle-dialog': 'track-screen',
};

interface ScreenDefinition {
  readonly path: (data: UxData) => string;
  readonly ready: (page: Page, data: UxData) => Promise<void>;
  /** Data the screen needs beyond {@link UxData}, created before navigating. */
  readonly prepare?: (data: UxData) => Promise<unknown>;
}

const screens: Readonly<Record<ScreenId, ScreenDefinition>> = {
  import: { path: () => '/', ready: importReady },
  tracks: { path: () => '/tracks', ready: tracksReady },
  track: { path: (d) => `/tracks/${d.detailTrack.id}`, ready: (p) => trackReady(p, 'completed') },
  'track-no-detail': { path: (d) => `/tracks/${d.plainTrack.id}`, ready: (p, d) => trackReady(p, d.plainTrack.detail.status) },
  mixer: { path: (d) => `/tracks/${d.detailTrack.id}/mixer`, ready: mixerReady },
  'mixer-expanded': { path: (d) => `/tracks/${d.detailTrack.id}/mixer`, ready: mixerExpandedReady },
  mixes: { path: () => '/mixes', ready: (p, d) => expect(p.getByTestId(`mix-row-${d.mixId}`)).toBeVisible() },
  'band-info': { path: (d) => `/tracks/${d.detailTrack.id}`, ready: bandInfoReady },
  'export-dialog': { path: (d) => `/tracks/${d.detailTrack.id}/mixer`, ready: exportDialogReady },
  library: { path: () => '/library', ready: libraryReady, prepare: (d) => ensureLibraryItem(d.detailTrack.id) },
  'style-library': { path: () => '/styles', ready: styleLibraryReady },
  song: { path: () => '/songs/new', ready: songReady },
  voices: { path: () => '/voices', ready: voicesReady },
  'restyle-dialog': { path: (d) => `/tracks/${d.detailTrack.id}`, ready: restyleDialogReady },
};

/**
 * Opens a screen (after creating the data it needs), waits for the shell and the screen's
 * content, then for the web fonts.
 */
export async function openScreen(page: Page, id: ScreenId, data: UxData): Promise<void> {
  const screen = screens[id];
  await screen.prepare?.(data);
  await page.goto(screen.path(data));
  await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', /^(beginner|advanced|expert)$/);
  await screen.ready(page, data);
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
}

async function importReady(page: Page): Promise<void> {
  await expect(page.getByTestId('legal-notice')).toBeVisible();
  await expect(page.getByTestId('worker-device')).toHaveAttribute('data-device', /^(cpu|gpu|unknown)$/);
}

async function tracksReady(page: Page, data: UxData): Promise<void> {
  await expect(page.getByTestId(`track-row-${data.detailTrack.id}`)).toBeVisible();
  await expect(page.getByTestId(`track-row-${data.plainTrack.id}`)).toBeVisible();
}

async function trackReady(page: Page, detailStatus: string): Promise<void> {
  await expect(page.getByTestId('track-status')).toHaveText(/completed/i);
  await expect(page.getByTestId('detail-status')).toHaveText(detailStatus);
}

async function mixerReady(page: Page): Promise<void> {
  await expect(page.getByTestId('strip-vocals')).toBeVisible();
  await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
}

async function mixerExpandedReady(page: Page): Promise<void> {
  await mixerReady(page);
  await page.getByTestId('expand-drums').click();
  await expect(page.getByTestId('expand-drums')).toHaveAttribute('aria-expanded', 'true', { timeout: 30_000 });
  await expect(page.getByTestId('strip-drums-kick')).toBeVisible();
  // The re-layout leaves the pointer over the drums Mute/Solo, whose help tooltip would then be captured.
  await movePointerAway(page);
}

async function movePointerAway(page: Page): Promise<void> {
  await page.mouse.move(0, 0);
  await expect(page.getByTestId(/^help-tooltip-/)).toHaveCount(0);
}

/** Track page with the band info panel open and its lookup settled (any online mode). */
async function bandInfoReady(page: Page): Promise<void> {
  await trackReady(page, 'completed');
  await page.getByTestId('band-info-open').click();
  await expect(page.getByTestId('band-info-panel')).toBeVisible();
  await expect(page.getByTestId('band-info-status')).toHaveAttribute('data-status', settledMetadata, { timeout: 60_000 });
  await movePointerAway(page);
}

/** Mixer with the export dialog open in mode `mix`. */
async function exportDialogReady(page: Page): Promise<void> {
  await mixerReady(page);
  await page.getByTestId('mixer-export').click();
  await expect(page.getByTestId('export-dialog')).toHaveAttribute('data-mode', 'mix');
  await movePointerAway(page);
}

/** Library with the seeded item ready. */
async function libraryReady(page: Page): Promise<void> {
  await expect(page.getByTestId('library-total-size')).toBeVisible();
  await expect(page.getByTestId(/^asset-row-/).first()).toHaveAttribute('data-status', 'ready', { timeout: 60_000 });
}

/** Generation status banner or chip settled (`data-state` read from the API). */
async function generationStatusReady(page: Page): Promise<void> {
  await expect(page.getByTestId('generation-status')).toHaveAttribute('data-state', /^(notInstalled|unavailable|ready)$/);
}

/** Style library with its presets listed. */
async function styleLibraryReady(page: Page): Promise<void> {
  await generationStatusReady(page);
  await expect(page.locator('[data-testid^="style-row-"]').first()).toBeVisible();
}

/** New song page with the empty lyrics editor. */
async function songReady(page: Page): Promise<void> {
  await generationStatusReady(page);
  await expect(page.getByTestId('song-lyrics-text')).toBeVisible();
  await expect(page.getByTestId('song-style-select')).toBeVisible();
}

/** Voice profiles with the "new voice profile" panel and its consent statement open. */
async function voicesReady(page: Page): Promise<void> {
  await generationStatusReady(page);
  await expect(page.getByTestId('voices-empty').or(page.locator('[data-testid^="voice-row-"]').first())).toBeVisible();
  await page.getByTestId('voice-new').click();
  await expect(page.getByTestId('voice-consent-text')).toHaveAttribute('data-version', /\S/);
  await movePointerAway(page);
}

/** Track page with the restyle dialog open; skips unless a generator is ready. */
async function restyleDialogReady(page: Page): Promise<void> {
  await trackReady(page, 'completed');
  await generationStatusReady(page);
  const state = await page.getByTestId('generation-status').getAttribute('data-state');
  test.skip(state !== 'ready', `the restyle dialog needs a ready generator (generation status "${state ?? ''}")`);
  await page.getByTestId('track-restyle').click();
  await expect(page.getByTestId('restyle-dialog')).toBeVisible();
  await expect(page.getByTestId('restyle-style-description')).not.toBeEmpty();
  await movePointerAway(page);
}

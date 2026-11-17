/**
 * The screens of the feature 004 matrices (SC-001, SC-005): how to open each one with the seeded
 * data and when it is ready to be scanned or captured.
 *
 * @packageDocumentation
 */
import { expect, test, type Page } from '@playwright/test';
import type { UxData } from './ux-data';
import { ensureLibraryItem, ensureScoreItem, uxLibraryItemName, uxScoreItemName } from './ux-library';

/** Terminal `BandInfo.status` values (no skeleton left on screen). */
const settledMetadata = /^(found|notFound|failed|disabled)$/;

/**
 * Screen states covered by the visual and accessibility matrices. `band-info`, `export-dialog`
 * and `library` are the 005/007 states (ui-test-ids 1.4.0/1.5.0); the covers and thumbnails of
 * 005 appear on the 004 screens themselves. Feature 006 adds the karaoke banner to `mixer` and
 * the lyrics card to `track` (fake lyrics mode `word`); feature 012 adds `mixer-practice` (the
 * practice panel engaged at 65 % / +2 st); feature 008 adds `score-dialog`, the
 * `score-stem-*` row to `track` and a ready score item to `library`. `style-library`, `song`, `voices` (with the consent
 * panel open) and `restyle-dialog` are the 009/010 states (1.8.0/1.9.0); the restyle dialog opens
 * only with a ready generator, so it skips on the default stack (TEST-STRATEGY 16.6 item 7).
 * `settings` is the 011 page `/settings` with its agent section (ui-test-ids 1.10.0); its screen
 * topic is `agent-backends`, the topic of its first section. Feature 022 (ui-test-ids 1.22.0):
 * `library` is the song folder that holds the seeded items (the 007 rows render there at every
 * level; `/library?view=all` is Advanced+), `library-root` the folder view `/library`.
 */
export type ScreenId =
  | 'import'
  | 'tracks'
  | 'track'
  | 'track-no-detail'
  | 'mixer'
  | 'mixer-expanded'
  | 'mixer-practice'
  | 'mixes'
  | 'band-info'
  | 'export-dialog'
  | 'library'
  | 'library-root'
  | 'score-dialog'
  | 'style-library'
  | 'song'
  | 'voices'
  | 'restyle-dialog'
  | 'settings';

/** Every screen of the matrices, in report order. */
export const screenIds: readonly ScreenId[] = [
  'import',
  'tracks',
  'track',
  'track-no-detail',
  'mixer',
  'mixer-expanded',
  'mixer-practice',
  'mixes',
  'band-info',
  'export-dialog',
  'library',
  'library-root',
  'score-dialog',
  'style-library',
  'song',
  'voices',
  'restyle-dialog',
  'settings',
];

/** Help screen topic per screen (design-spec.md 7; V-H1). */
export const screenTopics: Readonly<Record<ScreenId, string>> = {
  import: 'import-screen',
  tracks: 'tracks-library',
  track: 'track-screen',
  'track-no-detail': 'track-screen',
  mixer: 'mixer-screen',
  'mixer-expanded': 'mixer-screen',
  'mixer-practice': 'mixer-screen',
  mixes: 'mixes-library',
  'band-info': 'track-screen',
  'export-dialog': 'mixer-screen',
  library: 'library',
  'library-root': 'library',
  'score-dialog': 'track-screen',
  'style-library': 'styles-library',
  song: 'song-options',
  voices: 'voice-profiles',
  'restyle-dialog': 'track-screen',
  settings: 'agent-backends',
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
  'mixer-practice': { path: (d) => `/tracks/${d.detailTrack.id}/mixer`, ready: mixerPracticeReady },
  mixes: { path: () => '/mixes', ready: (p, d) => expect(p.getByTestId(`mix-row-${d.mixId}`)).toBeVisible() },
  'band-info': { path: (d) => `/tracks/${d.detailTrack.id}`, ready: bandInfoReady },
  'export-dialog': { path: (d) => `/tracks/${d.detailTrack.id}/mixer`, ready: exportDialogReady },
  library: { path: () => librarySongPath, ready: libraryReady, prepare: prepareLibrary },
  'library-root': { path: () => '/library', ready: libraryRootReady, prepare: prepareLibrary },
  'score-dialog': { path: (d) => `/tracks/${d.detailTrack.id}`, ready: scoreDialogReady },
  'style-library': { path: () => '/styles', ready: styleLibraryReady },
  song: { path: () => '/songs/new', ready: songReady },
  voices: { path: () => '/voices', ready: voicesReady },
  'restyle-dialog': { path: (d) => `/tracks/${d.detailTrack.id}`, ready: restyleDialogReady },
  settings: { path: () => '/settings', ready: settingsReady },
};

/** Song folder of the seeded items (022), set by {@link prepareLibrary}; "All items" until known. */
let librarySongPath = '/library?view=all';

async function prepareLibrary(data: UxData): Promise<void> {
  const item = await ensureLibraryItem(data.detailTrack.id);
  await ensureScoreItem(data.detailTrack.id);
  const folderId = (item as { readonly placement?: { readonly song: { readonly folderId: string } } | null }).placement?.song.folderId;
  librarySongPath = folderId === undefined ? '/library?view=all' : `/library/${folderId}`;
}

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

/**
 * Mixer with the practice panel opened and engaged through every-level controls (feature 012,
 * ui-test-ids 1.11.0): speed 65 % (quality hint shown) and +2 semitones, stretch stage active. Nothing is
 * saved, so the seeded data (and the mix library) stay as they are.
 */
async function mixerPracticeReady(page: Page): Promise<void> {
  await mixerReady(page);
  const speed = page.getByTestId('practice-speed');
  const panel = page.getByTestId('practice-panel');
  // The matrix stores no panel choice, so the panel starts closed at neutral settings (012 integration).
  const toggle = page.getByTestId('practice-panel-toggle');
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-expanded', 'true');
  await speed.focus();
  await speed.press('Home');
  await expect(panel).toHaveAttribute('data-speed', '50');
  // +5 % steps, each confirmed: fast key repeats on the slider can race the server re-render
  // (a lost step made this screen flaky in the 012 integration run).
  for (const percent of ['55', '60', '65']) {
    await page.getByTestId('practice-speed-up').click();
    await expect(panel).toHaveAttribute('data-speed', percent);
  }
  for (const semitones of ['1', '2']) {
    await page.getByTestId('practice-transpose-up').click();
    await expect(panel).toHaveAttribute('data-transpose', semitones);
  }
  await expect(panel).toHaveAttribute('data-stretch', 'active', { timeout: 15_000 });
  await expect(page.getByTestId('practice-quality-hint')).toBeVisible();
  await speed.blur();
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

/** Library with the seeded audio and score items ready (the score row shows its PDF preview). */
async function libraryRootReady(page: Page): Promise<void> {
  await expect(page.getByTestId('library-folders')).toHaveAttribute('data-level', 'root');
  await expect(page.getByTestId(/^folder-tile-ar[0-9a-f]{16}$/u).first()).toBeVisible();
}

async function libraryReady(page: Page): Promise<void> {
  await expect(page.getByTestId('library-folders').or(page.getByTestId('library-total-size')).first()).toBeVisible();
  const rows = page.getByTestId(/^asset-row-/);
  const audio = rows.filter({ hasText: uxLibraryItemName, hasNotText: uxScoreItemName });
  for (const row of [audio, rows.filter({ hasText: uxScoreItemName })]) {
    await expect(row.first()).toHaveAttribute('data-status', 'ready', { timeout: 60_000 });
  }
  await expect(page.getByTestId(/^asset-preview-/).first()).toBeVisible();
}

/** Track page with the sheet music dialog open for the bass. */
async function scoreDialogReady(page: Page): Promise<void> {
  await trackReady(page, 'completed');
  await page.getByTestId('score-stem-bass').click();
  await expect(page.getByTestId('score-dialog')).toHaveAttribute('data-source', 'channel');
  await expect(page.getByTestId('score-estimate')).toBeVisible();
  // The click scrolled the stem row into view; a full-page capture of a scrolled page draws the
  // fixed shell and backdrop at that offset, so the screen is captured from the top.
  await page.evaluate(() => {
    window.scrollTo(0, 0);
  });
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  await movePointerAway(page);
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

/** Settings page with the agent section: backend choice and the Ollama and Claude status. */
async function settingsReady(page: Page): Promise<void> {
  await expect(page.getByTestId('settings-agent-backend')).toBeVisible();
  await expect(page.getByTestId('settings-agent-ollama-status')).toBeVisible();
  await expect(page.getByTestId('settings-agent-claude-status')).toHaveAttribute('data-key-configured', /^(true|false)$/);
}

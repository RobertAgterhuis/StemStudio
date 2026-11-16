import type { Page } from '@playwright/test';
import type { TrackDto } from '../support/api';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import {
  advancedMixDialogIds,
  advancedTrackFeatureIds,
  agentShellIds,
  beginnerHiddenDialogIds,
  beginnerHiddenLibraryIds,
  beginnerHiddenSettingsIds,
  beginnerHiddenTrackFeatureIds,
  everyLevelLibraryIds,
  everyLevelMixDialogIds,
  everyLevelMixerFeatureIds,
  everyLevelSettingsIds,
  everyLevelTrackFeatureIds,
  expectNoIdWithPrefix,
  expectNotRendered,
  expectRendered,
  expertSettingsIds,
} from '../support/levels';
import { levelState } from '../support/preferences';
import { expect, test } from '../support/test';
import type { ExportDialog } from '../pages/export-dialog';
import type { MixerPage } from '../pages/mixer-page';

// Level rules of the 005/007 additions (ui-test-ids 1.4.0 and 1.5.0 "Beginner" columns, R7;
// 005 FR-015, 007 FR-014, quickstart X10). The band info facts at Beginner are asserted in
// band-info.spec.ts (they need STEMSTUDIO_FAKE_ONLINE=found). The 011 settings page (ui-test-ids
// 1.10.0) is covered at every level; the Advanced/Expert pickers depend on the backend state, so
// only their absence below their level is asserted.
let track: TrackDto;

test.beforeEach(async ({ api }) => {
  test.setTimeout(fakeJobTimeoutMs + 60_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

async function openTrack(page: Page): Promise<void> {
  await page.goto(`/tracks/${track.id}`);
  await expect(page.getByTestId('track-status')).toHaveText(/completed/i);
}

/** Opens `/settings` (feature 011) and waits for its agent section. */
async function openSettings(page: Page): Promise<void> {
  await page.goto('/settings');
  await expect(page.getByTestId('settings-agent-backend')).toBeVisible();
}

async function openMixDialog(page: Page, mixer: MixerPage, exportDialog: ExportDialog): Promise<void> {
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  await expectRendered(page, everyLevelMixerFeatureIds);
  await exportDialog.openForMix();
}

test.describe('Beginner', () => {
  test.use({ storageState: levelState('beginner') });

  test('the track page shows cover, status, band info and raw stem export, no own picture or stem pack', { tag: ['@artwork', '@export'] }, async ({ page }) => {
    await openTrack(page);
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'beginner');
    await expectRendered(page, everyLevelTrackFeatureIds(fakeStems));
    await expectNotRendered(page, beginnerHiddenTrackFeatureIds);
  });

  test('the export dialog offers only the format, with FLAC preselected', { tag: '@export' }, async ({ page, mixer, exportDialog }) => {
    await openMixDialog(page, mixer, exportDialog);
    await expectRendered(page, everyLevelMixDialogIds);
    await expect(exportDialog.byId('export-format')).toHaveValue('flac');
    await expect(exportDialog.byId('export-source-audible')).toBeChecked();
    await expectNotRendered(page, beginnerHiddenDialogIds);
    await expectNoIdWithPrefix(page, 'export-channel-');
  });

  test('the library has no filters or search', { tag: '@library' }, async ({ page, library }) => {
    await library.goto();
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'beginner');
    await expectRendered(page, everyLevelLibraryIds);
    await expectNotRendered(page, beginnerHiddenLibraryIds);
  });

  test('the settings page shows the backend choice, status, test and save, no model pickers', { tag: '@agent' }, async ({ page }) => {
    await openSettings(page);
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'beginner');
    await expectRendered(page, [...agentShellIds, ...everyLevelSettingsIds]);
    await expectNotRendered(page, beginnerHiddenSettingsIds);
  });
});

test.describe('Advanced', () => {
  test.use({ storageState: levelState('advanced') });

  test('the track page adds the own picture input and the stem pack', { tag: ['@artwork', '@export'] }, async ({ page }) => {
    await openTrack(page);
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'advanced');
    await expectRendered(page, [...everyLevelTrackFeatureIds(fakeStems), ...advancedTrackFeatureIds]);
    await expectNotRendered(page, ['artwork-reset', 'artwork-error']);
  });

  test('the export dialog adds bit depth, range and selected channels; MP3 swaps bit depth for bit rate', { tag: '@export' }, async ({ page, mixer, exportDialog }) => {
    await openMixDialog(page, mixer, exportDialog);
    await expectRendered(page, [...everyLevelMixDialogIds, ...advancedMixDialogIds]);
    await expectNotRendered(page, ['export-range-loop', 'export-include-detail']);
    await expectNoIdWithPrefix(page, 'export-channel-');
    await exportDialog.byId('export-source-selected').check();
    await expectRendered(page, fakeStems.map((stem) => `export-channel-${stem}`));
    await exportDialog.byId('export-format').selectOption('mp3');
    await expect(exportDialog.byId('export-mp3-bitrate')).toHaveValue('320');
    await expectNotRendered(page, ['export-bit-depth']);
  });

  test('the library has the track and kind filters and search', { tag: '@library' }, async ({ page, library }) => {
    await library.goto();
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'advanced');
    await expectRendered(page, [...everyLevelLibraryIds, ...beginnerHiddenLibraryIds]);
  });

  test('the settings page renders every level-free control and no Claude pickers (Expert only)', { tag: '@agent' }, async ({ page }) => {
    await openSettings(page);
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'advanced');
    await expectRendered(page, [...agentShellIds, ...everyLevelSettingsIds]);
    await expectNotRendered(page, expertSettingsIds);
  });
});

test.describe('Expert', () => {
  test.use({ storageState: levelState('expert') });

  test('the settings page renders every level-free control', { tag: '@agent' }, async ({ page }) => {
    await openSettings(page);
    await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', 'expert');
    await expectRendered(page, [...agentShellIds, ...everyLevelSettingsIds]);
  });
});

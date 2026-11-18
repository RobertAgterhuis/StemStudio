import { RecordingPanel } from '../pages/recording-panel';
import { axeViolations } from '../support/axe';
import { requireChromium } from '../support/feature-skips';
import { recordingLaunchOptions, takeReadyTimeoutMs } from '../support/recording';
import { RecordingApi } from '../support/recording-api';
import { apiBaseUrl } from '../support/env';
import { cellState, matrixThemes } from '../support/visual';
import { expect, test } from '../support/ux-test';

// Feature 015 T085 (SC-009, FR-021): axe-core on the recording panel, the calibration dialog, the Takes panel and the take
// strip in both themes at Expert (every control rendered); a keyboard-only record -> compare -> delete flow; the live
// region; no animation with reduced motion (the a11y project runs with `reducedMotion: 'reduce'`). Fake capture device.
test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-vocals.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(({ browserName }) => {
  requireChromium(browserName);
  test.setTimeout(240_000);
});

test.afterEach(async ({ playwright, uxData }) => {
  const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
  await new RecordingApi(context).deleteAllTakes(uxData.plainTrack.id);
  await context.dispose();
});

for (const theme of matrixThemes) {
  test.describe(`recording / ${theme}`, { tag: '@recording' }, () => {
    test.use({ storageState: cellState('expert', theme) });

    test('panel, calibration dialog, Takes panel and take strip have no violations', async ({ page, uxData }) => {
      const recording = new RecordingPanel(page);
      await page.goto(`/tracks/${uxData.plainTrack.id}/mixer`);
      await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
      await recording.openWithInput();
      expect(await axeViolations(page)).toEqual([]);

      await recording.byId('rec-calibrate').click();
      await expect(recording.byId('cal-dialog')).toBeVisible();
      expect(await axeViolations(page)).toEqual([]);
      await recording.closeCalibration();

      await recording.chooseCountIn(0);
      await recording.recordFor(2);
      await recording.waitForRow(await recording.newestSavedTakeId(), 'ready', takeReadyTimeoutMs);
      await expect(recording.strip).toBeVisible();
      expect(await axeViolations(page)).toEqual([]);
    });
  });
}

test.describe('recording by keyboard', { tag: '@recording' }, () => {
  // No microphone permission up front: with it granted the panel opens the input at once and never shows "Allow
  // microphone" (PermissionGate). The permission is granted while the button is shown, so pressing it by keyboard opens
  // the input as a user's "Allow" in the browser prompt would.
  test.use({ storageState: cellState('advanced', 'dark'), permissions: [] });

  test('record, compare and delete without a mouse; the live region announces the state', async ({ context, page, uxData }) => {
    const recording = new RecordingPanel(page);
    await context.clearPermissions();
    await page.goto(`/tracks/${uxData.plainTrack.id}/mixer`);
    await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
    await page.keyboard.press('r');
    await expect(recording.panel).toBeVisible();
    await expect(recording.byId('rec-allow-mic')).toBeVisible();
    await context.grantPermissions(['microphone']);
    await recording.byId('rec-allow-mic').focus();
    await page.keyboard.press('Enter');
    await expect(recording.panel).toHaveAttribute('data-permission', 'granted', { timeout: 15_000 });
    await recording.byId('rec-count-in').focus();
    await recording.byId('rec-count-in').selectOption('0');

    await recording.record.focus();
    await page.keyboard.press('Enter');
    await expect(recording.byId('rec-status')).toHaveText('Recording', { timeout: 15_000 });
    await expect(recording.byId('rec-status')).toHaveAttribute('aria-live', 'polite');
    await expect(recording.byId('rec-elapsed')).toHaveText(/^0:0[2-9]/, { timeout: 10_000 });
    await page.keyboard.press('Enter');
    const takeId = await recording.newestSavedTakeId();
    await recording.waitForRow(takeId, 'ready', takeReadyTimeoutMs);
    await expect(recording.byId('rec-status')).toHaveText('Saved');

    await recording.byId('take-compare-both').focus();
    await page.keyboard.press('ArrowRight');
    await expect(recording.byId('take-compare-take')).toHaveAttribute('aria-checked', 'true');
    await expect(recording.byId('take-compare-take')).toBeFocused();

    await recording.byId(`rec-take-delete-${takeId}`).focus();
    await page.keyboard.press('Enter');
    await page.getByTestId('confirm-dialog-ok').focus();
    await page.keyboard.press('Enter');
    await expect(recording.row(takeId)).toHaveCount(0);
  });

  test('nothing in the recording panel animates with reduced motion', async ({ context, page, uxData }) => {
    const recording = new RecordingPanel(page);
    await context.grantPermissions(['microphone']);
    await page.goto(`/tracks/${uxData.plainTrack.id}/mixer`);
    await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
    await recording.openWithInput();

    const running = await recording.panel.evaluate((panel) => panel.getAnimations({ subtree: true }).length);

    expect(running).toBe(0);
  });
});

import type { TrackDto } from '../support/api';
import { fakeStems, silentDb } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import {
  expectOnlyAppOrigins,
  flacBitDepth,
  recordingFamilies,
  recordingFixturePath,
  recordingLaunchOptions,
  takeReadyTimeoutMs,
  watchOrigins,
} from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 015 US1 (T044; FR-001..FR-006, FR-009, FR-011, FR-012, FR-017; SC-007, SC-008), project `recording`
// (full Chromium, fake capture device, TEST-STRATEGY 21.3). One case per instrument family, each with its own input
// file: record 10 s over the mix, the take row goes `local` -> `ready`, the take plays in the strip and is stored as a
// 24-bit FLAC; only the web and API origins are contacted. Further cases: the speed rule, monitoring and a denied
// microphone. The launch options are worker-scoped, so each family is its own describe block (its own browser).
let track: TrackDto;

test.beforeEach(async ({ api, browserName }) => {
  requireChromium(browserName);
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.afterEach(async ({ recordingApi }) => {
  await recordingApi.deleteAllTakes(track.id);
});

for (const { family, part, input, channels } of recordingFamilies) {
  test.describe(`recording ${family}`, { tag: '@recording' }, () => {
    test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions(input)), { scope: 'worker' }], permissions: ['microphone'] });

    test(`${family}: a 10 s take is saved as 24-bit FLAC and plays in the take strip (US1, SC-008)`, async ({
      page, mixer, recording, recordingApi, assetsApi,
    }) => {
      recordingFixturePath(input);
      const origins = watchOrigins(page);
      await mixer.goto(track.id);
      await mixer.waitForReady(fakeStems);
      await recording.openWithInput();
      await recording.choosePart(part);
      await recording.chooseCountIn(0);
      await recording.recordFor(10);

      await expect(recording.strip).toBeVisible({ timeout: 2_000 });
      const takeId = await recording.newestSavedTakeId();
      await recording.waitForRow(takeId, 'ready', takeReadyTimeoutMs);
      const take = await recordingApi.getTake(takeId);
      expect([take.part, take.inputChannels]).toEqual([part, channels]);
      expect(take.durationSeconds).toBeGreaterThan(9.5);
      expect(take.durationSeconds).toBeLessThan(11);
      const asset = await assetsApi.waitForReady(take.assetId, takeReadyTimeoutMs);
      expect([asset.audio?.format, asset.audio?.bitDepth]).toEqual(['flac', 24]);
      expect(flacBitDepth((await assetsApi.download(take.assetId)).bytes)).toBe(24);

      await expect(recording.strip).toHaveAttribute('data-take-id', takeId);
      await mixer.seek(take.startSample / 44_100);
      await mixer.play();
      await expect.poll(() => mixer.peak('take'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
      expectOnlyAppOrigins(origins);
    });
  });
}

test.describe('recording rules', { tag: '@recording' }, () => {
  test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-vocals.wav')), { scope: 'worker' }], permissions: ['microphone'] });

  test('speed 80 % disables Record with the reason; "Set to 100 %" enables it again (FR-009)', async ({ mixer, practice, recording }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await practice.set(80, 0);
    await recording.openWithInput();

    await expect(recording.record).toBeDisabled();
    await expect(recording.byId('rec-record-disabled-reason')).toHaveAttribute('data-reason', 'speed');
    await recording.byId('rec-set-normal-speed').click();

    await expect(recording.record).toBeEnabled();
    await expect(practice.panel).toHaveAttribute('data-speed', '100');
  });

  test('monitoring shows the headphones warning (US1-6, FR-006)', async ({ mixer, recording }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await recording.openWithInput();
    await expect(recording.byId('rec-monitoring-warning')).toHaveCount(0);

    await recording.byId('rec-monitoring').click();

    await expect(recording.byId('rec-monitoring')).toHaveAttribute('aria-checked', 'true');
    await expect(recording.byId('rec-monitoring-warning')).toBeVisible();
  });

  test('the privacy note comes before the microphone prompt (US1-1, FR-003)', async ({ mixer, recording }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await recording.open();

    await expect(recording.byId('rec-privacy-note')).toContainText('Recordings stay on this computer');
  });
});

test.describe('recording without permission', { tag: '@recording' }, () => {
  test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-vocals.wav')), { scope: 'worker' }], permissions: [] });

  test('a denied microphone shows rec-unavailable "denied" and the mixer still plays (FR-003)', async ({ context, mixer, recording }) => {
    await context.clearPermissions();
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await recording.open();
    await recording.byId('rec-allow-mic').click();

    await recording.expectUnavailable('denied');
    await expect(recording.byId('rec-retry-permission')).toBeVisible();
    await mixer.play();
    await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  });
});

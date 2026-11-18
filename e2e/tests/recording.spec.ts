import type { TrackDto } from '../support/api';
import { fakeStems, silentDb } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import { recordingLaunchOptions } from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 015 US1 (T044; FR-003, FR-006, FR-009), project `recording` (full Chromium, fake capture device,
// TEST-STRATEGY 21.3): the speed rule, monitoring, the privacy note and a denied microphone. The take case per
// instrument family is in `recording-family-<family>.spec.ts` (`support/recording-family.ts`): the fake capture file is
// a worker-scoped launch option, which Playwright accepts only at the top level of a spec file.
let track: TrackDto;

test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-vocals.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(async ({ api, browserName }) => {
  requireChromium(browserName);
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.afterEach(async ({ recordingApi }) => {
  await recordingApi.deleteAllTakes(track.id);
});

test.describe('recording rules', { tag: '@recording' }, () => {
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
  test.use({ permissions: [] });

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

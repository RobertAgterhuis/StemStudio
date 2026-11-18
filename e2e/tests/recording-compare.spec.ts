import type { TrackDto } from '../support/api';
import { fakeStems, silentDb } from '../support/detail';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import { recordingLaunchOptions, takeReadyTimeoutMs } from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 015 US4 (T065; FR-012, FR-013): a bass take; "My take" gates the bass stem (meter-bass silent) while the take
// meter moves; "Original" is the reverse; user mute/solo stay as they were; a saved mix restores the take strip.
let track: TrackDto;

test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-bass.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(async ({ api, browserName }) => {
  requireChromium(browserName);
  test.setTimeout(fakeJobTimeoutMs + 180_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.afterEach(async ({ recordingApi }) => {
  await recordingApi.deleteAllTakes(track.id);
});

test.describe('compare take and original', { tag: '@recording' }, () => {
  test('My take silences the bass stem, Original silences the take; the saved mix restores the strip (US4)', async ({
    api, mixer, recording, recordingApi,
  }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await recording.openWithInput();
    await recording.choosePart('bass');
    await recording.chooseCountIn(0);
    await recording.recordFor(8);
    const takeId = await recording.newestSavedTakeId();
    const take = await recordingApi.waitForTakeStatus(takeId, 'ready', takeReadyTimeoutMs);
    await expect(recording.strip).toHaveAttribute('data-take-id', takeId);

    await mixer.seek(take.startSample / 44_100);
    await mixer.play();
    await recording.byId('take-compare-take').click();
    await expect(recording.byId('take-compare-take')).toHaveAttribute('aria-checked', 'true');
    await expect.poll(() => mixer.peak('bass'), { timeout: 5_000 }).toBeLessThanOrEqual(silentDb);
    await expect.poll(() => mixer.peak('take'), { timeout: 5_000 }).toBeGreaterThan(silentDb);
    await expect(mixer.mute('bass')).toHaveAttribute('aria-pressed', 'false');

    await recording.byId('take-compare-original').click();
    await expect.poll(() => mixer.peak('take'), { timeout: 5_000 }).toBeLessThanOrEqual(silentDb);
    await expect.poll(() => mixer.peak('bass'), { timeout: 5_000 }).toBeGreaterThan(silentDb);
    await mixer.byId('transport-pause').click();

    // A run-unique name: a rerun against the same data volume must not hit "name taken" (the dialog then stays open).
    const name = `With my bass take ${runId()}`;
    await mixer.saveAs(name);
    await expect.poll(async () => (await api.findMix(track.id, name))?.id).toBeDefined();
    const mix = await api.findMix(track.id, name);
    await mixer.goto(track.id, mix?.id);
    await mixer.waitForReady(fakeStems);

    await expect(recording.strip).toHaveAttribute('data-take-id', takeId, { timeout: 15_000 });
  });
});

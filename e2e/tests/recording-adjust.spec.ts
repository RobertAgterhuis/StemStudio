import type { TrackDto } from '../support/api';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import { recordingLaunchOptions, takeReadyTimeoutMs } from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 015 US7 (T083; FR-016), Expert: adjusting a take by -10.0 ms and applying it stores the take again with its start
// 441 samples earlier, alignment revision 1 and the summed adjustment; the note names the shift.
let track: TrackDto;

test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-drums.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(async ({ api, browserName }) => {
  requireChromium(browserName);
  test.setTimeout(fakeJobTimeoutMs + 180_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.afterEach(async ({ recordingApi }) => {
  await recordingApi.deleteAllTakes(track.id);
});

test.describe('timing adjustment', { tag: '@recording' }, () => {
  test('-10.0 ms applied moves the start 441 samples earlier, revision 1 (US7)', async ({ mixer, recording, recordingApi, shell }) => {
    await mixer.goto(track.id);
    await shell.setLevel('expert');
    await mixer.waitForReady(fakeStems);
    await recording.openWithInput();
    await recording.chooseCountIn(0);
    await mixer.seek(5);
    await recording.recordFor(3);
    const takeId = await recording.newestSavedTakeId();
    const before = await recordingApi.waitForTakeStatus(takeId, 'ready', takeReadyTimeoutMs);
    await expect(recording.strip).toHaveAttribute('data-take-id', takeId);

    await recording.byId('take-adjust-ms').fill('-10.0');
    await recording.byId('take-adjust-ms').press('Tab');
    await recording.byId('take-adjust-apply').click();

    await expect.poll(async () => (await recordingApi.getTake(takeId)).alignmentRevision, { timeout: takeReadyTimeoutMs }).toBe(1);
    const after = await recordingApi.waitForTakeStatus(takeId, 'ready', takeReadyTimeoutMs);
    expect([before.startSample - after.startSample, after.adjustmentSamples]).toEqual([441, -441]);
    await expect(recording.byId('take-adjust-note')).toHaveText('Timing adjusted by -10.0 ms');
  });
});

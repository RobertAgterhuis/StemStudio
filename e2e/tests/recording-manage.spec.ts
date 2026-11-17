import type { MixerPage } from '../pages/mixer-page';
import type { RecordingPanel } from '../pages/recording-panel';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import { fixturePath } from '../support/fixtures';
import { recordingLaunchOptions, takeReadyTimeoutMs } from '../support/recording';
import type { RecordingApi, RecordingTakeDto } from '../support/recording-api';
import { expect, test } from '../support/recording-test';
import { uniqueWavCopy } from '../support/unique-audio';

// Feature 015 US5 (T070; FR-014, FR-018): three takes on an own track; rename one, star one, delete one (its asset answers
// 404 afterwards); the track delete confirmation names the takes, and deleting the track removes every take.
test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-other.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(({ browserName }) => {
  requireChromium(browserName);
  test.setTimeout(fakeJobTimeoutMs + 240_000);
});

test.describe('managing takes', { tag: '@recording' }, () => {
  test('rename, star and delete takes; deleting the track deletes its takes (US5)', async ({
    api, mixer, recording, recordingApi, trackPage,
  }, info) => {
    const track = await api.ensureFileCompleted(uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `takes-${info.project.name}-${runId()}`), fakeJobTimeoutMs);
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    const [newest, middle, oldest] = await recordThreeTakes(mixer, recording, recordingApi, track.id);

    await recording.rename(newest.id, 'Best ending');
    await recording.star(middle.id);
    await recording.deleteTake(oldest.id);

    expect((await recordingApi.getTake(newest.id)).name).toBe('Best ending');
    expect((await recordingApi.getTake(middle.id)).favorite).toBe(true);
    expect(await recordingApi.status(`/api/takes/${oldest.id}`)).toBe(404);
    expect(await recordingApi.status(`/api/assets/${oldest.assetId}`)).toBe(404);

    await trackPage.goto(track.id);
    await recording.byId('track-delete').click();
    await expect(recording.byId('track-delete-takes-note')).toContainText('2 takes');
    await recording.byId('track-delete-confirm').click();
    await expect.poll(() => api.trackStatus(track.id)).toBe(404);
    expect(await recordingApi.status(`/api/takes/${newest.id}`)).toBe(404);
    expect(await recordingApi.status(`/api/assets/${newest.assetId}`)).toBe(404);
  });
});

/** Records 2 s takes from 0, 3 and 6 s and waits until all three are ready; newest first. */
async function recordThreeTakes(
  mixer: MixerPage,
  recording: RecordingPanel,
  recordingApi: RecordingApi,
  trackId: string,
): Promise<[RecordingTakeDto, RecordingTakeDto, RecordingTakeDto]> {
  await recording.openWithInput();
  await recording.chooseCountIn(0);
  for (const start of [0, 3, 6]) {
    await mixer.seek(start);
    await recording.recordFor(2);
  }
  await expect.poll(async () => (await recordingApi.listTakes(trackId)).items.length, { timeout: 30_000 }).toBe(3);
  const takes = (await recordingApi.listTakes(trackId)).items;
  for (const take of takes) {
    await recordingApi.waitForTakeStatus(take.id, 'ready', takeReadyTimeoutMs);
  }
  return takes as unknown as [RecordingTakeDto, RecordingTakeDto, RecordingTakeDto];
}

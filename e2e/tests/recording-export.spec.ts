import type { TrackDto } from '../support/api';
import { wavFacts } from '../support/audio-check';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import { recordingLaunchOptions, takeReadyTimeoutMs } from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 015 US6 (T076; FR-015): from a take row, "Take only" as WAV 24-bit is exactly the take (its length in samples);
// "Take + mix" as MP3 appears in the Library with contents starting with the take name.
let track: TrackDto;

test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-piano.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(async ({ api, browserName }) => {
  requireChromium(browserName);
  test.setTimeout(fakeJobTimeoutMs + 240_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.afterEach(async ({ recordingApi }) => {
  await recordingApi.deleteAllTakes(track.id);
});

test.describe('exporting a take', { tag: '@recording @export' }, () => {
  test('take only (WAV 24-bit) is the take; take + mix (MP3) is in the Library as "Take n ..." (US6)', async ({
    assetsApi, exportDialog, mixer, recording, recordingApi,
  }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await recording.openWithInput();
    await recording.choosePart('piano');
    await recording.chooseCountIn(0);
    await mixer.seek(3);
    await recording.recordFor(4);
    const takeId = await recording.newestSavedTakeId();
    const take = await recordingApi.waitForTakeStatus(takeId, 'ready', takeReadyTimeoutMs);

    await recording.byId(`rec-take-export-${takeId}`).click();
    await recording.byId('export-source-take-only').check();
    await exportDialog.save({ format: 'wav', bitDepth: 24 });
    const only = await assetsApi.waitForReady(await exportDialog.savedAssetId(), takeReadyTimeoutMs);
    const facts = wavFacts((await assetsApi.download(only.id)).bytes);
    expect([facts.bitsPerSample, facts.channels, facts.frames]).toEqual([24, 2, take.lengthSamples]);
    await exportDialog.byId('export-close').click();

    await recording.byId(`rec-take-export-${takeId}`).click();
    await recording.byId('export-source-take-mix').check();
    await exportDialog.save({ format: 'mp3', mp3Bitrate: 192 });
    const mixed = await assetsApi.waitForReady(await exportDialog.savedAssetId(), takeReadyTimeoutMs);
    expect(mixed.provenance.contents.startsWith(take.name)).toBe(true);
    expect((await assetsApi.list({ trackId: track.id })).items.map((a) => a.id)).toContain(mixed.id);
  });
});

import type { TrackDto } from '../support/api';
import { fakeJobTimeoutMs } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import {
  alignmentOffset,
  recordingFixturePath,
  recordingLaunchOptions,
  recordingSampleRate,
  requireLoopbackStack,
  takeReadyTimeoutMs,
} from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 015 US2 (T051; FR-007, FR-008; SC-001, SC-003) on the TestLoopback stack (TEST-STRATEGY 21.3): the web runs
// with `StemStudio__Recording__TestLoopback=true`, so the engine's output is its input. Calibration succeeds within
// 10 s; a take recorded over the click-track fixture lines up with its stem within 2 samples (cross-correlation of the
// stored FLAC, which is padded to song sample 0); a manual latency labels the next take "manual".
let track: TrackDto;

test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-vocals.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(async ({ api, browserName, recordingApi }) => {
  requireChromium(browserName);
  requireLoopbackStack();
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  track = await api.ensureFileCompleted(recordingFixturePath('click-track.wav'), fakeJobTimeoutMs);
  await recordingApi.deleteAllCalibrations();
});

test.afterEach(async ({ recordingApi }) => {
  await recordingApi.deleteAllTakes(track.id);
  await recordingApi.deleteAllCalibrations();
});

test.describe('loopback calibration and alignment', { tag: '@recording' }, () => {
  test('calibration succeeds within 10 s and is stored for the input (SC-003)', async ({ mixer, recording, recordingApi }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(['vocals']);
    await recording.openWithInput();

    const { ok, elapsedMs } = await recording.calibrate();

    expect(ok).toBe(true);
    expect(elapsedMs).toBeLessThanOrEqual(10_000);
    await expect.poll(async () => (await recordingApi.listCalibrations()).map((c) => c.method)).toEqual(['loopback']);
    await recording.closeCalibration();
    await expect(recording.byId('rec-latency-status')).toHaveAttribute('data-method', 'loopback');
  });

  test('a take of the click track lines up with its stem within 2 samples (SC-001)', async ({ api, mixer, recording, recordingApi, scratch }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(['vocals']);
    await recording.openWithInput();
    expect((await recording.calibrate()).ok).toBe(true);
    await recording.closeCalibration();
    await recording.choosePart('none');
    await recording.chooseCountIn(0);
    await mixer.seek(2);

    await recording.recordFor(4);

    const takeId = await recording.newestSavedTakeId();
    const take = await recordingApi.waitForTakeStatus(takeId, 'ready', takeReadyTimeoutMs);
    expect(take.latency.method).toBe('loopback');
    const stemUrl = (await api.getTrack(track.id)).stems.find((s) => s.name === 'vocals')?.url ?? '';
    const files = { take: await recordingApi.audio(take.audioUrl ?? ''), stem: await recordingApi.audio(stemUrl) };
    const range = { from: take.startSample, to: take.startSample + take.lengthSamples };
    const offset = await alignmentOffset(scratch, files, range);
    expect(Math.abs(offset), `offset ${String(offset)} samples at ${String(recordingSampleRate)} Hz`).toBeLessThanOrEqual(2);
  });

  test('a manual latency labels the next take "manual" (US2 scenario 3)', async ({ mixer, recording, recordingApi }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(['vocals']);
    await recording.openWithInput();

    await recording.enterManualLatency(20);
    await recording.closeCalibration();
    await expect(recording.byId('rec-latency-status')).toHaveAttribute('data-method', 'manual');
    await recording.chooseCountIn(0);
    await recording.recordFor(2);

    const takeId = await recording.newestSavedTakeId();
    const take = await recordingApi.waitForTakeStatus(takeId, 'ready', takeReadyTimeoutMs);
    expect([take.latency.method, take.latency.milliseconds]).toEqual(['manual', 20]);
  });
});

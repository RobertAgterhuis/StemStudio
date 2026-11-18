import type { TrackDto } from '../support/api';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import { recordingLaunchOptions, takeReadyTimeoutMs } from '../support/recording';
import type { RecordingApi, RecordingTakeDto } from '../support/recording-api';
import { expect, test } from '../support/recording-test';

// Feature 015 US3 (T058; FR-010, FR-014): with the A-B loop 4-8 s enabled, recording three passes plus a 2 s partial pass
// gives four takes of one session (passes 1-3 full, pass 4 partial); starring pass 2 and "Delete the other passes"
// leaves exactly that take. Level Advanced (pass badges and session groups are Advanced, R10).
let track: TrackDto;

test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-guitar.wav')), { scope: 'worker' }], permissions: ['microphone'] });

test.beforeEach(async ({ api, browserName }) => {
  requireChromium(browserName);
  test.setTimeout(fakeJobTimeoutMs + 180_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.afterEach(async ({ recordingApi }) => {
  await recordingApi.deleteAllTakes(track.id);
});

test.describe('loop recording', { tag: '@recording' }, () => {
  test('three passes and a partial pass become four takes of one session; keep the starred pass (US3)', async ({
    mixer, recording, recordingApi, shell,
  }) => {
    await mixer.goto(track.id);
    // Interactive first: a click on the prerendered level switch before the circuit is up does nothing.
    await mixer.waitForReady(fakeStems);
    await shell.setLevel('advanced');
    await mixer.setLoop(4, 8, true);
    await mixer.seek(4);
    await recording.openWithInput();
    await recording.choosePart('guitar');
    await recording.chooseCountIn(0);

    await recording.recordLoopUntilPass(4, 2);

    const passes = await sessionPasses(recordingApi, track.id, 4);
    expect(passes.map((t) => [t.passIndex, t.partial])).toEqual([[1, false], [2, false], [3, false], [4, true]]);
    for (const take of passes) {
      await recordingApi.waitForTakeStatus(take.id, 'ready', takeReadyTimeoutMs);
    }
    const [first, second, , last] = passes as [RecordingTakeDto, RecordingTakeDto, RecordingTakeDto, RecordingTakeDto];
    await expect(recording.byId(`takes-session-${first.sessionId}`)).toHaveAttribute('data-passes', '4');
    await expect(recording.byId(`rec-take-pass-${last.id}`)).toContainText('partial');

    const keep = second.id;
    await recording.star(keep);
    await recording.keepStarred(first.sessionId);

    await expect.poll(async () => (await recordingApi.listTakes(track.id)).items.map((t) => t.id)).toEqual([keep]);
    await expect(recording.rows).toHaveCount(1);
  });
});

/** Waits for `count` takes of one session and returns them in pass order. */
async function sessionPasses(recordingApi: RecordingApi, trackId: string, count: number): Promise<RecordingTakeDto[]> {
  await expect.poll(async () => (await recordingApi.listTakes(trackId)).items.length, { timeout: 30_000 }).toBe(count);
  const takes = (await recordingApi.listTakes(trackId)).items;
  expect(new Set(takes.map((t) => t.sessionId)).size, 'sessions').toBe(1);
  return [...takes].sort((a, b) => (a.passIndex ?? 0) - (b.passIndex ?? 0));
}

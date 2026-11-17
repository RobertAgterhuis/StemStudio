import type { TrackDto } from '../support/api';
import { fakeStems, silentDb } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { requireFirefox } from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 015 T085 Firefox smoke (research R-01: recording itself is Chromium/Edge): the panel opens with the privacy note
// and shows either "Allow microphone" or a readable unavailable state with a valid reason, and the mixer still plays.
let track: TrackDto;

test.beforeEach(async ({ api, browserName }) => {
  requireFirefox(browserName);
  test.setTimeout(fakeJobTimeoutMs + 60_000);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.describe('recording in Firefox', { tag: '@recording' }, () => {
  test('the panel explains its state and the mixer keeps playing (T085)', async ({ mixer, recording }) => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await recording.open();

    await expect(recording.byId('rec-privacy-note')).toBeVisible();
    await expect(recording.byId('rec-allow-mic').or(recording.byId('rec-unavailable'))).toBeVisible();
    await mixer.play();
    await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  });
});

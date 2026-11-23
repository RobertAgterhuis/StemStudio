import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { referenceMs, referenceTrack, singThrough } from '../support/instrument-singing';
import { useInstrumentApi } from '../support/instrument-views';
import { takeReadyTimeoutMs } from '../support/recording';
import { expect, test } from '../support/recording-test';

// Feature 016 US4 (T107; FR-035..FR-047, SC-004, SC-006, quickstart V9/V10): singing feedback for the vocals family in
// the fake-mic projects of playwright.config.ts (one fixture per project, selected by its tag): `sing-on-pitch.wav`
// follows the reference melody one octave lower (octave independence), `sing-semitone-off.wav` is a semitone off,
// `silence.wav` is silent (TEST-STRATEGY 22.2). The track is the 015 click track whose grid the fixtures follow; the
// spec waits on the lyrics, the vocal-pitch analysis it started and the summary at the track end. Levels: the suite
// starts at Advanced, so the tolerance is 50 cents (data-model 3.4).
const advancedToleranceCents = '50';

const suite = useInstrumentApi(fakeJobTimeoutMs + 3 * referenceMs);

test.describe('singing on pitch (016 US4)', { tag: ['@fake-mic', '@fake-mic-on-pitch'] }, () => {
  test('every line scores at least 90 an octave below the reference, and nothing is uploaded', async ({ api, lyricsApi, mixer, page }) => {
    const track = await referenceTrack(api, lyricsApi, suite.views());

    const pass = await singThrough(page, mixer, track);

    expect(pass.unitScores.length).toBeGreaterThan(0);
    expect(Math.min(...pass.unitScores)).toBeGreaterThanOrEqual(90);
    expect(pass.average).toBeGreaterThanOrEqual(90);
    expect(pass.uploads.map((r) => `${r.method()} ${r.url()}`)).toEqual([]);
  });

  test('a recorded 015 take can be scored', async ({ api, lyricsApi, mixer, recording, page }) => {
    const track = await referenceTrack(api, lyricsApi, suite.views());
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems.filter((s) => track.stems.some((t) => t.name === s)));
    await recording.openWithInput();
    await recording.choosePart('vocals');

    await recording.recordFor(4);
    // The strip shows the local id until the upload is saved; the row then carries the server id (as the 015 specs read it).
    const takeId = await recording.newestSavedTakeId();
    await recording.waitForRow(takeId, 'ready', takeReadyTimeoutMs);
    await page.getByTestId(`take-score-${takeId}`).click();

    const result = page.getByTestId(`take-score-result-${takeId}`);
    await expect(result).toHaveAttribute('data-average', /^\d{1,3}$/u, { timeout: referenceMs });
    expect(Number(await result.getAttribute('data-average'))).toBeLessThanOrEqual(100);
  });
});

test.describe('singing a semitone off (016 US4)', { tag: ['@fake-mic', '@fake-mic-semitone'] }, () => {
  test('a semitone off scores at most 20 at 50 cents tolerance', async ({ api, lyricsApi, mixer, page }) => {
    const track = await referenceTrack(api, lyricsApi, suite.views());

    const pass = await singThrough(page, mixer, track);

    await expect(page.getByTestId('singing-tolerance')).toHaveAttribute('aria-valuenow', advancedToleranceCents);
    expect(pass.unitScores.length).toBeGreaterThan(0);
    expect(Math.max(...pass.unitScores)).toBeLessThanOrEqual(20);
    expect(pass.average).toBeLessThanOrEqual(20);
  });
});

test.describe('singing silence (016 US4)', { tag: ['@fake-mic', '@fake-mic-silence'] }, () => {
  test('silence scores 0', async ({ api, lyricsApi, mixer, page }) => {
    const track = await referenceTrack(api, lyricsApi, suite.views());

    const pass = await singThrough(page, mixer, track);

    expect(pass.unitScores.every((score) => score === 0)).toBe(true);
    expect(pass.average).toBe(0);
  });
});

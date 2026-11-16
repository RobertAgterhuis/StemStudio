import type { Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import type { TrackDto } from '../support/api';
import { accents, heardClicks, installClickProbe, musicStartTimes, readClicks, spacings, type ClickRecording } from '../support/click-probe';
import { fillAndCommit, setToggle } from '../support/controls';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { fixtureFacts } from '../support/fixtures';
import { beatIntervalAt, distinctValues, firstBeatAtOrAfter, heardLatencySeconds, readWatched, watchAttributes } from '../support/practice';
import { expertState } from '../support/preferences';
import { expect, test } from '../support/test';

// Feature 012 US3 (T061; FR-012, FR-013, FR-018; SC-005; quickstart P9). Fake stack, the 12 s
// click fixture at 120 BPM in 4/4 (reliable grid). Click times come from the click probe (the
// planned `start(when)` of every click on the audio clock), the music's start from the stem
// sources: per audio-engine.md "Count-in" the clicks lie at `T0 + (b - A)/r - k d/r` (k = N..1),
// where `T0` is the heard start of the music at position `A` and `b` the first grid beat >= A, so
// the count-in lasts N d / r up to that beat.
const clickBeats = fixtureFacts('click_120bpm_4-4_12s.wav').beatTimes;
const browserToleranceS = 0.05;
const clickToleranceS = 0.001;
let track: TrackDto;

test.beforeEach(async ({ api, mixer, page }) => {
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  track = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
  await installClickProbe(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  await setToggle(mixer.byId('metronome-toggle'), false);
});

/** The detected grid's beats (the fixture's when the API does not list them). */
function gridBeats(): readonly number[] {
  return track.beatGrid?.beats ?? clickBeats;
}

/** One count-in as heard: its clicks and the time the music reaches the beat `b`. */
interface HeardCountIn {
  readonly clicks: ClickRecording['clicks'];
  readonly beatHeardAt: number;
  readonly interval: number;
}

/** The `bars x 4` clicks before the music start `start` (+ latency) at position `a` and rate `r`. */
function countInBefore(recording: ClickRecording, start: number, at: { readonly a: number; readonly r: number; readonly latency: number; readonly beats: number }): HeardCountIn {
  const b = firstBeatAtOrAfter(gridBeats(), at.a);
  const beatHeardAt = start + at.latency + (b - at.a) / at.r;
  const clicks = heardClicks(recording, 0, beatHeardAt - clickToleranceS).slice(-at.beats);
  return { clicks, beatHeardAt, interval: beatIntervalAt(gridBeats(), b) / at.r };
}

function expectCountIn(heard: HeardCountIn, beats: number): void {
  expect(heard.clicks).toHaveLength(beats);
  const gaps = spacings(heard.clicks);
  expect(gaps.every((gap) => Math.abs(gap - heard.interval) <= clickToleranceS), JSON.stringify(gaps)).toBe(true);
  expect(accents(heard.clicks)).toEqual(Array.from({ length: beats }, (_, k) => k % 4 === 0));
  const first = heard.clicks[0]?.when ?? Number.NaN;
  expect(Math.abs(heard.beatHeardAt - first - beats * heard.interval)).toBeLessThanOrEqual(browserToleranceS);
}

async function waitUntilPlayed(page: Page, contextTime: number): Promise<ClickRecording> {
  await expect.poll(async () => (await readClicks(page)).now, { timeout: 60_000 }).toBeGreaterThan(contextTime);
  return readClicks(page);
}

test.describe('count-in', { tag: '@practice' }, () => {
  test('1 bar in 4/4: four clicks, accent first, 2.0 s up to the next beat; the indicator counts 4..1 (P9, US3-1, US3-6, SC-005)', async ({ mixer, page, practice }) => {
    await practice.setCountIn(1);
    await watchAttributes(page, 'countin-indicator', ['data-beats-remaining', 'data-beats-total']);
    const before = (await readClicks(page)).now;
    await mixer.play();
    await expect(practice.countInIndicator).toBeVisible();
    await expect(practice.countInIndicator).toHaveCount(0, { timeout: 10_000 });
    const started = musicStartTimes(await readClicks(page), before)[0] ?? Number.NaN;
    const recording = await waitUntilPlayed(page, started + 0.6);
    const heard = countInBefore(recording, started, { a: 0, r: 1, latency: 0, beats: 4 });
    expectCountIn(heard, 4);
    expect(Math.abs(4 * heard.interval - 2)).toBeLessThanOrEqual(browserToleranceS);
    const samples = await readWatched(page, 'countin-indicator');
    expect(distinctValues(samples, 'data-beats-remaining')).toEqual(['4', '3', '2', '1']);
    expect(distinctValues(samples, 'data-beats-total')).toEqual(['4']);
  });

  test('pause and seek cancel a running count-in; the next play counts in again (US3-5)', async ({ mixer, page, practice }) => {
    await practice.setCountIn(2);
    await mixer.play();
    await expect(practice.countInIndicator).toHaveAttribute('data-beats-remaining', /^[1-6]$/u, { timeout: 10_000 });
    await mixer.byId('transport-pause').click();
    await expect(practice.countInIndicator).toHaveCount(0);
    const paused = await readClicks(page);
    expect(heardClicks(paused, paused.now + 0.05)).toEqual([]);

    await mixer.play();
    await expect(practice.countInIndicator).toHaveAttribute('data-beats-total', '8');
    await mixer.seek(5);
    await expect(practice.countInIndicator).toHaveCount(0);
    await expect(mixer.byId('transport-position')).toHaveText(/^0:0[5-9]/u, { timeout: 5_000 });
  });
});

test.describe('count-in before every pass (Expert)', { tag: '@practice' }, () => {
  test.use({ storageState: expertState });

  test('2 bars before every pass at 50 %: 8 clicks 1.0 s apart in silence, 8.0 s up to the beat (P9, US3-2, US3-3, SC-005)', async ({ mixer, page, practice }) => {
    test.setTimeout(fakeJobTimeoutMs + 180_000);
    const a = 1.75;
    await mixer.setLoop(a, a + 4, true);
    await practice.setCountIn(2, true);
    await practice.setSpeed(50);
    await practice.waitForStretch('active');
    const latency = heardLatencySeconds(50, 0);
    const shownLatency = async (): Promise<number> => Number(await practice.byId('practice-stretch-status').getAttribute('data-latency-ms'));
    await expect.poll(async () => Math.abs((await shownLatency()) - latency * 1000)).toBeLessThanOrEqual(1);
    const before = (await readClicks(page)).now;
    await mixer.play();

    const starts = await passStarts(page, before, 2);
    const recording = await waitUntilPlayed(page, (starts[1] ?? 0) + latency + 1);
    for (const start of starts) {
      expectCountIn(countInBefore(recording, start, { a, r: 0.5, latency, beats: 8 }), 8);
    }
    const gap = 8 * (beatIntervalAt(gridBeats(), firstBeatAtOrAfter(gridBeats(), a)) / 0.5);
    expect(Math.abs((starts[1] ?? 0) - (starts[0] ?? 0) - (4 / 0.5 + gap))).toBeLessThanOrEqual(0.005);
  });
});

async function passStarts(page: Page, from: number, count: number): Promise<number[]> {
  const starts = async (): Promise<number> => musicStartTimes(await readClicks(page), from).length;
  await expect.poll(starts, { timeout: 90_000 }).toBeGreaterThanOrEqual(count);
  return musicStartTimes(await readClicks(page), from).slice(0, count);
}

test.describe('count-in availability', { tag: '@practice' }, () => {
  test('without a reliable grid the count-in is unavailable until a manual BPM is set (US3-4, FR-013)', async ({ api, mixer, practice }) => {
    const tone = await api.ensureCompleted('tone_440hz_20s.flac', fakeJobTimeoutMs);
    await openWithoutGrid(mixer, tone);
    await expect(practice.byId('countin-unavailable')).toHaveText('Count-in needs a beat grid or a manual BPM.');
    await expect(practice.countInToggle).toBeDisabled();
    await fillAndCommit(mixer.byId('metronome-bpm'), '100');
    await expect(practice.byId('countin-unavailable')).toHaveCount(0);
    await expect(practice.countInToggle).toBeEnabled();
  });
});

async function openWithoutGrid(mixer: MixerPage, tone: TrackDto): Promise<void> {
  expect(tone.beatGrid?.reliable ?? false, 'the tone fixture has no reliable beat grid').toBe(false);
  await mixer.goto(tone.id);
  await mixer.waitForReady(fakeStems);
}

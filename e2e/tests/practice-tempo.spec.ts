import type { Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import type { PracticePanel } from '../pages/practice-panel';
import { installAlignmentProbe } from '../support/alignment-probe';
import type { TrackDto } from '../support/api';
import { heardClicks, installClickProbe, readClicks, spacings } from '../support/click-probe';
import { fillAndCommit, isOn, setToggle } from '../support/controls';
import { ensureDetailCompleted, fakeStems, keptStems, silentDb } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs, runId } from '../support/env';
import { fixtureFacts } from '../support/fixtures';
import { installKaraokeProbe, landingErrors, maxLandingErrorMs, recordKaraoke } from '../support/karaoke-probe';
import { readFakeLyrics, requireLyricsMode } from '../support/lyrics';
import type { LyricsApi } from '../support/lyrics-api';
import { installOutputProbe, outputDominant, timeFrequencyStep, transposedHz, withinRatio } from '../support/output-probe';
import {
  defaultPractice,
  heardLatencySeconds,
  installRateAwareReference,
  maxAnchorSpreadMs,
  mixSettings,
  positionRate,
  practiceWith,
  recordPositions,
  stretchModuleFiles,
} from '../support/practice';
import { expertState } from '../support/preferences';
import { expect, test } from '../support/test';

// Feature 012 US1 (T047; FR-001..FR-005, FR-007, FR-009, FR-014; SC-001, SC-002, SC-003, SC-007;
// quickstart P3-P7, P10, P11). Fake stack: every stem is the uploaded mix, so the 440 Hz tone
// fixture gives a pure tone on all six stems. Pitch is read by the output probe (FFT of what
// reaches the destination), speed by the slope of `transport-position`, clicks by the click probe.
// Settings are made while stopped where a test does not measure live changes, so the one-time
// engagement restart (FR-003) is not part of the measurement.
const toneHz = fixtureFacts('tone_440hz_20s.flac').frequencyHz ?? 440;
const pitchTolerance = 0.01;
const fake = readFakeLyrics();
let tone: TrackDto;

test.beforeEach(async ({ api }) => {
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  tone = await api.ensureCompleted('tone_440hz_20s.flac', fakeJobTimeoutMs);
});

async function openMixer(page: Page, mixer: MixerPage, track: TrackDto, mixId?: string): Promise<void> {
  await installOutputProbe(page);
  await mixer.goto(track.id, mixId);
  await mixer.waitForReady(track.stems.map((s) => s.name));
}

/** Sets speed and transpose while stopped, waits for the stage (when needed) and plays. */
async function playAt(mixer: MixerPage, practice: PracticePanel, speed: number, semitones: number): Promise<void> {
  await practice.set(speed, semitones);
  await practice.waitForStretch(speed === 100 && semitones === 0 ? 'bypass' : 'active');
  await mixer.play();
  await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
}

async function expectDominantNear(page: Page, hz: number): Promise<void> {
  const near = async (): Promise<boolean> => withinRatio((await outputDominant(page)).hz, hz, pitchTolerance);
  await expect.poll(near, { message: `dominant output frequency within 1 % of ${hz.toFixed(1)} Hz`, timeout: 10_000 }).toBe(true);
}

test.describe('speed', { tag: '@practice' }, () => {
  for (const speed of [50, 120]) {
    test(`at ${String(speed)} % the position advances ${String(speed / 10)} s in 10 s of wall time (P3, SC-003)`, async ({ page, mixer, practice }) => {
      await openMixer(page, mixer, tone);
      await playAt(mixer, practice, speed, 0);
      await expect(practice.panel).toHaveAttribute('data-speed', String(speed));
      const advance = positionRate(await recordPositions(mixer.byId('transport-position'), 10_000)) * 10;
      test.info().annotations.push({ type: 'advance s', description: advance.toFixed(3) });
      expect(Math.abs(advance - speed / 10)).toBeLessThanOrEqual(Math.max(0.2, 0.02 * (speed / 10)));
    });
  }

  test('combined 80 % and -3 st keep the stems and the expanded detail stems together (US1-3, FR-004)', async ({ api, page, mixer, practice }) => {
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 120_000);
    const detail = await ensureDetailCompleted(api);
    await installAlignmentProbe(page);
    await openMixer(page, mixer, detail);
    await playAt(mixer, practice, 80, -3);
    await mixer.setExpanded('drums', true, keptStems('drums'));
    await expect.poll(() => mixer.peak('drums-kick'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
    expect(await maxAnchorSpreadMs(page)).toBeLessThanOrEqual(1);
    await expect(practice.panel).toHaveAttribute('data-speed', '80');
    await expect(practice.panel).toHaveAttribute('data-transpose', '-3');
  });
});

const pitchCases: readonly (readonly [number, number])[] = [
  [50, 0], [70, 0], [100, 0], [120, 0],
  [100, -12], [100, -5], [100, 2], [100, 12],
  [50, -12], [50, 2], [120, -5], [120, 12],
];

test.describe('pitch', { tag: '@practice' }, () => {
  for (const [speed, semitones] of pitchCases) {
    test(`at ${String(speed)} % and ${String(semitones)} st the tone is 2^(n/12) x 440 Hz within 1 % (P4, SC-002)`, async ({ page, mixer, practice }) => {
      await openMixer(page, mixer, tone);
      await playAt(mixer, practice, speed, semitones);
      await expectDominantNear(page, transposedHz(toneHz, semitones));
      await expect(practice.panel).toHaveAttribute('data-speed', String(speed));
    });
  }
});

test.describe('live changes (Expert)', { tag: '@practice' }, () => {
  test.use({ storageState: expertState });

  test('transpose steps are heard within 250 ms; 100 speed steps cause no dropout (P5, FR-003, SC-001)', async ({ page, mixer, practice }) => {
    test.setTimeout(fakeJobTimeoutMs + 180_000);
    await openMixer(page, mixer, tone);
    await playAt(mixer, practice, 100, 1);
    const input = practice.byId('practice-transpose-input');
    const timings: number[] = [];
    for (const [from, to] of [[1, 8], [8, 1], [1, 8], [8, 1], [1, 8], [8, 1]] as const) {
      await expectDominantNear(page, transposedHz(toneHz, from));
      timings.push(await timeFrequencyStep(input, { action: { kind: 'value', value: to }, fromHz: transposedHz(toneHz, from), toHz: transposedHz(toneHz, to) }));
    }
    test.info().annotations.push({ type: 'change ms', description: JSON.stringify(timings.map(Math.round)) });
    expect(Math.max(...timings)).toBeLessThanOrEqual(250);

    await practice.speed.focus();
    for (let step = 0; step < 100; step++) await page.keyboard.press(step < 50 ? 'ArrowLeft' : 'ArrowRight', { delay: 100 });
    await expect(practice.panel).toHaveAttribute('data-speed', '100');
    await expectDominantNear(page, transposedHz(toneHz, 1));
    await expect(practice.panel).toHaveAttribute('data-stretch', 'active');
    await expect(practice.byId('practice-stretch-status')).toHaveAttribute('data-underruns', '0');
  });
});

test.describe('neutral settings', { tag: '@practice' }, () => {
  test('a mix saved without practice opens at 100 % / 0 and never requests the module (P6, FR-007, SC-007)', async ({ api, page, mixer, practice }, testInfo) => {
    const mix = await api.createMix(tone.id, mixSettings({ name: `Pre-012 ${runId()} ${testInfo.project.name}` }));
    expect((await api.getMix(mix.id)).settings.practice).toEqual(defaultPractice);
    const requests: string[] = [];
    page.on('request', (request) => {
      if (stretchModuleFiles.test(request.url())) requests.push(request.url());
    });
    await openMixer(page, mixer, tone, mix.id);
    await expect(practice.panel).toHaveAttribute('data-stretch', 'bypass');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
    await expect(practice.panel).toHaveAttribute('data-transpose', '0');
    await expect(practice.reset).toBeDisabled();
    await mixer.play();
    await expectDominantNear(page, toneHz);
    await expect(practice.panel).toHaveAttribute('data-stretch', 'bypass');
    expect(requests).toEqual([]);
  });
});

/** Word-mode lyrics of `trackId` found again with offset 0 (as in karaoke.spec.ts). */
async function wordLyricsReady(lyricsApi: LyricsApi, trackId: string): Promise<void> {
  requireLyricsMode('word');
  await lyricsApi.requireFeature(trackId);
  const current = await lyricsApi.waitForStatus(trackId, ['found', 'removed', 'notFound', 'failed', 'instrumental', 'alignPending'], 60_000);
  if (current.status === 'removed') expect(await lyricsApi.search(trackId)).toBe(202);
  await lyricsApi.waitForStatus(trackId, ['found'], 60_000);
  expect(await lyricsApi.setOffset(trackId, 0)).toBe(200);
}

test.describe('karaoke and metronome follow the speed', { tag: ['@practice', '@karaoke'] }, () => {
  for (const speed of [50, 75, 120]) {
    test(`the ball lands within 50 ms of every word at ${String(speed)} % (P7, FR-005, SC-003)`, async ({ api, lyricsApi, karaoke, mixer, page, practice }) => {
      const playMs = Math.round((19.9 / (speed / 100)) * 1000) + 1_000;
      test.setTimeout(fakeJobTimeoutMs + playMs + 120_000);
      const music = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
      await wordLyricsReady(lyricsApi, music.id);
      await installKaraokeProbe(page);
      await installRateAwareReference(page, heardLatencySeconds(speed, 0));
      await openMixer(page, mixer, music);
      await mixer.setLoop(0, 19.9, true);
      await playAt(mixer, practice, speed, 0);
      const landings = landingErrors(await recordKaraoke(karaoke.host, playMs), fake.lines, 0);
      expect(landings.length).toBeGreaterThanOrEqual(fake.lines.flatMap((l) => l.words).length - 1);
      expect(maxLandingErrorMs(landings), JSON.stringify(landings)).toBeLessThanOrEqual(50);
    });
  }

  test('metronome clicks at 50 % are 60 / 120 / 0.5 = 1.000 s apart (+/- 1 ms) (P7)', async ({ api, mixer, page, practice }) => {
    const music = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
    await installClickProbe(page);
    await openMixer(page, mixer, music);
    await fillAndCommit(mixer.byId('metronome-bpm'), '120');
    await setToggle(mixer.byId('metronome-toggle'), true);
    await playAt(mixer, practice, 50, 0);
    const from = (await readClicks(page)).now + 0.5;
    const played = async (): Promise<number> => {
      const recording = await readClicks(page);
      return heardClicks(recording, from, recording.now).length;
    };
    await expect.poll(played, { timeout: 15_000 }).toBeGreaterThanOrEqual(6);
    const recording = await readClicks(page);
    const gaps = spacings(heardClicks(recording, from, recording.now));
    test.info().annotations.push({ type: 'click spacing s', description: JSON.stringify(gaps) });
    expect(gaps.every((gap) => Math.abs(gap - 1) <= 0.001), JSON.stringify(gaps)).toBe(true);
  });
});

test.describe('persistence', { tag: '@practice' }, () => {
  test('speed, transpose and count-in are saved, restored after a reload and kept by a duplicate (P10, US1-5, FR-014)', async ({ api, mixer, mixLibrary, page, practice }, testInfo) => {
    const click = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
    await openMixer(page, mixer, click);
    await practice.set(75, -3);
    await practice.setCountIn(2);
    const name = `Practice ${runId()} ${testInfo.project.name}`;
    await mixer.saveNew(name);
    await expect.poll(async () => (await api.findMix(click.id, name))?.id).toBeTruthy();
    const id = (await api.findMix(click.id, name))?.id ?? '';
    const expected = practiceWith({ speedPercent: 75, transposeSemitones: -3, countIn: { bars: 2 } });
    expect((await api.getMix(id)).settings.practice).toEqual(expected);
    expect((await api.duplicateMix(id)).settings.practice).toEqual(expected);

    await page.reload();
    await mixLibrary.goto();
    await mixLibrary.load(id);
    await mixer.waitForReady(fakeStems);
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '75');
    await expect(practice.panel).toHaveAttribute('data-transpose', '-3');
    await practice.waitForStretch('active');
    expect(await isOn(practice.countInToggle)).toBe(true);
    await expect(practice.byId('countin-bars')).toHaveValue('2');
  });
});

/** The panel may know at load time (no AudioWorkletNode); otherwise the first change finds out. */
async function tryToSlowDown(practice: PracticePanel): Promise<void> {
  const slower = practice.byId('practice-speed-down');
  if (await slower.isEnabled()) await slower.click();
}

test.describe('time stretch unavailable', { tag: '@practice' }, () => {
  const variants = [
    { name: 'soundtouch.wasm answers 404', block: (page: Page) => page.route('**/soundtouch.wasm', (route) => route.fulfill({ status: 404, body: '' })) },
    {
      name: 'the browser has no AudioWorkletNode',
      block: (page: Page) =>
        page.addInitScript(() => {
          delete (window as { AudioWorkletNode?: unknown }).AudioWorkletNode;
        }),
    },
  ] as const;

  for (const variant of variants) {
    test(`${variant.name}: speed and transpose are disabled with the reason; playback and count-in work (P11, US1-6, FR-009)`, async ({ api, mixer, page, practice }) => {
      const click = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
      await variant.block(page);
      await installClickProbe(page);
      await openMixer(page, mixer, click);
      await tryToSlowDown(practice);
      await practice.waitForStretch('unavailable');
      await expect(practice.byId('practice-unavailable')).toContainText('Playback works at the original speed and key.');
      await expect(practice.speed).toBeDisabled();
      await expect(practice.byId('practice-transpose-up')).toBeDisabled();
      await expect(practice.panel).toHaveAttribute('data-speed', '100');
      await setToggle(practice.countInToggle, true);
      await mixer.play();
      await expect(practice.countInIndicator).toBeVisible();
      await expect.poll(async () => heardClicks(await readClicks(page)).length, { timeout: 10_000 }).toBeGreaterThanOrEqual(4);
      await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
    });
  }
});

test.describe('quality hint and effective tempo', { tag: '@practice' }, () => {
  test('the hint shows below 70 % and beyond +-7 st; the effective tempo is track tempo x speed (US1-4, FR-008)', async ({ api, mixer, page, practice }) => {
    const click = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
    const bpm = click.beatGrid?.bpm ?? 120;
    await openMixer(page, mixer, click);
    const hint = practice.byId('practice-quality-hint');
    await practice.setSpeed(75);
    const shownBpm = async (): Promise<number> => Number(/(\d+)\s*BPM/u.exec((await practice.byId('practice-effective-bpm').textContent()) ?? '')?.[1]);
    await expect.poll(async () => Math.abs((await shownBpm()) - Math.round(bpm * 0.75))).toBeLessThanOrEqual(1);
    await expect(hint).toHaveCount(0);
    await practice.setSpeed(69);
    await expect(hint).toBeVisible();
    await practice.setSpeed(70);
    await expect(hint).toHaveCount(0);
    for (const [semitones, shown] of [[8, 1], [7, 0], [-8, 1]] as const) {
      await practice.setTranspose(semitones);
      await expect(hint).toHaveCount(shown);
    }
    await practice.reset.click();
    await expect(practice.panel).toHaveAttribute('data-transpose', '0');
    await expect(practice.speed).toHaveAttribute('aria-valuenow', '100');
    await expect(practice.reset).toBeDisabled();
  });
});

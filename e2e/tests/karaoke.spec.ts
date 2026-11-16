import type { Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import type { TrackDto } from '../support/api';
import { setNumericValue } from '../support/controls';
import { fakeJobTimeoutMs } from '../support/env';
import { requireChromium } from '../support/feature-skips';
import { installKaraokeProbe, landingErrors, maxClockDriftMs, maxLandingErrorMs, recordKaraoke } from '../support/karaoke-probe';
import { readFakeLyrics, requireLyricsMode } from '../support/lyrics';
import type { LyricsApi } from '../support/lyrics-api';
import { expect, test } from '../support/test';
import { levelState } from '../support/preferences';

// Feature 006 US1 + US3 (006:T039, T055; FR-005, FR-006, FR-008, FR-009, FR-011; SC-002,
// SC-004; quickstart L2, L4, L5). Fake stack, STEMSTUDIO_FAKE_LYRICS=word unless a describe says
// otherwise; the expected words come from tests/fixtures/lyrics/fake-lyrics.json (the worker's
// fake document on the 20 s fixture). Landing errors are measured against an independent
// audio-clock reference (support/karaoke-probe.ts). The fake document has no gap longer than
// 5 s, so the gap countdown (US1-6) is covered by the engine's dom/timeline unit tests only.
const fake = readFakeLyrics();
const settleMs = 60_000;
const maxErrorMs = 50;
const loops = Number(process.env.E2E_KARAOKE_LOOPS ?? '50');
const seeks = Number(process.env.E2E_KARAOKE_SEEKS ?? '20');
const loopRegion = { start: 1.5, end: 9.5 };
/** Word starts inside the A-B loop region (each loop lands on every one of them). */
const wordsPerLoop = fake.lines
  .flatMap((l) => l.words)
  .filter((w) => w.startMs >= loopRegion.start * 1000 && w.startMs < loopRegion.end * 1000).length;
let track: TrackDto;

async function restoreLyrics(lyricsApi: LyricsApi): Promise<void> {
  await lyricsApi.requireFeature(track.id);
  const current = await lyricsApi.waitForStatus(track.id, ['found', 'removed', 'notFound', 'failed', 'instrumental', 'alignPending'], settleMs);
  if (current.status === 'removed') {
    expect(await lyricsApi.search(track.id)).toBe(202);
    await lyricsApi.waitForStatus(track.id, ['found'], settleMs);
  }
  expect(await lyricsApi.setOffset(track.id, 0)).toBe(200);
}

/** Word mode only: the track's lyrics are found again with offset 0. */
async function wordLyricsReady(lyricsApi: LyricsApi): Promise<void> {
  requireLyricsMode('word');
  await restoreLyrics(lyricsApi);
  await lyricsApi.waitForStatus(track.id, ['found'], settleMs);
}

async function openPlaying(page: Page, mixer: MixerPage): Promise<void> {
  await installKaraokeProbe(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(['vocals']);
  await mixer.play();
}

function seededPositions(count: number, maxSeconds: number): number[] {
  let seed = 20_261_115;
  return Array.from({ length: count }, () => {
    seed = (seed * 1_103_515_245 + 12_345) % 2 ** 31;
    return Math.round((seed / 2 ** 31) * maxSeconds * 10) / 10;
  });
}

test.beforeEach(async ({ api }) => {
  test.setTimeout(fakeJobTimeoutMs + 2 * settleMs);
  track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
});

test.describe('karaoke word sync', { tag: '@karaoke' }, () => {
  test.beforeEach(async ({ lyricsApi }) => {
    await wordLyricsReady(lyricsApi);
  });

  test('the banner goes countdown -> singing -> ended (L2)', async ({ karaoke, mixer, page }) => {
    await openPlaying(page, mixer);
    await expect(karaoke.host).toHaveAttribute('data-sync', 'word');
    await expect(karaoke.credit).toHaveText(fake.modes.word.credit ?? '');
    await karaoke.waitForState('countdown', 5_000);
    await karaoke.waitForState('singing', 10_000);
    await expect(karaoke.currentWord).toHaveText(/\S/u);
    await karaoke.waitForState('ended', 20_000);
  });

  test('the ball lands within 50 ms of every word for 30 s of playback (L4, SC-002)', async ({ karaoke, mixer, page }) => {
    test.setTimeout(3 * settleMs);
    await openPlaying(page, mixer);
    await mixer.setLoop(0, 19.9, true);
    const samples = await recordKaraoke(karaoke.host, 30_000);
    const landings = landingErrors(samples, fake.lines, 0);
    expect(landings.length).toBeGreaterThanOrEqual(fake.lines.flatMap((l) => l.words).length);
    expect(maxLandingErrorMs(landings), JSON.stringify(landings)).toBeLessThanOrEqual(maxErrorMs);
    expect(maxClockDriftMs(samples, 0)).toBeLessThanOrEqual(maxErrorMs);
  });

  test(`the ball stays within 50 ms over ${String(loops)} A-B loops of 8 s (L4)`, async ({ karaoke, mixer, page }) => {
    test.setTimeout(loops * 8_000 + 3 * settleMs);
    await openPlaying(page, mixer);
    await mixer.setLoop(loopRegion.start, loopRegion.end, true);
    const samples = await recordKaraoke(karaoke.host, loops * 8_000);
    const landings = landingErrors(samples, fake.lines, 0);
    expect(landings.length).toBeGreaterThanOrEqual((loops - 1) * wordsPerLoop);
    expect(maxLandingErrorMs(landings)).toBeLessThanOrEqual(maxErrorMs);
  });

  test(`the ball stays within 50 ms after ${String(seeks)} random seeks (L4)`, async ({ karaoke, mixer, page }) => {
    test.setTimeout(seeks * 5_000 + 3 * settleMs);
    await openPlaying(page, mixer);
    for (const seconds of seededPositions(seeks, 17)) {
      await mixer.seek(seconds);
      // The seek reaches the engine after a server round trip; the jump itself is not a landing.
      await expect
        .poll(async () => {
          const ahead = Number(await karaoke.host.getAttribute('data-position-ms')) - seconds * 1000;
          return ahead >= -50 && ahead <= 400;
        }, { timeout: 5_000, intervals: [50] })
        .toBe(true);
      const landings = landingErrors(await recordKaraoke(karaoke.host, 2_500), fake.lines, 0);
      expect(maxLandingErrorMs(landings), `after a seek to ${String(seconds)} s`).toBeLessThanOrEqual(maxErrorMs);
    }
  });

  test('an offset moves the landing time and is stored; reset returns to 0', async ({ karaoke, lyricsApi, mixer, page }) => {
    await openPlaying(page, mixer);
    await setNumericValue(karaoke.offset, 500);
    await expect.poll(async () => (await lyricsApi.get(track.id)).offsetMs, { timeout: 5_000 }).toBe(500);
    await mixer.setLoop(0, 19.9, true);
    const landings = landingErrors(await recordKaraoke(karaoke.host, 12_000), fake.lines, 500);
    expect(landings.length).toBeGreaterThan(5);
    expect(maxLandingErrorMs(landings)).toBeLessThanOrEqual(maxErrorMs);
    await karaoke.offsetReset.click();
    await expect(karaoke.offset).toHaveAttribute('aria-valuenow', '0');
  });
});

/** Frame statistics of one measurement window. */
interface FrameStats {
  readonly fps: number;
  /** Timed `requestAnimationFrame` callbacks (the engine's frame loop: meters and karaoke). */
  readonly callbacks: number;
  readonly p95WorkMs: number;
  readonly maxWorkMs: number;
}

/**
 * Init script: times every `requestAnimationFrame` callback into `window.__rafWork`. The engine
 * emits no `performance.measure` entries (audio-engine 006), so its frame work is timed from the
 * outside; this includes the meters, so it is stricter than the karaoke work alone.
 */
function timeFrameCallbacks(): void {
  const work: number[] = [];
  Object.assign(window, { __rafWork: work });
  const original = window.requestAnimationFrame.bind(window);
  window.requestAnimationFrame = (callback: FrameRequestCallback): number =>
    original((now) => {
      const begin = performance.now();
      callback(now);
      work.push(performance.now() - begin);
    });
}

/** In-page: frames per second over `ms` and the timed callback work of that window. */
function measureFrames(ms: number): Promise<FrameStats> {
  const work = (window as unknown as { __rafWork: number[] }).__rafWork;
  work.length = 0;
  return new Promise((resolve) => {
    const begin = performance.now();
    let frames = 0;
    const tick = (now: number): void => {
      frames += 1;
      if (now - begin < ms) {
        requestAnimationFrame(tick);
        return;
      }
      const sorted = [...work].sort((a, b) => a - b);
      const p95WorkMs = sorted[Math.floor(sorted.length * 0.95)] ?? 0;
      resolve({ fps: (1000 * frames) / (now - begin), callbacks: sorted.length, p95WorkMs, maxWorkMs: sorted.at(-1) ?? 0 });
    };
    requestAnimationFrame(tick);
  });
}

test.describe('karaoke frame rate', { tag: '@karaoke' }, () => {
  test.beforeEach(async ({ lyricsApi }) => {
    await wordLyricsReady(lyricsApi);
  });

  test('frame rate stays >= 55 fps and the frame work stays < 2 ms (L5, SC-004)', async ({ browserName, karaoke, mixer, page }, info) => {
    requireChromium(browserName);
    await page.addInitScript(timeFrameCallbacks);
    await openPlaying(page, mixer);
    await karaoke.waitForState('singing', 10_000);
    const result = await page.evaluate(measureFrames, 20_000);
    info.annotations.push({ type: 'frames', description: JSON.stringify(result) });
    expect(result.fps).toBeGreaterThanOrEqual(55);
    expect(result.callbacks).toBeGreaterThan(result.fps * 10);
    expect(result.p95WorkMs, JSON.stringify(result)).toBeLessThan(2);
  });
});

test.describe('karaoke line sync and instrumental', { tag: '@karaoke' }, () => {
  test('line-synced lyrics sing with the "word timing estimated" credit (L3)', async ({ karaoke, lyricsApi, mixer, page }) => {
    requireLyricsMode('line');
    await restoreLyrics(lyricsApi);
    await lyricsApi.waitForStatus(track.id, ['found'], settleMs);
    await openPlaying(page, mixer);
    await expect(karaoke.host).toHaveAttribute('data-sync', 'line');
    await expect(karaoke.credit).toHaveText(fake.modes.line.credit ?? '');
    await karaoke.waitForState('singing', 10_000);
  });

  test('an instrumental track shows no banner (US1-5)', async ({ karaoke, lyricsApi, mixer }) => {
    requireLyricsMode('instrumental');
    await lyricsApi.requireFeature(track.id);
    await lyricsApi.waitForStatus(track.id, ['instrumental'], settleMs);
    await mixer.goto(track.id);
    await karaoke.expectUnavailable('instrumental');
    await expect(karaoke.ball).toHaveCount(0);
  });
});

test.describe('karaoke controls', { tag: '@karaoke' }, () => {
  test.beforeEach(async ({ lyricsApi }) => {
    await wordLyricsReady(lyricsApi);
  });

  test('hiding the banner is remembered across a reload (FR-009)', async ({ karaoke, mixer, page }) => {
    await mixer.goto(track.id);
    await expect(karaoke.toggle).toHaveAttribute('aria-pressed', 'true');
    await karaoke.toggle.click();
    await expect(karaoke.host).toHaveCount(0);
    await page.reload();
    await expect(karaoke.toggle).toHaveAttribute('aria-pressed', 'false');
    await expect(karaoke.host).toHaveCount(0);
    await karaoke.toggle.click();
    await expect(karaoke.host).toBeVisible();
  });

  test('a size change keeps the landing accuracy (FR-011)', async ({ karaoke, mixer, page }) => {
    await openPlaying(page, mixer);
    await karaoke.setSize('l');
    await mixer.setLoop(0, 19.9, true);
    const landings = landingErrors(await recordKaraoke(karaoke.host, 12_000), fake.lines, 0);
    expect(landings.length).toBeGreaterThan(5);
    expect(maxLandingErrorMs(landings)).toBeLessThanOrEqual(maxErrorMs);
  });

  test('remove, then search again brings the lyrics back (FR-009)', async ({ karaoke, lyrics, lyricsApi, mixer }) => {
    await lyrics.goto(track.id);
    await lyrics.remove();
    await mixer.goto(track.id);
    await karaoke.expectUnavailable('removed');
    await lyrics.goto(track.id);
    await lyrics.search.click();
    await lyrics.waitForStatus('found', settleMs);
    expect((await lyricsApi.get(track.id)).lines).toHaveLength(fake.lines.length);
  });
});

test.describe('karaoke with reduced motion', { tag: '@karaoke' }, () => {
  test.use({ reducedMotion: 'reduce' });

  test('there is no ball; the current word is highlighted (FR-011)', async ({ karaoke, lyricsApi, mixer, page }) => {
    await wordLyricsReady(lyricsApi);
    await openPlaying(page, mixer);
    await karaoke.waitForState('singing', 10_000);
    await expect(karaoke.currentWord).toHaveCount(1);
    await expect(karaoke.ball).toBeHidden();
  });
});

test.describe('karaoke at Beginner', { tag: '@karaoke' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('banner and toggle are shown; size and offset are not rendered (ui-test-ids 1.6.0)', async ({ karaoke, lyricsApi, mixer }) => {
    await wordLyricsReady(lyricsApi);
    await mixer.goto(track.id);
    await expect(karaoke.host).toBeVisible();
    await expect(karaoke.toggle).toBeVisible();
    await expect(karaoke.size).toHaveCount(0);
    await expect(karaoke.offset).toHaveCount(0);
  });
});

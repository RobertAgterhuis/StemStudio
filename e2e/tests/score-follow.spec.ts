import type { APIRequestContext, Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import { ScorePanel } from '../pages/score-panel';
import type { TrackDto } from '../support/api';
import type { AssetsApi } from '../support/assets-api';
import { fillAndCommit } from '../support/controls';
import { ensureDetailCompleted, fakeStems } from '../support/detail';
import { apiContextOptions, detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { positionSeconds } from '../support/levels';
import { barAt, PracticeFlowApi, type ScoreViewDto } from '../support/practice-flow';
import type { CreateScoreDto, ScoreApi, ScoreProfile } from '../support/score-api';
import { expect, test } from '../support/test';

// Feature 014 US4 (T084; FR-009..FR-012, FR-014, FR-033, SC-004): the Score panel follows playback. Fake stack: the fake
// transcriber writes one note per beat of the track's grid and the score view (Verovio pages, bar map) is real, so the
// bar the follower shows is checked against the API's bar map at the heard position. One case per instrument family
// (vocals, bass, guitar, piano as keys, drums after the split).
const scoreMs = 90_000;
const viewMs = 30_000;
const fixture = 'music_100bpm_20s.flac';

let context: APIRequestContext;
let flow: PracticeFlowApi;

test.beforeEach(async ({ playwright }) => {
  test.setTimeout(fakeJobTimeoutMs + 3 * scoreMs);
  context = await playwright.request.newContext(apiContextOptions);
  flow = new PracticeFlowApi(context);
});

test.afterEach(async () => {
  await context.dispose();
});

/** What the score tests share: the API clients and the mixer. */
interface ScoreContext {
  readonly scoreApi: ScoreApi;
  readonly assetsApi: AssetsApi;
  readonly mixer: MixerPage;
  readonly page: Page;
}

/** A ready score and its ready view. */
interface ReadyScore {
  readonly assetId: string;
  readonly label: string;
  readonly view: ScoreViewDto;
}

const profileNames: Readonly<Record<ScoreProfile, string>> = { vocals: 'Vocals', bass: 'Bass', guitar: 'Guitar', keys: 'Keys', piano: 'Piano', drums: 'Drums' };

async function readyScore(ctx: ScoreContext, trackId: string, channel: string, profile: ScoreProfile): Promise<ReadyScore> {
  const created = await ctx.scoreApi.create({ source: { trackId, channel }, profile });
  await ctx.assetsApi.waitForReady(created.id, scoreMs);
  const view = await flow.waitForScoreView(created.id, viewMs);
  return { assetId: created.id, label: `${profileNames[profile]} - ${created.name}`, view };
}

async function openScore(ctx: ScoreContext, track: TrackDto, score: ReadyScore): Promise<ScorePanel> {
  const panel = new ScorePanel(ctx.page);
  await ctx.mixer.goto(track.id);
  await ctx.mixer.waitForReady(fakeStems);
  await panel.open();
  await panel.choose(score.label, score.assetId, viewMs);
  return panel;
}

async function heardSeconds(page: Page): Promise<number> {
  return positionSeconds((await page.getByTestId('transport-position').textContent()) ?? '');
}

/** Seeks into a bar, and expects the follower to show that bar (paused, so the heard position is the seek target). */
async function expectBarAfterSeek(mixer: MixerPage, panel: ScorePanel, view: ScoreViewDto, index: number): Promise<void> {
  const bar = view.bars[index];
  expect(bar, `the score has a bar ${String(index)}`).toBeDefined();
  await mixer.seek((bar?.startSeconds ?? 0) + 0.2);
  await expect.poll(() => panel.barIndex()).toBe(index);
  await expect.poll(() => panel.barNumber()).toBe(bar?.number);
}

/** Plays for a while at the current speed, pauses and checks the bar at the heard position. */
async function expectBarWhilePlaying(mixer: MixerPage, panel: ScorePanel, view: ScoreViewDto, page: Page): Promise<void> {
  const start = await heardSeconds(page);
  await mixer.play();
  await expect.poll(() => heardSeconds(page), { timeout: 10_000 }).toBeGreaterThan(start + 1.5);
  // Pause (transport-play is not a toggle: clicking it again kept playing, so at 50 % the bar ran on past the read position).
  await mixer.byId('transport-pause').click();
  await expect(mixer.byId('transport-pause')).toHaveAttribute('aria-pressed', 'true');
  await expect.poll(async () => barsNear(view, await heardSeconds(page)).includes(await panel.barNumber())).toBe(true);
}

/**
 * Bar numbers possible at the shown position: `transport-position` floors to 0.1 s (TimeText.Position), so the heard
 * position lies in [shown, shown + 0.1) and a bar boundary inside that window allows either bar.
 */
function barsNear(view: ScoreViewDto, shownSeconds: number): (number | undefined)[] {
  return [barAt(view, shownSeconds)?.number, barAt(view, shownSeconds + 0.0999)?.number];
}

/** `data-bar-index` when no bar is current (before the first bar, audio-engine 2.4.0 "Score follower"). */
const noBar = '-1';

/** Loop bounds over bars `from`..`to` (indexes) of a view. */
function loopOverBars(view: ScoreViewDto, from: number, to: number): { readonly start: number; readonly end: number } {
  const [first, last] = [view.bars[from], view.bars[to]];
  if (first === undefined || last === undefined) {
    throw new Error(`the score has fewer than ${String(to + 1)} bars`);
  }

  return { start: first.startSeconds, end: last.endSeconds };
}

test.describe('score follow per instrument family (014 US4, FR-033)', { tag: '@practice-flow' }, () => {
  for (const [channel, profile] of [['vocals', 'vocals'], ['bass', 'bass'], ['guitar', 'guitar'], ['piano', 'keys']] as const) {
    test(`${channel}: the bar under the heard position is highlighted (scenarios 1-2)`, async ({ api, scoreApi, assetsApi, mixer, page }) => {
      const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
      const ctx = { scoreApi, assetsApi, mixer, page };
      const score = await readyScore(ctx, track.id, channel, profile);
      const panel = await openScore(ctx, track, score);

      expect(score.view.bars.length).toBeGreaterThan(2);
      await expectBarAfterSeek(mixer, panel, score.view, 1);
      await expectBarAfterSeek(mixer, panel, score.view, Math.min(4, score.view.bars.length - 1));
      await expectBarWhilePlaying(mixer, panel, score.view, page);
    });
  }

  test('drums (after the split): the bar under the heard position is highlighted', async ({ api, scoreApi, assetsApi, mixer, page }) => {
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 2 * scoreMs);
    const band = await ensureDetailCompleted(api);
    const ctx = { scoreApi, assetsApi, mixer, page };
    const score = await readyScore(ctx, band.id, 'drums', 'drums');
    const panel = await openScore(ctx, band, score);

    expect(score.view.bars.length).toBeGreaterThan(1);
    await expectBarAfterSeek(mixer, panel, score.view, 1);
  });
});

test.describe('score follow with loops, speed and ranges (014 US4)', { tag: '@practice-flow' }, () => {
  test('at 50 % and 120 % speed the bar keeps matching the heard position (scenario 3)', async ({ api, scoreApi, assetsApi, mixer, page, practice }) => {
    const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
    const ctx = { scoreApi, assetsApi, mixer, page };
    const score = await readyScore(ctx, track.id, 'bass', 'bass');
    const panel = await openScore(ctx, track, score);
    expect(score.view.bars.length).toBeGreaterThan(1);

    for (const speed of ['50', '120']) {
      await fillAndCommit(practice.speed, speed);
      await mixer.seek(score.view.bars[1]?.startSeconds ?? 0);
      await expectBarWhilePlaying(mixer, panel, score.view, page);
    }
  });

  test('a loop wrap jumps the highlight back to the loop start bar (scenario 3)', async ({ api, scoreApi, assetsApi, mixer, page }) => {
    const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
    const ctx = { scoreApi, assetsApi, mixer, page };
    const score = await readyScore(ctx, track.id, 'bass', 'bass');
    const panel = await openScore(ctx, track, score);
    const { start, end } = loopOverBars(score.view, 1, 3);
    await mixer.setLoop(start, end, true);
    await mixer.seek(end - 0.4);
    const seen: number[] = [];

    await mixer.play();
    await expect.poll(async () => {
      seen.push(await panel.barIndex());
      return seen.includes(3) && seen.lastIndexOf(1) > seen.indexOf(3);
    }, { timeout: 10_000, intervals: [50] }).toBe(true);
  });

  test('a score of a range starts at the range and shows no bar outside it (scenario 5)', async ({ api, scoreApi, assetsApi, mixer, page }) => {
    const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
    // 008 CreateScoreRequest.range (backend 1.7.0); the shared CreateScoreDto of score-api.ts does not list it.
    const request: CreateScoreDto & { readonly range: { readonly startSeconds: number; readonly endSeconds: number } } = {
      source: { trackId: track.id, channel: 'bass' },
      profile: 'bass',
      range: { startSeconds: 6, endSeconds: 14 },
    };
    const created = await scoreApi.create(request);
    await assetsApi.waitForReady(created.id, scoreMs);
    const view = await flow.waitForScoreView(created.id, viewMs);
    const panel = await openScore({ scoreApi, assetsApi, mixer, page }, track, { assetId: created.id, label: `Bass - ${created.name}`, view });

    expect(view.offsetSeconds).toBe(6);
    expect(view.bars[0]?.startSeconds ?? 0).toBeGreaterThanOrEqual(6);
    await mixer.seek(2);
    await expect(panel.view).toHaveAttribute('data-bar-index', noBar); // before the first bar (audio-engine "Score follower")
    await mixer.seek(7);
    await expect.poll(() => panel.barNumber()).toBe(barAt(view, 7)?.number);
  });

  test('after scrolling by hand, Follow brings the current bar back into view (scenario 4, FR-011)', async ({ api, scoreApi, assetsApi, mixer, page }) => {
    const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
    const ctx = { scoreApi, assetsApi, mixer, page };
    const score = await readyScore(ctx, track.id, 'bass', 'bass');
    const panel = await openScore(ctx, track, score);
    await expect(panel.follow).toHaveAttribute('aria-pressed', 'true');
    await expectBarAfterSeek(mixer, panel, score.view, 1);

    await panel.view.hover();
    await page.mouse.wheel(0, 2000);
    await panel.follow.click();

    const current = score.view.bars[await panel.barIndex()];
    await expect(panel.pageOf(current?.page ?? 1)).toBeInViewport();
  });
});

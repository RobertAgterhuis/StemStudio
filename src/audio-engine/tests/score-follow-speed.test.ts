import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ScoreViewBar, ScoreViewMap } from '../src/index';
import { loadedPracticeRig, practice, type PracticeRig } from './mocks/practice-rig';

/**
 * The score follower through the engine at a practice speed (014+015 E2E `score-follow.spec.ts` "at 50 % and 120 %
 * speed"): set the speed, seek into bar 2, play, pause; the host shows the bar of the engine position, while playing
 * and after the pause.
 */
const HOST = 'score-speed-view';

/** 8 bars of 2 s from 0 s on one page. */
const VIEW: ScoreViewMap = {
  pages: [{ page: 1, url: '/api/assets/a/score-view/pages/1?v=1', widthPx: 840, heightPx: 1188 }],
  bars: Array.from({ length: 8 }, (_, index): ScoreViewBar => ({ index, number: index + 1, page: 1, x: 0.1, y: 0.1, width: 0.4, height: 0.2, startSeconds: index * 2, endSeconds: index * 2 + 2 })),
};

let frames: FrameRequestCallback[] = [];

/** Runs the pending animation frames (and the ones they request) once. */
function flushFrames(): void {
  const due = frames;
  frames = [];
  due.forEach((callback) => {
    callback(performance.now());
  });
}

function makeHost(): void {
  const host = document.createElement('div');
  host.id = HOST;
  const container = document.createElement('div');
  container.dataset.scorePage = '1';
  host.appendChild(container);
  document.body.replaceChildren(host);
}

function shownBar(): number {
  return Number(document.getElementById(HOST)?.dataset.barIndex ?? Number.NaN);
}

function barAt(seconds: number): number {
  return VIEW.bars.findIndex((bar) => bar.startSeconds <= seconds && seconds < bar.endSeconds);
}

async function followed(speedPercent: number): Promise<PracticeRig> {
  const rig = await loadedPracticeRig();
  makeHost();
  rig.engine.attachScoreFollower(HOST, VIEW, { follow: false, reducedMotion: true });
  await rig.engine.setPractice(practice({ speedPercent }));
  rig.engine.seek(VIEW.bars[1]?.startSeconds ?? 0);
  flushFrames();
  return rig;
}

/** Advances the context clock in 50 ms steps, drawing a frame after each. */
function advance(rig: PracticeRig, seconds: number): void {
  for (let i = 0; i < Math.round(seconds / 0.05); i++) {
    rig.ctx.currentTime += 0.05;
    flushFrames();
  }
}

beforeEach(() => {
  frames = [];
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => frames.push(callback));
  vi.stubGlobal('cancelAnimationFrame', () => undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
  document.body.replaceChildren();
});

describe('score follower at a practice speed (engine)', () => {
  it.each([50, 100, 120])('at %i percent the shown bar is the bar of the engine position, playing and paused', async (speedPercent) => {
    const rig = await followed(speedPercent);
    expect(shownBar()).toBe(1);
    await rig.engine.play();
    advance(rig, 3);
    expect(shownBar()).toBe(barAt(rig.engine.getPosition()));
    rig.engine.pause();
    flushFrames();
    const paused = rig.engine.getPosition();
    expect(paused).toBeGreaterThan(2);
    expect(shownBar()).toBe(barAt(paused));
  });
});

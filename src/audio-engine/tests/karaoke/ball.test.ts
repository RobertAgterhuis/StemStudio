import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HOP_HEIGHT_RATIO, SHORT_HOP_MS, ballAt, ballTransform, hopHeight, hopProgress } from '../../src/karaoke/ball';
import { LYRICS, attached, attr, frameAt, part, play, showAt, type KaraokeRig } from '../mocks/karaoke-rig';
import { FakeResizeObserver, LAYOUT, rowY, stubLayout, stubResizeObserver, wordX } from '../mocks/layout';

describe('ball math', () => {
  it('hopProgress is the clamped progress from one word start to the next', () => {
    expect(hopProgress(4000, 4000, 4400)).toBe(0);
    expect(hopProgress(4100, 4000, 4400)).toBe(0.25);
    expect(hopProgress(4400, 4000, 4400)).toBe(1);
    expect(hopProgress(3000, 4000, 4400)).toBe(0);
    expect(hopProgress(9000, 4000, 4400)).toBe(1);
    expect(hopProgress(4000, 4000, 4000)).toBe(1);
  });

  it('hopHeight is 0.6 x line height, scaled down for hops shorter than 150 ms', () => {
    expect(HOP_HEIGHT_RATIO).toBe(0.6);
    expect(SHORT_HOP_MS).toBe(150);
    expect(hopHeight(40, 400)).toBeCloseTo(24, 9);
    expect(hopHeight(40, 150)).toBeCloseTo(24, 9);
    expect(hopHeight(40, 75)).toBeCloseTo(12, 9);
    expect(hopHeight(40, 0)).toBe(0);
  });

  it('ballAt lands exactly on both words and peaks at h in the middle', () => {
    const from = { x: 10, y: 40 };
    const to = { x: 70, y: 80 };
    expect(ballAt(from, to, 0, 24)).toEqual(from);
    expect(ballAt(from, to, 1, 24)).toEqual(to);
    const mid = ballAt(from, to, 0.5, 24);
    expect(mid.x).toBeCloseTo(40, 9);
    expect(mid.y).toBeCloseTo(60 - 24, 9);
  });

  it('ballTransform writes translate3d in px', () => {
    expect(ballTransform({ x: 12.5, y: -3 })).toBe('translate3d(12.5px, -3px, 0)');
  });
});

function ball(rig: KaraokeRig): { x: number; y: number } {
  const match = /^translate3d\((-?[\d.e-]+)px, (-?[\d.e-]+)px, 0\)$/.exec(part(rig, '.karaoke-ball').style.transform);
  if (match === null) {
    throw new Error(`no ball transform: "${part(rig, '.karaoke-ball').style.transform}"`);
  }
  return { x: Number(match[1]), y: Number(match[2]) };
}

function expectBall(rig: KaraokeRig, x: number, y: number): void {
  const at = ball(rig);
  expect(at.x).toBeCloseTo(x, 6);
  expect(at.y).toBeCloseTo(y, 6);
}

/** Delivers the ResizeObserver callback the browser gives after a line change. */
function settle(): void {
  FakeResizeObserver.latest().trigger();
}

const H = HOP_HEIGHT_RATIO * LAYOUT.rowHeight;

describe('karaoke ball', () => {
  beforeEach(() => {
    stubLayout();
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('rests on the first word before the lyrics start', () => {
    const rig = attached();
    expectBall(rig, wordX(0), rowY('current'));
  });

  it('lands exactly on every word at its start time (0 ms error on the mock clock)', () => {
    const rig = attached();
    LYRICS.lines.forEach((line) => {
      line.words.forEach((word, index) => {
        showAt(rig, word.startMs);
        settle();
        expect(attr(rig, 'data-word-index')).toBe(String(index));
        expectBall(rig, wordX(index), rowY('current'));
      });
    });
  });

  it('flies a parabola between two words of a line', () => {
    const rig = attached();
    showAt(rig, 4200);
    expectBall(rig, (wordX(0) + wordX(1)) / 2, rowY('current') - H);
    showAt(rig, 4100);
    expectBall(rig, wordX(0) + 0.25 * (wordX(1) - wordX(0)), rowY('current') - H * 4 * 0.25 * 0.75);
  });

  it('flattens the arc for hops shorter than 150 ms', () => {
    const rig = attached();
    showAt(rig, 6550);
    settle();
    expectBall(rig, (wordX(1) + wordX(2)) / 2, rowY('current') - (H * 100) / 150);
  });

  it('arcs from the last word of a line to the first word of the next line in the next slot', () => {
    const rig = attached();
    showAt(rig, 5300);
    expectBall(rig, (wordX(2) + wordX(0)) / 2, (rowY('current') + rowY('next')) / 2 - H);
  });

  it('rests on the last word after the last word start', () => {
    const rig = attached();
    showAt(rig, 16_000);
    settle();
    expectBall(rig, wordX(1), rowY('current'));
    showAt(rig, 30_000);
    expectBall(rig, wordX(1), rowY('current'));
  });

  it('with jittered 60 Hz frames every word becomes current within one frame of its start', async () => {
    const rig = attached();
    await play(rig);
    const starts = LYRICS.lines.flatMap((line, l) => line.words.map((w, i) => ({ key: `${String(l)}:${String(i)}`, at: w.startMs })));
    const seen = new Map<string, number>();
    let seed = 7;
    let previous = 3500;
    for (let t = 3500; t < 17_500; ) {
      seed = (seed * 1_103_515_245 + 12_345) % 2_147_483_648;
      const step = 16.7 + ((seed / 2_147_483_648) * 10 - 5);
      previous = t;
      t += step;
      frameAt(rig, t);
      settle();
      const key = `${attr(rig, 'data-line-index') ?? ''}:${attr(rig, 'data-word-index') ?? ''}`;
      if (!seen.has(key)) {
        seen.set(key, t - previous);
        const start = starts.find((s) => s.key === key);
        if (start !== undefined) {
          expect(Number(attr(rig, 'data-position-ms')) - start.at).toBeGreaterThanOrEqual(0);
          expect(Number(attr(rig, 'data-position-ms')) - start.at).toBeLessThanOrEqual(step + 0.5);
        }
      }
    }
    expect(starts.every((s) => seen.has(s.key))).toBe(true);
  });
});

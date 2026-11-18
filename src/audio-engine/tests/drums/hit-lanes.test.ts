import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HitLanes, LANE_ORDER } from '../../src/drums/hit-lanes';
import type { BeatGrid, DrumHitEvent, DrumPiece, HitLaneOptions } from '../../src/types';
import { FakeContext2D, stubCanvas, type Primitive } from '../mocks/canvas';
import { FakeResizeObserver } from '../mocks/layout';
import { GRID, addHost, frameAt, hostSize, makeViewRig, showAt, type ViewRig } from '../mocks/views-rig';

interface LanesRig extends ViewRig {
  readonly lanes: HitLanes;
  readonly host: HTMLElement;
}

function makeLanes(grid: BeatGrid | null = GRID): LanesRig {
  const rig = makeViewRig();
  stubCanvas();
  const host = addHost('hit-lanes');
  const lanes = new HitLanes({ loop: rig.loop, clock: rig.clock, playing: rig.playing, grid: () => grid, document });
  rig.transport.addListener(lanes);
  return { ...rig, lanes, host };
}

/** One hit per piece at 1000 ms, a kick every 500 ms from 2000 to 20000 ms, strength rising. */
function hits(): DrumHitEvent[] {
  const list: DrumHitEvent[] = (['cymbals', 'hihat', 'toms', 'snare', 'kick'] as const).map((piece) => ({ timeMs: 1000, piece, strength: 1 }));
  for (let t = 2000; t <= 20_000; t += 500) {
    list.push({ timeMs: t, piece: t % 1000 === 0 ? 'kick' : 'snare', strength: (t % 2000) / 2000 });
  }
  return list;
}

function attached(options: HitLaneOptions = { reducedMotion: false }, list: DrumHitEvent[] = hits(), grid: BeatGrid | null = GRID): LanesRig {
  const rig = makeLanes(grid);
  rig.lanes.attach('hit-lanes', list, options);
  rig.frames.step(0);
  return rig;
}

function marks(): Primitive[] {
  return FakeContext2D.latest().ops('roundRect');
}

function lastFrame(_rig: LanesRig, draw: () => void): Primitive[] {
  FakeContext2D.latest().reset();
  draw();
  return FakeContext2D.latest().ops();
}

beforeEach(() => {
  document.body.replaceChildren();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('hit lanes canvas', () => {
  it('adds one hidden canvas sized to the host and the host attributes', () => {
    const rig = attached();
    const canvas = rig.host.querySelector('canvas');
    expect(rig.host.children).toHaveLength(1);
    expect(canvas?.getAttribute('aria-hidden')).toBe('true');
    expect([canvas?.width, canvas?.height]).toEqual([800, 120]);
    expect(rig.host.dataset.lookAheadMs).toBe('4000');
    expect(rig.host.dataset.positionMs).toBe('0');
  });

  it('scales the canvas by devicePixelRatio', () => {
    vi.stubGlobal('devicePixelRatio', 2);
    const rig = attached();
    const canvas = rig.host.querySelector('canvas');
    expect([canvas?.width, canvas?.height]).toEqual([1600, 240]);
    expect(FakeContext2D.latest().ops('setTransform')[0]?.args).toEqual([2, 0, 0, 2, 0, 0]);
  });

  it('draws the lanes top to bottom: cymbals, hihat, toms, snare, kick', () => {
    expect(LANE_ORDER).toEqual(['cymbals', 'hihat', 'toms', 'snare', 'kick']);
    attached();
    const atOneSecond = marks().filter((m) => m.args[0] === 316);
    // 120 px / 5 lanes = 24 px each; strength 1 = the whole lane.
    expect(atOneSecond.map((m) => m.args[1])).toEqual([0, 24, 48, 72, 96]);
  });

  it('places the now line at 20 % and the hits by time over the look-ahead window', () => {
    attached();
    const now = FakeContext2D.latest().ops('moveTo');
    expect(now.at(-1)?.args).toEqual([160, 0]);
    // window = [-1000, 4000) ms over 800 px; mark 8 px wide, centred on the hit.
    const xs = marks().map((m) => m.args[0]);
    expect(xs).toContain(316);
    expect(xs).toContain(716);
    expect(Math.max(...(xs as number[]))).toBeLessThan(800);
  });

  it('sizes marks 40-100 % of the lane by strength, centred in the lane', () => {
    attached({ reducedMotion: false }, [
      { timeMs: 1000, piece: 'kick', strength: 0 },
      { timeMs: 2000, piece: 'kick', strength: 0.5 },
    ]);
    const sizes = marks().map((m) => [m.args[1], m.args[3]].map((v) => Math.round(Number(v) * 1000) / 1000));
    expect(sizes).toEqual([
      [96 + 7.2, 9.6],
      [96 + 3.6, 16.8],
    ]);
  });

  it('flashes hits within 30 ms in the past', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    const frame = lastFrame(rig, () => {
      frameAt(rig, 1020);
    });
    const flashed = frame.filter((p) => p.op === 'roundRect' && Math.abs(Number(p.args[0]) - 152.8) < 1e-6);
    expect(flashed).toHaveLength(5);
    expect(frame.filter((p) => p.op === 'fill').map((p) => p.fill)).toEqual(['#3b82f6', '#f59e0b']);
    frameAt(rig, 1040);
    expect(FakeContext2D.latest().ops('fill').at(-1)?.fill).toBe('#3b82f6');
  });

  it('reads the colours from the host CSS custom properties', () => {
    const rig = makeLanes();
    rig.host.style.setProperty('--hit-lane-mark', 'rgb(1, 2, 3)');
    rig.lanes.attach('hit-lanes', hits(), { reducedMotion: false });
    rig.frames.step(0);
    expect(FakeContext2D.latest().ops('fill')[0]?.fill).toBe('rgb(1, 2, 3)');
  });
});

describe('hit lanes host attributes', () => {
  it('writes the next hit, its piece and the number of visible hits', () => {
    const rig = attached();
    expect(rig.host.dataset.nextHitMs).toBe('1000');
    expect(rig.host.dataset.nextHitPiece).toBe('cymbals');
    // 5 at 1000 + 2000, 2500, 3000, 3500 (window end 4000 exclusive)
    expect(rig.host.dataset.visibleHits).toBe('9');
    showAt(rig, 2000);
    expect(rig.host.dataset.nextHitMs).toBe('2500');
    expect(rig.host.dataset.nextHitPiece).toBe('snare');
  });

  it('writes empty next-hit attributes after the last hit', () => {
    const rig = attached();
    showAt(rig, 30_000);
    expect(rig.host.dataset.nextHitMs).toBe('');
    expect(rig.host.dataset.nextHitPiece).toBe('');
    expect(rig.host.dataset.visibleHits).toBe('0');
  });
});

describe('hit lanes options', () => {
  it('changes look-ahead and pieces without re-attaching', () => {
    const rig = attached();
    const canvas = rig.host.querySelector('canvas');
    const frame = lastFrame(rig, () => {
      rig.lanes.setOptions({ lookAheadMs: 8000, pieces: ['snare', 'kick'] });
      rig.frames.step(1);
    });
    expect(rig.host.querySelector('canvas')).toBe(canvas);
    expect(rig.host.dataset.lookAheadMs).toBe('8000');
    // window [-2000, 8000) ms: 0.08 px/ms; two lanes of 60 px (snare, kick); the cymbal, hi-hat and tom hits are gone
    const atOne = frame.filter((m) => m.op === 'roundRect' && m.args[0] === 236);
    expect(atOne.map((m) => [m.args[1], m.args[3]])).toEqual([[0, 60], [60, 60]]);
    expect(rig.host.dataset.nextHitPiece).toBe('snare');
    expect(rig.host.dataset.nextHitMs).toBe('1000');
  });

  it('clamps the look-ahead to 2000..8000 ms', () => {
    const rig = attached({ lookAheadMs: 500, reducedMotion: false });
    expect(rig.host.dataset.lookAheadMs).toBe('2000');
    rig.lanes.setOptions({ lookAheadMs: 20_000 });
    rig.frames.step(1);
    expect(rig.host.dataset.lookAheadMs).toBe('8000');
  });

  it('rejects an unknown piece in the options and keeps the old ones', () => {
    const rig = attached();
    expect(() => {
      rig.lanes.setOptions({ pieces: ['cowbell' as DrumPiece] });
    }).toThrow('Invalid drum hits: unknown piece cowbell');
    expect(rig.host.dataset.lookAheadMs).toBe('4000');
  });

  it('switches to paging when reducedMotion is turned on by the options', () => {
    const rig = attached();
    showAt(rig, 3000);
    rig.lanes.setOptions({ reducedMotion: true });
    rig.frames.step(1);
    expect(FakeContext2D.latest().ops('moveTo').at(-1)?.args).toEqual([320, 0]);
  });

  it('ignores options without an attached view', () => {
    const rig = makeLanes();
    rig.lanes.setOptions({ lookAheadMs: 3000 });
    expect(rig.frames.size).toBe(0);
  });
});

describe('hit lanes reduced motion', () => {
  it('pages per bar of the beat grid', async () => {
    const rig = attached({ reducedMotion: true });
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    const seen = new Set<string>();
    for (let ms = 2000; ms < 4000; ms += 100) {
      const frame = lastFrame(rig, () => {
        frameAt(rig, ms);
      });
      seen.add(frame.filter((p) => p.op === 'roundRect').map((p) => Number(p.args[0])).sort((a, b) => a - b).join());
    }
    expect(seen.size).toBe(1);
    frameAt(rig, 4000);
    expect(FakeContext2D.latest().ops('moveTo').at(-1)?.args).toEqual([160, 0]);
  });

  it('pages per look-ahead without a grid, the now line moving inside the page', () => {
    const rig = attached({ reducedMotion: true }, hits(), null);
    showAt(rig, 2000);
    expect(FakeContext2D.latest().ops('moveTo').at(-1)?.args).toEqual([160 + 2000 * 0.16, 0]);
    showAt(rig, 4000);
    expect(FakeContext2D.latest().ops('moveTo').at(-1)?.args).toEqual([160, 0]);
  });
});

describe('hit lanes validation', () => {
  const ok: DrumHitEvent = { timeMs: 0, piece: 'kick', strength: 0.5 };

  it.each([
    ['too many', Array.from({ length: 40_001 }, () => ok), 'Invalid drum hits: more than 40000 hits'],
    ['unknown piece', [{ ...ok, piece: 'cowbell' }], 'Invalid drum hits: unknown piece cowbell'],
    ['strength', [{ ...ok, strength: 1.5 }], 'Invalid drum hits: strength outside 0..1'],
    ['NaN strength', [{ ...ok, strength: Number.NaN }], 'Invalid drum hits: strength outside 0..1'],
    ['times decreasing', [{ ...ok, timeMs: 10 }, ok], 'Invalid drum hits: times decreasing'],
    ['bad time', [{ ...ok, timeMs: Number.NaN }], 'Invalid drum hits: times decreasing'],
    ['not a list', null, 'Invalid drum hits: hits missing'],
  ])('rejects %s', (_, list, message) => {
    const rig = makeLanes();
    expect(() => {
      rig.lanes.attach('hit-lanes', list as unknown as DrumHitEvent[]);
    }).toThrow(message);
  });

  it('rejects an unknown host', () => {
    const rig = makeLanes();
    expect(() => {
      rig.lanes.attach('nope', [ok]);
    }).toThrow('Hit lane host not found: nope');
  });

  it('validates before replacing an attached view', () => {
    const rig = attached();
    expect(() => {
      rig.lanes.attach('hit-lanes', [{ ...ok, strength: 2 }]);
    }).toThrow('strength');
    expect(rig.host.querySelector('canvas')).not.toBeNull();
  });
});

describe('hit lanes lifecycle', () => {
  it('re-measures the canvas on resize', () => {
    const rig = attached();
    hostSize.width = 400;
    FakeResizeObserver.latest().trigger();
    rig.frames.step(1);
    expect(rig.host.querySelector('canvas')?.width).toBe(400);
    expect(FakeContext2D.latest().ops('moveTo').at(-1)?.args).toEqual([80, 0]);
  });

  it('replaces an attached view and detaches cleanly (idempotent)', () => {
    const rig = attached();
    const other = addHost('other');
    rig.lanes.attach('other', hits());
    expect(rig.host.children).toHaveLength(0);
    expect(rig.host.dataset.lookAheadMs).toBeUndefined();
    expect(other.querySelector('canvas')).not.toBeNull();
    rig.lanes.detach();
    rig.lanes.detach();
    expect(other.children).toHaveLength(0);
    expect(Object.keys(other.dataset)).toEqual([]);
  });

  it('drops a frame that was requested before detach and reports zero primitives without a view', () => {
    const rig = makeLanes();
    rig.lanes.attach('hit-lanes', hits());
    rig.lanes.detach();
    rig.frames.step(0);
    expect(rig.lanes.stats).toEqual({ frames: 0, primitives: 0 });
  });

  it('keeps the attributes without a 2D context (no canvas support)', () => {
    const rig = makeLanes();
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
    rig.lanes.attach('hit-lanes', hits(), { reducedMotion: false });
    rig.frames.step(0);
    expect(rig.host.dataset.visibleHits).toBe('9');
  });

  it('follows prefers-reduced-motion at runtime', () => {
    const listeners: ((event: { matches: boolean }) => void)[] = [];
    vi.stubGlobal('matchMedia', () => ({ matches: false, addEventListener: (_: string, l: (event: { matches: boolean }) => void) => listeners.push(l), removeEventListener: vi.fn() }));
    const rig = attached({});
    showAt(rig, 3000);
    expect(FakeContext2D.latest().ops('moveTo').at(-1)?.args).toEqual([160, 0]);
    listeners.forEach((l) => { l({ matches: true }); });
    rig.frames.step(1);
    expect(FakeContext2D.latest().ops('moveTo').at(-1)?.args).toEqual([160 + 1000 * 0.16, 0]);
  });
});

describe('hit lanes frame budget (counted primitives)', () => {
  it('a frame draws the clear, the now line and one primitive per visible hit plus two batches', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    const frame = lastFrame(rig, () => {
      frameAt(rig, 5000);
    });
    const visible = Number(rig.host.dataset.visibleHits);
    expect(frame.filter((p) => p.op === 'roundRect')).toHaveLength(visible);
    expect(frame.length).toBeLessThanOrEqual(visible + 9);
    expect(rig.lanes.stats.frames).toBeGreaterThan(1);
    expect(rig.lanes.stats.primitives).toBeGreaterThan(0);
  });
});

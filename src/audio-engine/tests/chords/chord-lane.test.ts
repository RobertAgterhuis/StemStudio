import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MAX_LANE_ITEMS } from '../../src/chords/lane-view';
import { ChordLane } from '../../src/chords/lane';
import { ChordDisplayState } from '../../src/chords/display';
import type { BeatGrid, ChordChartData, ChordLaneOptions } from '../../src/types';
import { FakeResizeObserver } from '../mocks/layout';
import { GRID, addHost, cycleChart, frameAt, hostSize, makeViewRig, showAt, type ViewRig } from '../mocks/views-rig';

interface LaneRig extends ViewRig {
  readonly lane: ChordLane;
  readonly host: HTMLElement;
  readonly display: ChordDisplayState;
  grid: BeatGrid | null;
}

function makeLane(grid: BeatGrid | null = GRID): LaneRig {
  const rig = makeViewRig();
  const host = addHost('chord-lane');
  const display = new ChordDisplayState();
  const state = { grid };
  const lane = new ChordLane({ loop: rig.loop, clock: rig.clock, playing: rig.playing, grid: () => state.grid, display, document });
  rig.transport.addListener(lane);
  return Object.assign(state, { ...rig, lane, host, display });
}

function attached(options: ChordLaneOptions = { reducedMotion: false }, chart: ChordChartData = cycleChart(), grid: BeatGrid | null = GRID): LaneRig {
  const rig = makeLane(grid);
  rig.lane.attach('chord-lane', chart, options);
  rig.frames.step(0);
  return rig;
}

function items(rig: LaneRig): HTMLElement[] {
  return [...rig.host.querySelectorAll<HTMLElement>('.chord-lane-item')];
}

function track(rig: LaneRig): HTMLElement {
  const element = rig.host.querySelector<HTMLElement>('.chord-lane-track');
  if (element === null) {
    throw new Error('no track');
  }
  return element;
}

function current(rig: LaneRig): HTMLElement[] {
  return items(rig).filter((i) => i.dataset.state === 'current');
}

beforeEach(() => {
  document.body.replaceChildren();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('chord lane DOM', () => {
  it('builds a track with one text item per visible segment, start and state', () => {
    const rig = attached();
    const all = items(rig);
    expect(track(rig).parentElement).toBe(rig.host);
    expect(all.map((i) => i.textContent)).toEqual(['N.C.', 'C', 'Am', 'F', 'G7/B', 'C', 'Am', 'F']);
    expect(all.map((i) => i.dataset.startMs)).toEqual(['0', '1000', '3000', '5000', '7000', '9000', '11000', '13000']);
    expect(all.map((i) => i.dataset.state)).toEqual(['current', 'future', 'future', 'future', 'future', 'future', 'future', 'future']);
  });

  it('writes the host attributes', () => {
    const rig = attached();
    expect(rig.host.dataset.chordIndex).toBe('0');
    expect(rig.host.dataset.currentSymbol).toBe('N.C.');
    expect(rig.host.dataset.nextSymbol).toBe('C');
    expect(rig.host.dataset.positionMs).toBe('0');
  });

  it('positions items by style.transform in bar units and sizes them by their length', () => {
    const rig = attached();
    // 1 bar behind + current + 6 ahead = 8 bars over 800 px: 100 px per bar of 2 s.
    const [, c] = items(rig);
    expect(c?.style.transform).toBe('translateX(50px)');
    expect(c?.style.width).toBe('100px');
    expect(track(rig).style.transform).toBe('translateX(100px)');
  });
});

describe('chord lane window', () => {
  it('shows barsBehind before and barsAhead after the current bar', () => {
    const rig = attached({ barsBehind: 2, barsAhead: 2, reducedMotion: false });
    showAt(rig, 10_000);
    // bars 3..7 = 6000..16000 ms (current bar 5 = 10000..12000).
    expect(items(rig).map((i) => i.dataset.startMs)).toEqual(['5000', '7000', '9000', '11000', '13000', '15000']);
  });

  it('uses 4 s behind and 12 s ahead without a beat grid', () => {
    const rig = attached({ reducedMotion: false }, cycleChart(), null);
    showAt(rig, 10_000);
    expect(items(rig).map((i) => i.dataset.startMs)).toEqual(['5000', '7000', '9000', '11000', '13000', '15000', '17000', '19000', '21000']);
    expect(track(rig).style.transform).toBe('translateX(-300px)');
  });

  it('uses seconds for a grid with fewer than two downbeats', () => {
    const rig = attached({ reducedMotion: false }, cycleChart(), { ...GRID, downbeats: [0] });
    showAt(rig, 10_000);
    expect(items(rig)).toHaveLength(9);
  });

  it('scrolls the track continuously while playing', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    frameAt(rig, 3000);
    expect(track(rig).style.transform).toBe('translateX(-50px)');
    frameAt(rig, 3500);
    expect(track(rig).style.transform).toBe('translateX(-75px)');
  });

  it('never keeps more than the item limit in the DOM', () => {
    const segments = Array.from({ length: 300 }, (_, i) => ({ startMs: i * 100, endMs: (i + 1) * 100, root: i % 12, quality: 'maj' as const, bass: null }));
    const rig = attached({ reducedMotion: false }, { segments, keyTonic: 0, keyMode: 'major' });
    showAt(rig, 10_000);
    expect(items(rig).length).toBeLessThanOrEqual(MAX_LANE_ITEMS);
    expect(current(rig)).toHaveLength(1);
  });

  it('clamps barsBehind to 0..8 and barsAhead to 1..16', () => {
    const rig = attached({ barsBehind: -3, barsAhead: 99, reducedMotion: false });
    expect(track(rig).style.transform).toBe('translateX(0px)');
    expect(items(rig).at(-1)?.dataset.startMs).toBe('33000');
  });
});

describe('chord lane states', () => {
  it('follows the heard position while playing (past/current/future) with next symbol', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    frameAt(rig, 3500);
    expect(rig.host.dataset.chordIndex).toBe('2');
    expect(rig.host.dataset.currentSymbol).toBe('Am');
    expect(rig.host.dataset.nextSymbol).toBe('F');
    expect(rig.host.dataset.positionMs).toBe('3500');
    expect(items(rig).map((i) => i.dataset.state).slice(0, 4)).toEqual(['past', 'current', 'future', 'future']);
  });

  it('binary-searches after a seek and a loop wrap', () => {
    const rig = attached();
    showAt(rig, 40_000);
    expect(current(rig).map((i) => i.dataset.startMs)).toEqual(['39000']);
    showAt(rig, 2000);
    expect(current(rig).map((i) => i.dataset.startMs)).toEqual(['1000']);
  });

  it('writes an empty next symbol at the last segment', () => {
    const rig = attached();
    showAt(rig, 59_500);
    expect(rig.host.dataset.currentSymbol).toBe('N.C.');
    expect(rig.host.dataset.nextSymbol).toBe('');
  });

  it('reports -1 before the first segment of a chart that starts later', () => {
    const chart: ChordChartData = { segments: [{ startMs: 5000, endMs: 9000, root: 2, quality: 'min', bass: null }], keyTonic: null, keyMode: null };
    const rig = attached({ reducedMotion: false }, chart);
    expect(rig.host.dataset.chordIndex).toBe('-1');
    expect(rig.host.dataset.currentSymbol).toBe('');
    expect(rig.host.dataset.nextSymbol).toBe('Dm');
  });
});

describe('chord lane reduced motion', () => {
  it('pages instead of scrolling: the track moves only at a page boundary', async () => {
    const rig = attached({ reducedMotion: true });
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    const transforms = new Set<string>();
    for (let ms = 0; ms < 14_000; ms += 250) {
      frameAt(rig, ms);
      transforms.add(track(rig).style.transform);
    }
    frameAt(rig, 14_000);
    // page = 7 bars (current + 6 ahead) = 14 s; window starts one bar before the page.
    expect([...transforms]).toEqual(['translateX(100px)']);
    expect(track(rig).style.transform).toBe('translateX(-600px)');
  });

  it('follows prefers-reduced-motion by default, including runtime changes', () => {
    const listeners: ((event: { matches: boolean }) => void)[] = [];
    const query = { matches: true, addEventListener: (_: string, l: (event: { matches: boolean }) => void) => listeners.push(l), removeEventListener: vi.fn() };
    vi.stubGlobal('matchMedia', () => query);
    const rig = attached({});
    showAt(rig, 3000);
    expect(track(rig).style.transform).toBe('translateX(100px)');
    listeners.forEach((l) => { l({ matches: false }); });
    rig.frames.step(1);
    expect(track(rig).style.transform).toBe('translateX(-50px)');
    rig.lane.detach();
    expect(query.removeEventListener).toHaveBeenCalled();
  });
});

describe('chord lane layout', () => {
  it('re-measures on resize and redraws one frame', () => {
    const rig = attached();
    hostSize.width = 1600;
    FakeResizeObserver.latest().trigger();
    expect(rig.frames.size).toBe(1);
    rig.frames.step(1);
    expect(items(rig)[1]?.style.transform).toBe('translateX(100px)');
    expect(track(rig).style.transform).toBe('translateX(200px)');
  });

  it('works without ResizeObserver', () => {
    const rig = makeLane();
    vi.stubGlobal('ResizeObserver', undefined);
    rig.lane.attach('chord-lane', cycleChart(), { reducedMotion: false });
    rig.frames.step(0);
    expect(items(rig)).toHaveLength(8);
  });
});

describe('chord lane validation', () => {
  const ok = cycleChart();
  const seg = { startMs: 0, endMs: 1000, root: 0, quality: 'maj' as const, bass: null };

  it.each([
    ['not ordered', { ...ok, segments: [{ ...seg, startMs: 1000, endMs: 2000 }, seg] }, 'Invalid chord chart: segments not ordered/contiguous'],
    ['gap', { ...ok, segments: [seg, { ...seg, startMs: 1500, endMs: 2000 }] }, 'Invalid chord chart: segments not ordered/contiguous'],
    ['empty segment', { ...ok, segments: [{ ...seg, endMs: 0 }] }, 'Invalid chord chart: segments not ordered/contiguous'],
    ['negative start', { ...ok, segments: [{ ...seg, startMs: -5 }] }, 'Invalid chord chart: segments not ordered/contiguous'],
    ['too many', { ...ok, segments: Array.from({ length: 4001 }, (_, i) => ({ ...seg, startMs: i, endMs: i + 1 })) }, 'Invalid chord chart: more than 4000 segments'],
    ['root', { ...ok, segments: [{ ...seg, root: 12 }] }, 'Invalid chord chart: root outside 0..11'],
    ['fractional root', { ...ok, segments: [{ ...seg, root: 1.5 }] }, 'Invalid chord chart: root outside 0..11'],
    ['bass', { ...ok, segments: [{ ...seg, bass: -1 }] }, 'Invalid chord chart: bass outside 0..11'],
    ['quality', { ...ok, segments: [{ ...seg, quality: 'maj9' }] }, 'Invalid chord chart: unknown quality maj9'],
    ['no chord with a quality', { ...ok, segments: [{ ...seg, root: null }] }, 'Invalid chord chart: unknown quality maj'],
    ['root without quality', { ...ok, segments: [{ ...seg, quality: null }] }, 'Invalid chord chart: unknown quality null'],
    ['key tonic', { ...ok, keyTonic: 13 }, 'Invalid chord chart: key tonic outside 0..11'],
    ['region tonic', { ...ok, keyRegions: [{ startMs: 0, endMs: 10, tonic: 12, mode: 'major' }] }, 'Invalid chord chart: key tonic outside 0..11'],
    ['region mode', { ...ok, keyRegions: [{ startMs: 0, endMs: 10, tonic: 1, mode: 'dorian' }] }, 'Invalid chord chart: unknown key mode dorian'],
    ['regions order', { ...ok, keyRegions: [{ startMs: 10, endMs: 20, tonic: 1, mode: 'major' }, { startMs: 0, endMs: 10, tonic: 1, mode: 'major' }] }, 'Invalid chord chart: key regions not ordered'],
    ['too many regions', { ...ok, keyRegions: Array.from({ length: 51 }, (_, i) => ({ startMs: i, endMs: i + 1, tonic: 1, mode: 'major' })) }, 'Invalid chord chart: more than 50 key regions'],
    ['segments not an array', { ...ok, segments: null }, 'Invalid chord chart: segments missing'],
  ])('rejects %s', (_, chart, message) => {
    const rig = makeLane();
    expect(() => {
      rig.lane.attach('chord-lane', chart as unknown as ChordChartData);
    }).toThrow(message);
  });

  it('rejects an unknown host', () => {
    const rig = makeLane();
    expect(() => {
      rig.lane.attach('nope', ok);
    }).toThrow('Chord lane host not found: nope');
  });

  it('validates before replacing an attached lane (the old one stays)', () => {
    const rig = attached();
    expect(() => {
      rig.lane.attach('chord-lane', { ...ok, segments: [{ ...seg, root: 99 }] });
    }).toThrow('root outside');
    expect(items(rig)).toHaveLength(8);
  });

  it('accepts an empty chart', () => {
    const rig = attached({ reducedMotion: false }, { segments: [], keyTonic: null, keyMode: null });
    expect(items(rig)).toHaveLength(0);
    expect(rig.host.dataset.chordIndex).toBe('-1');
  });
});

describe('chord lane attach and detach', () => {
  it('replaces an attached lane', () => {
    const rig = attached();
    const other = addHost('other-lane');
    rig.lane.attach('other-lane', cycleChart(), { reducedMotion: false });
    rig.frames.step(1);
    expect(rig.host.children).toHaveLength(0);
    expect(rig.host.dataset.chordIndex).toBeUndefined();
    expect(other.querySelectorAll('.chord-lane-item')).toHaveLength(8);
  });

  it('detach removes the children and host attributes and stops drawing; a second detach is a no-op', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    rig.lane.detach();
    expect(rig.host.children).toHaveLength(0);
    expect(Object.keys(rig.host.dataset)).toEqual([]);
    expect(FakeResizeObserver.latest().disconnected).toBe(true);
    rig.frames.step(1);
    expect(rig.frames.size).toBe(0);
    expect(() => {
      rig.lane.detach();
    }).not.toThrow();
  });

  it('detach without an attached lane is a no-op', () => {
    const rig = makeLane();
    expect(() => {
      rig.lane.detach();
    }).not.toThrow();
    rig.lane.redraw();
    expect(rig.frames.size).toBe(0);
  });

  it('draws one frame after pause, seek, stop and loop/timing changes', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    rig.transport.pause();
    rig.frames.step(1);
    expect(rig.frames.size).toBe(0);
    rig.lane.onTimingChanged();
    expect(rig.frames.size).toBe(1);
  });

  it('follows a grid change on re-attach (the grid is read at attach)', () => {
    const rig = attached();
    rig.grid = null;
    rig.lane.attach('chord-lane', cycleChart(), { reducedMotion: false });
    rig.frames.step(1);
    expect(items(rig)).toHaveLength(7);
  });
});

describe('chord lane frame budget (counted writes)', () => {
  it('a steady playback frame writes only the track transform and the position', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    frameAt(rig, 1500);
    const before = rig.lane.stats.writes;
    frameAt(rig, 1516);
    expect(rig.lane.stats.writes - before).toBe(2);
  });

  it('a chord change writes at most the two changed states, one write per entering/leaving item and the host attributes', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    frameAt(rig, 2990);
    const before = rig.lane.stats.writes;
    frameAt(rig, 3006);
    expect(rig.lane.stats.writes - before).toBeLessThanOrEqual(10);
    expect(rig.lane.stats.frames).toBeGreaterThan(2);
  });

  it('a paused frame without a change writes nothing', () => {
    const rig = attached();
    const before = rig.lane.stats.writes;
    rig.lane.redraw();
    rig.frames.step(1);
    expect(rig.lane.stats.writes).toBe(before);
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PitchView } from '../../src/singing/pitch-view';
import type { SingingUnit } from '../../src/types';
import { FakeContext2D, stubCanvas, type Primitive } from '../mocks/canvas';
import { FakeResizeObserver } from '../mocks/layout';
import { addHost, hostSize, makeViewRig } from '../mocks/views-rig';

/** 6900 from 0 to 1 s, 7100 from 1 to 2 s, unvoiced to 10 s. */
const REFERENCE = Int16Array.from({ length: 1000 }, (_, i) => (i < 100 ? 6900 : i < 200 ? 7100 : 0));
const UNITS: readonly SingingUnit[] = [
  { index: 0, startMs: 0, endMs: 1000 },
  { index: 1, startMs: 1000, endMs: 2000 },
];

interface Rig {
  readonly view: PitchView;
  readonly host: HTMLElement;
  readonly state: { transpose: number };
  readonly frames: number[];
}

function makeView(reducedMotion: boolean | undefined = false, reference: Int16Array = REFERENCE): Rig {
  makeViewRig();
  stubCanvas();
  const host = addHost('pitch-view');
  const state = { transpose: 0 };
  const frames: number[] = [];
  const view = new PitchView({ host, reference, units: UNITS, latencySource: 'loopback', reducedMotion, transpose: () => state.transpose, requestFrame: () => frames.push(1) });
  return { view, host, state, frames };
}

function drawn(rig: Rig, t: number): Primitive[] {
  FakeContext2D.latest().reset();
  rig.view.draw(t);
  return FakeContext2D.latest().ops();
}

function round(values: readonly number[]): number[] {
  return values.map((v) => Math.round(v * 100) / 100);
}

beforeEach(() => {
  document.body.replaceChildren();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('pitch view canvas (data-model 3.5)', () => {
  it('owns one hidden canvas and writes the latency source and state', () => {
    const rig = makeView();
    rig.view.setState('running');
    rig.view.draw(0);
    expect(rig.host.querySelector('canvas')?.getAttribute('aria-hidden')).toBe('true');
    expect(rig.host.dataset.latencySource).toBe('loopback');
    expect(rig.host.dataset.state).toBe('running');
  });

  it('draws reference bars per run of equal voiced frames over 4 s back / 2 s ahead, now at 2/3', () => {
    const rig = makeView();
    const frame = drawn(rig, 0);
    // range 6900..7100 +-300 -> 800 cents, widened to one octave around 7000: 6400..7600 over 120 px
    const bars = frame.filter((p) => p.op === 'roundRect').map((p) => round(p.args.slice(0, 4)));
    expect(bars).toEqual([
      [533.33, 67, 133.33, 6],
      [666.67, 47, 133.33, 6],
    ]);
    expect(round(frame.filter((p) => p.op === 'moveTo').at(-1)?.args ?? [])).toEqual([533.33, 0]);
  });

  it('widens a narrow range to an octave and keeps a wide one', () => {
    const wide = Int16Array.from({ length: 300 }, (_, i) => (i < 150 ? 4800 : 7200));
    const rig = makeView(false, wide);
    const bars = drawn(rig, 1500).filter((p) => p.op === 'roundRect');
    // 4800 - 300 .. 7200 + 300 = 4500..7500: 4800 at y = 120 * (1 - 300 / 3000) - 3
    expect(round([Number(bars[0]?.args[1])])).toEqual([105]);
  });

  it('draws the singer as a polyline folded into the octave of the target; null breaks the line', () => {
    const rig = makeView();
    [[0, 8100], [10, 8100], [20, null], [30, 5700], [40, 5700]].forEach(([ms, cents]) => {
      rig.view.addSinger(Number(ms), cents ?? null);
    });
    const frame = drawn(rig, 40);
    const line = frame.filter((p) => p.op === 'moveTo' || p.op === 'lineTo').slice(0, 4);
    expect(line.map((p) => [p.op, Math.round(Number(p.args[1]))])).toEqual([
      ['moveTo', 70],
      ['lineTo', 70],
      ['moveTo', 70],
      ['lineTo', 70],
    ]);
  });

  it('folds a singer without a target towards the previous point', () => {
    const rig = makeView();
    [[5000, 6900], [5010, 8100]].forEach(([ms, cents]) => {
      rig.view.addSinger(Number(ms), Number(cents));
    });
    const frame = drawn(rig, 5010);
    const ys = frame.filter((p) => p.op === 'moveTo' || p.op === 'lineTo').slice(0, 2).map((p) => Math.round(Number(p.args[1])));
    expect(ys).toEqual([70, 70]);
  });

  it('forgets singer points older than the window', () => {
    const rig = makeView();
    rig.view.addSinger(0, 6900);
    rig.view.addSinger(7000, 6900);
    const frame = drawn(rig, 7000);
    expect(frame.filter((p) => p.op === 'moveTo')).toHaveLength(2);
  });
});

describe('pitch view host attributes', () => {
  it('writes singer and target cents, the unit and the last score', () => {
    const rig = makeView();
    rig.view.addSinger(500, 6912.4);
    rig.view.draw(500);
    expect(rig.host.dataset.singerCents).toBe('6912');
    expect(rig.host.dataset.targetCents).toBe('6900');
    expect(rig.host.dataset.unitIndex).toBe('0');
    expect(rig.host.dataset.lastScore).toBe('');
    rig.view.setLastScore(85);
    rig.view.addSinger(510, null);
    rig.view.draw(1500);
    expect(rig.host.dataset.singerCents).toBe('');
    expect(rig.host.dataset.targetCents).toBe('7100');
    expect(rig.host.dataset.unitIndex).toBe('1');
    expect(rig.host.dataset.lastScore).toBe('85');
    rig.view.setLastScore(null);
    rig.view.draw(3000);
    expect(rig.host.dataset.lastScore).toBe('');
    expect(rig.host.dataset.targetCents).toBe('');
    expect(rig.host.dataset.unitIndex).toBe('-1');
  });

  it('moves the target with the 012 transpose (the vertical range moves with it)', () => {
    const rig = makeView();
    rig.state.transpose = 2;
    const bars = drawn(rig, 0).filter((p) => p.op === 'roundRect');
    expect(rig.host.dataset.targetCents).toBe('7100');
    expect(round([Number(bars[0]?.args[1])])).toEqual([67]);
  });

  it('dispose removes the canvas and the attributes', () => {
    const rig = makeView();
    rig.view.setState('running');
    rig.view.draw(0);
    rig.view.dispose();
    expect(rig.host.children).toHaveLength(0);
    expect(Object.keys(rig.host.dataset)).toEqual([]);
  });
});

describe('pitch view motion and layout', () => {
  it('pages every 6 s with reduced motion; the now line moves inside the page', () => {
    const rig = makeView(true);
    const at = (t: number): number[] => round(drawn(rig, t).filter((p) => p.op === 'moveTo').at(-1)?.args ?? []);
    expect(at(7000)).toEqual([133.33, 0]);
    expect(at(11_000)).toEqual([666.67, 0]);
    expect(at(12_000)).toEqual([0, 0]);
  });

  it('follows prefers-reduced-motion by default', () => {
    vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
    const rig = makeView(undefined);
    expect(round(drawn(rig, 7000).filter((p) => p.op === 'moveTo').at(-1)?.args ?? [])).toEqual([133.33, 0]);
  });

  it('re-measures on resize and requests a frame', () => {
    const rig = makeView();
    hostSize.width = 600;
    FakeResizeObserver.latest().trigger();
    expect(rig.frames).toEqual([1]);
    expect(round(drawn(rig, 0).filter((p) => p.op === 'moveTo').at(-1)?.args ?? [])).toEqual([400, 0]);
  });

  it('stays within the frame budget: one primitive per point and bar plus a few', () => {
    const rig = makeView();
    for (let ms = 0; ms <= 6000; ms += 10) {
      rig.view.addSinger(ms, 6900);
    }
    const frame = drawn(rig, 6000);
    expect(frame.length).toBeLessThanOrEqual(401 + 2 + 10);
    expect(rig.view.primitives).toBeGreaterThan(0);
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { bannerChords } from '../../src/chords/banner';
import { ChordDisplayState } from '../../src/chords/display';
import { ChordLane } from '../../src/chords/lane';
import type { ChordChartData } from '../../src/types';
import { attached as attachedBanner, type KaraokeRig } from '../mocks/karaoke-rig';
import { stubLayout, stubResizeObserver } from '../mocks/layout';
import { GRID, addHost, chordSegment as seg, frameAt, makeViewRig, showAt, type ViewRig } from '../mocks/views-rig';

/** FR-075: capo mode (data-model 2.9): shapes with the capo, sounding symbols, host `data-capo`. */

/** Eb major, Cm, Ab, Bb of 2 s each in E-flat major (sounding); with capo 3 the shapes are C, Am, F, G. */
const CHART: ChordChartData = {
  segments: [seg(0, 2000, 3), seg(2000, 4000, 0, 'min'), seg(4000, 6000, 8), seg(6000, 8000, 10), seg(8000, 30_000, null)],
  keyTonic: 3,
  keyMode: 'major',
};

interface LaneRig extends ViewRig {
  readonly lane: ChordLane;
  readonly host: HTMLElement;
  readonly display: ChordDisplayState;
}

function lane(display = new ChordDisplayState()): LaneRig {
  const rig = makeViewRig();
  const host = addHost('chord-lane');
  const chordLane = new ChordLane({ loop: rig.loop, clock: rig.clock, playing: rig.playing, grid: () => GRID, display, document });
  rig.transport.addListener(chordLane);
  chordLane.attach('chord-lane', CHART, { reducedMotion: false });
  rig.frames.step(0);
  return Object.assign(rig, { lane: chordLane, host, display });
}

/** Shape text (the first text node) of the chord elements. */
function shapes(elements: Iterable<HTMLElement>): string[] {
  return [...elements].map((e) => String(e.firstChild?.textContent));
}

/** Text of the `.chord-sounding` child of every element (`null` when it has none). */
function soundings(elements: Iterable<HTMLElement>): (string | null)[] {
  return [...elements].map((e) => e.querySelector(':scope > .chord-sounding')?.textContent ?? null);
}

function laneItems(rig: LaneRig): HTMLElement[] {
  return [...rig.host.querySelectorAll<HTMLElement>('.chord-lane-item')].slice(0, 4);
}

function redraw(rig: ViewRig): void {
  rig.frames.step(16);
}

beforeEach(() => {
  document.body.replaceChildren();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('capo display on the chord lane', () => {
  it('shows the sounding symbols and data-capo 0 without a capo', () => {
    const rig = lane();
    expect(shapes(laneItems(rig))).toEqual(['Eb', 'Cm', 'Ab', 'Bb']);
    expect(soundings(laneItems(rig))).toEqual([null, null, null, null]);
    expect(rig.host.dataset.capo).toBe('0');
  });

  it('shows the shapes of the capo and data-capo on the next frame', () => {
    const rig = lane();
    rig.display.set({ capo: 3 });
    redraw(rig);
    expect(shapes(laneItems(rig))).toEqual(['C', 'Am', 'F', 'G']);
    expect(rig.host.dataset.capo).toBe('3');
  });

  it('adds a .chord-sounding child with the sounding symbol when showSounding is set', () => {
    const rig = lane();
    rig.display.set({ capo: 3, showSounding: true });
    redraw(rig);
    expect(shapes(laneItems(rig))).toEqual(['C', 'Am', 'F', 'G']);
    expect(soundings(laneItems(rig))).toEqual(['Eb', 'Cm', 'Ab', 'Bb']);
  });

  it('removes the .chord-sounding children when showSounding is cleared', () => {
    const rig = lane();
    rig.display.set({ capo: 3, showSounding: true });
    redraw(rig);
    rig.display.set({ capo: 3 });
    redraw(rig);
    expect(soundings(laneItems(rig))).toEqual([null, null, null, null]);
    expect(laneItems(rig).map((i) => i.textContent)).toEqual(['C', 'Am', 'F', 'G']);
  });

  it('gives items that enter the window later the sounding child too', () => {
    const rig = lane();
    rig.display.set({ capo: 3, showSounding: true });
    showAt(rig, 25_000);
    const items = [...rig.host.querySelectorAll<HTMLElement>('.chord-lane-item')];
    expect(items.length).toBeGreaterThan(0);
    expect(soundings(items).every((s) => s === 'N.C.')).toBe(true);
  });

  it('moves shapes and sounding symbols together with the 012 transpose', () => {
    const rig = lane();
    rig.display.set({ capo: 3, showSounding: true });
    rig.display.setTranspose(2);
    redraw(rig);
    expect(shapes(laneItems(rig))).toEqual(['D', 'Bm', 'G', 'A']);
    expect(soundings(laneItems(rig))).toEqual(['F', 'Dm', 'Bb', 'C']);
  });

  it('keeps a steady playback frame at two writes with sounding symbols shown', async () => {
    const rig = lane();
    rig.display.set({ capo: 3, showSounding: true });
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    frameAt(rig, 500);
    const before = rig.lane.stats.writes;
    frameAt(rig, 516);
    expect(rig.lane.stats.writes - before).toBe(2);
  });

  it('removes data-capo on detach', () => {
    const rig = lane();
    rig.lane.detach();
    expect(rig.host.hasAttribute('data-capo')).toBe(false);
  });
});

describe('capo range', () => {
  it.each([-1, 13, 2.5, Number.NaN])('rejects capo %s and keeps the display', (capo) => {
    const rig = lane();
    rig.display.set({ capo: 2, showSounding: true });
    redraw(rig);
    expect(() => {
      rig.display.set({ capo });
    }).toThrow(`Capo out of range: ${String(capo)}`);
    redraw(rig);
    expect(rig.host.dataset.capo).toBe('2');
    expect(rig.display.showSounding).toBe(true);
  });
});

describe('capo display in the karaoke banner', () => {
  let display: ChordDisplayState;

  function banner(): KaraokeRig {
    const rig = attachedBanner();
    rig.karaoke.setChords(bannerChords(CHART, display));
    rig.frames.step(1);
    return rig;
  }

  function bannerChordsShown(rig: KaraokeRig): HTMLElement[] {
    return [...rig.host.querySelectorAll<HTMLElement>('.karaoke-chord')];
  }

  /** Sets the capo with sounding symbols and draws frame `frame`. */
  function showCapo(rig: KaraokeRig, capo: number, frame: number): void {
    display.set({ capo, showSounding: true });
    rig.karaoke.redraw();
    rig.frames.step(frame);
  }

  function firstSounding(rig: KaraokeRig): Element {
    const element = rig.host.querySelector('.karaoke-chord > .chord-sounding');
    if (element === null) {
      throw new Error('no sounding child');
    }
    return element;
  }

  beforeEach(() => {
    stubLayout();
    stubResizeObserver();
    display = new ChordDisplayState();
  });

  it('shows the capo shapes with .chord-sounding children', () => {
    const rig = banner();
    const sounding = shapes(bannerChordsShown(rig));
    showCapo(rig, 3, 2);
    const shown = bannerChordsShown(rig);
    expect(shown.length).toBeGreaterThan(0);
    expect(soundings(shown)).toEqual(sounding);
    expect(shapes(shown)).not.toEqual(sounding);
  });

  it('keeps the .chord-sounding children across frames and replaces them only when a symbol changes', () => {
    const rig = banner();
    showCapo(rig, 3, 2);
    const first = firstSounding(rig);
    rig.karaoke.redraw();
    rig.frames.step(3);
    expect(firstSounding(rig)).toBe(first);
    showCapo(rig, 5, 4);
    const after = firstSounding(rig);
    expect(after).not.toBe(first);
    expect(after.textContent).toBe(first.textContent);
  });

  it('drops the .chord-sounding children when showSounding is cleared', () => {
    const rig = banner();
    showCapo(rig, 3, 2);
    display.set({ capo: 3 });
    rig.karaoke.redraw();
    rig.frames.step(3);
    expect(soundings(bannerChordsShown(rig)).every((s) => s === null)).toBe(true);
  });
});

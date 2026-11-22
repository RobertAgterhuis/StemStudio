import { beforeEach, describe, expect, it } from 'vitest';
import { ChordDisplayState } from '../../src/chords/display';
import { ChordLane } from '../../src/chords/lane';
import type { ChordChartData, ChordSymbolEvent } from '../../src/types';
import { GRID, addHost, frameAt, makeViewRig, showAt, type ViewRig } from '../mocks/views-rig';

/** FR-071 / FR-073: the chord lane host shows the key of the region at the position, at the current transpose. */

interface KeyRig extends ViewRig {
  readonly lane: ChordLane;
  readonly host: HTMLElement;
  readonly display: ChordDisplayState;
}

/** Three F#/Gb major chords of 20 s each. */
function segments(): ChordSymbolEvent[] {
  return [0, 20_000, 40_000].map((startMs) => ({ startMs, endMs: startMs + 20_000, root: 6, quality: 'maj', bass: null }));
}

/** D major 0-20 s, D-flat major 20-40 s, global A minor after. */
function regionChart(): ChordChartData {
  return {
    segments: segments(),
    keyTonic: 9,
    keyMode: 'minor',
    keyRegions: [
      { startMs: 0, endMs: 20_000, tonic: 2, mode: 'major' },
      { startMs: 20_000, endMs: 40_000, tonic: 1, mode: 'major' },
    ],
  };
}

function attached(chart: ChordChartData = regionChart()): KeyRig {
  const rig = makeViewRig();
  const host = addHost('chord-lane');
  const display = new ChordDisplayState();
  const lane = new ChordLane({ loop: rig.loop, clock: rig.clock, playing: rig.playing, grid: () => GRID, display, document });
  rig.transport.addListener(lane);
  lane.attach('chord-lane', chart, { reducedMotion: false });
  rig.frames.step(0);
  return Object.assign(rig, { lane, host, display });
}

function texts(rig: KeyRig): string[] {
  return [...rig.host.querySelectorAll<HTMLElement>('.chord-lane-item')].map((i) => String(i.textContent));
}

beforeEach(() => {
  document.body.replaceChildren();
});

describe('chord lane data-current-key', () => {
  it('shows the key of the region at the position', () => {
    const rig = attached();
    expect(rig.host.dataset.currentKey).toBe('D major');
    showAt(rig, 25_000);
    expect(rig.host.dataset.currentKey).toBe('Db major');
  });

  it('falls back to the global key outside the regions', () => {
    const rig = attached();
    showAt(rig, 45_000);
    expect(rig.host.dataset.currentKey).toBe('A minor');
  });

  it('uses the global key everywhere without regions', () => {
    const rig = attached({ segments: segments(), keyTonic: 7, keyMode: 'major' });
    showAt(rig, 30_000);
    expect(rig.host.dataset.currentKey).toBe('G major');
  });

  it('is empty when the chart has no key at all', () => {
    const rig = attached({ segments: segments(), keyTonic: null, keyMode: null });
    expect(rig.host.dataset.currentKey).toBe('');
  });

  it('follows the position while playing', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    frameAt(rig, 19_000);
    expect(rig.host.dataset.currentKey).toBe('D major');
    frameAt(rig, 20_000);
    expect(rig.host.dataset.currentKey).toBe('Db major');
  });

  it('follows the 012 transpose on the next frame, with the original key', () => {
    const rig = attached();
    rig.display.setTranspose(2);
    rig.frames.step(16);
    expect(rig.host.dataset.currentKey).toBe('E major (original D major, +2)');
    rig.display.setTranspose(-1);
    rig.frames.step(32);
    expect(rig.host.dataset.currentKey).toBe('Db major (original D major, -1)');
  });

  it('is not rewritten while the key stays the same', async () => {
    const rig = attached();
    rig.ctx.currentTime = 0;
    await rig.transport.play();
    frameAt(rig, 1000);
    const before = rig.lane.stats.writes;
    frameAt(rig, 1100);
    // Only the track transform and the position change on a steady frame.
    expect(rig.lane.stats.writes - before).toBe(2);
    expect(rig.host.dataset.currentKey).toBe('D major');
  });

  it('is removed on detach', () => {
    const rig = attached();
    rig.lane.detach();
    expect(rig.host.hasAttribute('data-current-key')).toBe(false);
  });
});

describe('chord lane spelling per region key', () => {
  it('spells sharps in D major and flats in D-flat major', () => {
    const rig = attached();
    showAt(rig, 19_000);
    expect(texts(rig).slice(0, 2)).toEqual(['F#', 'Gb']);
  });
});

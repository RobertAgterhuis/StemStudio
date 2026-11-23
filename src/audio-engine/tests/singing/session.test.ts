import { afterEach, describe, expect, it, vi } from 'vitest';
import { LiveSession } from '../../src/singing/session';
import type { SingingUnitScore } from '../../src/types';
import { FakeContext2D, stubCanvas } from '../mocks/canvas';
import { FakeResizeObserver } from '../mocks/layout';
import { addHost, makeViewRig, type ViewRig } from '../mocks/views-rig';

function session(rig: ViewRig, scores: SingingUnitScore[]): { live: LiveSession; host: HTMLElement } {
  stubCanvas();
  const host = addHost('pitch-view');
  const request = { pitchViewElementId: 'pitch-view', reference: { hopMs: 10 as const, frames: [] }, units: [{ index: 0, startMs: 0, endMs: 1000 }], toleranceCents: 100, reducedMotion: false };
  const live = new LiveSession(
    { request, reference: new Int16Array(200).fill(6900), host, latency: { ms: 20, source: 'manual' }, onUnit: (s) => scores.push(s) },
    { loop: rig.loop, clock: rig.clock, playing: rig.playing, transpose: () => 0 },
  );
  rig.transport.addListener(live);
  return { live, host };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('LiveSession (scorer + pitch view on the frame loop)', () => {
  it('draws on attach, after state, timing and resize changes, and shows the last score', () => {
    const rig = makeViewRig();
    document.body.replaceChildren();
    const scores: SingingUnitScore[] = [];
    const { live, host } = session(rig, scores);
    rig.frames.step(0);
    expect(FakeContext2D.latest().ops('clearRect')).toHaveLength(1);
    live.setState('running');
    rig.frames.step(1);
    live.onTimingChanged();
    rig.frames.step(2);
    FakeResizeObserver.latest().trigger();
    rig.frames.step(3);
    expect(FakeContext2D.latest().ops('clearRect')).toHaveLength(4);
    for (let ms = 0; ms <= 1000; ms += 10) {
      live.frame(ms, 6900);
    }
    rig.frames.step(4);
    expect(scores).toEqual([{ index: 0, score: 100, pass: 1 }]);
    expect(host.dataset.lastScore).toBe('100');
    expect(host.dataset.latencySource).toBe('manual');
  });

  it('stop keeps the canvas but ends the drawing; dispose removes it', () => {
    const rig = makeViewRig();
    document.body.replaceChildren();
    const { live, host } = session(rig, []);
    rig.frames.step(0);
    live.stop();
    live.onReposition();
    expect(rig.frames.size).toBe(0);
    expect(host.querySelector('canvas')).not.toBeNull();
    live.dispose();
    expect(host.children).toHaveLength(0);
  });
});

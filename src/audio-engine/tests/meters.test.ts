import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CLIP_HOLD_MS, DECAY_DB_PER_SECOND, METER_FLOOR_DB, Meters, browserFrames, peakOf, peakToDb, type FrameScheduler } from '../src/meters';
import type { MeterTarget } from '../src/types';
import { MockAudioContext, type MockAnalyserNode } from './mocks/audio-context';

/** Manually stepped requestAnimationFrame. */
class FakeFrames implements FrameScheduler {
  private readonly pending = new Map<number, FrameRequestCallback>();
  private next = 1;

  public request(callback: FrameRequestCallback): number {
    const id = this.next++;
    this.pending.set(id, callback);
    return id;
  }

  public cancel(handle: number): void {
    this.pending.delete(handle);
  }

  public get size(): number {
    return this.pending.size;
  }

  public step(timestamp: number): void {
    const callbacks = [...this.pending.values()];
    this.pending.clear();
    callbacks.forEach((cb) => {
      cb(timestamp);
    });
  }
}

interface Rig {
  readonly frames: FakeFrames;
  readonly meters: Meters;
  readonly analysers: Map<MeterTarget, MockAnalyserNode>;
  readonly element: HTMLElement;
}

function makeRig(): Rig {
  const ctx = new MockAudioContext();
  const analysers = new Map<MeterTarget, MockAnalyserNode>([
    ['vocals', ctx.createAnalyser()],
    ['master', ctx.createAnalyser()],
  ]);
  const frames = new FakeFrames();
  const meters = new Meters({
    source: (target) => analysers.get(target) as unknown as AnalyserNode | undefined,
    frames,
    document,
  });
  const element = document.createElement('div');
  element.id = 'meter-vocals';
  document.body.replaceChildren(element);
  return { frames, meters, analysers, element };
}

function setPeak(rig: Rig, target: MeterTarget, peak: number): void {
  const analyser = rig.analysers.get(target);
  if (analyser !== undefined) {
    analyser.samples = [0, -peak / 2, peak, 0];
  }
}

function level(element: HTMLElement): number {
  return Number(element.style.getPropertyValue('--meter-level'));
}

describe('peak math', () => {
  it('peakOf returns the largest absolute sample', () => {
    expect(peakOf(new Float32Array([0.1, -0.8, 0.5]))).toBeCloseTo(0.8, 6);
    expect(peakOf(new Float32Array(0))).toBe(0);
  });

  it('peakToDb is 20*log10(p) clamped to -60..0', () => {
    expect(peakToDb(1)).toBe(0);
    expect(peakToDb(2)).toBe(0);
    expect(peakToDb(0.5)).toBeCloseTo(-6.0206, 3);
    expect(peakToDb(0)).toBe(METER_FLOOR_DB);
    expect(peakToDb(0.0001)).toBe(METER_FLOOR_DB);
  });
});

describe('Meters', () => {
  beforeEach(() => {
    document.body.replaceChildren();
  });

  it('writes --meter-level, data-peak-db and data-clip on every frame', () => {
    const rig = makeRig();
    rig.meters.attach('vocals', 'meter-vocals');
    setPeak(rig, 'vocals', 0.5);
    rig.frames.step(16);
    expect(rig.element.dataset.peakDb).toBe('-6');
    expect(level(rig.element)).toBeCloseTo((-6.0206 + 60) / 60, 3);
    expect(rig.element.dataset.clip).toBe('false');
  });

  it('silence reads -60 dBFS and level 0', () => {
    const rig = makeRig();
    rig.meters.attach('vocals', 'meter-vocals');
    rig.frames.step(16);
    expect(rig.element.dataset.peakDb).toBe('-60');
    expect(level(rig.element)).toBe(0);
  });

  it('falls back at about 20 dB/s after a peak', () => {
    const rig = makeRig();
    rig.meters.attach('vocals', 'meter-vocals');
    setPeak(rig, 'vocals', 1 - 1e-6);
    rig.frames.step(1000);
    setPeak(rig, 'vocals', 0);
    rig.frames.step(1500);
    expect(Number(rig.element.dataset.peakDb)).toBe(-DECAY_DB_PER_SECOND * 0.5);
    rig.frames.step(10_000);
    expect(rig.element.dataset.peakDb).toBe('-60');
  });

  it('a louder frame raises the level immediately', () => {
    const rig = makeRig();
    rig.meters.attach('vocals', 'meter-vocals');
    setPeak(rig, 'vocals', 0.01);
    rig.frames.step(16);
    setPeak(rig, 'vocals', 0.5);
    rig.frames.step(32);
    expect(rig.element.dataset.peakDb).toBe('-6');
  });

  it('data-clip is "true" for 1.5 s after a sample with |x| >= 1.0', () => {
    const rig = makeRig();
    rig.meters.attach('vocals', 'meter-vocals');
    setPeak(rig, 'vocals', 1);
    rig.frames.step(100);
    expect(rig.element.dataset.clip).toBe('true');
    setPeak(rig, 'vocals', 0.1);
    rig.frames.step(100 + CLIP_HOLD_MS - 1);
    expect(rig.element.dataset.clip).toBe('true');
    rig.frames.step(100 + CLIP_HOLD_MS);
    expect(rig.element.dataset.clip).toBe('false');
  });

  it('runs the frame loop only while meters are attached', () => {
    const rig = makeRig();
    expect(rig.frames.size).toBe(0);
    rig.meters.attach('vocals', 'meter-vocals');
    rig.meters.attach('master', 'meter-master');
    expect(rig.frames.size).toBe(1);
    rig.frames.step(16);
    expect(rig.frames.size).toBe(1);
    rig.meters.detach('vocals');
    expect(rig.frames.size).toBe(1);
    rig.meters.detach('master');
    expect(rig.frames.size).toBe(0);
    rig.meters.detach('master');
    expect(rig.frames.size).toBe(0);
  });

  it('dispose stops the frame loop and forgets the meters', () => {
    const rig = makeRig();
    rig.meters.attach('vocals', 'meter-vocals');
    rig.meters.dispose();
    expect(rig.frames.size).toBe(0);
    rig.frames.step(16);
    expect(rig.element.dataset.peakDb).toBeUndefined();
  });

  it('rejects targets without an analyser', () => {
    const rig = makeRig();
    expect(() => {
      rig.meters.attach('drums', 'meter-drums');
    }).toThrow('Unknown meter target "drums"');
  });

  it('skips missing elements and targets whose analyser disappeared, then picks up re-rendered elements', () => {
    const rig = makeRig();
    rig.meters.attach('master', 'meter-master');
    rig.meters.attach('vocals', 'meter-vocals');
    setPeak(rig, 'master', 0.5);
    rig.frames.step(16);
    const master = document.createElement('div');
    master.id = 'meter-master';
    document.body.append(master);
    rig.analysers.delete('vocals');
    rig.frames.step(32);
    expect(master.dataset.peakDb).toBe('-6');
    expect(rig.element.dataset.peakDb).toBe('-60');
  });

  it('re-attaching a target moves it to the new element', () => {
    const rig = makeRig();
    const other = document.createElement('div');
    other.id = 'other';
    document.body.append(other);
    rig.meters.attach('vocals', 'meter-vocals');
    rig.meters.attach('vocals', 'other');
    setPeak(rig, 'vocals', 0.5);
    rig.frames.step(16);
    expect(other.dataset.peakDb).toBe('-6');
    expect(rig.element.dataset.peakDb).toBeUndefined();
  });
});

describe('browserFrames', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('delegates to requestAnimationFrame / cancelAnimationFrame', () => {
    const raf = vi.fn(() => 7);
    const caf = vi.fn();
    vi.stubGlobal('requestAnimationFrame', raf);
    vi.stubGlobal('cancelAnimationFrame', caf);
    const callback = vi.fn();
    expect(browserFrames.request(callback)).toBe(7);
    browserFrames.cancel(7);
    expect(raf).toHaveBeenCalledWith(callback);
    expect(caf).toHaveBeenCalledWith(7);
  });
});

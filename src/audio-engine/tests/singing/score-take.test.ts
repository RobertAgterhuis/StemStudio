import { afterEach, describe, expect, it, vi } from 'vitest';
import { scoreTake } from '../../src/singing/take-score';
import type { TakeScoreRequest } from '../../src/types';
import { MockAudioBuffer, MockAudioContext } from '../mocks/audio-context';
import { FetchStub } from '../mocks/fetch';
import { loadVocals, makeEngineRig } from '../mocks/engine-rig';
import { tone } from '../pitch/signals';

/** A4 (6900) reference over 4 s; four 1 s units. */
const REQUEST: TakeScoreRequest = {
  takeUrl: '/api/takes/t1/audio',
  startSample: 44_100,
  reference: { hopMs: 10, frames: new Array<number>(400).fill(6900) },
  units: [0, 1, 2, 3].map((i) => ({ index: i, startMs: i * 1000, endMs: (i + 1) * 1000 })),
  toleranceCents: 50,
};

/** A context whose decoded take holds `samples` (mono, 44.1 kHz). */
function contextWith(samples: Float32Array): MockAudioContext {
  const ctx = new MockAudioContext();
  vi.spyOn(ctx, 'decodeAudioData').mockImplementation(() => {
    const buffer = new MockAudioBuffer(1, samples.length, 44_100);
    buffer.getChannelData(0).set(samples);
    return Promise.resolve(buffer);
  });
  return ctx;
}

function deps(ctx: MockAudioContext): { context: AudioContext; fetch: typeof fetch } {
  return { context: ctx.asAudioContext(), fetch: new FetchStub().reply(REQUEST.takeUrl, { bytes: 64 }).fetch };
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('scoreTake (FR-046)', () => {
  it('scores every unit the take covers with the live core; the take starts at startSample', async () => {
    const result = await scoreTake(REQUEST, deps(contextWith(tone(44_100, 2, [[440, 0.4]]))));
    expect(result.units).toEqual([
      { index: 1, score: 100, pass: 1 },
      { index: 2, score: 100, pass: 1 },
    ]);
    expect(result).toMatchObject({ unitsScored: 2, average: 100, best: { index: 1, score: 100 }, toleranceCents: 50, latencySource: 'estimated' });
  });

  it('a take off by more than the tolerance scores 0', async () => {
    const result = await scoreTake(REQUEST, deps(contextWith(tone(44_100, 2, [[466.16, 0.4]]))));
    expect(result.units.map((u) => u.score)).toEqual([0, 0]);
  });

  it('applies no latency (015 takes are already compensated)', async () => {
    const reference = Array.from({ length: 400 }, (_, i) => (i >= 101 && i < 150 ? 6900 : 0));
    const samples = new Float32Array(44_100);
    samples.set(tone(44_100, 0.5, [[440, 0.4]]), 22_050);
    const result = await scoreTake({ ...REQUEST, startSample: 22_050, reference: { hopMs: 10, frames: reference } }, deps(contextWith(samples)));
    expect(result.units).toEqual([{ index: 1, score: 100, pass: 1 }]);
  });

  it('averages the channels of a stereo take', async () => {
    const ctx = new MockAudioContext();
    vi.spyOn(ctx, 'decodeAudioData').mockImplementation(() => {
      const buffer = new MockAudioBuffer(2, 44_100 * 2, 44_100);
      buffer.getChannelData(0).set(tone(44_100, 2, [[440, 0.4]]));
      return Promise.resolve(buffer);
    });
    const result = await scoreTake(REQUEST, deps(ctx));
    expect(result.units.map((u) => u.score)).toEqual([100, 100]);
  });

  it('rejects with the loader message when the take cannot be fetched', async () => {
    const ctx = new MockAudioContext();
    await expect(scoreTake(REQUEST, { context: ctx.asAudioContext(), fetch: new FetchStub().fetch })).rejects.toThrow(/take/);
  });

  it.each([
    ['startSample', { ...REQUEST, startSample: -1 }, 'Invalid singing request: startSample'],
    ['fractional startSample', { ...REQUEST, startSample: 1.5 }, 'Invalid singing request: startSample'],
    ['takeUrl', { ...REQUEST, takeUrl: '' }, 'Invalid singing request: takeUrl'],
    ['tolerance', { ...REQUEST, toleranceCents: 300 }, 'Invalid singing request: tolerance outside 10..200'],
    ['contour', { ...REQUEST, reference: { hopMs: 10, frames: [13_000] } }, 'Invalid pitch contour: values outside 0..12700'],
  ])('rejects an invalid %s before fetching', async (_, request, message) => {
    const stub = new FetchStub();
    await expect(scoreTake(request as TakeScoreRequest, { context: new MockAudioContext().asAudioContext(), fetch: stub.fetch })).rejects.toThrow(message);
    expect(stub.requested).toEqual([]);
  });
});

describe('engine scoreTake', () => {
  it('fetches the take with the engine fetch and needs no worklet', async () => {
    const rig = makeEngineRig();
    rig.stub.reply(REQUEST.takeUrl, { bytes: 64 });
    vi.spyOn(rig.ctx, 'decodeAudioData').mockImplementation((data: ArrayBuffer) => {
      const buffer = new MockAudioBuffer(1, data.byteLength === 64 ? 88_200 : 44_100 * 60, 44_100);
      if (data.byteLength === 64) {
        buffer.getChannelData(0).set(tone(44_100, 2, [[440, 0.4]]));
      }
      return Promise.resolve(buffer);
    });
    await loadVocals(rig);
    const result = await rig.engine.scoreTake(REQUEST);
    expect(result.unitsScored).toBe(2);
    expect(rig.stub.requested).toContain(REQUEST.takeUrl);
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEngine, type AudioEngine } from '../../src/index';
import { TAKE_LEAD_SECONDS } from '../../src/recording/take-channel';
import type { MockAudioBuffer, MockAudioBufferSourceNode } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { RecordingContext } from '../mocks/media';

/** Timing adjustment of the loaded take (015 US7, FR-016, data-model 2.4). */
const SR = 44100;
const LENGTH = 1000;

interface Rig {
  readonly ctx: RecordingContext;
  readonly engine: AudioEngine;
}

/** A track plus a saved take of {@link LENGTH} samples at song sample `startSample`; take sample i = (i + 1) / 2^23. */
async function makeRig(startSample: number): Promise<Rig> {
  const ctx = new RecordingContext();
  ctx.decodeDurations.set(100, 20);
  const original = ctx.decodeAudioData.bind(ctx);
  ctx.decodeAudioData = async (data: ArrayBuffer): Promise<MockAudioBuffer> => {
    const buffer = await original(data);
    if (data.byteLength === 200) {
      buffer.getChannelData(0).forEach((_, i, channel) => {
        channel[i] = (i - startSample + 1) / 2 ** 23;
      });
    }
    return buffer;
  };
  const stub = new FetchStub().reply('/vocals', { bytes: 100 }).reply('/take', { bytes: 200 });
  const engine = createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), fetch: stub.fetch });
  await engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
  await engine.loadTake({ takeId: 'take-1', source: { url: '/take', startSample, lengthSamples: LENGTH, channels: 1 }, part: 'vocals' });
  return { ctx, engine };
}

function takeSources(rig: Rig): MockAudioBufferSourceNode[] {
  return rig.ctx.sources.filter((s) => s.buffer?.length === LENGTH);
}

/** First start call and stop count of a source (NaN / 0 when missing). */
function startOf(source: MockAudioBufferSourceNode | undefined): { when: number; offset: number; stops: number } {
  const none = { startCalls: [{ when: Number.NaN, offset: Number.NaN }], stopCalls: [] };
  const { startCalls, stopCalls } = source ?? none;
  const [call = { when: Number.NaN, offset: Number.NaN }] = startCalls;
  return { when: call.when, offset: call.offset ?? Number.NaN, stops: stopCalls.length };
}

/** Data chunk of a WAV as 24-bit integers. */
async function samples(blob: Blob): Promise<number[]> {
  const view = new DataView(await blob.arrayBuffer());
  const count = view.getUint32(40, true) / 3;
  return Array.from({ length: count }, (_, i) => view.getUint16(44 + 3 * i, true) | (view.getInt8(46 + 3 * i) << 16));
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('requestAnimationFrame', () => 0);
  vi.stubGlobal('cancelAnimationFrame', () => undefined);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('setTakeAdjustment', () => {
  it('re-schedules the loaded take at currentTime + 0.01 with the shift (positive = later)', async () => {
    const rig = await makeRig(SR);
    await rig.engine.play();
    expect(takeSources(rig)[0]?.startCalls[0]?.when).toBeCloseTo(0.1 + 1, 9);
    rig.ctx.currentTime = 0.5;
    rig.engine.setTakeAdjustment(441);
    const [first, second] = takeSources(rig).map(startOf);
    expect(first?.stops).toBe(1);
    expect(second?.when).toBeCloseTo(0.1 + 1 + 441 / SR, 9);
    expect(TAKE_LEAD_SECONDS).toBe(0.01);
  });

  it('splices a running take at currentTime + 0.01', async () => {
    const rig = await makeRig(0);
    await rig.engine.play();
    rig.ctx.currentTime = 0.105;
    rig.engine.setTakeAdjustment(-100);
    const [first, second] = takeSources(rig);
    expect(first?.stopCalls).toEqual([0.105 + 0.01]);
    const start = startOf(second);
    expect([start.when, start.offset].map((x) => Math.round(x * 1e9) / 1e9)).toEqual([0.115, Math.round((0.015 + 100 / SR) * 1e9) / 1e9]);
  });

  it('validates the adjustment (-8820..8820 integer samples); without a take it is a no-op', async () => {
    const rig = await makeRig(0);
    expect(() => {
      rig.engine.setTakeAdjustment(8821);
    }).toThrow('Invalid take adjustment: 8821');
    expect(() => {
      rig.engine.setTakeAdjustment(1.5);
    }).toThrow('Invalid take adjustment');
    rig.engine.unloadTake();
    expect(() => {
      rig.engine.setTakeAdjustment(10);
    }).not.toThrow();
  });
});

describe('re-encoding the loaded take (captureWav of the loaded take id)', () => {
  it('a positive shift keeps every sample (the new start is later)', async () => {
    const rig = await makeRig(500);
    rig.engine.setTakeAdjustment(441);
    const data = await samples(rig.engine.captureWav('take-1'));
    expect([data.length, data[0], data.at(-1)]).toEqual([LENGTH, 1, LENGTH]);
  });

  it('a negative shift before the song start is clamped at 0: the leading samples are dropped', async () => {
    const rig = await makeRig(100);
    rig.engine.setTakeAdjustment(-300);
    const data = await samples(rig.engine.captureWav('take-1'));
    expect([data.length, data[0]]).toEqual([LENGTH - 200, 201]);
  });

  it('loadTake with an adjustment uses it for playback and the re-encoding', async () => {
    const rig = await makeRig(0);
    await rig.engine.loadTake({ takeId: 'take-2', source: { url: '/take', startSample: 0, lengthSamples: LENGTH, channels: 1 }, part: 'none', adjustmentSamples: -50 });
    expect(await samples(rig.engine.captureWav('take-2'))).toHaveLength(LENGTH - 50);
    expect(() => rig.engine.captureWav('take-1')).toThrow('Unknown capture "take-1"');
  });
});

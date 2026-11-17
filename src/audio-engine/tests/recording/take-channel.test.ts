import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TakeChannel, planTake, sliceTake } from '../../src/recording/take-channel';
import type { MockAudioBuffer, MockAudioBufferSourceNode, MockGainNode } from '../mocks/audio-context';
import { makeTransport, type TransportRig } from '../mocks/transport-rig';

/** The take channel's playback (research R-09, contract 2.5.0, FR-009, FR-012). */
const SR = 44100;

interface Rig extends TransportRig {
  readonly take: TakeChannel;
  readonly output: MockGainNode;
}

function makeRig(): Rig {
  const base = makeTransport();
  const output = base.ctx.createGain();
  const take = new TakeChannel({ context: base.ctx.asAudioContext(), transport: base.transport, output: () => output as unknown as AudioNode });
  base.transport.addListener(take);
  return { ...base, take, output };
}

/** A take buffer of `seconds` (mono). */
function buffer(rig: Rig, seconds: number, channels = 1): AudioBuffer {
  return rig.ctx.createBuffer(channels, Math.round(seconds * SR), SR) as unknown as AudioBuffer;
}

/** Take sources (connected to the take output), in creation order. */
function takeSources(rig: Rig): MockAudioBufferSourceNode[] {
  return rig.ctx.sources.filter((s) => s.connections.includes(rig.output));
}

function starts(rig: Rig): number[][] {
  return takeSources(rig).map((s) => {
    const { when, offset = Number.NaN, duration = Number.NaN } = s.startCalls[0] ?? { when: Number.NaN };
    return [when, offset, duration].map((x) => Math.round(x * 1e6) / 1e6);
  });
}

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('planTake', () => {
  const take = { origin: 2, duration: 1 };

  it('cuts every heard segment to the take span', () => {
    expect(planTake([{ trackFrom: 0, trackTo: 10, contextStart: 5, rate: 1 }], take)).toEqual([{ heardStart: 7, bufferOffset: 0, duration: 1, rate: 1 }]);
    expect(planTake([{ trackFrom: 2.5, trackTo: 10, contextStart: 5, rate: 0.5 }], take)).toEqual([{ heardStart: 5, bufferOffset: 0.5, duration: 0.5, rate: 0.5 }]);
  });

  it('plans nothing for segments outside the take', () => {
    expect(planTake([{ trackFrom: 3, trackTo: 10, contextStart: 0, rate: 1 }, { trackFrom: 0, trackTo: 2, contextStart: 0, rate: 1 }], take)).toEqual([]);
  });

  it('plans one chunk per loop cycle', () => {
    const cycles = [0, 1, 2].map((k) => ({ trackFrom: 1, trackTo: 3, contextStart: 0.1 + 2 * k, rate: 1 }));
    expect(planTake(cycles, take).map((c) => c.heardStart)).toEqual([1.1, 3.1, 5.1]);
  });
});

describe('sliceTake', () => {
  it('copies [start, start + length) and reduces a stored stereo copy of a mono take to one channel', () => {
    const rig = makeRig();
    const full = rig.ctx.createBuffer(2, 100, SR);
    full.getChannelData(0).forEach((_, i, data) => {
      data[i] = i;
    });
    full.getChannelData(1).fill(-1);
    const mono = sliceTake(rig.ctx.asAudioContext(), full as unknown as AudioBuffer, { startSample: 10, lengthSamples: 20, channels: 1 }) as unknown as MockAudioBuffer;
    const stereo = sliceTake(rig.ctx.asAudioContext(), full as unknown as AudioBuffer, { startSample: 90, lengthSamples: 20, channels: 2 }) as unknown as MockAudioBuffer;
    expect([mono.numberOfChannels, mono.length, mono.getChannelData(0)[0], mono.getChannelData(0)[19]]).toEqual([1, 20, 10, 29]);
    expect([stereo.numberOfChannels, stereo.getChannelData(0)[9], stereo.getChannelData(0)[10], stereo.getChannelData(1)[0]]).toEqual([2, 99, 0, -1]);
  });

  it('duplicates a mono file when a stereo take is requested', () => {
    const rig = makeRig();
    const full = rig.ctx.createBuffer(1, 10, SR);
    full.getChannelData(0).fill(0.5);
    const out = sliceTake(rig.ctx.asAudioContext(), full as unknown as AudioBuffer, { startSample: 0, lengthSamples: 10, channels: 2 }) as unknown as MockAudioBuffer;
    expect(out.getChannelData(1)[3]).toBe(0.5);
  });
});

describe('TakeChannel', () => {
  it('starts the take sample-locked at its song time when playing from 0', async () => {
    const rig = makeRig();
    rig.take.load(buffer(rig, 1), 2 * SR);
    await rig.transport.play();
    expect(starts(rig)).toEqual([[2.1, 0, 1]]);
    expect(takeSources(rig)[0]?.loop).toBe(false);
  });

  it('a seek inside the take starts it at the right buffer offset; after the take nothing plays', async () => {
    const rig = makeRig();
    rig.take.load(buffer(rig, 2), 2 * SR);
    rig.transport.seek(3);
    await rig.transport.play();
    expect(starts(rig)).toEqual([[0.1, 1, 1]]);
    rig.ctx.currentTime = 0.5;
    rig.transport.seek(10);
    expect(takeSources(rig)[0]?.stopCalls.length).toBe(1);
    expect(takeSources(rig)).toHaveLength(1);
  });

  it('pause and stop end the take sources; play resumes from the position', async () => {
    const rig = makeRig();
    rig.take.load(buffer(rig, 4), 0);
    await rig.transport.play();
    rig.ctx.currentTime = 1.1;
    rig.transport.pause();
    expect(takeSources(rig)[0]?.stopCalls.length).toBe(1);
    rig.ctx.currentTime = 5;
    await rig.transport.play();
    expect(starts(rig).at(-1)).toEqual([5.1, 1, 3]);
    rig.transport.stop();
    expect(takeSources(rig).at(-1)?.stopCalls.length).toBe(1);
  });

  it('loading while playing schedules at once (from currentTime + 0.01); a new take replaces the old one', async () => {
    const rig = makeRig();
    await rig.transport.play();
    rig.ctx.currentTime = 1;
    rig.take.load(buffer(rig, 4), 0);
    expect(starts(rig)).toEqual([[1.01, 0.91, 3.09]]);
    rig.take.load(buffer(rig, 4), 0);
    expect(takeSources(rig)[0]?.stopCalls.length).toBe(1);
    expect(takeSources(rig)).toHaveLength(2);
  });

  it('replays the take in every loop pass (not looped itself) and extends the plan as passes go by', async () => {
    const rig = makeRig();
    rig.transport.setLoop({ start: 1, end: 1.5, enabled: true });
    rig.take.load(buffer(rig, 0.25), 1.25 * SR);
    await rig.transport.play();
    const planned = takeSources(rig).length;
    expect(starts(rig).slice(0, 2)).toEqual([
      [0.35, 0, 0.25],
      [0.85, 0, 0.25],
    ]);
    expect(planned).toBeGreaterThanOrEqual(10);
    rig.ctx.currentTime = 0.5 * (planned - 2);
    await vi.advanceTimersByTimeAsync(300);
    expect(takeSources(rig).length).toBeGreaterThan(planned);
  });

  it('plays through the tempo stage like the stems: rate and the heard latency', async () => {
    const rig = makeRig();
    rig.transport.setTiming({ rate: 0.5, latency: 0.05 });
    rig.take.load(buffer(rig, 1), 1 * SR);
    await rig.transport.play();
    const [source] = takeSources(rig);
    expect([source?.playbackRate.value, starts(rig)[0]]).toEqual([0.5, [0.15 + 2 - 0.05, 0, 1]]);
  });

  it('splices at a live rate change: the running source stops there and a new one continues', async () => {
    const rig = makeRig();
    rig.take.load(buffer(rig, 10), 0);
    await rig.transport.play();
    rig.ctx.currentTime = 1;
    rig.transport.retime({ at: 1.2, rate: 0.5, latency: 0, anchor: { when: 1.2, offset: 1.1, rate: 0.5 } });
    const [first, second] = takeSources(rig);
    expect(first?.stopCalls).toEqual([1.2]);
    expect([second?.playbackRate.value, starts(rig)[1]]).toEqual([0.5, [1.2, 1.1, 8.9]]);
  });

  it('a new pass launched ahead ends the running take source only where that pass starts', async () => {
    const rig = makeRig();
    rig.take.load(buffer(rig, 30), 0);
    await rig.transport.play();
    rig.ctx.currentTime = 1;
    rig.transport.launch({ heardWhen: 20, offset: 0, rate: 1, latency: 0, duration: 5 });
    expect(takeSources(rig)[0]?.stopCalls).toEqual([20]);
  });

  it('unload stops the sources and plays nothing afterwards', async () => {
    const rig = makeRig();
    rig.take.load(buffer(rig, 1), 0);
    rig.take.unload();
    rig.take.unload();
    await rig.transport.play();
    expect(takeSources(rig)).toEqual([]);
    expect(rig.take.loaded).toBe(false);
  });

  it('dispose stops everything', async () => {
    const rig = makeRig();
    rig.take.load(buffer(rig, 1), 0);
    await rig.transport.play();
    rig.take.dispose();
    expect(takeSources(rig)[0]?.stopCalls.length).toBe(1);
  });
});

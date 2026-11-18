import { describe, expect, it } from 'vitest';
import { KaraokeClock } from '../../src/karaoke/clock';
import { TimelineClock, audioNow, heardTime, outputLatency } from '../../src/timeline/clock';
import { MockAudioContext } from '../mocks/audio-context';
import { makeTransport, type TransportRig } from '../mocks/transport-rig';

interface ClockRig extends TransportRig {
  readonly clock: TimelineClock;
  readonly karaoke: KaraokeClock;
  readonly perf: { now: number };
}

async function playing(): Promise<ClockRig> {
  const rig = makeTransport();
  const perf = { now: 0 };
  const deps = { context: rig.ctx.asAudioContext(), transport: rig.transport, now: () => perf.now };
  await rig.transport.play();
  return { ...rig, clock: new TimelineClock(deps), karaoke: new KaraokeClock(deps), perf };
}

describe('audioNow / outputLatency / heardTime (moved from the karaoke clock)', () => {
  it('extrapolates getOutputTimestamp with performance.now()', () => {
    const ctx = new MockAudioContext();
    ctx.currentTime = 2.2;
    ctx.outputTimestamp = { contextTime: 2.09, performanceTime: 1000 };
    expect(audioNow(ctx.asAudioContext(), 1005)).toBeCloseTo(2.095, 9);
  });

  it('falls back to currentTime when getOutputTimestamp returns zeros, is missing or has undefined members', () => {
    const ctx = new MockAudioContext();
    ctx.currentTime = 3;
    expect(audioNow(ctx.asAudioContext(), 1234)).toBe(3);
    Object.defineProperty(ctx, 'getOutputTimestamp', { value: () => ({}), configurable: true });
    expect(audioNow(ctx.asAudioContext(), 1234)).toBe(3);
    Object.defineProperty(ctx, 'getOutputTimestamp', { value: undefined });
    expect(audioNow(ctx.asAudioContext(), 1234)).toBe(3);
  });

  it('uses outputLatency, else baseLatency, else 0, and subtracts it for the heard time', () => {
    const ctx = new MockAudioContext();
    ctx.currentTime = 1;
    expect(outputLatency(ctx.asAudioContext())).toBe(0);
    ctx.baseLatency = 0.005;
    expect(outputLatency(ctx.asAudioContext())).toBe(0.005);
    ctx.outputLatency = 0.03;
    expect(outputLatency(ctx.asAudioContext())).toBe(0.03);
    expect(heardTime(ctx.asAudioContext(), 0)).toBeCloseTo(0.97, 9);
  });
});

describe('TimelineClock (shared clock = karaoke clock without offset)', () => {
  it('equals the karaoke clock at offset 0 while playing, across the loop wrap and while paused', async () => {
    const rig = await playing();
    rig.ctx.outputLatency = 0.05;
    const samples: number[] = [];
    for (const t of [0.05, 1.1, 2.6, 4.13, 4.17]) {
      if (t === 4.13) {
        rig.transport.setLoop({ start: 2, end: 4, enabled: true });
      }
      rig.ctx.currentTime = t;
      expect(rig.clock.positionMs()).toBe(rig.karaoke.positionMs());
      samples.push(rig.clock.positionMs());
    }
    expect(samples).toEqual([0, 950, 2450, 3980, 2020]);
    rig.transport.pause();
    expect(rig.clock.positionMs()).toBe(rig.karaoke.positionMs());
  });

  it('applies the 012 rate through the transport position', async () => {
    const rig = makeTransport();
    rig.transport.setTiming({ rate: 0.5, latency: 0 });
    await rig.transport.play();
    const clock = new TimelineClock({ context: rig.ctx.asAudioContext(), transport: rig.transport, now: () => 0 });
    rig.ctx.currentTime = 2.1;
    expect(clock.positionMs()).toBe(1000);
  });

  it('maps an arbitrary context time to track ms (used by the singing alignment)', async () => {
    const rig = await playing();
    expect(rig.clock.positionAtMs(1.6)).toBe(1500);
    expect(rig.clock.positionAtMs(0.05)).toBe(0);
  });

  it('reads performance.now() by default', async () => {
    const rig = makeTransport();
    await rig.transport.play();
    const clock = new TimelineClock({ context: rig.ctx.asAudioContext(), transport: rig.transport });
    rig.ctx.outputTimestamp = { contextTime: 1.1, performanceTime: performance.now() };
    expect(Math.abs(clock.positionMs() - 1000)).toBeLessThanOrEqual(50);
  });
});

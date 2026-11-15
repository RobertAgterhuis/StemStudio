import { describe, expect, it, vi } from 'vitest';
import { KaraokeClock, MAX_OFFSET_MS, audioNow, outputLatency, validateOffset } from '../../src/karaoke/clock';
import { MockAudioContext } from '../mocks/audio-context';
import { makeTransport, type TransportRig } from '../mocks/transport-rig';

interface ClockRig extends TransportRig {
  readonly clock: KaraokeClock;
  readonly perf: { now: number };
}

function makeClock(): ClockRig {
  const rig = makeTransport();
  const perf = { now: 0 };
  const clock = new KaraokeClock({ context: rig.ctx.asAudioContext(), transport: rig.transport, now: () => perf.now });
  return { ...rig, clock, perf };
}

/** Plays from 0 with `when` = 0.1 s (context time 0 at the call). */
async function playing(): Promise<ClockRig> {
  const rig = makeClock();
  await rig.transport.play();
  return rig;
}

describe('audioNow', () => {
  it('extrapolates getOutputTimestamp with performance.now()', () => {
    const ctx = new MockAudioContext();
    ctx.currentTime = 2.2;
    ctx.outputTimestamp = { contextTime: 2.09, performanceTime: 1000 };
    expect(audioNow(ctx.asAudioContext(), 1005)).toBeCloseTo(2.095, 9);
  });

  it('falls back to currentTime when getOutputTimestamp returns zeros or is missing', () => {
    const ctx = new MockAudioContext();
    ctx.currentTime = 3;
    expect(audioNow(ctx.asAudioContext(), 1234)).toBe(3);
    Object.defineProperty(ctx, 'getOutputTimestamp', { value: undefined });
    ctx.outputTimestamp = { contextTime: 1, performanceTime: 1 };
    expect(audioNow(ctx.asAudioContext(), 1234)).toBe(3);
  });

  it('falls back to currentTime when the timestamp has undefined members', () => {
    const ctx = new MockAudioContext();
    ctx.currentTime = 4;
    Object.defineProperty(ctx, 'getOutputTimestamp', { value: () => ({}) });
    expect(audioNow(ctx.asAudioContext(), 99)).toBe(4);
  });
});

describe('outputLatency', () => {
  it('uses outputLatency, else baseLatency, else 0', () => {
    const ctx = new MockAudioContext();
    expect(outputLatency(ctx.asAudioContext())).toBe(0);
    ctx.baseLatency = 0.005;
    expect(outputLatency(ctx.asAudioContext())).toBe(0.005);
    ctx.outputLatency = 0.03;
    expect(outputLatency(ctx.asAudioContext())).toBe(0.03);
  });
});

describe('validateOffset', () => {
  it('accepts -5000..5000 ms and rejects everything else', () => {
    expect(MAX_OFFSET_MS).toBe(5000);
    expect(validateOffset(-5000)).toBe(-5000);
    expect(validateOffset(5000)).toBe(5000);
    expect(() => validateOffset(5001)).toThrow('Karaoke offset out of range: 5001');
    expect(() => validateOffset(-5000.5)).toThrow('Karaoke offset out of range: -5000.5');
    expect(() => validateOffset(Number.NaN)).toThrow('Karaoke offset out of range: NaN');
  });
});

describe('KaraokeClock', () => {
  it('is the transport position at the heard audio time, minus latency, minus the offset (ms)', async () => {
    const rig = await playing();
    rig.ctx.currentTime = 2.2;
    rig.ctx.outputTimestamp = { contextTime: 2.09, performanceTime: 1000 };
    rig.perf.now = 1005;
    rig.ctx.outputLatency = 0.02;
    expect(rig.clock.positionMs()).toBe(1975);
    rig.clock.setOffset(100);
    expect(rig.clock.offsetMs).toBe(100);
    expect(rig.clock.positionMs()).toBe(1875);
    rig.clock.setOffset(-250);
    expect(rig.clock.positionMs()).toBe(2225);
  });

  it('reads performance.now() by default', async () => {
    const rig = makeTransport();
    await rig.transport.play();
    const clock = new KaraokeClock({ context: rig.ctx.asAudioContext(), transport: rig.transport });
    vi.spyOn(performance, 'now').mockReturnValue(2000);
    rig.ctx.outputTimestamp = { contextTime: 1.1, performanceTime: 1500 };
    expect(clock.positionMs()).toBe(1500);
  });

  it('rejects an out-of-range offset and keeps the previous one', async () => {
    const rig = await playing();
    rig.clock.setOffset(40);
    expect(() => {
      rig.clock.setOffset(7000);
    }).toThrow('Karaoke offset out of range: 7000');
    expect(rig.clock.offsetMs).toBe(40);
  });

  it('uses currentTime when no output timestamp is available yet', async () => {
    const rig = await playing();
    rig.ctx.currentTime = 1.1;
    expect(rig.clock.positionMs()).toBe(1000);
  });

  it('stays at the start offset until the shared `when` is heard', async () => {
    const rig = await playing();
    rig.ctx.currentTime = 0.05;
    rig.ctx.outputLatency = 0.02;
    expect(rig.clock.positionMs()).toBe(0);
  });

  it('wraps into the loop region at the heard time (not the scheduled time)', async () => {
    const rig = await playing();
    rig.transport.setLoop({ start: 2, end: 4, enabled: true });
    rig.ctx.outputLatency = 0.05;
    rig.ctx.currentTime = 4.13;
    expect(rig.clock.positionMs()).toBe(3980);
    rig.ctx.currentTime = 4.17;
    expect(rig.clock.positionMs()).toBe(2020);
  });

  it('paused and stopped: the stored transport position minus the offset, no latency', async () => {
    const rig = await playing();
    rig.ctx.outputLatency = 0.04;
    rig.clock.setOffset(30);
    rig.ctx.currentTime = 5.1;
    rig.transport.pause();
    rig.ctx.currentTime = 9;
    expect(rig.clock.positionMs()).toBe(4970);
    rig.transport.seek(12.3456);
    expect(rig.clock.positionMs()).toBe(12_316);
    rig.transport.stop();
    expect(rig.clock.positionMs()).toBe(-30);
  });

  it('never accumulates: 1,000 jittered frames show no drift against the audio clock', async () => {
    const rig = await playing();
    const quantum = 128 / 44_100;
    const latency = 0.012;
    rig.ctx.outputLatency = latency;
    let seed = 42;
    let t = 0.5;
    let worst = 0;
    for (let frame = 0; frame < 1000; frame += 1) {
      seed = (seed * 1_103_515_245 + 12_345) % 2_147_483_648;
      t += (16.7 + ((seed / 2_147_483_648) * 10 - 5)) / 1000;
      const rendered = Math.floor(t / quantum) * quantum;
      rig.ctx.currentTime = rendered + quantum;
      rig.ctx.outputTimestamp = { contextTime: rendered, performanceTime: 1000 + rendered * 1000 };
      rig.perf.now = 1000 + t * 1000;
      const exact = (t - latency - 0.1) * 1000;
      worst = Math.max(worst, Math.abs(rig.clock.positionMs() - exact));
    }
    expect(t).toBeGreaterThan(16);
    expect(worst).toBeLessThanOrEqual(0.5 + 1e-6);
  });
});

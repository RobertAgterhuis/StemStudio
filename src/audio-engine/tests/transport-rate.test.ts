import { describe, expect, it, vi } from 'vitest';
import type { ChannelId } from '../src/types';
import type { MockAudioBufferSourceNode } from './mocks/audio-context';
import { makeTransport } from './mocks/transport-rig';

/** Rate-aware transport anchor (contract 2.3.0 "Transport", data-model 4; FR-004, FR-005). */
const L = 0.116;

function nth(sources: MockAudioBufferSourceNode[], index: number): MockAudioBufferSourceNode {
  const source = sources.at(index);
  if (source === undefined) {
    throw new Error(`no source ${String(index)}`);
  }
  return source;
}

function startOf(source: MockAudioBufferSourceNode): { when: number; offset: number } {
  const call = source.startCalls[0];
  return { when: call?.when ?? Number.NaN, offset: call?.offset ?? Number.NaN };
}

function rateCalls(source: MockAudioBufferSourceNode): readonly (readonly number[])[] {
  return source.playbackRate.calls.filter((c) => c.method === 'setValueAtTime').map((c) => c.args);
}

describe('transport timing at rate r and latency L', () => {
  it('starts every source L before the heard when at the given rate', async () => {
    const { ctx, transport } = makeTransport();
    transport.setTiming({ rate: 0.5, latency: L });
    ctx.currentTime = 2;
    await transport.play();
    const [source] = ctx.startedSources();
    expect(source?.startCalls).toEqual([{ when: 2.1, offset: 0 }]);
    expect(source?.playbackRate.value).toBe(0.5);
    expect(transport.anchor()).toEqual({ when: 2.1 + L, offset: 0, rate: 0.5 });
  });

  it('derives the heard position as offset + (t - when) * rate', async () => {
    const { ctx, transport } = makeTransport();
    transport.setTiming({ rate: 0.5, latency: L });
    await transport.play();
    const when = 0.1 + L;
    expect(transport.positionAt(when - 0.05)).toBe(0);
    ctx.currentTime = when + 4;
    expect(transport.position()).toBeCloseTo(2, 9);
    expect(transport.positionAt(when + 10)).toBeCloseTo(5, 9);
  });

  it('wraps the rate-aware position into a native loop and keeps loop properties with playbackRate', async () => {
    const { ctx, transport } = makeTransport();
    transport.setLoop({ start: 1, end: 2, enabled: true });
    transport.setTiming({ rate: 2, latency: 0 });
    await transport.play();
    const source = nth(ctx.startedSources(), 0);
    expect([source.loop, source.loopStart, source.loopEnd, source.playbackRate.value]).toEqual([true, 1, 2, 2]);
    expect(source.startCalls).toEqual([{ when: 0.1, offset: 1 }]);
    expect(transport.positionAt(0.1 + 0.75)).toBeCloseTo(1.5, 9);
    expect(transport.positionAt(0.1 + 1.25)).toBeCloseTo(1.5, 9);
  });

  it('keeps the 2.1.0 anchor without a rate field at rate 1 and latency 0', async () => {
    const { transport } = makeTransport();
    transport.setTiming({ rate: 1, latency: 0 });
    await transport.play();
    expect(transport.anchor()).toEqual({ when: 0.1, offset: 0 });
  });

  it('pause stores the heard position; play resumes it with the current timing', async () => {
    const { ctx, transport } = makeTransport();
    transport.setTiming({ rate: 0.5, latency: L });
    await transport.play();
    ctx.currentTime = 0.1 + L + 2;
    transport.pause();
    expect(transport.position()).toBeCloseTo(1, 9);
    transport.setTiming({ rate: 1.2, latency: 0.08 });
    ctx.currentTime = 5;
    await transport.play();
    const last = nth(ctx.startedSources(), -1);
    expect(startOf(last).when).toBeCloseTo(5.1, 9);
    expect(startOf(last).offset).toBeCloseTo(1, 9);
    expect(last.playbackRate.value).toBe(1.2);
    expect(transport.anchor()?.when).toBeCloseTo(5.18, 9);
  });

  it('calls the restart hook before every start of all sources', async () => {
    const { transport } = makeTransport();
    const hook = vi.fn();
    transport.setRestartHook(hook);
    await transport.play();
    transport.seek(10);
    expect(hook).toHaveBeenCalledTimes(2);
  });

  it('starts at an explicit heard time with the start position until then', async () => {
    const { ctx, transport } = makeTransport();
    transport.seek(4);
    transport.setTiming({ rate: 0.5, latency: L });
    expect(transport.startPosition()).toBe(4);
    await transport.play(3);
    const [source] = ctx.startedSources();
    expect(source?.startCalls[0]?.when).toBeCloseTo(3 - L, 9);
    expect(transport.positionAt(2)).toBe(4);
    expect(transport.positionAt(5)).toBeCloseTo(5, 9);
  });

  it('never starts before currentTime + startDelay + L', async () => {
    const { ctx, transport } = makeTransport();
    transport.setTiming({ rate: 1, latency: L });
    ctx.currentTime = 1;
    await transport.play(1.05);
    expect(ctx.startedSources()[0]?.startCalls[0]?.when).toBeCloseTo(1.1, 9);
  });
});

describe('live rate change (retime)', () => {
  it('automates playbackRate at U on every running source and re-anchors from the new heard time', async () => {
    const { ctx, transport } = makeTransport();
    transport.load(new Map<ChannelId, AudioBuffer>([
      ['vocals', ctx.createBuffer(2, 60 * 44100, 44100) as unknown as AudioBuffer],
      ['drums', ctx.createBuffer(2, 60 * 44100, 44100) as unknown as AudioBuffer],
    ]));
    transport.setTiming({ rate: 1, latency: L });
    await transport.play();
    ctx.currentTime = 2;
    const oldAnchor = transport.anchor();
    transport.retime({ rate: 0.5, latency: 0.14, at: 2.02, anchor: { when: 2.3, offset: 2.1, rate: 0.5 } });
    ctx.startedSources().forEach((source) => {
      expect(rateCalls(source)).toEqual([[0.5, 2.02]]);
    });
    expect(transport.positionAt(2.2)).toBeCloseTo(2.2 - Number(oldAnchor?.when), 9);
    expect(transport.positionAt(2.5)).toBeCloseTo(2.1 + 0.1, 9);
    expect(transport.anchors()).toHaveLength(2);
  });

  it('replaces a pending change that has not started yet (same U)', async () => {
    const { ctx, transport } = makeTransport();
    transport.setTiming({ rate: 1, latency: L });
    await transport.play();
    ctx.currentTime = 2;
    transport.retime({ rate: 0.5, latency: 0.14, at: 2.02, anchor: { when: 2.3, offset: 2.1, rate: 0.5 } });
    ctx.currentTime = 2.01;
    transport.retime({ rate: 0.8, latency: 0.12, at: 2.02, anchor: { when: 2.28, offset: 2.08, rate: 0.8 } });
    const [source] = ctx.startedSources();
    expect(source?.playbackRate.calls.map((c) => [c.method, ...c.args])).toEqual([
      ['setValueAtTime', 0.5, 2.02],
      ['cancelScheduledValues', 2.02],
      ['setValueAtTime', 0.8, 2.02],
    ]);
    expect(transport.anchors().map((a) => a.rate ?? 1)).toEqual([1, 0.8]);
  });

  it('keeps an applied change when the next one arrives after U', async () => {
    const { ctx, transport } = makeTransport();
    await transport.play();
    ctx.currentTime = 2;
    transport.retime({ rate: 0.5, latency: 0, at: 2.02, anchor: { when: 2.02, offset: 1.92, rate: 0.5 } });
    ctx.currentTime = 3;
    transport.retime({ rate: 0.8, latency: 0, at: 3.02, anchor: { when: 3.02, offset: 2.42, rate: 0.8 } });
    expect(transport.anchors().map((a) => a.rate ?? 1)).toEqual([1, 0.5, 0.8]);
  });

  it('notifies listeners of the timing change and ignores retime while not playing', async () => {
    const { ctx, transport } = makeTransport();
    const listener = { onStateChanged: vi.fn(), onReposition: vi.fn(), onTimingChanged: vi.fn() };
    transport.addListener(listener);
    transport.retime({ rate: 0.5, latency: 0, at: 0.02, anchor: { when: 0.02, offset: 0, rate: 0.5 } });
    expect(listener.onTimingChanged).not.toHaveBeenCalled();
    await transport.play();
    ctx.currentTime = 1;
    transport.retime({ rate: 0.5, latency: 0, at: 1.02, anchor: { when: 1.02, offset: 0.92, rate: 0.5 } });
    expect(listener.onTimingChanged).toHaveBeenCalledTimes(1);
    expect(transport.timing).toEqual({ rate: 0.5, latency: 0 });
  });

  it('forgets anchors more than 2 s in the past', async () => {
    const { ctx, transport } = makeTransport();
    await transport.play();
    for (let t = 1; t <= 10; t++) {
      ctx.currentTime = t;
      transport.retime({ rate: 1, latency: 0, at: t + 0.02, anchor: { when: t + 0.02, offset: t - 0.08 } });
    }
    expect(transport.anchors().length).toBeLessThanOrEqual(4);
    expect(transport.position()).toBeCloseTo(9.9, 9);
  });
});

describe('addBuffers on a rate/latency anchor', () => {
  it('starts late buffers at the shared buffer position with the current rate and pending automation', async () => {
    const { ctx, transport } = makeTransport();
    transport.setTiming({ rate: 0.5, latency: L });
    await transport.play();
    ctx.currentTime = 2;
    transport.retime({ rate: 0.8, latency: 0.12, at: 2.02, anchor: { when: 2.26, offset: 1.0, rate: 0.8 } });
    expect(transport.startTimeAfter(0.01)).toBeCloseTo(2.01, 9);
    transport.addBuffers(new Map<ChannelId, AudioBuffer>([['drums.kick', ctx.createBuffer(2, 44100, 44100) as unknown as AudioBuffer]]), 2.01);
    const late = nth(ctx.startedSources(), -1);
    expect(startOf(late).when).toBe(2.01);
    expect(startOf(late).offset).toBeCloseTo((2.01 - 0.1) * 0.5, 9);
    expect(late.playbackRate.value).toBe(0.8);
    expect(rateCalls(late)).toEqual([[0.8, 2.02]]);
    expect(late.playbackRate.calls.map((c) => c.method)).toEqual(['setValueAtTime']);
  });

  it('startTimeAfter is never before the first source start', async () => {
    const { transport } = makeTransport();
    transport.setTiming({ rate: 1, latency: L });
    await transport.play();
    expect(transport.startTimeAfter(0.01)).toBeCloseTo(0.1, 9);
  });
});

describe('restart (engaging the stretch stage)', () => {
  it('restarts every source at the heard position with the new timing while playing', async () => {
    const { ctx, transport } = makeTransport();
    transport.restart();
    expect(ctx.startedSources()).toHaveLength(0);
    expect(transport.anchors()).toEqual([]);
    await transport.play();
    ctx.currentTime = 3.1;
    transport.setTiming({ rate: 0.5, latency: L });
    transport.restart();
    const last = ctx.startedSources().at(-1);
    expect(last?.startCalls[0]?.when).toBeCloseTo(3.2, 9);
    expect(last?.startCalls[0]?.offset).toBeCloseTo(3, 9);
    expect(transport.anchor()).toEqual({ when: 3.2 + L, offset: 3, rate: 0.5 });
  });

  it('re-bases both queues on a loop change, keeping a pending change', async () => {
    const { ctx, transport } = makeTransport();
    await transport.play();
    ctx.currentTime = 5;
    transport.retime({ rate: 0.5, latency: 0, at: 5.02, anchor: { when: 5.02, offset: 4.92, rate: 0.5 } });
    transport.setLoop({ start: 2, end: 20, enabled: true });
    expect(transport.anchors().map((a) => a.when)).toEqual([5, 5.02]);
    ctx.currentTime = 6.02;
    expect(transport.position()).toBeCloseTo(5.42, 9);
  });
});

describe('karaoke clock at a rate (no karaoke change)', () => {
  it('follows the rate-aware anchor at the heard time', async () => {
    const { KaraokeClock } = await import('../src/karaoke/clock');
    const { ctx, transport } = makeTransport();
    transport.setTiming({ rate: 0.75, latency: L });
    await transport.play();
    const clock = new KaraokeClock({ context: ctx.asAudioContext(), transport, now: () => 0 });
    ctx.currentTime = 0.1 + L + 4;
    expect(clock.positionMs()).toBe(3000);
  });
});

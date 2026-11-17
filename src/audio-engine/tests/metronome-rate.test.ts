import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ClickPlayer } from '../src/metronome/clicks';
import { MetronomeScheduler } from '../src/metronome/scheduler';
import { segmentsFor } from '../src/metronome/timeline';
import { Transport, type TransportAnchor, type TransportClock } from '../src/transport';
import type { LoopRegion, MetronomeSettings } from '../src/types';
import { MockAudioContext, type MockAudioBufferSourceNode } from './mocks/audio-context';

/** Metronome clicks stay in track time and are mapped through the rate-aware anchor (contract 2.3.0 "Metronome"). */
const NO_LOOP: LoopRegion = { start: 0, end: 0, enabled: false };

function clock(anchors: TransportAnchor[], loop: LoopRegion = NO_LOOP): TransportClock {
  return { anchor: () => anchors.at(-1) ?? null, anchors: () => anchors, loop, duration: 100 };
}

describe('segmentsFor with a rate', () => {
  it('maps a context window onto track time at the anchor rate', () => {
    const segments = segmentsFor(clock([{ when: 5, offset: 2, rate: 0.5 }]), { from: 5, to: 6 });
    expect(segments).toEqual([{ trackFrom: 2, trackTo: 2.5, contextStart: 5, rate: 0.5 }]);
  });

  it('continues at loopStart with context times divided by the rate', () => {
    const loop = { start: 3, end: 4, enabled: true };
    const segments = segmentsFor(clock([{ when: 1, offset: 3, rate: 2 }], loop), { from: 1.4, to: 1.6 });
    expect(segments).toHaveLength(2);
    expect(segments[0]).toMatchObject({ trackFrom: 3.8, contextStart: 1.4, rate: 2 });
    expect(segments[0]?.trackTo).toBeCloseTo(4, 9);
    expect(segments[1]?.trackFrom).toBeCloseTo(3, 9);
    expect(segments[1]?.trackTo).toBeCloseTo(3.2, 9);
    expect(segments[1]?.contextStart).toBeCloseTo(1.5, 9);
  });

  it('splits a window at the next anchor of the queue', () => {
    const anchors = [
      { when: 0, offset: 0 },
      { when: 10, offset: 10, rate: 0.5 },
    ];
    const segments = segmentsFor(clock(anchors), { from: 9.9, to: 10.2 });
    expect(segments).toHaveLength(2);
    expect(segments[0]).toMatchObject({ trackFrom: 9.9, contextStart: 9.9, rate: 1 });
    expect(segments[0]?.trackTo).toBeCloseTo(10, 9);
    expect(segments[1]).toMatchObject({ trackFrom: 10, contextStart: 10, rate: 0.5 });
    expect(segments[1]?.trackTo).toBeCloseTo(10.1, 9);
  });

  it('stops an anchor at its end time (explicit pass) and leaves the gap silent', () => {
    const anchors = [{ when: 0, offset: 2, rate: 1, until: 1 }, { when: 1.5, offset: 2 }];
    const segments = segmentsFor(clock(anchors), { from: 0.9, to: 1.6 });
    expect(segments).toHaveLength(2);
    expect(segments[0]).toMatchObject({ trackFrom: 2.9, contextStart: 0.9 });
    expect(segments[0]?.trackTo).toBeCloseTo(3, 9);
    expect(segments[1]).toMatchObject({ trackFrom: 2, contextStart: 1.5 });
    expect(segments[1]?.trackTo).toBeCloseTo(2.1, 9);
  });
});

describe('scheduler at a rate', () => {
  const SETTINGS: MetronomeSettings = { enabled: true, volume: 0.8, bpmOverride: 120, offsetMs: 0, beatsPerBar: 4 };
  let ctx: MockAudioContext;
  let transport: Transport;
  let player: ClickPlayer;

  beforeEach(() => {
    vi.useFakeTimers();
    ctx = new MockAudioContext();
    transport = new Transport({ context: ctx.asAudioContext(), startDelaySeconds: 0.1, output: () => ctx.destination as unknown as AudioNode });
    transport.load(new Map([['drums', ctx.createBuffer(2, 60 * 44100, 44100) as unknown as AudioBuffer]]));
    player = new ClickPlayer(ctx.asAudioContext());
    const scheduler = new MetronomeScheduler({ context: ctx.asAudioContext(), clock: transport, output: player, aheadSeconds: 0.1, intervalMs: 25 });
    transport.addListener(scheduler);
    scheduler.configure(SETTINGS, null);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  function live(): number[] {
    return ctx.sources
      .filter((s: MockAudioBufferSourceNode) => (s.connections[0] as unknown) === player.output && s.stopCalls.length === 0)
      .map((s) => s.startCalls[0]?.when ?? Number.NaN);
  }

  function run(seconds: number): void {
    for (let i = 0; i < Math.round(seconds / 0.025); i++) {
      ctx.currentTime += 0.025;
      vi.advanceTimersByTime(25);
    }
  }

  it('halves the audible click rate at 50 % (track period 0.5 s -> 1 s of context time)', async () => {
    transport.setTiming({ rate: 0.5, latency: 0.116 });
    await transport.play();
    run(3);
    const when = 0.216;
    const expected = [0, 1, 2, 3].map((k) => when + k);
    live().forEach((time, i) => {
      expect(time).toBeCloseTo(expected[i] ?? Number.NaN, 6);
    });
    expect(live().length).toBe(3);
  });

  it('cancels and reschedules future clicks on a rate change inside the look-ahead window', async () => {
    await transport.play();
    run(1.02);
    const pending = ctx.sources.filter((s) => (s.connections[0] as unknown) === player.output && (s.startCalls[0]?.when ?? 0) >= ctx.currentTime);
    expect(pending.length).toBe(1);
    const now = ctx.currentTime;
    transport.retime({ rate: 0.5, latency: 0, at: now + 0.02, anchor: { when: now + 0.02, offset: now + 0.02 - 0.1, rate: 0.5 } });
    expect(pending[0]?.stopCalls).toHaveLength(1);
    run(1.2);
    const after = live().filter((t) => t > now);
    expect(after[0]).toBeCloseTo(now + 0.02 + (1 - (now + 0.02 - 0.1)) / 0.5, 6);
  });
});

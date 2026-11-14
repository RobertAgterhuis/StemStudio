import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RAMP_TIME_CONSTANT } from '../src/channel';
import { createEngine } from '../src/index';
import { ACCENT_TONE, ClickPlayer, NORMAL_TONE, renderClick } from '../src/metronome/clicks';
import { createClickGrid } from '../src/metronome/grid';
import { MetronomeScheduler } from '../src/metronome/scheduler';
import { segmentsFor } from '../src/metronome/timeline';
import { Transport, type TransportAnchor, type TransportClock } from '../src/transport';
import type { BeatGrid, LoopRegion, MetronomeSettings } from '../src/types';
import { MockAudioContext, type MockAudioBufferSourceNode } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';
import { FetchStub } from './mocks/fetch';

const AHEAD = 0.1;
const SETTINGS: MetronomeSettings = { enabled: true, volume: 0.8, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 };

/** Beats every `period` s from `first`; a downbeat every `perBar` beats. */
function makeGrid(options: { period?: number; first?: number; count?: number; perBar?: number; reliable?: boolean } = {}): BeatGrid {
  const { period = 0.5, first = 0, count = 120, perBar = 4, reliable = true } = options;
  const beats = Array.from({ length: count }, (_, i) => first + i * period);
  return {
    bpm: 60 / period,
    beats,
    downbeats: beats.filter((_, i) => i % perBar === 0),
    beatsPerBar: perBar,
    confidence: 0.9,
    reliable,
  };
}

interface Rig {
  readonly ctx: MockAudioContext;
  readonly transport: Transport;
  readonly player: ClickPlayer;
  readonly scheduler: MetronomeScheduler;
}

function makeRig(grid: BeatGrid | null, settings: Partial<MetronomeSettings> = {}, seconds = 60): Rig {
  const ctx = new MockAudioContext();
  const transport = new Transport({
    context: ctx.asAudioContext(),
    startDelaySeconds: 0.1,
    output: () => ctx.destination as unknown as AudioNode,
  });
  transport.load(new Map([['drums', ctx.createBuffer(2, seconds * ctx.sampleRate, ctx.sampleRate) as unknown as AudioBuffer]]));
  const player = new ClickPlayer(ctx.asAudioContext());
  const scheduler = new MetronomeScheduler({
    context: ctx.asAudioContext(),
    clock: transport,
    output: player,
    aheadSeconds: AHEAD,
    intervalMs: 25,
  });
  transport.addListener(scheduler);
  scheduler.configure({ ...SETTINGS, ...settings }, grid);
  return { ctx, transport, player, scheduler };
}

function clicks(rig: Rig): MockAudioBufferSourceNode[] {
  return rig.ctx.sources.filter((s) => (s.connections[0] as unknown) === rig.player.output);
}

function whenOf(source: MockAudioBufferSourceNode): number {
  return source.startCalls[0]?.when ?? Number.NaN;
}

/** Live (not cancelled) click start times. */
function liveTimes(rig: Rig): number[] {
  return clicks(rig)
    .filter((s) => s.stopCalls.length === 0)
    .map(whenOf);
}

/**
 * Advances audio clock and fake timers together for `seconds`. The audio clock drifts against
 * the timer (jitter), and every click scheduled by a wake-up must lie in [now, now + AHEAD).
 */
function run(rig: Rig, seconds: number, jitter = 0): void {
  const steps = Math.round(seconds / 0.025);
  for (let i = 0; i < steps; i++) {
    const before = rig.ctx.sources.length;
    rig.ctx.currentTime += 0.025 + jitter * Math.sin(i * 1.7);
    const now = rig.ctx.currentTime;
    vi.advanceTimersByTime(25);
    for (const source of rig.ctx.sources.slice(before)) {
      expect(whenOf(source)).toBeGreaterThanOrEqual(now);
      expect(whenOf(source)).toBeLessThan(now + AHEAD);
    }
  }
}

function expectWithinOneMs(actual: readonly number[], expected: readonly number[]): void {
  expect(actual).toHaveLength(expected.length);
  actual.forEach((t, i) => {
    expect(Math.abs(t - (expected[i] ?? Number.NaN))).toBeLessThan(0.001);
  });
}

describe('click grid', () => {
  it('detected grid: beats[i] + offsetMs/1000, accent from the nearest preceding downbeat', () => {
    const grid: BeatGrid = { ...makeGrid(), beats: [0, 0.5, 1, 1.5, 2, 2.5, 3], downbeats: [1, 3] };
    const four = createClickGrid(grid, { ...SETTINGS, offsetMs: 100 }).between(0, 10);
    expectWithinOneMs(
      four.map((c) => c.time),
      [0.1, 0.6, 1.1, 1.6, 2.1, 2.6, 3.1],
    );
    expect(four.map((c) => c.accent)).toEqual([false, false, true, false, false, false, true]);
    const two = createClickGrid(grid, { ...SETTINGS, beatsPerBar: 2 }).between(0, 10);
    expect(two.map((c) => c.accent)).toEqual([true, false, true, false, true, false, true]);
  });

  it('detected grid without downbeats counts bars from the first beat', () => {
    const grid: BeatGrid = { ...makeGrid({ count: 5 }), downbeats: [] };
    const result = createClickGrid(grid, { ...SETTINGS, beatsPerBar: 2 }).between(0, 10);
    expect(result.map((c) => c.accent)).toEqual([true, false, true, false, true]);
  });

  it('between is half-open [from, to)', () => {
    const grid = createClickGrid(makeGrid(), SETTINGS);
    expect(grid.between(1, 2).map((c) => c.time)).toEqual([1, 1.5]);
    const constant = createClickGrid(null, { ...SETTINGS, bpmOverride: 120 });
    expect(constant.between(1, 2).map((c) => c.time)).toEqual([1, 1.5]);
  });

  it('override grid: t0 + k*60/bpm + offset, t0 = downbeats[0] ?? beats[0] ?? 0, accent every beatsPerBar', () => {
    const grid: BeatGrid = { ...makeGrid({ first: 0.5 }), downbeats: [1] };
    const result = createClickGrid(grid, { ...SETTINGS, bpmOverride: 100, beatsPerBar: 3, offsetMs: 50 }).between(0, 3);
    expectWithinOneMs(
      result.map((c) => c.time),
      [0.45, 1.05, 1.65, 2.25, 2.85],
    );
    expect(result.map((c) => c.accent)).toEqual([false, true, false, false, true]);
    const fromBeats = createClickGrid({ ...grid, downbeats: [] }, { ...SETTINGS, bpmOverride: 120 }).between(0, 1);
    expect(fromBeats.map((c) => c.time)).toEqual([0, 0.5]);
    const fromZero = createClickGrid({ ...grid, beats: [], downbeats: [] }, { ...SETTINGS, bpmOverride: 60 }).between(0, 2.5);
    expect(fromZero.map((c) => c.time)).toEqual([0, 1, 2]);
  });

  it('is silent for an unreliable or missing grid without override', () => {
    expect(createClickGrid(makeGrid({ reliable: false }), SETTINGS).between(0, 10)).toEqual([]);
    expect(createClickGrid(null, SETTINGS).between(0, 10)).toEqual([]);
    expect(createClickGrid(makeGrid({ reliable: false }), { ...SETTINGS, bpmOverride: 60 }).between(0, 1.5)).toHaveLength(2);
  });
});

describe('timeline segments', () => {
  const clock = (anchor: TransportAnchor | null, loop: LoopRegion, duration = 100): TransportClock => ({
    anchor: () => anchor,
    loop,
    duration,
  });
  const NO_LOOP = { start: 0, end: 0, enabled: false };

  it('maps a context window to track time, starting no earlier than when', () => {
    expect(segmentsFor(clock(null, NO_LOOP), { from: 0, to: 1 })).toEqual([]);
    expect(segmentsFor(clock({ when: 5, offset: 2 }, NO_LOOP), { from: 4, to: 4.5 })).toEqual([]);
    const [segment] = segmentsFor(clock({ when: 5, offset: 2 }, NO_LOOP), { from: 4.9, to: 5.1 });
    expect(segment?.trackFrom).toBe(2);
    expect(segment?.trackTo).toBeCloseTo(2.1, 9);
    expect(segment?.contextStart).toBe(5);
  });

  it('stops at the end of the track', () => {
    const [segment] = segmentsFor(clock({ when: 0, offset: 0 }, NO_LOOP, 10), { from: 9.95, to: 10.05 });
    expect(segment?.trackTo).toBe(10);
  });

  it('splits a window crossing loopEnd and maps the remainder to loopStart', () => {
    const loop = { start: 2, end: 4, enabled: true };
    const segments = segmentsFor(clock({ when: 1, offset: 3 }, loop), { from: 1.95, to: 2.05 });
    expect(segments).toHaveLength(2);
    expect(segments[0]?.trackFrom).toBeCloseTo(3.95, 9);
    expect(segments[0]?.trackTo).toBeCloseTo(4, 9);
    expect(segments[1]?.trackFrom).toBeCloseTo(2, 9);
    expect(segments[1]?.trackTo).toBeCloseTo(2.05, 9);
    expect(segments[1]?.contextStart).toBeCloseTo(2, 9);
    const later = segmentsFor(clock({ when: 1, offset: 3 }, loop), { from: 8.5, to: 8.6 });
    expect(later[0]?.trackFrom).toBeCloseTo(2.5, 9);
  });

  it('caps the number of segments for absurdly short loops', () => {
    const loop = { start: 0, end: 0.0001, enabled: true };
    expect(segmentsFor(clock({ when: 0, offset: 0 }, loop), { from: 0, to: 1 }).length).toBeLessThanOrEqual(64);
  });
});

describe('click rendering', () => {
  it('renders short mono click buffers once; accent is higher and louder', () => {
    const ctx = new MockAudioContext();
    const accent = renderClick(ctx.asAudioContext(), ACCENT_TONE);
    expect(accent.numberOfChannels).toBe(1);
    expect(accent.duration).toBeLessThan(0.05);
    expect(ACCENT_TONE.frequency).toBeGreaterThan(NORMAL_TONE.frequency);
    expect(ACCENT_TONE.amplitude).toBeGreaterThan(NORMAL_TONE.amplitude);
    const peak = Math.max(...Array.from(accent.getChannelData(0), Math.abs));
    expect(peak).toBeGreaterThan(0.5);
    expect(peak).toBeLessThanOrEqual(1);
  });

  it('click gain goes straight to the destination (bypasses the master) and ramps the volume', () => {
    const ctx = new MockAudioContext();
    const player = new ClickPlayer(ctx.asAudioContext());
    ctx.currentTime = 3;
    player.setVolume(0.3);
    const output = player.output as unknown as (typeof ctx.gains)[number];
    expect(output.connections).toEqual([ctx.destination]);
    expect(output.gain.calls.at(-1)).toEqual({ method: 'setTargetAtTime', args: [0.3, 3, RAMP_TIME_CONSTANT] });
    const source = player.play(5, true) as unknown as MockAudioBufferSourceNode;
    expect(source.startCalls).toEqual([{ when: 5, offset: undefined }]);
    source.end();
    expect(source.disconnected).toBe(true);
  });
});

describe('metronome scheduler', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('SC-003: every click of the detected grid lands within 1 ms of its beat (jittery timer)', async () => {
    const rig = makeRig(makeGrid());
    await rig.transport.play();
    run(rig, 10.05, 0.004);
    const now = rig.ctx.currentTime;
    const expected = makeGrid()
      .beats.map((b) => 0.1 + b)
      .filter((t) => t < now + AHEAD);
    expect(expected).toHaveLength(21);
    expectWithinOneMs(liveTimes(rig), expected);
  });

  it('accent clicks on downbeats use the pre-rendered accent buffer; only two buffers exist', async () => {
    const rig = makeRig(makeGrid());
    await rig.transport.play();
    run(rig, 3.05);
    const sources = clicks(rig);
    const buffers = new Set(sources.map((s) => s.buffer));
    expect(buffers.size).toBe(2);
    const accentBuffer = sources[0]?.buffer;
    expect(sources.map((s) => s.buffer === accentBuffer)).toEqual(sources.map((_, i) => i % 4 === 0));
  });

  it('applies offsetMs; a click shifted before 0 is skipped', async () => {
    const rig = makeRig(makeGrid(), { offsetMs: -200 });
    await rig.transport.play();
    run(rig, 1.5);
    expectWithinOneMs(liveTimes(rig), [0.4, 0.9, 1.4]);
  });

  it('override grid: constant tempo from t0 with accents every beatsPerBar', async () => {
    const rig = makeRig(makeGrid({ first: 0.25 }), { bpmOverride: 100, beatsPerBar: 3 });
    await rig.transport.play();
    run(rig, 3);
    const expected = [0, 1, 2, 3, 4].map((k) => 0.1 + 0.25 + k * 0.6);
    expectWithinOneMs(liveTimes(rig), expected);
    const accent = clicks(rig)[0]?.buffer;
    expect(clicks(rig).map((s) => s.buffer === accent)).toEqual([true, false, false, true, false]);
  });

  it('SC-003 with a loop: the window crossing loopEnd continues at loopStart', async () => {
    const rig = makeRig(makeGrid());
    rig.transport.setLoop({ start: 2, end: 4, enabled: true });
    await rig.transport.play();
    run(rig, 10.05, 0.003);
    const now = rig.ctx.currentTime;
    const expected: number[] = [];
    for (let cycle = 0; cycle < 6; cycle++) {
      for (const t of [2, 2.5, 3, 3.5]) {
        expected.push(0.1 + cycle * 2 + (t - 2));
      }
    }
    expectWithinOneMs(
      liveTimes(rig),
      expected.filter((t) => t < now + AHEAD),
    );
    const accent = clicks(rig)[0]?.buffer;
    expect(clicks(rig).filter((s) => s.buffer === accent).length).toBe(6);
  });

  it('a loop not aligned with the beats keeps every click on the grid', async () => {
    const rig = makeRig(makeGrid());
    rig.transport.setLoop({ start: 1.25, end: 2.8, enabled: true });
    await rig.transport.play();
    run(rig, 6);
    const length = 1.55;
    const cycleBeats = [1.5, 2, 2.5];
    const now = rig.ctx.currentTime;
    const expected: number[] = [];
    for (let cycle = 0; cycle < 5; cycle++) {
      cycleBeats.forEach((b) => expected.push(0.1 + cycle * length + (b - 1.25)));
    }
    expectWithinOneMs(
      liveTimes(rig),
      expected.filter((t) => t < now + AHEAD),
    );
  });

  it('does not click past the end of the track', async () => {
    const rig = makeRig(makeGrid(), {}, 2);
    await rig.transport.play();
    run(rig, 3);
    expect(liveTimes(rig)).toEqual([0.1, 0.6, 1.1, 1.6]);
  });

  it('seek cancels already scheduled future clicks and reschedules from the new position', async () => {
    const rig = makeRig(makeGrid());
    await rig.transport.play();
    run(rig, 1.05);
    const pendingBefore = clicks(rig).filter((s) => whenOf(s) >= rig.ctx.currentTime);
    expect(pendingBefore.length).toBeGreaterThan(0);
    rig.transport.seek(10.2);
    pendingBefore.forEach((s) => {
      expect(s.stopCalls).toHaveLength(1);
    });
    const seekAt = rig.ctx.currentTime;
    const anchor = rig.transport.anchor() ?? { when: 0, offset: 0 };
    run(rig, 1);
    const after = liveTimes(rig).filter((t) => t >= seekAt);
    expectWithinOneMs(after, [10.5, 11].map((b) => anchor.when + (b - 10.2)).filter((t) => t < rig.ctx.currentTime + AHEAD));
  });

  it('pause cancels future clicks and stops the wake-up timer', async () => {
    const rig = makeRig(makeGrid());
    await rig.transport.play();
    run(rig, 0.55);
    const pending = clicks(rig).filter((s) => whenOf(s) >= rig.ctx.currentTime);
    expect(pending).toHaveLength(1);
    rig.transport.pause();
    pending.forEach((s) => {
      expect(s.stopCalls).toHaveLength(1);
    });
    const count = clicks(rig).length;
    rig.ctx.currentTime += 5;
    vi.advanceTimersByTime(5000);
    expect(clicks(rig)).toHaveLength(count);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('a loop change while playing cancels and reschedules', async () => {
    const rig = makeRig(makeGrid());
    await rig.transport.play();
    run(rig, 0.55);
    const pending = clicks(rig).filter((s) => whenOf(s) >= rig.ctx.currentTime);
    expect(pending.length).toBeGreaterThan(0);
    rig.transport.setLoop({ start: 0, end: 4, enabled: true });
    pending.forEach((s) => {
      expect(s.stopCalls).toHaveLength(1);
    });
    expect(liveTimes(rig).filter((t) => t >= rig.ctx.currentTime)).toHaveLength(pending.length);
  });

  it('unreliable grid without override is silent; setting an override makes it click', async () => {
    const rig = makeRig(makeGrid({ reliable: false }));
    await rig.transport.play();
    run(rig, 2);
    expect(clicks(rig)).toEqual([]);
    rig.scheduler.configure({ ...SETTINGS, bpmOverride: 120 }, makeGrid({ reliable: false }));
    run(rig, 1);
    expect(clicks(rig).length).toBeGreaterThan(0);
  });

  it('disabled is silent; enabling starts at the next tick; disabling cancels future clicks', async () => {
    const rig = makeRig(makeGrid(), { enabled: false });
    await rig.transport.play();
    run(rig, 1);
    expect(clicks(rig)).toEqual([]);
    rig.scheduler.configure(SETTINGS, makeGrid());
    run(rig, 1.05);
    const pending = clicks(rig).filter((s) => whenOf(s) >= rig.ctx.currentTime);
    expect(pending.length).toBeGreaterThan(0);
    rig.scheduler.configure({ ...SETTINGS, enabled: false }, makeGrid());
    pending.forEach((s) => {
      expect(s.stopCalls).toHaveLength(1);
    });
  });

  it('volume changes are immediate and independent of the master', () => {
    const rig = makeRig(makeGrid());
    rig.ctx.currentTime = 2;
    rig.scheduler.configure({ ...SETTINGS, volume: 0.25 }, makeGrid());
    const output = rig.player.output as unknown as (typeof rig.ctx.gains)[number];
    expect(output.gain.value).toBe(0.25);
    expect(output.connections).toEqual([rig.ctx.destination]);
  });

  it('a starved timer never schedules clicks in the past', async () => {
    const rig = makeRig(makeGrid());
    await rig.transport.play();
    run(rig, 0.5);
    rig.ctx.currentTime += 1;
    const now = rig.ctx.currentTime;
    const before = rig.ctx.sources.length;
    vi.advanceTimersByTime(25);
    for (const source of rig.ctx.sources.slice(before)) {
      expect(whenOf(source)).toBeGreaterThanOrEqual(now);
    }
  });

  it('dispose stops the timer and cancels future clicks', async () => {
    const rig = makeRig(makeGrid());
    await rig.transport.play();
    run(rig, 0.55);
    const pending = clicks(rig).filter((s) => whenOf(s) >= rig.ctx.currentTime);
    expect(pending).toHaveLength(1);
    rig.scheduler.dispose();
    pending.forEach((s) => {
      expect(s.stopCalls).toHaveLength(1);
    });
    expect(vi.getTimerCount()).toBe(0);
  });
});

describe('engine wiring', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('setMetronome and the loaded beat grid drive the scheduler; dispose stops it', async () => {
    const ctx = new MockAudioContext();
    const stub = new FetchStub().reply('/drums', {});
    const engine = createEngine(new FakeDotNetRef(), {
      audioContextFactory: () => ctx.asAudioContext(),
      fetch: stub.fetch,
      scheduleAheadSeconds: 0.2,
      schedulerIntervalMs: 50,
    });
    await engine.loadTrack({ stems: [{ name: 'drums', url: '/drums' }], beatGrid: makeGrid() });
    engine.setMetronome({ ...SETTINGS, volume: 0.4 });
    await engine.play();
    for (let i = 0; i < 20; i++) {
      ctx.currentTime += 0.05;
      vi.advanceTimersByTime(50);
    }
    const clickSources = ctx.sources.filter((s) => s.buffer?.numberOfChannels === 1);
    expect(clickSources.map(whenOf)).toEqual([0.1, 0.6, 1.1]);
    const clickGain = clickSources[0]?.connections[0] as unknown;
    expect(ctx.gains.find((g) => g === clickGain)?.gain.value).toBe(0.4);
    await engine.dispose();
    expect(vi.getTimerCount()).toBe(0);
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FrameLoop } from '../src/frame-loop';
import { LoopPassTracker, wrapsAt } from '../src/loop-pass';
import type { LoopPassInfo } from '../src/types';
import { FakeFrames } from './mocks/frames';
import { loadedPracticeRig, practice, type PracticeRig } from './mocks/practice-rig';
import { makeTransport, type TransportRig } from './mocks/transport-rig';

/** `OnLoopPass` (contract 2.4.0 "Loop passes", ADR-023 D7, 014 FR-025). */
const A = 2;
const B = 4;

interface Rig extends TransportRig {
  readonly frames: FakeFrames;
  readonly loop: FrameLoop;
  readonly tracker: LoopPassTracker;
  readonly passes: LoopPassInfo[];
  /** Heard context time read by the tracker (defaults to `currentTime`). */
  heard: number | null;
}

function makeRig(): Rig {
  const base = makeTransport();
  const frames = new FakeFrames();
  const loop = new FrameLoop({ frames, document });
  const passes: LoopPassInfo[] = [];
  const rig: Rig = { ...base, frames, loop, passes, heard: null, tracker: undefined as unknown as LoopPassTracker };
  const tracker = new LoopPassTracker({
    transport: base.transport,
    frames: loop,
    now: () => rig.heard ?? base.ctx.currentTime,
    report: (info) => passes.push(info),
  });
  base.transport.addListener(tracker);
  return Object.assign(rig, { tracker });
}

/** Rig with the loop A..B enabled and playing from A (heard start 0.1 s). */
async function looping(): Promise<Rig> {
  const rig = makeRig();
  rig.transport.setLoop({ start: A, end: B, enabled: true });
  rig.tracker.onLoopChanged();
  await rig.transport.play();
  return rig;
}

function at(rig: Rig, time: number): void {
  rig.ctx.currentTime = time;
  rig.tracker.observe();
}

describe('wrapsAt', () => {
  const region = { start: A, end: B, enabled: true };

  it('counts completed passes of an open (native loop) anchor', () => {
    const anchor = { when: 0, offset: A };
    expect([1.99, 2, 3.99, 4, 9].map((t) => wrapsAt(anchor, t, region))).toEqual([0, 1, 1, 2, 4]);
  });

  it('counts one completed pass for an explicit pass anchor once it ended', () => {
    const anchor = { when: 0, offset: A, rate: 0.5, until: 4 };
    expect([3.99, 4, 100].map((t) => wrapsAt(anchor, t, region))).toEqual([0, 1, 1]);
  });
});

describe('LoopPassTracker', () => {
  it('fires once per heard wrap with the region, the pass number and the speed', async () => {
    const rig = await looping();
    at(rig, 2.09);
    expect(rig.passes).toEqual([]);
    at(rig, 2.1);
    at(rig, 2.1);
    at(rig, 3);
    expect(rig.passes).toEqual([{ start: A, end: B, pass: 1, speedPercent: 100 }]);
    at(rig, 4.1);
    expect(rig.passes.map((p) => p.pass)).toEqual([1, 2]);
  });

  it('reports every pass that completed between two observations (hidden tab)', async () => {
    const rig = await looping();
    at(rig, 6.2);
    expect(rig.passes.map((p) => p.pass)).toEqual([1, 2, 3]);
  });

  it('uses the heard clock, not currentTime', async () => {
    const rig = await looping();
    rig.ctx.currentTime = 2.15;
    rig.heard = 2.05;
    rig.tracker.observe();
    expect(rig.passes).toEqual([]);
    rig.heard = 2.1;
    rig.tracker.observe();
    expect(rig.passes).toHaveLength(1);
  });

  it('never goes back in time when the heard clock jitters', async () => {
    const rig = await looping();
    rig.heard = 2.2;
    rig.tracker.observe();
    rig.heard = 2.0;
    rig.tracker.observe();
    rig.heard = 2.3;
    rig.tracker.observe();
    expect(rig.passes).toHaveLength(1);
  });

  it('never fires without an enabled loop', async () => {
    const rig = makeRig();
    await rig.transport.play();
    at(rig, 30);
    rig.transport.setLoop({ start: A, end: B, enabled: false });
    rig.tracker.onLoopChanged();
    at(rig, 40);
    expect(rig.passes).toEqual([]);
  });

  it('never fires while not playing', async () => {
    const rig = await looping();
    rig.transport.pause();
    at(rig, 10);
    expect(rig.passes).toEqual([]);
  });

  it('a seek resets the pass number and does not report the partial pass', async () => {
    const rig = await looping();
    at(rig, 2.1);
    rig.ctx.currentTime = 2.5;
    rig.transport.seek(3);
    rig.tracker.reset();
    // New heard start 2.6 at track 3: the partial pass ends at 3.6, the first full pass at 5.6.
    at(rig, 3.6);
    expect(rig.passes.map((p) => p.pass)).toEqual([1]);
    at(rig, 5.6);
    expect(rig.passes.map((p) => p.pass)).toEqual([1, 1]);
  });

  it('a seek to the loop start is a full pass', async () => {
    const rig = await looping();
    rig.ctx.currentTime = 1;
    rig.transport.seek(A);
    rig.tracker.reset();
    at(rig, 3.1);
    expect(rig.passes).toEqual([{ start: A, end: B, pass: 1, speedPercent: 100 }]);
  });

  it('a loop change resets the pass number; re-sending the same region does not', async () => {
    const rig = await looping();
    at(rig, 2.1);
    rig.transport.setLoop({ start: A, end: B, enabled: true });
    rig.tracker.onLoopChanged();
    at(rig, 4.1);
    expect(rig.passes.map((p) => p.pass)).toEqual([1, 2]);
    rig.ctx.currentTime = 4.6;
    rig.transport.setLoop({ start: A, end: 5, enabled: true });
    rig.tracker.onLoopChanged();
    // At 4.6 the position is 2.5 (inside A..5): partial until 7.1, then a full pass of 3 s.
    at(rig, 7.1);
    at(rig, 10.1);
    expect(rig.passes.slice(2)).toEqual([{ start: A, end: 5, pass: 1, speedPercent: 100 }]);
  });

  it('stop resets the pass number; play from the stored position is a full pass', async () => {
    const rig = await looping();
    at(rig, 2.1);
    rig.transport.stop();
    rig.tracker.reset();
    rig.ctx.currentTime = 10;
    await rig.transport.play();
    at(rig, 12.1);
    expect(rig.passes.map((p) => p.pass)).toEqual([1, 1]);
  });

  it('pause and play continue the pass count', async () => {
    const rig = await looping();
    at(rig, 2.1);
    rig.ctx.currentTime = 3.1;
    rig.transport.pause();
    rig.ctx.currentTime = 20;
    await rig.transport.play();
    // Paused at track 3; resumed heard at 20.1: the pass ends at 21.1.
    at(rig, 21.0);
    expect(rig.passes).toHaveLength(1);
    at(rig, 21.1);
    expect(rig.passes.map((p) => p.pass)).toEqual([1, 2]);
  });

  it.each([
    [50, 0.1 + 2 / 0.5],
    [75, 0.1 + 2 / 0.75],
    [120, 0.1 + 2 / 1.2],
  ])('at %i %% the wrap is heard after (B - A) / rate and reports that speed', async (speed, wrap) => {
    const rig = makeRig();
    rig.transport.setTiming({ rate: speed / 100, latency: 0 });
    rig.transport.setLoop({ start: A, end: B, enabled: true });
    rig.tracker.onLoopChanged();
    await rig.transport.play();
    at(rig, wrap - 0.001);
    expect(rig.passes).toEqual([]);
    at(rig, wrap);
    expect(rig.passes).toEqual([{ start: A, end: B, pass: 1, speedPercent: speed }]);
  });

  it('reports the speed of the anchor in effect at the wrap after a live speed change', async () => {
    const rig = await looping();
    rig.ctx.currentTime = 1;
    // Heard at 1.0: track 2.9; from there at 50 % the pass ends at 1 + 1.1 / 0.5 = 3.2.
    rig.transport.retime({ at: 1, rate: 0.5, latency: 0, anchor: { when: 1, offset: 2.9, rate: 0.5 } });
    at(rig, 3.19);
    expect(rig.passes).toEqual([]);
    at(rig, 3.2);
    expect(rig.passes).toEqual([{ start: A, end: B, pass: 1, speedPercent: 50 }]);
  });

  it('observes on the shared frame loop only while playing with an enabled loop', async () => {
    const rig = makeRig();
    await rig.transport.play();
    expect(rig.frames.size).toBe(0);
    rig.transport.setLoop({ start: A, end: B, enabled: true });
    rig.tracker.onLoopChanged();
    expect(rig.frames.size).toBe(1);
    rig.ctx.currentTime = 2.1;
    rig.frames.step(16);
    expect(rig.passes).toHaveLength(1);
    rig.transport.pause();
    rig.frames.step(32);
    expect(rig.frames.size).toBe(0);
  });

  it('dispose stops observing', async () => {
    const rig = await looping();
    rig.tracker.dispose();
    expect(rig.frames.size).toBe(0);
    rig.ctx.currentTime = 2.1;
    rig.frames.step(16);
    expect(rig.passes).toEqual([]);
  });
});

describe('engine OnLoopPass', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal('requestAnimationFrame', () => 0);
    vi.stubGlobal('cancelAnimationFrame', () => undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  /** Advances the audio clock and the fake timers together in 25 ms steps. */
  async function run(rig: PracticeRig, seconds: number): Promise<void> {
    for (let i = 0; i < Math.round(seconds / 0.025); i++) {
      rig.ctx.currentTime += 0.025;
      await vi.advanceTimersByTimeAsync(25);
    }
  }

  function passes(rig: PracticeRig): LoopPassInfo[] {
    return rig.ref.argsOf('OnLoopPass') as LoopPassInfo[];
  }

  it('fires once per pass on the scheduler tick (no animation frames) with region and speed', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.play();
    await run(rig, 4.2);
    expect(passes(rig)).toEqual([
      { start: A, end: B, pass: 1, speedPercent: 100 },
      { start: A, end: B, pass: 2, speedPercent: 100 },
    ]);
  });

  it('reports the heard wrap: output latency delays the call', async () => {
    const rig = await loadedPracticeRig();
    rig.ctx.outputLatency = 0.2;
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.play();
    await run(rig, 2.2);
    expect(passes(rig)).toEqual([]);
    await run(rig, 0.1);
    expect(passes(rig)).toHaveLength(1);
  });

  it('reports the speed of each trainer pass (explicit passes)', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.setPractice(
      practice({ trainer: { enabled: true, startPercent: 50, stepPercent: 10, targetPercent: 70, passesPerStep: 1 } }),
    );
    await rig.engine.play();
    await run(rig, 4 + 2 / 0.6 + 2 / 0.7 + 0.5);
    expect(passes(rig).map((p) => [p.pass, p.speedPercent])).toEqual([
      [1, 50],
      [2, 60],
      [3, 70],
    ]);
  });

  it('at 50 % speed (stretch engaged) the pass is reported once with speed 50', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.setPractice(practice({ speedPercent: 50 }));
    await rig.engine.play();
    await run(rig, 4.6);
    expect(passes(rig)).toEqual([{ start: A, end: B, pass: 1, speedPercent: 50 }]);
  });

  it('a seek resets the count and skips the partial pass; a disabled loop never fires', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.play();
    await run(rig, 2.2);
    rig.engine.seek(3);
    await run(rig, 3.2);
    expect(passes(rig).map((p) => p.pass)).toEqual([1, 1]);
    rig.engine.setLoop({ start: A, end: B, enabled: false });
    await run(rig, 6);
    expect(passes(rig)).toHaveLength(2);
  });

  it('stop resets the count; loading a track resets it', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.play();
    await run(rig, 2.2);
    rig.engine.stop();
    await rig.engine.play();
    await run(rig, 2.2);
    expect(passes(rig).map((p) => p.pass)).toEqual([1, 1]);
  });
});

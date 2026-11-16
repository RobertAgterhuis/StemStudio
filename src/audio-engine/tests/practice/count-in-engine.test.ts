import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { BeatGrid, MetronomeSettings } from '../../src/types';
import type { MockAudioBufferSourceNode } from '../mocks/audio-context';
import { loadedPracticeRig, practice, running, statuses, type PracticeRig } from '../mocks/practice-rig';

/** Count-in in the engine (contract 2.3.0 "Count-in"; FR-012, FR-013, FR-018, SC-005). */
const GRID: BeatGrid = {
  bpm: 120,
  beats: Array.from({ length: 40 }, (_, i) => i * 0.5),
  downbeats: Array.from({ length: 10 }, (_, i) => i * 2),
  beatsPerBar: 4,
  confidence: 0.9,
  reliable: true,
};
const METRONOME: MetronomeSettings = { enabled: false, volume: 0.6, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 };

function clickSources(rig: PracticeRig): MockAudioBufferSourceNode[] {
  return rig.ctx.sources.filter((s) => s.buffer !== null && s.buffer.length < 44100 && s.startCalls.length > 0);
}

function liveClicks(rig: PracticeRig): number[] {
  return clickSources(rig)
    .filter((s) => s.stopCalls.length === 0)
    .map((s) => s.startCalls[0]?.when ?? Number.NaN);
}

function expectTimes(actual: readonly (number | undefined)[], expected: readonly number[]): void {
  expect(actual).toHaveLength(expected.length);
  expected.forEach((time, i) => {
    expect(actual[i]).toBeCloseTo(time, 9);
  });
}

async function run(rig: PracticeRig, seconds: number): Promise<void> {
  for (let i = 0; i < Math.round(seconds / 0.025); i++) {
    rig.ctx.currentTime += 0.025;
    await vi.advanceTimersByTimeAsync(25);
  }
}

async function countInRig(bars = 1): Promise<PracticeRig> {
  const rig = await loadedPracticeRig(GRID);
  rig.engine.setMetronome(METRONOME);
  await rig.engine.setPractice(practice({ countIn: { bars } }));
  return rig;
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

describe('count-in before play', () => {
  it('plays N clicks through the metronome output (also with the metronome off) and starts the music one beat after the last', async () => {
    const rig = await countInRig();
    await rig.engine.play();
    expect(rig.engine.getState()).toBe('playing');
    const clicks = clickSources(rig);
    expectTimes(clicks.map((s) => s.startCalls[0]?.when), [0.1, 0.6, 1.1, 1.6]);
    const output = clicks[0]?.connections[0] as unknown as { gain: { value: number }; connections: unknown[] };
    expect(output.gain.value).toBe(0.6);
    expect(output.connections).toEqual([rig.ctx.destination]);
    expect(clicks.map((s) => s.buffer?.getChannelData(0)[10] ?? 0).map((v, _i, all) => v === all[0])).toEqual([true, false, false, false]);
    running(rig).forEach((source) => {
      expect(source.startCalls[0]?.when).toBeCloseTo(1.6 + 0.5, 9);
    });
  });

  it('keeps the position at A until the music starts and counts the beats down', async () => {
    const rig = await countInRig();
    rig.engine.seek(4);
    await rig.engine.play();
    expect(rig.engine.getPracticeStatus().countIn).toEqual({ available: true, active: true, beatsRemaining: 4, beatsTotal: 4 });
    await run(rig, 1.2);
    expect(rig.engine.getPosition()).toBe(4);
    expect(rig.engine.getPracticeStatus().countIn.beatsRemaining).toBe(2);
    await run(rig, 1.2);
    expect(rig.engine.getPracticeStatus().countIn).toMatchObject({ active: false, beatsRemaining: 0 });
    expect(rig.engine.getPosition()).toBeGreaterThan(4);
    const remaining = statuses(rig).map((s) => s.countIn.beatsRemaining);
    [4, 3, 2, 1, 0].forEach((beat) => {
      expect(remaining).toContain(beat);
    });
  });

  it('pause during the count-in cancels the future clicks; the next play counts in again', async () => {
    const rig = await countInRig();
    await rig.engine.play();
    await run(rig, 0.3);
    rig.engine.pause();
    expectTimes(liveClicks(rig), [0.1]);
    expect(rig.engine.getPracticeStatus().countIn.active).toBe(false);
    await rig.engine.play();
    expect(liveClicks(rig)).toHaveLength(5);
  });

  it('a seek while playing never counts in; stop cancels a running count-in', async () => {
    const rig = await countInRig();
    await rig.engine.play();
    await run(rig, 2.5);
    const clicks = clickSources(rig).length;
    rig.engine.seek(8);
    expect(clickSources(rig)).toHaveLength(clicks);
    rig.engine.stop();
    await rig.engine.play();
    rig.engine.stop();
    expect(liveClicks(rig).filter((t) => t > rig.ctx.currentTime)).toEqual([]);
  });

  it('uses two bars, the rate and the heard latency of the engaged stage', async () => {
    const rig = await loadedPracticeRig(GRID);
    rig.engine.setMetronome(METRONOME);
    await rig.engine.setPractice(practice({ speedPercent: 50, countIn: { bars: 2 } }));
    await rig.engine.play();
    const times = clickSources(rig).map((s) => Number(s.startCalls[0]?.when));
    expect(times).toHaveLength(8);
    expect(Number(times[1]) - Number(times[0])).toBeCloseTo(1, 9);
    const music = Number(running(rig)[0]?.startCalls[0]?.when);
    const { latencyMs } = rig.engine.getPracticeStatus();
    expect(latencyMs).toBeGreaterThan(0);
    expect(music + latencyMs / 1000).toBeCloseTo(Number(times[7]) + 1, 6);
  });

  it('starts without a count-in when no tempo is known', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.setPractice(practice({ countIn: { bars: 1 } }));
    await rig.engine.play();
    expect(clickSources(rig)).toHaveLength(0);
    expect(rig.engine.getPracticeStatus().countIn).toMatchObject({ available: false, active: false });
  });

  it('still works when the time stretch is unavailable', async () => {
    const rig = await loadedPracticeRig(GRID);
    rig.engine.setMetronome(METRONOME);
    rig.failWith = new Error('Time stretch unavailable: x');
    await expect(rig.engine.setPractice(practice({ speedPercent: 60, countIn: { bars: 1 } }))).rejects.toThrow('x');
    await rig.engine.play();
    expect(clickSources(rig)).toHaveLength(4);
  });
});

describe('count-in before every pass', () => {
  it('leaves a gap of N beats between passes with clicks and position A in the gap', async () => {
    const rig = await loadedPracticeRig(GRID);
    rig.engine.setMetronome(METRONOME);
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.setPractice(practice({ countIn: { bars: 1, everyPass: true } }));
    await rig.engine.play();
    expectTimes(liveClicks(rig), [0.1, 0.6, 1.1, 1.6]);
    await run(rig, 4);
    const pass2 = running(rig).filter((s) => Math.abs((s.startCalls[0]?.when ?? 0) - 6.1) < 1e-9);
    expect(pass2).toHaveLength(2);
    expectTimes(liveClicks(rig).slice(4), [4.1, 4.6, 5.1, 5.6]);
    await run(rig, 0.5);
    expect(rig.engine.getPosition()).toBe(2);
    expect(rig.engine.getPracticeStatus().countIn.active).toBe(true);
  });

  it('takes the gap at the next pass rate of the trainer', async () => {
    const rig = await loadedPracticeRig(GRID);
    rig.engine.setMetronome(METRONOME);
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.setPractice(practice({ trainer: { enabled: true, startPercent: 50, stepPercent: 20, targetPercent: 70 }, countIn: { bars: 1, everyPass: true } }));
    await rig.engine.play();
    const first = running(rig)[0]?.startCalls[0]?.when ?? 0;
    await run(rig, first + 4);
    const second = running(rig).map((s) => s.startCalls[0]?.when ?? 0).filter((t) => t > first + 1);
    expect(second[0]).toBeCloseTo(first + 2 / 0.5 + (4 * 0.5) / 0.7, 9);
  });
});

describe('reports for the UI (frontend 012 requests)', () => {
  it('sends one OnPracticeChanged per count-in beat and a final one with active false', async () => {
    const rig = await countInRig();
    rig.ref.clear();
    await rig.engine.play();
    await run(rig, 2.5);
    const counts = statuses(rig).map((s) => s.countIn);
    const beats = counts.filter((c) => c.active).map((c) => c.beatsRemaining);
    expect([...new Set(beats)]).toEqual([4, 3, 2, 1]);
    expect(counts.at(-1)).toMatchObject({ active: false, beatsRemaining: 0 });
  });

  it('sends a status at every speed-trainer pass start', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.setPractice(practice({ trainer: { enabled: true, startPercent: 60, stepPercent: 10, targetPercent: 90 } }));
    rig.ref.clear();
    await rig.engine.play();
    await run(rig, 12);
    const passes = statuses(rig).map((s) => s.trainer.pass);
    // Passes at 60/70/80/90 % last 3.33 + 2.86 + 2.5 + 2.22 s, so pass 5 starts before 12 s.
    expect([...new Set(passes)]).toEqual([1, 2, 3, 4, 5]);
  });

  it('play while already playing changes nothing', async () => {
    const rig = await countInRig();
    await rig.engine.play();
    const clicks = clickSources(rig).length;
    await rig.engine.play();
    expect(clickSources(rig)).toHaveLength(clicks);
  });

  it('every-pass count-in without a tempo plays gapless passes', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.setPractice(practice({ countIn: { bars: 1, everyPass: true } }));
    await rig.engine.play();
    await run(rig, 2);
    expect(running(rig).filter((s) => Math.abs(Number(s.startCalls[0]?.when) - 2.1) < 1e-9)).toHaveLength(2);
    expect(clickSources(rig)).toHaveLength(0);
  });
});

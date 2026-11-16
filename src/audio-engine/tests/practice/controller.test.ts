import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { STRETCH_DELAY_FRAMES } from '../../src/stretch/constants';
import { contentOffsetFrames, heardLatencySeconds } from '../../src/stretch/params';
import type { PracticeSettings } from '../../src/types';
import {
  NEUTRAL,
  loadedPracticeRig,
  practice,
  rateSteps,
  running,
  stageOf,
  statuses,
  type PracticeRig,
} from '../mocks/practice-rig';

/** Practice controller over the fake stage (contract 2.3.0 "Practice tempo"; FR-001..FR-005, FR-007, FR-009, FR-018). */
const SR = 44100;
const D = STRETCH_DELAY_FRAMES / SR;

function L(p: number): number {
  return heardLatencySeconds(p, SR);
}

function masterGraph(rig: PracticeRig): unknown[] {
  return rig.ctx.gains[0]?.connections ?? [];
}

const flush = async (): Promise<void> => {
  await vi.advanceTimersByTimeAsync(0);
};

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('requestAnimationFrame', () => 0);
  vi.stubGlobal('cancelAnimationFrame', () => undefined);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('validation', () => {
  it.each<[string, PracticeSettings, string]>([
    ['speed below range', practice({ speedPercent: 49 }), 'speedPercent must be an integer 50..120 (got 49)'],
    ['speed with a fraction', practice({ speedPercent: 75.5 }), 'speedPercent must be an integer 50..120 (got 75.5)'],
    ['transpose above range', practice({ transposeSemitones: 13 }), 'transposeSemitones must be an integer -12..12 (got 13)'],
    ['trainer start', practice({ trainer: { startPercent: 120 } }), 'trainer.startPercent must be an integer 50..119 (got 120)'],
    ['trainer step', practice({ trainer: { stepPercent: 0 } }), 'trainer.stepPercent must be an integer 1..20 (got 0)'],
    ['trainer target', practice({ trainer: { targetPercent: 121 } }), 'trainer.targetPercent must be an integer 51..120 (got 121)'],
    ['passes per step', practice({ trainer: { passesPerStep: 9 } }), 'trainer.passesPerStep must be an integer 1..8 (got 9)'],
    ['target not above start', practice({ trainer: { startPercent: 90, targetPercent: 90 } }), 'trainer.targetPercent must be above the start speed (start 90, target 90)'],
    ['count-in bars', practice({ countIn: { bars: 3 } }), 'countIn.bars must be an integer 0..2 (got 3)'],
    ['trainer flag', practice({ trainer: { enabled: 'yes' as unknown as boolean } }), 'trainer.enabled must be a boolean (got yes)'],
    ['every-pass flag', practice({ countIn: { everyPass: 1 as unknown as boolean } }), 'countIn.everyPass must be a boolean (got 1)'],
    ['missing trainer', { ...NEUTRAL, trainer: undefined as unknown as PracticeSettings['trainer'] }, 'trainer must be an object'],
    ['missing count-in', { ...NEUTRAL, countIn: null as unknown as PracticeSettings['countIn'] }, 'countIn must be an object'],
  ])('rejects %s and changes nothing', async (_name, settings, reason) => {
    const rig = await loadedPracticeRig();
    await expect(rig.engine.setPractice(settings)).rejects.toThrow(`Invalid practice settings: ${reason}`);
    expect(rig.calls).toBe(0);
    expect(rig.engine.getPracticeStatus()).toMatchObject({ speedPercent: 100, transposeSemitones: 0, stretch: 'bypass' });
    expect(statuses(rig)).toEqual([]);
  });
});

describe('neutral settings (FR-007, SC-007)', () => {
  it('loads nothing and keeps the 2.1.0 graph and starts', async () => {
    const rig = await loadedPracticeRig();
    const master = masterGraph(rig);
    await rig.engine.setPractice(practice({ countIn: { bars: 1 } }));
    await rig.engine.play();
    expect(rig.calls).toBe(0);
    expect(masterGraph(rig)).toEqual(master);
    expect(running(rig).map((s) => s.startCalls[0])).toEqual([
      { when: 0.1, offset: 0 },
      { when: 0.1, offset: 0 },
    ]);
    expect(rig.engine.getPracticeStatus()).toEqual({
      speedPercent: 100,
      transposeSemitones: 0,
      stretch: 'bypass',
      stretchReason: null,
      latencyMs: 0,
      underruns: 0,
      trainer: { state: 'off', pass: 0, speedPercent: 70 },
      countIn: { available: false, active: false, beatsRemaining: 0, beatsTotal: 4 },
    });
  });
});

describe('engaging the stage', () => {
  it('while stopped: loading -> active, stage inserted before the master meter, nothing started', async () => {
    const rig = await loadedPracticeRig();
    let release: () => void = () => undefined;
    rig.gate = new Promise((resolve) => {
      release = resolve;
    });
    const pending = rig.engine.setPractice(practice({ speedPercent: 50 }));
    await flush();
    expect(rig.engine.getPracticeStatus().stretch).toBe('loading');
    release();
    await pending;
    const stage = stageOf(rig);
    expect(rig.engine.getPracticeStatus()).toMatchObject({ stretch: 'active', speedPercent: 50, latencyMs: L(2) * 1000 });
    expect(masterGraph(rig)).toEqual([stage.input]);
    expect((stage.input as unknown as { connections: unknown[] }).connections).toEqual([rig.ctx.analysers[0]]);
    expect(running(rig)).toEqual([]);
    expect(statuses(rig).map((s) => s.stretch)).toContain('loading');
    await vi.advanceTimersByTimeAsync(200);
    expect(statuses(rig).at(-1)?.stretch).toBe('active');
  });

  it('a later play starts every source L(p) before the heard when at rate r and resets the stage first', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.setPractice(practice({ speedPercent: 50 }));
    rig.ctx.currentTime = 1;
    await rig.engine.play();
    const stage = stageOf(rig);
    expect(stage.resets).toBe(1);
    expect(stage.pitches).toEqual([{ factor: 2, atFrame: 0 }]);
    running(rig).forEach((source) => {
      expect(source.startCalls[0]?.when).toBeCloseTo(1.1, 9);
      expect(source.playbackRate.value).toBe(0.5);
    });
    rig.ctx.currentTime = 1.1 + L(2) + 2;
    expect(rig.engine.getPosition()).toBeCloseTo(1, 9);
  });

  it('while playing: restarts all sources at the heard position on the new anchor', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.play();
    rig.ctx.currentTime = 4.1;
    await rig.engine.setPractice(practice({ transposeSemitones: 12 }));
    const fresh = running(rig);
    expect(fresh).toHaveLength(2);
    fresh.forEach((source) => {
      expect(source.startCalls[0]?.when).toBeCloseTo(4.2, 9);
      expect(source.startCalls[0]?.offset).toBeCloseTo(4, 9);
      expect(source.playbackRate.value).toBe(1);
    });
    expect(stageOf(rig).pitches).toEqual([{ factor: 2, atFrame: 0 }]);
    rig.ctx.currentTime = 4.2 + L(2);
    expect(rig.engine.getPosition()).toBeCloseTo(4, 9);
  });
});

describe('live changes while engaged (no gap)', () => {
  async function playingAt50(): Promise<PracticeRig> {
    const rig = await loadedPracticeRig();
    await rig.engine.setExpanded('drums', true);
    await rig.engine.setPractice(practice({ speedPercent: 50 }));
    await rig.engine.play();
    rig.ctx.currentTime = 3;
    return rig;
  }

  it('automates every source incl. detail sources at one U, sends the pitch at frame(U) and re-anchors at T', async () => {
    const rig = await playingAt50();
    const before = rig.engine.getPosition();
    await rig.engine.setPractice(practice({ speedPercent: 80 }));
    const U = 3.02;
    const sources = running(rig);
    expect(sources).toHaveLength(4);
    sources.forEach((source) => {
      expect(rateSteps(source)).toEqual([[0.8, U]]);
    });
    expect(stageOf(rig).pitches.at(-1)).toEqual({ factor: 1.25, atFrame: Math.round(U * SR) });
    const T = U + D;
    const newWhen = T + contentOffsetFrames(1.25) / SR;
    const oldPosition = (t: number): number => before + (t - 3) * 0.5;
    expect(rig.engine.getPosition()).toBeCloseTo(before, 9);
    rig.ctx.currentTime = newWhen + 1;
    expect(rig.engine.getPosition()).toBeCloseTo(oldPosition(T + contentOffsetFrames(2) / SR) + 0.8, 6);
    expect(rig.engine.getPracticeStatus()).toMatchObject({ speedPercent: 80, latencyMs: L(1.25) * 1000 });
  });

  it('a change arriving before U replaces the pending one at the same U', async () => {
    const rig = await playingAt50();
    await rig.engine.setPractice(practice({ speedPercent: 80 }));
    rig.ctx.currentTime = 3.01;
    await rig.engine.setPractice(practice({ speedPercent: 90 }));
    const [source] = running(rig);
    expect(source?.playbackRate.calls.map((c) => [c.method, ...c.args])).toEqual([
      ['setValueAtTime', 0.8, 3.02],
      ['cancelScheduledValues', 3.02],
      ['setValueAtTime', 0.9, 3.02],
    ]);
    expect(stageOf(rig).pitches.slice(-2).map((p) => p.atFrame)).toEqual([Math.round(3.02 * SR), Math.round(3.02 * SR)]);
    rig.ctx.currentTime = 6;
    const position = rig.engine.getPosition();
    rig.ctx.currentTime = 7;
    expect(rig.engine.getPosition() - position).toBeCloseTo(0.9, 9);
  });

  it('returning to neutral keeps the stage engaged (pitch 1, no restart); the next restart bypasses it', async () => {
    const rig = await playingAt50();
    const started = rig.ctx.startedSources().length;
    await rig.engine.setPractice(NEUTRAL);
    expect(rig.ctx.startedSources()).toHaveLength(started);
    expect(stageOf(rig).pitches.at(-1)?.factor).toBe(1);
    expect(rig.engine.getPracticeStatus()).toMatchObject({ stretch: 'active', speedPercent: 100, latencyMs: L(1) * 1000 });
    rig.engine.pause();
    await rig.engine.play();
    expect(rig.engine.getPracticeStatus()).toMatchObject({ stretch: 'bypass', latencyMs: 0 });
    expect(masterGraph(rig)).toEqual([rig.ctx.analysers[0]]);
    running(rig).forEach((source) => {
      expect(source.startCalls[0]?.when).toBeCloseTo(3.1, 9);
      expect(source.playbackRate.value).toBe(1);
    });
  });

  it('a transpose change while paused applies at the next play without loading again', async () => {
    const rig = await playingAt50();
    rig.engine.pause();
    expect(stageOf(rig).resets).toBe(2);
    await rig.engine.setPractice(practice({ speedPercent: 50, transposeSemitones: -12 }));
    await rig.engine.play();
    expect(rig.calls).toBe(1);
    expect(stageOf(rig).pitches.at(-1)).toEqual({ factor: 1, atFrame: 0 });
  });

  it('keeps all sources sample-locked after 20 changes and 50 loop wraps', async () => {
    const rig = await playingAt50();
    rig.engine.setLoop({ start: 1, end: 1.5, enabled: true });
    for (let i = 0; i < 20; i++) {
      rig.ctx.currentTime += 0.1;
      await rig.engine.setPractice(practice({ speedPercent: 50 + 3 * i }));
    }
    rig.ctx.currentTime += 50 * (0.5 / 1.07);
    const sources = running(rig);
    const signature = (s: (typeof sources)[number]): unknown => [s.startCalls, rateSteps(s), s.loop, s.loopStart, s.loopEnd];
    sources.forEach((source) => {
      expect(signature(source)).toEqual(signature(sources[0] ?? source));
    });
    const position = rig.engine.getPosition();
    expect(position).toBeGreaterThanOrEqual(1);
    expect(position).toBeLessThan(1.5);
  });
});

describe('time stretch unavailable (FR-009)', () => {
  it('rejects, keeps 100 % and 0 st, applies the count-in and keeps playing', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.play();
    rig.failWith = new Error('Time stretch unavailable: AudioWorklet is not supported');
    const started = rig.ctx.startedSources().length;
    await expect(rig.engine.setPractice(practice({ speedPercent: 60, countIn: { bars: 2 } }))).rejects.toThrow(
      'Time stretch unavailable: AudioWorklet is not supported',
    );
    expect(rig.ctx.startedSources()).toHaveLength(started);
    expect(rig.engine.getState()).toBe('playing');
    expect(rig.engine.getPracticeStatus()).toMatchObject({
      speedPercent: 100,
      transposeSemitones: 0,
      stretch: 'unavailable',
      stretchReason: 'AudioWorklet is not supported',
      countIn: { beatsTotal: 8 },
    });
  });

  it('retries the load once per loadTrack only', async () => {
    const rig = await loadedPracticeRig();
    rig.failWith = new Error('Time stretch unavailable: x');
    await expect(rig.engine.setPractice(practice({ speedPercent: 60 }))).rejects.toThrow('Time stretch unavailable: x');
    rig.failWith = null;
    await expect(rig.engine.setPractice(practice({ speedPercent: 60 }))).rejects.toThrow('Time stretch unavailable: x');
    expect(rig.calls).toBe(1);
    await rig.engine.setPractice(NEUTRAL);
    await rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
    await rig.engine.setPractice(practice({ speedPercent: 60 }));
    expect(rig.calls).toBe(2);
    expect(rig.engine.getPracticeStatus().stretch).toBe('active');
  });

  it('a processor failure after ready bypasses the stage and restarts at the heard position', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.setPractice(practice({ speedPercent: 50 }));
    await rig.engine.play();
    rig.ctx.currentTime = 2;
    const heard = rig.engine.getPosition();
    stageOf(rig).fail('trap');
    expect(rig.engine.getPracticeStatus()).toMatchObject({ stretch: 'unavailable', stretchReason: 'trap', speedPercent: 100 });
    expect(stageOf(rig).disposed).toBe(true);
    expect(masterGraph(rig)).toEqual([rig.ctx.analysers[0]]);
    running(rig).forEach((source) => {
      expect(source.startCalls[0]?.when).toBeCloseTo(2.1, 9);
      expect(source.startCalls[0]?.offset).toBeCloseTo(heard, 9);
      expect(source.playbackRate.value).toBe(1);
    });
  });

  it('a non-error failure gives a readable reason', async () => {
    const rig = await loadedPracticeRig();
    rig.failWith = 'odd' as unknown as Error;
    await expect(rig.engine.setPractice(practice({ speedPercent: 60 }))).rejects.toThrow('Time stretch unavailable: odd');
  });
});

describe('status reports (OnPracticeChanged at most 10 Hz)', () => {
  it('coalesces bursts into one immediate and one trailing report', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.setPractice(practice({ speedPercent: 90 }));
    const count = statuses(rig).length;
    for (const speed of [91, 92, 93, 94]) {
      await rig.engine.setPractice(practice({ speedPercent: speed }));
    }
    expect(statuses(rig).length).toBeLessThanOrEqual(count + 1);
    await vi.advanceTimersByTimeAsync(150);
    expect(statuses(rig).at(-1)?.speedPercent).toBe(94);
    const total = statuses(rig).length;
    await vi.advanceTimersByTimeAsync(1000);
    expect(statuses(rig)).toHaveLength(total);
  });

  it('reports underrun bursts while playing, counted since loadTrack', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.setPractice(practice({ speedPercent: 90 }));
    stageOf(rig).underrunCount = 3;
    await rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
    await rig.engine.play();
    await vi.advanceTimersByTimeAsync(300);
    stageOf(rig).underrunCount = 8;
    await vi.advanceTimersByTimeAsync(300);
    expect(statuses(rig).at(-1)?.underruns).toBe(5);
    expect(rig.engine.getPracticeStatus().underruns).toBe(5);
  });

  it('dispose frees the stage and stops reporting', async () => {
    const rig = await loadedPracticeRig();
    await rig.engine.setPractice(practice({ speedPercent: 90 }));
    await rig.engine.play();
    await rig.engine.dispose();
    expect(stageOf(rig).disposed).toBe(true);
    const total = statuses(rig).length;
    await vi.advanceTimersByTimeAsync(1000);
    expect(statuses(rig)).toHaveLength(total);
  });
});

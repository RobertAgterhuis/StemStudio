import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MIN_PASS_SECONDS, PASS_LOOKAHEAD_SECONDS } from '../../src/practice/passes';
import { heardLatencySeconds, pitchFactor } from '../../src/stretch/params';
import type { MockAudioBufferSourceNode } from '../mocks/audio-context';
import { loadedPracticeRig, practice, running, stageOf, statuses, type PracticeRig } from '../mocks/practice-rig';

/** Explicit loop passes of the speed trainer (contract 2.3.0 "Explicit passes", FR-010, FR-011, SC-006). */
const SR = 44100;
const A = 2;
const B = 4;
const TRAINER = practice({ trainer: { enabled: true, startPercent: 50, stepPercent: 10, targetPercent: 70, passesPerStep: 1 } });

function L(speed: number, semitones = 0): number {
  return heardLatencySeconds(pitchFactor(speed, semitones), SR);
}

/** Track sources (not clicks), in creation order. */
function trackSources(rig: PracticeRig): MockAudioBufferSourceNode[] {
  return rig.ctx.sources.filter((s) => s.startCalls.length > 0 && s.buffer !== null && s.buffer.length > 44100);
}

/** Sources started at source time `when` (one pass), with their start calls. */
function passAt(rig: PracticeRig, when: number): MockAudioBufferSourceNode[] {
  return trackSources(rig).filter((s) => Math.abs((s.startCalls[0]?.when ?? -1) - when) < 1e-9);
}

/** Advances the audio clock and the fake timers together in 25 ms steps. */
async function run(rig: PracticeRig, seconds: number): Promise<void> {
  for (let i = 0; i < Math.round(seconds / 0.025); i++) {
    rig.ctx.currentTime += 0.025;
    await vi.advanceTimersByTimeAsync(25);
  }
}

async function trainerRig(settings = TRAINER): Promise<PracticeRig> {
  const rig = await loadedPracticeRig();
  rig.engine.setLoop({ start: A, end: B, enabled: true });
  await rig.engine.setPractice(settings);
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

describe('conditions', () => {
  it('uses explicit passes only with an enabled loop of at least 0.5 s', async () => {
    expect(MIN_PASS_SECONDS).toBe(0.5);
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: A + 0.4, enabled: true });
    await rig.engine.setPractice(TRAINER);
    await rig.engine.play();
    expect(rig.engine.getPracticeStatus().trainer.state).toBe('off');
    const [source] = running(rig);
    expect([source?.loop, source?.startCalls[0]?.duration]).toEqual([true, undefined]);
  });

  it('is waiting before play and running from the first pass', async () => {
    const rig = await trainerRig();
    expect(rig.engine.getPracticeStatus().trainer).toEqual({ state: 'waiting', pass: 0, speedPercent: 50 });
    await rig.engine.play();
    expect(rig.engine.getPracticeStatus()).toMatchObject({ speedPercent: 50, trainer: { state: 'running', pass: 1, speedPercent: 50 } });
  });
});

describe('pass scheduling', () => {
  it('starts pass 1 at A with duration B - A, all sources on one time and rate', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    const pass1 = passAt(rig, 0.1);
    expect(pass1).toHaveLength(2);
    pass1.forEach((source) => {
      expect(source.startCalls).toEqual([{ when: 0.1, offset: A, duration: B - A }]);
      expect(source.loop).toBe(false);
      expect(source.playbackRate.value).toBe(0.5);
    });
    rig.ctx.currentTime = 0.1 + L(50) + 1;
    expect(rig.engine.getPosition()).toBeCloseTo(A + 0.5, 9);
  });

  it('creates the next pass at least 0.3 s ahead, gapless, at the trainer speed, with the pitch at its first frame', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    const end1 = 0.1 + (B - A) / 0.5;
    await run(rig, end1 - PASS_LOOKAHEAD_SECONDS - 0.05);
    expect(passAt(rig, end1)).toHaveLength(0);
    await run(rig, 0.1);
    expect(rig.ctx.currentTime).toBeLessThanOrEqual(end1 - 0.3);
    const pass2 = passAt(rig, end1);
    expect(pass2).toHaveLength(2);
    pass2.forEach((source) => {
      expect(source.startCalls).toEqual([{ when: end1, offset: A, duration: B - A }]);
      expect(source.playbackRate.value).toBe(0.6);
    });
    expect(stageOf(rig).pitches.at(-1)).toEqual({ factor: pitchFactor(60, 0), atFrame: Math.round(end1 * SR) });
    expect(stageOf(rig).resets).toBe(1);
  });

  it('steps the speed exactly at each boundary up to the target and reports every pass', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    const end1 = 0.1 + 2 / 0.5;
    const end2 = end1 + 2 / 0.6;
    const end3 = end2 + 2 / 0.7;
    await run(rig, end3 + 0.2);
    expect([0.1, end1, end2, end3].map((t) => passAt(rig, t)[0]?.playbackRate.value)).toEqual([0.5, 0.6, 0.7, 0.7]);
    expect(rig.engine.getPracticeStatus()).toMatchObject({ speedPercent: 70, trainer: { state: 'target', pass: 4 } });
    const passes = statuses(rig).map((s) => s.trainer.pass);
    expect(new Set(passes)).toEqual(new Set([0, 1, 2, 3, 4]));
  });

  it('maps the heard position through the pass anchors (A inside every pass)', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    const end1 = 0.1 + 2 / 0.5;
    await run(rig, end1 + 1);
    const heard2 = end1 + L(60);
    expect(rig.engine.getPosition()).toBeCloseTo(A + (rig.ctx.currentTime - heard2) * 0.6, 6);
  });

  it('starts the first pass at the current position when it is inside [A, B)', async () => {
    const rig = await trainerRig();
    rig.engine.seek(3);
    await rig.engine.play();
    expect(passAt(rig, 0.1).map((s) => s.startCalls[0])).toEqual([
      { when: 0.1, offset: 3, duration: 1 },
      { when: 0.1, offset: 3, duration: 1 },
    ]);
    await run(rig, 0.1 + 1 / 0.5 + 0.1);
    expect(passAt(rig, 0.1 + 2)[0]?.startCalls[0]).toEqual({ when: 2.1, offset: A, duration: 2 });
  });

  it('starts a detail group decoded mid-pass on the pass anchor and in every later pass', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 1);
    await rig.engine.setExpanded('drums', true);
    const at = rig.ctx.currentTime + 0.01;
    const late = passAt(rig, at);
    expect(late).toHaveLength(2);
    late.forEach((source) => {
      expect(source.startCalls[0]?.offset).toBeCloseTo(A + (at - 0.1) * 0.5, 9);
      expect(source.startCalls[0]?.duration).toBeCloseTo(B - (A + (at - 0.1) * 0.5), 9);
    });
    await run(rig, 3.5);
    expect(passAt(rig, 4.1)).toHaveLength(4);
  });
});

describe('trainer lifecycle', () => {
  it('pause keeps pass and speed; play resumes them from the paused position', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 5);
    rig.engine.pause();
    expect(rig.engine.getPracticeStatus().trainer).toEqual({ state: 'waiting', pass: 2, speedPercent: 60 });
    await rig.engine.play();
    expect(running(rig)[0]?.playbackRate.value).toBe(0.6);
  });

  it('pausing just before a boundary does not count the unheard pass', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 4.1 - 0.2);
    expect(passAt(rig, 4.1)).toHaveLength(2);
    rig.engine.pause();
    expect(rig.engine.getPracticeStatus().trainer).toEqual({ state: 'waiting', pass: 1, speedPercent: 50 });
  });

  it('stop and a change of the loop start/end reset the trainer to pass 1', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 5);
    rig.engine.stop();
    expect(rig.engine.getPracticeStatus().trainer).toEqual({ state: 'waiting', pass: 0, speedPercent: 50 });
    await rig.engine.play();
    await run(rig, 5);
    rig.engine.setLoop({ start: A, end: 5, enabled: true });
    expect(rig.engine.getPracticeStatus().trainer).toMatchObject({ state: 'running', pass: 1, speedPercent: 50 });
    const ends = running(rig).map((s) => (s.startCalls[0]?.offset ?? 0) + (s.startCalls[0]?.duration ?? 0));
    ends.forEach((end) => {
      expect(end).toBeCloseTo(5, 9);
    });
  });

  it('switches back to the native loop at the next boundary when the trainer is disabled', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 1);
    await rig.engine.setPractice(practice({ speedPercent: 80 }));
    expect(rig.engine.getPracticeStatus().trainer.state).toBe('off');
    await run(rig, 3.5);
    const continuation = passAt(rig, 4.1);
    expect(continuation).toHaveLength(2);
    continuation.forEach((source) => {
      expect(source.startCalls).toEqual([{ when: 4.1, offset: A }]);
      expect([source.loop, source.loopStart, source.loopEnd, source.playbackRate.value]).toEqual([true, A, B, 0.8]);
    });
    await run(rig, 3);
    expect(trackSources(rig).filter((s) => (s.startCalls[0]?.when ?? 0) > 4.2)).toHaveLength(0);
  });

  it('disabling the loop leaves the passes at once (restart on the native path)', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 1);
    rig.engine.setLoop({ start: A, end: B, enabled: false });
    expect(rig.engine.getPracticeStatus().trainer.state).toBe('off');
    expect(running(rig).every((s) => s.startCalls[0]?.duration === undefined && !s.loop)).toBe(true);
  });

  it('enabling the trainer while playing starts pass 1 at once', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.play();
    rig.ctx.currentTime = 1;
    await rig.engine.setPractice(TRAINER);
    expect(running(rig).every((s) => s.startCalls[0]?.duration !== undefined)).toBe(true);
    expect(rig.engine.getPracticeStatus().trainer).toMatchObject({ state: 'running', pass: 1 });
  });

  it('applies a transpose change live during a pass and to the next pass', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 1);
    const sourcesBefore = trackSources(rig).length;
    await rig.engine.setPractice({ ...TRAINER, transposeSemitones: 2 });
    expect(trackSources(rig)).toHaveLength(sourcesBefore);
    const U = rig.ctx.currentTime + 0.02;
    expect(stageOf(rig).pitches.at(-1)).toEqual({ factor: pitchFactor(50, 2), atFrame: Math.round(U * SR) });
    await run(rig, 3);
    expect(stageOf(rig).pitches.at(-1)).toEqual({ factor: pitchFactor(60, 2), atFrame: Math.round(4.1 * SR) });
    expect(passAt(rig, 4.1)[0]?.playbackRate.value).toBe(0.6);
  });

  it('a transpose change after the next pass was created re-sends its pitch', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 3.8);
    expect(passAt(rig, 4.1)).toHaveLength(2);
    await rig.engine.setPractice({ ...TRAINER, transposeSemitones: -3 });
    expect(stageOf(rig).pitches.slice(-2)).toEqual([
      { factor: pitchFactor(50, -3), atFrame: Math.round((rig.ctx.currentTime + 0.02) * SR) },
      { factor: pitchFactor(60, -3), atFrame: Math.round(4.1 * SR) },
    ]);
  });

  it('leaving pass mode while stopped needs no restart', async () => {
    const rig = await trainerRig();
    await rig.engine.setPractice(practice({ speedPercent: 90 }));
    await rig.engine.play();
    expect(running(rig).every((s) => s.loop && s.startCalls[0]?.duration === undefined)).toBe(true);
  });
});

describe('pass edge cases', () => {
  it('a transpose change before the first pass starts applies to it', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await rig.engine.setPractice({ ...TRAINER, transposeSemitones: 1 });
    expect(stageOf(rig).pitches.at(-1)).toEqual({ factor: pitchFactor(50, 1), atFrame: Math.round(0.02 * SR) });
  });

  it('moving the loop away from the position restarts the passes at the new A', async () => {
    const rig = await trainerRig();
    await rig.engine.play();
    await run(rig, 1);
    rig.engine.setLoop({ start: 6, end: 8, enabled: true });
    running(rig).forEach((source) => {
      expect(source.startCalls[0]).toMatchObject({ offset: 6, duration: 2 });
    });
  });

  it('enabling the trainer while the stage is already engaged restarts once into pass 1', async () => {
    const rig = await loadedPracticeRig();
    rig.engine.setLoop({ start: A, end: B, enabled: true });
    await rig.engine.setPractice(practice({ speedPercent: 80 }));
    await rig.engine.play();
    rig.ctx.currentTime = 2;
    const resets = stageOf(rig).resets;
    await rig.engine.setPractice({ ...TRAINER, speedPercent: 80 });
    expect(stageOf(rig).resets).toBe(resets + 1);
    expect(running(rig).every((s) => s.playbackRate.value === 0.5 && s.startCalls[0]?.duration !== undefined)).toBe(true);
  });
});

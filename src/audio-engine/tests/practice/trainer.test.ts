import { describe, expect, it } from 'vitest';
import { SpeedTrainer } from '../../src/practice/trainer';
import type { SpeedTrainerSettings } from '../../src/types';

/** Speed trainer state machine (data-model 3.2, FR-010). */
const SETTINGS: SpeedTrainerSettings = { enabled: true, startPercent: 70, stepPercent: 10, targetPercent: 95, passesPerStep: 1 };

function trainer(partial: Partial<SpeedTrainerSettings> = {}, usable = true): SpeedTrainer {
  const subject = new SpeedTrainer();
  subject.configure({ ...SETTINGS, ...partial });
  subject.setUsable(usable);
  return subject;
}

describe('SpeedTrainer', () => {
  it('is off when disabled or without a usable loop, waiting when enabled and not playing', () => {
    expect(trainer({ enabled: false }).snapshot()).toEqual({ state: 'off', pass: 0, speedPercent: 70 });
    expect(trainer({}, false).snapshot().state).toBe('off');
    expect(trainer().snapshot()).toEqual({ state: 'waiting', pass: 0, speedPercent: 70 });
    expect(trainer().active).toBe(true);
    expect(trainer({}, false).active).toBe(false);
  });

  it('plays pass 1 at the start speed and steps after every completed pass, capped at the target', () => {
    const subject = trainer();
    subject.start();
    expect(subject.snapshot()).toEqual({ state: 'running', pass: 1, speedPercent: 70 });
    expect(subject.completePass()).toBe(80);
    expect(subject.snapshot()).toEqual({ state: 'running', pass: 2, speedPercent: 80 });
    expect(subject.completePass()).toBe(90);
    expect(subject.completePass()).toBe(95);
    expect(subject.snapshot()).toEqual({ state: 'target', pass: 4, speedPercent: 95 });
    expect(subject.completePass()).toBe(95);
    expect(subject.snapshot().state).toBe('target');
  });

  it('repeats each speed for passesPerStep completed passes', () => {
    const subject = trainer({ passesPerStep: 3 });
    subject.start();
    expect([subject.completePass(), subject.completePass(), subject.completePass(), subject.completePass()]).toEqual([70, 70, 80, 80]);
  });

  it('pause keeps pass and speed (waiting), play resumes them', () => {
    const subject = trainer();
    subject.start();
    subject.completePass();
    subject.pause();
    expect(subject.snapshot()).toEqual({ state: 'waiting', pass: 2, speedPercent: 80 });
    subject.start();
    expect(subject.snapshot()).toEqual({ state: 'running', pass: 2, speedPercent: 80 });
  });

  it('reset (stop, loadTrack, loop change) returns to pass 1 at the start speed', () => {
    const subject = trainer();
    subject.start();
    subject.completePass();
    subject.reset();
    expect(subject.snapshot()).toEqual({ state: 'waiting', pass: 0, speedPercent: 70 });
    subject.start();
    expect(subject.snapshot().pass).toBe(1);
  });

  it('a change of any trainer field resets; the same settings again do not', () => {
    const subject = trainer();
    subject.start();
    subject.completePass();
    subject.configure({ ...SETTINGS });
    expect(subject.snapshot().pass).toBe(2);
    subject.configure({ ...SETTINGS, stepPercent: 5 });
    expect(subject.snapshot()).toEqual({ state: 'running', pass: 1, speedPercent: 70 });
  });

  it('losing the loop turns it off and resets; getting it back waits', () => {
    const subject = trainer();
    subject.start();
    subject.completePass();
    subject.setUsable(false);
    expect(subject.snapshot()).toEqual({ state: 'off', pass: 0, speedPercent: 70 });
    subject.setUsable(true);
    expect(subject.snapshot().state).toBe('waiting');
  });

  it('save/restore undoes a step taken for a pass that was never heard', () => {
    const subject = trainer();
    subject.start();
    const saved = subject.save();
    subject.completePass();
    subject.restore(saved);
    expect(subject.snapshot()).toEqual({ state: 'running', pass: 1, speedPercent: 70 });
  });

  it('reaches the target with a step larger than the remaining distance', () => {
    const subject = trainer({ startPercent: 119, targetPercent: 120, stepPercent: 20 });
    subject.start();
    expect(subject.completePass()).toBe(120);
    expect(subject.snapshot().state).toBe('target');
    subject.pause();
    expect(subject.snapshot().state).toBe('waiting');
  });
});

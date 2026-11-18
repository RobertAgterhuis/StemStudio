import { describe, expect, it } from 'vitest';
import { UnitScorer } from '../../src/singing/scorer';
import type { SingingUnit, SingingUnitScore } from '../../src/types';

/** A4 (6900) from 0 to 3 s; units [0, 1000), [1000, 2000) and a 100 ms unit [2000, 2100). */
const REFERENCE = new Int16Array(300).fill(6900);
const UNITS: readonly SingingUnit[] = [
  { index: 0, startMs: 0, endMs: 1000 },
  { index: 1, startMs: 1000, endMs: 2000 },
  { index: 2, startMs: 2000, endMs: 2100 },
];

interface Rig {
  readonly scorer: UnitScorer;
  readonly scores: SingingUnitScore[];
  readonly state: { transpose: number };
}

function makeScorer(reference: Int16Array = REFERENCE, tolerance = 100): Rig {
  const scores: SingingUnitScore[] = [];
  const state = { transpose: 0 };
  const scorer = new UnitScorer({ reference, units: UNITS, toleranceCents: tolerance, transpose: () => state.transpose, onUnit: (s) => scores.push(s) });
  return { scorer, scores, state };
}

/** Sings `cents(ms)` every `step` ms from `from` to `to`. */
function sing(rig: Rig, range: { from: number; to: number; step?: number }, cents: (ms: number) => number | null): void {
  for (let ms = range.from; ms <= range.to; ms += range.step ?? 10) {
    rig.scorer.frame(ms, cents(ms));
  }
}

describe('UnitScorer (data-model 3.4)', () => {
  it('scores each unit once when it ends: 100 for an exact singer, null under 20 voiced reference frames', () => {
    const rig = makeScorer();
    sing(rig, { from: 0, to: 2200 }, () => 6900);
    expect(rig.scores).toEqual([
      { index: 0, score: 100, pass: 1 },
      { index: 1, score: 100, pass: 1 },
      { index: 2, score: null, pass: 1 },
    ]);
  });

  it('folds octaves: singing an octave up or down is a hit', () => {
    const rig = makeScorer();
    sing(rig, { from: 0, to: 1000 }, () => 8100);
    sing(rig, { from: 1010, to: 2000 }, () => 5700);
    expect(rig.scores.map((s) => s.score)).toEqual([100, 100]);
  });

  it('applies the tolerance (also when changed for later units)', () => {
    const rig = makeScorer(REFERENCE, 50);
    sing(rig, { from: 0, to: 1000 }, () => 6960);
    rig.scorer.setTolerance(100);
    sing(rig, { from: 1010, to: 2000 }, () => 6960);
    expect(rig.scores.map((s) => s.score)).toEqual([0, 100]);
  });

  it('follows the 012 transpose: the target moves by 100 cents per semitone', () => {
    const rig = makeScorer();
    rig.state.transpose = 2;
    sing(rig, { from: 0, to: 2000 }, (ms) => (ms < 1000 ? 7100 : 6900));
    expect(rig.scores.map((s) => s.score)).toEqual([100, 0]);
  });

  it('counts only voiced reference frames and misses where the singer is silent', () => {
    const reference = Int16Array.from({ length: 300 }, (_, i) => (i % 2 === 0 ? 6900 : 0));
    const rig = makeScorer(reference);
    sing(rig, { from: 0, to: 1000 }, (ms) => (ms < 500 ? 6900 : null));
    expect(rig.scores[0]?.score).toBe(50);
  });

  it('uses the nearest singer frame within 15 ms', () => {
    const rig = makeScorer();
    sing(rig, { from: 0, to: 1000, step: 40 }, () => 6900);
    sing(rig, { from: 1040, to: 1100, step: 40 }, () => 6900);
    expect(rig.scores[0]?.score).toBe(75);
  });

  it('scores a unit again on every loop pass (latest pass)', () => {
    const rig = makeScorer();
    sing(rig, { from: 0, to: 1000 }, () => 6900);
    sing(rig, { from: 0, to: 1000 }, () => 6000);
    expect(rig.scores).toEqual([
      { index: 0, score: 100, pass: 1 },
      { index: 0, score: 0, pass: 2 },
    ]);
    expect(rig.scorer.summary()).toEqual({ unitsScored: 1, average: 0, best: { index: 0, score: 0 }, toleranceCents: 100 });
  });

  it('discards the partial unit after a seek into it and starts again at pass 1', () => {
    const rig = makeScorer();
    sing(rig, { from: 0, to: 300 }, () => 6900);
    rig.scorer.reposition();
    sing(rig, { from: 500, to: 2000 }, () => 6900);
    expect(rig.scores).toEqual([{ index: 1, score: 100, pass: 1 }]);
  });

  it('arms a unit entered within 50 ms of its start (seek to a unit start)', () => {
    const rig = makeScorer();
    rig.scorer.reposition();
    sing(rig, { from: 1040, to: 2000 }, () => 6900);
    expect(rig.scores.map((s) => s.index)).toEqual([1]);
  });

  it('reports the current unit and the last score', () => {
    const rig = makeScorer();
    expect(rig.scorer.lastScore).toBeUndefined();
    sing(rig, { from: 0, to: 1500 }, () => 6900);
    expect(rig.scorer.unitAt(1500)).toBe(1);
    expect(rig.scorer.unitAt(2500)).toBe(-1);
    expect(rig.scorer.unitAt(-5)).toBe(-1);
    expect(rig.scorer.lastScore).toBe(100);
  });

  it('summarises: units with a score, rounded mean, best (first on ties)', () => {
    const rig = makeScorer();
    sing(rig, { from: 0, to: 2200 }, (ms) => (ms < 500 || (ms >= 1000 && ms < 2000) ? 6900 : null));
    expect(rig.scorer.summary()).toEqual({ unitsScored: 2, average: 75, best: { index: 1, score: 100 }, toleranceCents: 100 });
    expect(makeScorer().scorer.summary()).toEqual({ unitsScored: 0, average: null, best: null, toleranceCents: 100 });
  });

  it('ignores frames outside every unit and a reference shorter than the unit', () => {
    const rig = makeScorer(new Int16Array(50).fill(6900));
    sing(rig, { from: 0, to: 1000 }, () => 6900);
    expect(rig.scores).toEqual([{ index: 0, score: 100, pass: 1 }]);
    rig.scorer.reposition();
    sing(rig, { from: 2200, to: 2400 }, () => 6900);
    expect(rig.scores).toHaveLength(1);
  });
});

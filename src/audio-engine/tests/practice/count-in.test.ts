import { describe, expect, it } from 'vitest';
import { countInLead, countInPlan, countInTempo } from '../../src/practice/count-in';
import type { BeatGrid, MetronomeSettings } from '../../src/types';

/** Count-in plan (data-model 3.3, FR-012, FR-013, SC-005). */
const METRONOME: MetronomeSettings = { enabled: false, volume: 0.8, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 };

function grid(period = 0.5, first = 0.2, count = 40, reliable = true): BeatGrid {
  const beats = Array.from({ length: count }, (_, i) => first + i * period);
  return { bpm: 60 / period, beats, downbeats: beats.filter((_, i) => i % 4 === 0), beatsPerBar: 4, confidence: 0.9, reliable };
}

function times(plan: { time: number }[]): number[] {
  return plan.map((click) => Math.round(click.time * 1e6) / 1e6);
}

describe('count-in tempo', () => {
  it('uses the grid interval at the first click at or after A', () => {
    expect(countInTempo(grid(), METRONOME, 2.2)).toEqual({ interval: 0.5, firstBeat: 2.2 });
    const tempo = countInTempo(grid(), METRONOME, 2.3);
    expect(tempo?.interval).toBeCloseTo(0.5, 9);
    expect(tempo?.firstBeat).toBeCloseTo(2.7, 9);
  });

  it('follows a changing grid (interval at b) and the metronome offset', () => {
    const beats = [0, 0.5, 1, 1.6, 2.2, 2.8];
    const changing: BeatGrid = { bpm: 110, beats, downbeats: [0], beatsPerBar: 4, confidence: 0.9, reliable: true };
    const tempo = countInTempo(changing, { ...METRONOME, offsetMs: 100 }, 1.05);
    expect(tempo?.firstBeat).toBeCloseTo(1.1, 9);
    expect(tempo?.interval).toBeCloseTo(0.6, 9);
  });

  it('uses the last interval when A lies on or after the last beat', () => {
    const tempo = countInTempo(grid(0.5, 0, 4), METRONOME, 1.5);
    expect(tempo).toEqual({ interval: 0.5, firstBeat: 1.5 });
    expect(countInTempo(grid(0.5, 0, 4), METRONOME, 3)).toEqual({ interval: 0.5, firstBeat: 3 });
  });

  it('uses the manual BPM on the constant grid, or A without a grid', () => {
    const settings = { ...METRONOME, bpmOverride: 120 };
    const tempo = countInTempo(grid(0.6, 0.2), settings, 1);
    expect(tempo?.interval).toBe(0.5);
    expect(tempo?.firstBeat).toBeCloseTo(1.2, 9);
    expect(countInTempo(null, settings, 1.37)).toEqual({ interval: 0.5, firstBeat: 1.37 });
  });

  it('is unavailable without a manual BPM and a reliable grid of two beats', () => {
    expect(countInTempo(null, METRONOME, 0)).toBeNull();
    expect(countInTempo(grid(0.5, 0, 40, false), METRONOME, 0)).toBeNull();
    expect(countInTempo(grid(0.5, 0, 1), METRONOME, 0)).toBeNull();
  });
});

describe('count-in plan', () => {
  it('places N = bars x beatsPerBar clicks so the music enters exactly one beat after the last click (A on a beat)', () => {
    const plan = countInPlan({ tempo: { interval: 0.5, firstBeat: 2 }, start: 2, rate: 1, musicStart: 10, bars: 1, beatsPerBar: 4 });
    expect(times(plan)).toEqual([8, 8.5, 9, 9.5]);
    expect(plan.map((c) => c.accent)).toEqual([true, false, false, false]);
    expect(plan.map((c) => c.beat)).toEqual([4, 3, 2, 1]);
  });

  it('continues the grid into the music when A lies between beats', () => {
    const plan = countInPlan({ tempo: { interval: 0.5, firstBeat: 2.3 }, start: 2, rate: 1, musicStart: 10, bars: 1, beatsPerBar: 4 });
    expect(times(plan)).toEqual([8.3, 8.8, 9.3, 9.8]);
  });

  it.each([3, 4, 7])('accents beat 1 of each of 2 counted bars with %d beats per bar', (beatsPerBar) => {
    const plan = countInPlan({ tempo: { interval: 0.4, firstBeat: 0 }, start: 0, rate: 1, musicStart: 20, bars: 2, beatsPerBar });
    expect(plan).toHaveLength(2 * beatsPerBar);
    const accents = plan.flatMap((c, i) => (c.accent ? [i] : []));
    expect(accents).toEqual([0, beatsPerBar]);
  });

  it.each([0.5, 1.2])('divides the beat interval by the rate %f', (rate) => {
    const plan = countInPlan({ tempo: { interval: 0.5, firstBeat: 1.2 }, start: 1, rate, musicStart: 10, bars: 1, beatsPerBar: 2 });
    const d = 0.5 / rate;
    const b = 0.2 / rate;
    expect(plan[0]?.time).toBeCloseTo(10 + b - 2 * d, 9);
    expect(plan[1]?.time).toBeCloseTo(10 + b - d, 9);
  });

  it('is empty with bars = 0', () => {
    expect(countInPlan({ tempo: { interval: 0.5, firstBeat: 0 }, start: 0, rate: 1, musicStart: 5, bars: 0, beatsPerBar: 4 })).toEqual([]);
  });

  it('gives the lead from the first click to the music start', () => {
    expect(countInLead({ interval: 0.5, firstBeat: 2.3 }, 2, 0.5, 4)).toBeCloseTo((4 * 0.5 - 0.3) / 0.5, 9);
    expect(countInLead({ interval: 0.5, firstBeat: 2 }, 2, 1, 0)).toBe(0);
  });
});

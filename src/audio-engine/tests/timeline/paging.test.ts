import { describe, expect, it } from 'vitest';
import { BarScale, SecondScale, scaleFor } from '../../src/timeline/bars';
import { pageStart } from '../../src/timeline/paging';
import type { BeatGrid } from '../../src/types';

function grid(downbeats: number[], reliable = true): BeatGrid {
  return { bpm: 120, beats: downbeats, downbeats, beatsPerBar: 4, confidence: 0.9, reliable };
}

describe('pageStart (reduced-motion paging helper)', () => {
  it('returns the start of the page that holds the position', () => {
    expect(pageStart(0, 6000)).toBe(0);
    expect(pageStart(5999, 6000)).toBe(0);
    expect(pageStart(6000, 6000)).toBe(6000);
    expect(pageStart(13_500, 6000)).toBe(12_000);
    expect(pageStart(-1, 6000)).toBe(-6000);
  });

  it('pages from an origin', () => {
    expect(pageStart(2.4, 2, 0.5)).toBe(0.5);
    expect(pageStart(2.5, 2, 0.5)).toBe(2.5);
  });

  it('keeps the page for every position inside it (a page changes only at its boundary)', () => {
    const pages = new Set<number>();
    for (let t = 6000; t < 12_000; t += 7) {
      pages.add(pageStart(t, 6000));
    }
    expect([...pages]).toEqual([6000]);
  });
});

describe('BarScale (bar timeline from the beat grid)', () => {
  const scale = new BarScale([1000, 3000, 5000, 6000]);

  it('maps track ms to fractional bars and back, extrapolating outside the grid with the mean bar', () => {
    expect(scale.units(1000)).toBe(0);
    expect(scale.units(2000)).toBe(0.5);
    expect(scale.units(5500)).toBe(2.5);
    expect(scale.units(7000)).toBeCloseTo(3 + 1000 / (5000 / 3), 9);
    expect(scale.units(0)).toBeCloseTo(-1000 / (5000 / 3), 9);
    for (const ms of [0, 1500, 4200, 5000, 9000]) {
      expect(scale.ms(scale.units(ms))).toBeCloseTo(ms, 6);
    }
  });

  it('gives the bar start at or before a position', () => {
    expect(scale.barStart(4999)).toBe(3000);
    expect(scale.barStart(6100)).toBe(6000);
    expect(scale.barStart(500)).toBe(-(5000 / 3) + 1000);
  });
});

describe('scaleFor', () => {
  it('uses bars of a grid with at least two downbeats, else seconds', () => {
    expect(scaleFor(grid([0.5, 2.5]))).toBeInstanceOf(BarScale);
    expect(scaleFor(grid([0.5]))).toBeInstanceOf(SecondScale);
    expect(scaleFor(null)).toBeInstanceOf(SecondScale);
  });

  it('second scale: one unit per second', () => {
    const scale = new SecondScale();
    expect(scale.units(2500)).toBe(2.5);
    expect(scale.ms(3)).toBe(3000);
    expect(scale.barStart(2500)).toBe(2000);
  });
});

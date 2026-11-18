/**
 * Shared chord spelling vectors 1.0.0 (`specs/016-instrument-views/contracts/chord-spelling-vectors.json`, data-model
 * 2.5, 2.8, 2.9): the engine spells exactly like the backend (`ChordSpellingTests`).
 */
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { chordSymbol, keyAt, keyDisplay, keyName, noteName, usesFlats } from '../../src/chords/spelling';
import type { ChordChartData, ChordQuality, KeyMode } from '../../src/types';

interface SpellingCase {
  readonly root: number | null;
  readonly quality: ChordQuality | null;
  readonly bass: number | null;
  readonly keyTonic: number | null;
  readonly keyMode: KeyMode | null;
  readonly transpose: number;
  readonly capo: number;
  readonly symbol: string;
}

interface KeyCase {
  readonly tonic: number;
  readonly mode: KeyMode;
  readonly transpose: number;
  readonly confidence: number;
  readonly display: string;
}

interface CapoCase {
  readonly root: number;
  readonly quality: ChordQuality;
  readonly bass: number | null;
  readonly transpose: number;
  readonly capo: number;
  readonly shape: string;
}

interface Vectors {
  readonly version: string;
  readonly cases: readonly SpellingCase[];
  readonly keys: readonly KeyCase[];
  readonly capo: readonly CapoCase[];
}

const VECTORS_PATH = resolve(import.meta.dirname, '../../../../specs/016-instrument-views/contracts/chord-spelling-vectors.json');
const VECTORS = JSON.parse(readFileSync(VECTORS_PATH, 'utf8')) as Vectors;

function keyOf(c: { keyTonic: number | null; keyMode: KeyMode | null }): { tonic: number; mode: KeyMode } | null {
  return c.keyTonic === null || c.keyMode === null ? null : { tonic: c.keyTonic, mode: c.keyMode };
}

describe('chord spelling vectors 1.0.0', () => {
  it('is the pinned version with at least 200 spelling cases', () => {
    expect(VECTORS.version).toBe('1.0.0');
    expect(VECTORS.cases.length).toBeGreaterThanOrEqual(200);
  });

  it.each(VECTORS.cases.map((c) => [c.symbol, c] as const))('cases: %s', (_, c) => {
    expect(chordSymbol(c, keyOf(c), { transpose: c.transpose, capo: c.capo })).toBe(c.symbol);
  });

  it.each(VECTORS.keys.map((k) => [k.display, k] as const))('keys: %s', (_, k) => {
    expect(keyDisplay({ tonic: k.tonic, mode: k.mode }, k.transpose, k.confidence)).toBe(k.display);
  });

  it('capo: every root x quality x capo 0..12 (C major) and the transpose samples', () => {
    const wrong = VECTORS.capo.filter((c) => chordSymbol(c, { tonic: 0, mode: 'major' }, { transpose: c.transpose, capo: c.capo }) !== c.shape);
    expect(VECTORS.capo.length).toBeGreaterThan(2000);
    expect(wrong).toEqual([]);
  });
});

describe('spelling helpers', () => {
  it('names pitch classes with sharps or flats, modulo 12', () => {
    expect(noteName(10, false)).toBe('A#');
    expect(noteName(10, true)).toBe('Bb');
    expect(noteName(-1, true)).toBe('B');
    expect(noteName(13, false)).toBe('C#');
  });

  it('picks flats for F Bb Eb Ab Db Gb major and their relative minors', () => {
    expect([5, 10, 3, 8, 1, 6].every((t) => usesFlats(t, 'major'))).toBe(true);
    expect([2, 7, 0, 5, 10, 3].every((t) => usesFlats(t, 'minor'))).toBe(true);
    expect(usesFlats(7, 'major')).toBe(false);
    expect(usesFlats(4, 'minor')).toBe(false);
    expect(keyName({ tonic: 6, mode: 'major' })).toBe('Gb major');
    expect(keyName({ tonic: 3, mode: 'minor' })).toBe('Eb minor');
  });

  it('defaults the key confidence to certain', () => {
    expect(keyDisplay({ tonic: 9, mode: 'minor' }, 0)).toBe('A minor');
  });
});

describe('keyAt (region keys)', () => {
  const chart: ChordChartData = {
    segments: [],
    keyTonic: 0,
    keyMode: 'major',
    keyRegions: [
      { startMs: 0, endMs: 10_000, tonic: 2, mode: 'major' },
      { startMs: 10_000, endMs: 20_000, tonic: 1, mode: 'major' },
    ],
  };

  it('uses the region a chord starts in', () => {
    expect(keyAt(chart, 0)).toEqual({ tonic: 2, mode: 'major' });
    expect(keyAt(chart, 9_999)).toEqual({ tonic: 2, mode: 'major' });
    expect(keyAt(chart, 10_000)).toEqual({ tonic: 1, mode: 'major' });
    expect(chordSymbol({ root: 6, quality: 'min', bass: null }, keyAt(chart, 5000), { transpose: 0, capo: 0 })).toBe('F#m');
    expect(chordSymbol({ root: 6, quality: 'maj', bass: null }, keyAt(chart, 15_000), { transpose: 0, capo: 0 })).toBe('Gb');
  });

  it('falls back to the global key outside the regions and without regions, then to no key', () => {
    expect(keyAt(chart, 25_000)).toEqual({ tonic: 0, mode: 'major' });
    expect(keyAt({ ...chart, keyRegions: [] }, 5000)).toEqual({ tonic: 0, mode: 'major' });
    const { keyRegions: _, ...withoutRegions } = chart;
    expect(keyAt(withoutRegions, 5000)).toEqual({ tonic: 0, mode: 'major' });
    expect(keyAt({ segments: [], keyTonic: null, keyMode: null }, 5000)).toBeNull();
    expect(keyAt({ segments: [], keyTonic: 3, keyMode: null }, 5000)).toBeNull();
  });
});

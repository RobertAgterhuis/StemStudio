/**
 * Display transposition and spelling of chords and keys (data-model 2.5, 2.8, 2.9; shared with the backend through
 * `chord-spelling-vectors.json` 1.0.0): `displayRoot = (root + transpose - capo) mod 12`, spelled with the key of the
 * region the chord starts in, moved by the same amount; flats for keys whose relative major is F Bb Eb Ab Db Gb.
 *
 * @packageDocumentation
 */
import { lastAtOrBefore } from '../timeline/cursor';
import type { ChordChartData, ChordQuality, KeyMode } from '../types';

/** Sharp note names by pitch class. */
const SHARPS = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'] as const;
/** Flat note names by pitch class. */
const FLATS = ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B'] as const;
/** Major tonics spelled with flats (F Bb Eb Ab Db Gb; Gb, not F#, for tonic 6). */
const FLAT_MAJOR_TONICS: ReadonlySet<number> = new Set([5, 10, 3, 8, 1, 6]);

/** Display suffix per quality (data-model 2.1). */
export const QUALITY_SUFFIX: Readonly<Record<ChordQuality, string>> = {
  maj: '',
  min: 'm',
  dim: 'dim',
  aug: 'aug',
  min6: 'm6',
  maj6: '6',
  min7: 'm7',
  minmaj7: 'm(maj7)',
  maj7: 'maj7',
  '7': '7',
  dim7: 'dim7',
  hdim7: 'm7b5',
  sus2: 'sus2',
  sus4: 'sus4',
};

/** Symbol of a segment without a chord. */
export const NO_CHORD = 'N.C.';

/** A key used for spelling. */
export interface SpellingKey {
  /** Tonic pitch class 0..11. */
  readonly tonic: number;
  /** Mode. */
  readonly mode: KeyMode;
}

/** The chord fields spelling reads. */
export interface ChordPitches {
  /** Root pitch class, or null for no chord. */
  readonly root: number | null;
  /** Quality, or null for no chord. */
  readonly quality: ChordQuality | null;
  /** Slash bass pitch class, or null. */
  readonly bass: number | null;
}

/** Display shift: the 012 transpose (-12..12) and the capo (0..12). */
export interface DisplayShift {
  /** Semitones of the playback transpose. */
  readonly transpose: number;
  /** Capo fret (0 = none). */
  readonly capo: number;
}

/** @returns `value mod 12` in 0..11. */
function pitchClass(value: number): number {
  return ((value % 12) + 12) % 12;
}

/**
 * @param pc - Pitch class (any integer, taken mod 12).
 * @param flats - Flat names instead of sharps.
 * @returns The note name.
 */
export function noteName(pc: number, flats: boolean): string {
  return String((flats ? FLATS : SHARPS)[pitchClass(pc)]);
}

/**
 * @param tonic - Tonic pitch class.
 * @param mode - Mode.
 * @returns Whether the key spells with flats (its relative major is F Bb Eb Ab Db Gb).
 */
export function usesFlats(tonic: number, mode: KeyMode): boolean {
  return FLAT_MAJOR_TONICS.has(pitchClass(mode === 'major' ? tonic : tonic + 3));
}

/**
 * @param chord - Root, quality and bass.
 * @param key - Spelling key (region or global), or null for sharps.
 * @param shift - Transpose and capo.
 * @returns The display symbol, e.g. `Am7/G`, `Bb`, `N.C.`.
 */
export function chordSymbol(chord: ChordPitches, key: SpellingKey | null, shift: DisplayShift): string {
  if (chord.root === null || chord.quality === null) {
    return NO_CHORD;
  }
  const move = shift.transpose - shift.capo;
  const flats = key !== null && usesFlats(key.tonic + move, key.mode);
  return noteName(chord.root + move, flats) + QUALITY_SUFFIX[chord.quality] + slashBass(chord, move, flats);
}

function slashBass(chord: ChordPitches, move: number, flats: boolean): string {
  return chord.bass === null || chord.bass === chord.root ? '' : `/${noteName(chord.bass + move, flats)}`;
}

/**
 * @param key - Key.
 * @returns `<Tonic> major|minor` spelled by the flat-key rule.
 */
export function keyName(key: SpellingKey): string {
  return `${noteName(key.tonic, usesFlats(key.tonic, key.mode))} ${key.mode}`;
}

/**
 * Key display of data-model 2.8 (the engine's `data-current-key`).
 *
 * @param key - The key as analysed.
 * @param transpose - Current 012 transpose.
 * @param confidence - Key confidence 0..1; default 1.
 * @returns E.g. `D major (original C major, +2)`, with ` (uncertain)` below 0.5.
 */
export function keyDisplay(key: SpellingKey, transpose: number, confidence = 1): string {
  const original = keyName(key);
  const moved = transpose === 0 ? original : `${keyName({ tonic: key.tonic + transpose, mode: key.mode })} (original ${original}, ${transpose > 0 ? '+' : ''}${String(transpose)})`;
  return confidence < 0.5 ? `${moved} (uncertain)` : moved;
}

/** Normalises a spelling key's tonic to 0..11. */
function normalised(tonic: number, mode: KeyMode): SpellingKey {
  return { tonic: pitchClass(tonic), mode };
}

/**
 * @param chart - Chord chart with its global key and key regions.
 * @param ms - Track ms (a chord's start).
 * @returns The key of the region holding `ms`, else the global key, else null.
 */
export function keyAt(chart: ChordChartData, ms: number): SpellingKey | null {
  const regions = chart.keyRegions ?? [];
  const region = regions[lastAtOrBefore(regions.map((r) => r.startMs), ms)];
  return region !== undefined && ms < region.endMs ? normalised(region.tonic, region.mode) : globalKey(chart);
}

function globalKey(chart: ChordChartData): SpellingKey | null {
  return chart.keyTonic === null || chart.keyMode === null ? null : normalised(chart.keyTonic, chart.keyMode);
}

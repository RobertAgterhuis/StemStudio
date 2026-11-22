/**
 * The capo ranking of research R-14 (feature 016 US8), computed in the suite from the chord chart so the capo spec does
 * not copy the backend's result: shape of a chord with capo `c` at transpose `t` = root `(r + t - c) mod 12`; a shape is
 * open when it is one of the common open-position shapes; the score of capo `c` (0..7) is
 * `open share - 0.02 x c`; the three best capos are suggested (ties: the lower capo).
 *
 * @packageDocumentation
 */
import type { ChordChartDto, ChordQuality } from './instrument-views';

/** Open-position shapes per quality, as root pitch classes (R-14: C A G E D; Am Em Dm; ...). */
const openRoots: Readonly<Partial<Record<ChordQuality, readonly number[]>>> = {
  maj: [0, 9, 7, 4, 2],
  min: [9, 4, 2],
  '7': [0, 9, 7, 4, 2, 11],
  min7: [9, 4, 2],
  maj7: [0, 9, 7, 4, 2, 5],
  sus2: [9, 2],
  sus4: [9, 2, 4],
};

/** Highest capo the advisor considers. */
const maxCapo = 7;

/** Penalty per fret (lower frets on near ties). */
const fretPenalty = 0.02;

/** One ranked capo with its open-shape share. */
export interface CapoScore {
  readonly capo: number;
  readonly openShare: number;
}

/** True when the chord, played with `capo` at `transpose`, is an open-position shape. */
export function isOpenShape(root: number, quality: ChordQuality, transpose: number, capo: number): boolean {
  const shape = (((root + transpose - capo) % 12) + 12) % 12;
  return (openRoots[quality] ?? []).includes(shape);
}

/** Open-shape share of the chart's chords (by duration, `N` ignored) with `capo` at `transpose`. */
export function openShare(chart: ChordChartDto, transpose: number, capo: number): number {
  const chords = chart.segments.flatMap((s) => (s.root === null || s.quality === null ? [] : [{ root: s.root, quality: s.quality, length: s.endMs - s.startMs }]));
  const total = chords.reduce((sum, c) => sum + c.length, 0);
  const open = chords.filter((c) => isOpenShape(c.root, c.quality, transpose, capo)).reduce((sum, c) => sum + c.length, 0);
  return total === 0 ? 0 : open / total;
}

/** The best three capos for `transpose`, best first. */
export function rankCapos(chart: ChordChartDto, transpose: number): CapoScore[] {
  const scores = Array.from({ length: maxCapo + 1 }, (_, capo) => ({ capo, openShare: openShare(chart, transpose, capo) }));
  const rank = (s: CapoScore): number => s.openShare - fretPenalty * s.capo;
  return scores.sort((a, b) => rank(b) - rank(a) || a.capo - b.capo).slice(0, 3);
}

/**
 * Unit scoring (data-model 3.4, binding), shared by the live scorer and `scoreTake`: every voiced 10 ms reference frame
 * of the unit is a target (`r + 100 * transpose`); the singer value is the nearest singer frame within 15 ms; the
 * octave-folded error `((e + 600) mod 1200) - 600` is a hit within the tolerance. Score = round(100 * hits / voiced),
 * null below 20 voiced reference frames.
 *
 * @packageDocumentation
 */
import { lastAtOrBefore } from '../timeline/cursor';
import type { SingingUnit, SingingUnitScore, SingingSummary } from '../types';

/** Reference hop (ms). */
export const HOP_MS = 10;
/** Fewest voiced reference frames of a scored unit. */
export const MIN_VOICED_FRAMES = 20;
/** Largest distance of the singer frame used for a reference frame (ms). */
export const NEAREST_MS = 15;

/** Singer frames in song time order. */
export interface SingerTrack {
  /** Song ms of every frame (non-decreasing). */
  readonly times: readonly number[];
  /** Cents of every frame, null when unvoiced. */
  readonly cents: readonly (number | null)[];
}

/** What one unit is scored against. */
export interface UnitTarget {
  /** Reference frames (10 ms, centi-semitones, 0 = unvoiced). */
  readonly reference: Int16Array;
  /** The unit. */
  readonly unit: SingingUnit;
  /** Tolerance in cents. */
  readonly toleranceCents: number;
  /** 012 transpose in semitones. */
  readonly transpose: number;
}

/**
 * @param error - Singer minus target (cents).
 * @returns The error folded into -600..600 (octave-independent).
 */
export function foldError(error: number): number {
  return ((((error + 600) % 1200) + 1200) % 1200) - 600;
}

/**
 * @param singer - Singer frames.
 * @param ms - Song ms.
 * @returns The cents of the nearest frame within 15 ms (null when that frame is unvoiced or there is none).
 */
export function nearestSinger(singer: SingerTrack, ms: number): number | null {
  const before = lastAtOrBefore(singer.times, ms);
  const candidates = [before, before + 1].filter((i) => i >= 0 && i < singer.times.length);
  const best = candidates.reduce((a, b) => (Math.abs(Number(singer.times[b]) - ms) < Math.abs(Number(singer.times[a]) - ms) ? b : a), Number(candidates[0]));
  return Math.abs(Number(singer.times[best]) - ms) <= NEAREST_MS ? (singer.cents[best] ?? null) : null;
}

function isHit(target: UnitTarget, singer: number | null, reference: number): boolean {
  return singer !== null && Math.abs(foldError(singer - (reference + 100 * target.transpose))) <= target.toleranceCents;
}

/**
 * @param target - Reference, unit, tolerance and transpose.
 * @param singer - Singer frames of the unit (song time).
 * @returns 0..100, or null with fewer than 20 voiced reference frames.
 */
export function scoreUnit(target: UnitTarget, singer: SingerTrack): number | null {
  let voiced = 0;
  let hits = 0;
  const last = Math.min(Math.ceil(target.unit.endMs / HOP_MS), target.reference.length);
  for (let i = Math.max(Math.ceil(target.unit.startMs / HOP_MS), 0); i < last; i += 1) {
    const reference = Number(target.reference[i]);
    if (reference > 0) {
      voiced += 1;
      hits += isHit(target, nearestSinger(singer, i * HOP_MS), reference) ? 1 : 0;
    }
  }
  return voiced < MIN_VOICED_FRAMES ? null : Math.round((100 * hits) / voiced);
}

/**
 * @param scores - Latest score of every unit.
 * @param toleranceCents - Tolerance at the end.
 * @returns The summary without the latency source.
 */
export function summarise(scores: readonly SingingUnitScore[], toleranceCents: number): Omit<SingingSummary, 'latencySource'> {
  const scored = scores.filter((s): s is SingingUnitScore & { score: number } => s.score !== null);
  if (scored.length === 0) {
    return { unitsScored: 0, average: null, best: null, toleranceCents };
  }
  const best = scored.reduce((a, b) => (b.score > a.score ? b : a));
  const average = Math.round(scored.reduce((sum, s) => sum + s.score, 0) / scored.length);
  return { unitsScored: scored.length, average, best: { index: best.index, score: best.score }, toleranceCents };
}

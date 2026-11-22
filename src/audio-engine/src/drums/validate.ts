/**
 * Validation of drum hits and hit lane options (contract 2.6.0 errors: `Invalid drum hits: <reason>`; data-model 5.1,
 * 5.2).
 *
 * @packageDocumentation
 */
import type { DrumHitEvent, DrumPiece, HitLaneOptions } from '../types';

/** Most hits of a track. */
export const MAX_HITS = 40_000;
/** Lane order top to bottom (drum notation order). */
export const LANE_ORDER: readonly DrumPiece[] = ['cymbals', 'hihat', 'toms', 'snare', 'kick'];
/** Default look-ahead (ms). */
export const DEFAULT_LOOK_AHEAD_MS = 4000;
/** Look-ahead range (ms). */
export const LOOK_AHEAD_RANGE = { min: 2000, max: 8000 } as const;

/** Resolved options. */
export interface HitLaneSettings {
  /** Look-ahead in ms (clamped). */
  readonly lookAheadMs: number;
  /** Drawn lanes in {@link LANE_ORDER}. */
  readonly pieces: readonly DrumPiece[];
  /** Explicit reduced motion, or undefined to follow the media query. */
  readonly reducedMotion: boolean | undefined;
}

function fail(reason: string): never {
  throw new Error(`Invalid drum hits: ${reason}`);
}

function checkPiece(piece: unknown): void {
  if (!LANE_ORDER.includes(piece as DrumPiece)) {
    fail(`unknown piece ${String(piece)}`);
  }
}

function checkStrength(strength: number): void {
  if (!(strength >= 0 && strength <= 1)) {
    fail('strength outside 0..1');
  }
}

function checkTime(time: number, previous: number): void {
  if (!(Number.isFinite(time) && time >= previous)) {
    fail('times decreasing');
  }
}

function checkHit(hit: DrumHitEvent, previous: DrumHitEvent | undefined): void {
  checkPiece(hit.piece);
  checkStrength(hit.strength);
  checkTime(hit.timeMs, previous?.timeMs ?? Number.NEGATIVE_INFINITY);
}

/**
 * @param hits - Hits from Blazor (untrusted shape).
 * @returns The hits.
 * @throws Error `Invalid drum hits: <reason>`.
 */
export function validateHits(hits: readonly DrumHitEvent[]): readonly DrumHitEvent[] {
  const list: unknown = hits;
  if (!Array.isArray(list)) {
    fail('hits missing');
  }
  if (hits.length > MAX_HITS) {
    fail(`more than ${String(MAX_HITS)} hits`);
  }
  hits.forEach((hit, i) => {
    checkHit(hit, hits[i - 1]);
  });
  return hits;
}

/**
 * @param options - New options (absent fields keep `previous`).
 * @param previous - Current settings.
 * @returns The resolved settings (look-ahead clamped to 2000..8000 ms).
 * @throws Error `Invalid drum hits: unknown piece <p>`.
 */
export function resolveHitOptions(options: HitLaneOptions, previous: HitLaneSettings): HitLaneSettings {
  const pieces = options.pieces ?? previous.pieces;
  pieces.forEach(checkPiece);
  const look = options.lookAheadMs ?? previous.lookAheadMs;
  return {
    lookAheadMs: Math.min(Math.max(look, LOOK_AHEAD_RANGE.min), LOOK_AHEAD_RANGE.max),
    pieces: LANE_ORDER.filter((piece) => pieces.includes(piece)),
    reducedMotion: 'reducedMotion' in options ? options.reducedMotion : previous.reducedMotion,
  };
}

/** Settings before any option: every lane, 4 s, motion from the media query. */
export const DEFAULT_HIT_SETTINGS: HitLaneSettings = { lookAheadMs: DEFAULT_LOOK_AHEAD_MS, pieces: LANE_ORDER, reducedMotion: undefined };

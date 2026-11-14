/**
 * A-B loop helpers: validation, half-open region tests and position wrapping. The looping itself
 * is sample-accurate in the browser via `loop/loopStart/loopEnd` on every source.
 *
 * @packageDocumentation
 */
import type { LoopRegion } from './types';

/** No loop. */
export const NO_LOOP: LoopRegion = { start: 0, end: 0, enabled: false };

/** Anything that accepts a validated loop region (the transport). */
export interface LoopTarget {
  /** Track duration in seconds. */
  readonly duration: number;
  /** Applies an already validated region. */
  setLoop(region: LoopRegion): void;
}

/**
 * Validates a region against the track.
 *
 * @param loop - Requested region.
 * @param duration - Track duration in seconds.
 * @returns A plain copy of the region.
 * @throws Error `Invalid loop region: ...` unless `0 <= start < end <= duration`.
 */
export function validateLoopRegion(loop: LoopRegion, duration: number): LoopRegion {
  const problem = loopProblem(loop, duration);
  if (problem !== null) {
    throw new Error(`Invalid loop region: ${problem}`);
  }
  return { start: loop.start, end: loop.end, enabled: loop.enabled };
}

/**
 * Validates and applies a region; an invalid region leaves the previous loop unchanged.
 *
 * @param target - Transport.
 * @param loop - Requested region.
 */
export function setLoopChecked(target: LoopTarget, loop: LoopRegion): void {
  target.setLoop(validateLoopRegion(loop, target.duration));
}

/**
 * @param position - Track time (s).
 * @param region - Loop region.
 * @returns Whether `position` lies in `[start, end)`.
 */
export function isInside(position: number, region: LoopRegion): boolean {
  return position >= region.start && position < region.end;
}

/**
 * Folds a linearly advancing position back into an enabled region once it passes `end`.
 *
 * @param position - Unwrapped track time (s).
 * @param region - Loop region.
 * @returns The audible track time.
 */
export function wrapPosition(position: number, region: LoopRegion): number {
  if (!region.enabled || position < region.end) {
    return position;
  }
  return region.start + ((position - region.start) % (region.end - region.start));
}

/**
 * @param source - Buffer source.
 * @param region - Region copied onto `loop`, `loopStart`, `loopEnd`.
 */
export function applyLoop(source: AudioBufferSourceNode, region: LoopRegion): void {
  source.loop = region.enabled;
  source.loopStart = region.start;
  source.loopEnd = region.end;
}

function loopProblem(loop: LoopRegion, duration: number): string | null {
  if (!(loop.start >= 0)) {
    return `start must be >= 0 (got ${String(loop.start)})`;
  }
  if (!(loop.end <= duration)) {
    return `end must be <= duration ${String(duration)} (got ${String(loop.end)})`;
  }
  if (!(loop.end > loop.start)) {
    return `B <= A (start ${String(loop.start)}, end ${String(loop.end)})`;
  }
  return null;
}

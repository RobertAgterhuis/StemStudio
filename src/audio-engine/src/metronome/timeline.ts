/**
 * Maps a window of context time onto track-time segments, following the transport anchor and
 * the loop (a window crossing `loopEnd` continues at `loopStart`).
 *
 * @packageDocumentation
 */
import type { TransportAnchor, TransportClock } from '../transport';
import type { LoopRegion } from '../types';

/** A stretch of track time that plays linearly inside a context-time window. */
export interface Segment {
  /** First track time of the segment (inclusive). */
  readonly trackFrom: number;
  /** Last track time of the segment (exclusive). */
  readonly trackTo: number;
  /** Context time at which `trackFrom` is heard. */
  readonly contextStart: number;
}

/** Half-open context-time window `[from, to)`. */
export interface TimeWindow {
  /** Start (inclusive). */
  readonly from: number;
  /** End (exclusive). */
  readonly to: number;
}

/** Upper bound of segments per window (guards against absurdly short loops). */
export const MAX_SEGMENTS = 64;

/**
 * @param clock - Transport timing view.
 * @param window - Context-time window to cover.
 * @returns Track segments heard during the window (empty when not playing).
 */
export function segmentsFor(clock: TransportClock, window: TimeWindow): Segment[] {
  const anchor = clock.anchor();
  if (anchor === null) {
    return [];
  }
  const from = Math.max(window.from, anchor.when);
  if (from >= window.to) {
    return [];
  }
  const span = { from: toTrack(anchor, from), to: toTrack(anchor, window.to) };
  if (clock.loop.enabled) {
    return loopSegments(anchor, clock.loop, span);
  }
  return [{ trackFrom: span.from, trackTo: Math.min(span.to, clock.duration), contextStart: from }];
}

function toTrack(anchor: TransportAnchor, contextTime: number): number {
  return anchor.offset + (contextTime - anchor.when);
}

/** Walks the unwrapped track time `u` (offset + elapsed) through the loop cycles. */
function loopSegments(anchor: TransportAnchor, loop: LoopRegion, span: { from: number; to: number }): Segment[] {
  const segments: Segment[] = [];
  for (let u = span.from; u < span.to && segments.length < MAX_SEGMENTS; ) {
    const { track, cycleEnd } = locate(u, loop);
    const end = Math.min(cycleEnd, span.to);
    segments.push({ trackFrom: track, trackTo: track + (end - u), contextStart: anchor.when + (u - anchor.offset) });
    u = cycleEnd;
  }
  return segments;
}

function locate(u: number, loop: LoopRegion): { track: number; cycleEnd: number } {
  if (u < loop.end) {
    return { track: u, cycleEnd: loop.end };
  }
  const length = loop.end - loop.start;
  const cycle = Math.floor((u - loop.end) / length);
  return { track: loop.start + (u - loop.end - cycle * length), cycleEnd: loop.end + (cycle + 1) * length };
}

/**
 * Maps a window of context time onto track-time segments, following the transport anchor and
 * the loop (a window crossing `loopEnd` continues at `loopStart`).
 *
 * @packageDocumentation
 */
import { rateOf, trackAt, type TransportAnchor } from '../anchors';
import type { TransportClock } from '../transport';
import type { LoopRegion } from '../types';

/** A stretch of track time that plays linearly inside a context-time window. */
export interface Segment {
  /** First track time of the segment (inclusive). */
  readonly trackFrom: number;
  /** Last track time of the segment (exclusive). */
  readonly trackTo: number;
  /** Context time at which `trackFrom` is heard. */
  readonly contextStart: number;
  /** Track seconds per context second (contract 2.3.0); click context time = contextStart + (t - trackFrom) / rate. */
  readonly rate: number;
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
 * @returns Track segments heard during the window (empty when not playing), anchor by anchor of the queue.
 */
export function segmentsFor(clock: TransportClock, window: TimeWindow): Segment[] {
  const current = clock.anchor();
  if (current === null) {
    return [];
  }
  const anchors = clock.anchors?.() ?? [current];
  return anchors.flatMap((anchor, i) => {
    const piece = { from: Math.max(window.from, anchor.when), to: Math.min(window.to, pieceEnd(anchor, anchors[i + 1])) };
    return piece.from < piece.to ? pieceSegments(clock, anchor, piece) : [];
  });
}

function pieceEnd(anchor: TransportAnchor, next: TransportAnchor | undefined): number {
  return Math.min(next?.when ?? Infinity, anchor.until ?? Infinity);
}

function pieceSegments(clock: TransportClock, anchor: TransportAnchor, piece: TimeWindow): Segment[] {
  const span = { from: trackAt(anchor, piece.from), to: trackAt(anchor, piece.to) };
  if (clock.loop.enabled) {
    return loopSegments(anchor, clock.loop, span);
  }
  return [{ trackFrom: span.from, trackTo: Math.min(span.to, clock.duration), contextStart: piece.from, rate: rateOf(anchor) }];
}

/** Walks the unwrapped track time `u` (offset + elapsed) through the loop cycles. */
function loopSegments(anchor: TransportAnchor, loop: LoopRegion, span: { from: number; to: number }): Segment[] {
  const segments: Segment[] = [];
  const rate = rateOf(anchor);
  for (let u = span.from; u < span.to && segments.length < MAX_SEGMENTS; ) {
    const { track, cycleEnd } = locate(u, loop);
    const end = Math.min(cycleEnd, span.to);
    segments.push({ trackFrom: track, trackTo: track + (end - u), contextStart: anchor.when + (u - anchor.offset) / rate, rate });
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

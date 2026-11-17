/**
 * Transport-segment log of a recording session (015 T015, Phase 0 P0-8). The transport prunes its anchor queues as
 * time passes; the recorder needs every heard anchor of the session after Stop, so the transport hands each new
 * heard anchor to this log while it is attached.
 *
 * @packageDocumentation
 */
import type { TransportAnchor } from '../anchors';
import type { TransportSegment } from './mapping';

/** Most segments kept: 50 passes (the pass limit) plus the play start. */
export const MAX_LOGGED_SEGMENTS = 51;

/** Heard anchors of one session, converted to frame segments on demand. */
export class SegmentLog {
  private anchors: TransportAnchor[] = [];

  /** @param sampleRate - Context rate used to convert seconds to frames. */
  public constructor(private readonly sampleRate: number) {}

  /** Records a heard anchor (bound, so it can be handed to `Transport.setAnchorSink`). */
  public readonly record = (anchor: TransportAnchor): void => {
    this.anchors = [...this.anchors, anchor].slice(-MAX_LOGGED_SEGMENTS);
  };

  /** Forgets every anchor (a new session). */
  public clear(): void {
    this.anchors = [];
  }

  /** @returns The logged anchors as frame segments in `when` order. */
  public segments(): TransportSegment[] {
    const frames = (seconds: number): number => Math.round(seconds * this.sampleRate);
    return [...this.anchors]
      .sort((a, b) => a.when - b.when)
      .map((anchor) => ({
        whenFrame: frames(anchor.when),
        offsetSample: frames(anchor.offset),
        untilFrame: anchor.until === undefined ? null : frames(anchor.until),
      }));
  }
}

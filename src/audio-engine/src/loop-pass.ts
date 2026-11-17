/**
 * Loop passes (contract 2.4.0 "Loop passes", ADR-023 D7): reports every completed pass of an enabled loop at the
 * moment its wrap is heard. The count is derived from the transport's heard anchors (never from timer deltas), so a
 * frame and a scheduler tick that both observe the same wrap report it once, and several wraps between two
 * observations (hidden tab) are each reported.
 *
 * @packageDocumentation
 */
import { rateOf, trackAt, type TransportAnchor } from './anchors';
import type { FrameLoop } from './frame-loop';
import type { TransportListener } from './transport';
import type { LoopPassInfo, LoopRegion, PlaybackState } from './types';

/** Tolerance (track seconds) for a pass that ends exactly at `end` (explicit passes stop there). */
const END_TOLERANCE = 1e-9;
/** A start within this distance of the loop start (s) begins a full pass. */
const FULL_PASS_TOLERANCE = 0.001;

/** The part of the transport the tracker reads. */
export interface LoopPassSource {
  /** Active loop region. */
  readonly loop: LoopRegion;
  /** Playback state. */
  readonly state: PlaybackState;
  /** Heard anchors in `when` order while playing, else empty. */
  anchors(): readonly TransportAnchor[];
  /** Current (heard) position. */
  position(): number;
  /** Position the next play starts at. */
  startPosition(): number;
}

/** Construction parameters of {@link LoopPassTracker}. */
export interface LoopPassOptions {
  /** The transport. */
  readonly transport: LoopPassSource;
  /** Shared animation-frame loop; the tracker observes on it while playing with an enabled loop. */
  readonly frames: FrameLoop;
  /** Context time currently heard at the output (s). */
  readonly now: () => number;
  /** Receives each completed pass (the engine sends `OnLoopPass`). */
  readonly report: (info: LoopPassInfo) => void;
}

/** Wraps that completed under one anchor. */
interface AnchorWraps {
  readonly count: number;
  readonly rate: number;
}

/**
 * @param anchor - Heard anchor.
 * @param time - Heard context time.
 * @param region - Enabled loop region.
 * @returns Passes completed under `anchor` up to `time` (an explicit pass anchor counts at most one).
 */
export function wrapsAt(anchor: TransportAnchor, time: number, region: LoopRegion): number {
  const track = trackAt(anchor, time);
  if (track < region.end - END_TOLERANCE) {
    return 0;
  }
  return 1 + Math.floor((track - region.end + END_TOLERANCE) / (region.end - region.start));
}

/** Wraps heard in `(from, to]`, per anchor in order. */
function wrapsBetween(anchors: readonly TransportAnchor[], window: { from: number; to: number }, region: LoopRegion): AnchorWraps[] {
  return anchors
    .map((anchor, i) => {
      const lo = Math.max(window.from, anchor.when);
      const hi = Math.min(window.to, anchors[i + 1]?.when ?? Infinity);
      const count = hi > lo ? wrapsAt(anchor, hi, region) - wrapsAt(anchor, lo, region) : 0;
      return { count, rate: rateOf(anchor) };
    })
    .filter((wraps) => wraps.count > 0);
}

function sameRegion(a: LoopRegion, b: LoopRegion): boolean {
  return a.start === b.start && a.end === b.end && a.enabled === b.enabled;
}

/** Counts and reports completed loop passes. */
export class LoopPassTracker implements TransportListener {
  private pass = 0;
  private since = -Infinity;
  private skipPartial = false;
  private region: LoopRegion;
  private watching = false;

  /** @param options - Transport, frame loop, heard clock and report sink. */
  public constructor(private readonly options: LoopPassOptions) {
    this.region = options.transport.loop;
  }

  /** Reads the heard clock and reports the passes completed since the previous observation. */
  public readonly observe = (): void => {
    const { transport } = this.options;
    const region = transport.loop;
    if (!region.enabled || transport.state !== 'playing') {
      return;
    }
    const to = Math.max(this.options.now(), this.since);
    const wraps = wrapsBetween(transport.anchors(), { from: this.since, to }, region);
    this.since = to;
    wraps.forEach(({ count, rate }) => {
      this.completed(count, rate, region);
    });
  };

  /**
   * Seek, stop or a new track: the pass number restarts at 0; when the position is not the loop start, the partial
   * pass up to the next wrap is not reported.
   */
  public reset(): void {
    const { transport } = this.options;
    const playing = transport.state === 'playing';
    const position = playing ? transport.position() : transport.startPosition();
    this.pass = 0;
    this.since = playing ? this.options.now() : -Infinity;
    this.region = transport.loop;
    this.skipPartial = this.region.enabled && Math.abs(position - this.region.start) > FULL_PASS_TOLERANCE;
  }

  /** After `setLoop`: a different region (or enabling/disabling) resets; the same region does not. */
  public onLoopChanged(): void {
    if (!sameRegion(this.region, this.options.transport.loop)) {
      this.reset();
    }
    this.watch();
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(): void {
    this.watch();
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    // Seek, stop and loop changes are reset explicitly by the engine; pause keeps the count.
  }

  /** Stops observing on the frame loop. */
  public dispose(): void {
    this.options.frames.remove(this.observe);
    this.watching = false;
  }

  private completed(count: number, rate: number, region: LoopRegion): void {
    for (let i = 0; i < count; i++) {
      if (this.skipPartial) {
        this.skipPartial = false;
        continue;
      }
      this.pass += 1;
      this.options.report({ start: region.start, end: region.end, pass: this.pass, speedPercent: Math.round(rate * 100) });
    }
  }

  private watch(): void {
    const { transport, frames } = this.options;
    const wanted = transport.state === 'playing' && transport.loop.enabled;
    if (wanted === this.watching) {
      return;
    }
    this.watching = wanted;
    if (wanted) {
      frames.add(this.observe);
    } else {
      frames.remove(this.observe);
    }
  }
}

/**
 * Transport anchors (data-model 4): context time `when` plays track time `offset` at rate `rate`. A short queue
 * of anchors describes live rate changes (a new anchor from the time the change is heard) and explicit loop passes
 * (an anchor per pass that ends at `until`, with a count-in gap until the next one).
 *
 * @packageDocumentation
 */

/** Shared start of the running sources: context time `when` plays track time `offset`. */
export interface TransportAnchor {
  /** Context time (s) at which `offset` is heard. */
  readonly when: number;
  /** Track time (s) heard at `when`. */
  readonly offset: number;
  /** Track seconds per context second; absent = 1 (contract 2.1.0 anchors). */
  readonly rate?: number;
  /** Context time at which this anchor's sources end (explicit pass); absent = open-ended. */
  readonly until?: number;
}

/**
 * @param when - Context time.
 * @param offset - Track time heard at `when`.
 * @param rate - Rate; omitted from the anchor when it is 1, so 2.1.0 anchors stay `{ when, offset }`.
 * @returns The anchor.
 */
export function anchorOf(when: number, offset: number, rate: number): TransportAnchor {
  return rate === 1 ? { when, offset } : { when, offset, rate };
}

/**
 * @param anchor - Anchor.
 * @returns Its rate (1 when absent).
 */
export function rateOf(anchor: TransportAnchor): number {
  return anchor.rate ?? 1;
}

/**
 * @param anchor - Anchor.
 * @param time - Context time.
 * @returns Unwrapped track time at `time` (the offset before `when`, the end position after `until`).
 */
export function trackAt(anchor: TransportAnchor, time: number): number {
  const end = Math.min(time, anchor.until ?? Infinity);
  return anchor.offset + Math.max(0, end - anchor.when) * rateOf(anchor);
}

/** Anchors in ascending `when` order; never empty (the first anchor is only replaced by {@link AnchorQueue.reset}). */
export class AnchorQueue {
  private items: [TransportAnchor, ...TransportAnchor[]];

  /** @param first - Initial anchor. */
  public constructor(first: TransportAnchor) {
    this.items = [first];
  }

  /** All anchors, oldest first. */
  public get all(): readonly TransportAnchor[] {
    return this.items;
  }

  /** @param first - The only anchor after the call. */
  public reset(first: TransportAnchor): void {
    this.items = [first];
  }

  /** @param anchor - Anchor inserted in `when` order (after anchors with the same `when`), never before the first. */
  public push(anchor: TransportAnchor): void {
    const [first, ...rest] = this.items;
    const index = rest.findIndex((item) => item.when > anchor.when);
    rest.splice(index < 0 ? rest.length : index, 0, anchor);
    this.items = [first, ...rest];
  }

  /** @param anchor - Anchor removed by identity (the first anchor is kept). */
  public remove(anchor: TransportAnchor): void {
    const [first, ...rest] = this.items;
    this.items = [first, ...rest.filter((item) => item !== anchor)];
  }

  /**
   * @param time - Context time.
   * @returns The last anchor with `when <= time`, else the first anchor.
   */
  public at(time: number): TransportAnchor {
    let found = this.items[0];
    for (const item of this.items) {
      found = item.when <= time ? item : found;
    }
    return found;
  }

  /**
   * @param time - Context time.
   * @returns Unwrapped track time at `time`; inside a gap after an anchor's `until`, the next anchor's offset.
   */
  public positionAt(time: number): number {
    const anchor = this.at(time);
    const next = this.items[this.items.indexOf(anchor) + 1];
    const inGap = next !== undefined && time >= (anchor.until ?? Infinity);
    return inGap ? next.offset : trackAt(anchor, time);
  }

  /** @param before - Drops leading anchors whose successor starts at or before this context time. */
  public prune(before: number): void {
    const later = this.items.findIndex((item, i) => i > 0 && item.when > before);
    const keepFrom = (later < 0 ? this.items.length : later) - 1;
    this.items = this.items.slice(keepFrom) as [TransportAnchor, ...TransportAnchor[]];
  }
}

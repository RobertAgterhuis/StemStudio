/**
 * Karaoke clock: the shared timeline clock (`timeline/clock.ts`, contract 2.1.0 and 2.6.0, ADR-015 D5) minus the
 * user offset.
 *
 * @packageDocumentation
 */
import { TimelineClock, type TimelineClockOptions } from '../timeline/clock';

/** Construction parameters of {@link KaraokeClock} (those of the timeline clock). */
export type KaraokeClockOptions = TimelineClockOptions;

/** Largest user offset in either direction (ms). */
export const MAX_OFFSET_MS = 5000;

/**
 * @param offsetMs - Requested user offset.
 * @returns The offset when it is a number in -5000..5000.
 * @throws Error `Karaoke offset out of range: <value>` otherwise.
 */
export function validateOffset(offsetMs: number): number {
  if (!(Math.abs(offsetMs) <= MAX_OFFSET_MS)) {
    throw new Error(`Karaoke offset out of range: ${String(offsetMs)}`);
  }
  return offsetMs;
}

/** Position of the lyrics in integer milliseconds: heard transport position minus the user offset. */
export class KaraokeClock {
  private readonly clock: TimelineClock;
  private offset = 0;

  /** @param options - Context, transport and `performance.now`. */
  public constructor(options: KaraokeClockOptions) {
    this.clock = new TimelineClock(options);
  }

  /** User offset (ms); positive = the lyrics come later. */
  public get offsetMs(): number {
    return this.offset;
  }

  /** @param offsetMs - New user offset, validated with {@link validateOffset}. */
  public setOffset(offsetMs: number): void {
    this.offset = validateOffset(offsetMs);
  }

  /** @returns `round(1000 * heard transport position) - offsetMs`. */
  public positionMs(): number {
    return this.clock.positionMs() - this.offset;
  }
}

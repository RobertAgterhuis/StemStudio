/**
 * Karaoke clock: the transport position at the audio time currently heard, read on every
 * animation frame and never accumulated from frame deltas (contract 2.1.0, ADR-015 D5).
 *
 * @packageDocumentation
 */

/** Largest user offset in either direction (ms). */
export const MAX_OFFSET_MS = 5000;

/** The part of the transport the clock reads. */
export interface PositionSource {
  /** Track time (s) heard at context time `time` while playing, else the stored position. */
  positionAt(time: number): number;
}

/** Construction parameters of {@link KaraokeClock}. */
export interface KaraokeClockOptions {
  /** The engine's AudioContext. */
  readonly context: AudioContext;
  /** The transport. */
  readonly transport: PositionSource;
  /** `performance.now()`; injectable for tests. */
  readonly now?: () => number;
}

/**
 * Context time currently at the output: `getOutputTimestamp()` extrapolated with `performance.now()`.
 *
 * @param context - AudioContext.
 * @param perfNow - Current `performance.now()` (ms).
 * @returns Seconds of context time; `currentTime` when no output timestamp is available.
 */
export function audioNow(context: AudioContext, perfNow: number): number {
  const optional: Partial<Pick<AudioContext, 'getOutputTimestamp'>> = context;
  const stamp = usable(optional.getOutputTimestamp?.());
  return stamp === null ? context.currentTime : stamp.contextTime + (perfNow - stamp.performanceTime) / 1000;
}

function usable(stamp: AudioTimestamp | undefined): Required<AudioTimestamp> | null {
  const { contextTime = 0, performanceTime = 0 } = { ...stamp };
  return contextTime === 0 && performanceTime === 0 ? null : { contextTime, performanceTime };
}

/**
 * @param context - AudioContext.
 * @returns `outputLatency` when defined, else `baseLatency`, else 0 (s).
 */
export function outputLatency(context: AudioContext): number {
  const optional: Partial<Pick<AudioContext, 'outputLatency' | 'baseLatency'>> = context;
  return optional.outputLatency ?? optional.baseLatency ?? 0;
}

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
  private readonly now: () => number;
  private offset = 0;

  /** @param options - Context, transport and `performance.now`. */
  public constructor(private readonly options: KaraokeClockOptions) {
    this.now = options.now ?? (() => performance.now());
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
    const { context, transport } = this.options;
    const heard = audioNow(context, this.now()) - outputLatency(context);
    return Math.round(1000 * transport.positionAt(heard)) - this.offset;
  }
}

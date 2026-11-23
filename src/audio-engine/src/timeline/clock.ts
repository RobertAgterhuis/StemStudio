/**
 * Shared timeline clock of every engine view (contract 2.6.0 "Timeline clock and frame loop", ADR-015 D5): the
 * transport position at the audio time currently heard, read on every animation frame and never accumulated from
 * frame deltas. The output latency is subtracted before the transport wraps the loop; the 012 rate is applied by the
 * rate-aware transport anchors. The karaoke clock is this clock minus the user offset.
 *
 * @packageDocumentation
 */

/** The part of the transport the clock reads. */
export interface PositionSource {
  /** Track time (s) heard at context time `time` while playing, else the stored position. */
  positionAt(time: number): number;
}

/** Construction parameters of {@link TimelineClock}. */
export interface TimelineClockOptions {
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
 * @param context - AudioContext.
 * @param perfNow - Current `performance.now()` (ms).
 * @returns Context time heard at the output now: {@link audioNow} minus {@link outputLatency} (s).
 */
export function heardTime(context: AudioContext, perfNow: number): number {
  return audioNow(context, perfNow) - outputLatency(context);
}

/** Heard track position in integer milliseconds. */
export class TimelineClock {
  private readonly now: () => number;

  /** @param options - Context, transport and `performance.now`. */
  public constructor(private readonly options: TimelineClockOptions) {
    this.now = options.now ?? (() => performance.now());
  }

  /** @returns `round(1000 * transport position at the heard time)`. */
  public positionMs(): number {
    return this.positionAtMs(heardTime(this.options.context, this.now()));
  }

  /**
   * @param contextTime - A context time (s), e.g. a pitch frame's time minus the round trip.
   * @returns `round(1000 * transport position at that time)`.
   */
  public positionAtMs(contextTime: number): number {
    return Math.round(1000 * this.options.transport.positionAt(contextTime));
  }
}

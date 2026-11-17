/**
 * Timing of a live speed/transpose change (audio-engine.md 2.3.0 "Live changes"): the change enters the stage at
 * `U = now + 0.02`, is heard from `T = U + D / sampleRate`, and the heard anchor moves to
 * `{when: T + c(p) / sr, offset: positionOld(T + c(pOld) / sr), rate}`.
 *
 * @packageDocumentation
 */
import { CHANGE_LEAD_SECONDS } from '../stretch/constants';
import { contentOffsetFrames } from '../stretch/params';

export { CHANGE_LEAD_SECONDS } from '../stretch/constants';

/** Input of {@link liveChangeTimes}. */
export interface LiveChangeInput {
  /** `currentTime` of the call. */
  readonly now: number;
  /** `U` of a change that has not entered the stage yet (it is replaced), else null. */
  readonly pendingAt: number | null;
  /** Fixed delay D of the stage (frames). */
  readonly delayFrames: number;
  /** Context sample rate. */
  readonly sampleRate: number;
  /** Pitch factor before the change. */
  readonly fromPitch: number;
  /** Pitch factor after the change. */
  readonly toPitch: number;
}

/** Times of a live change. */
export interface LiveChangeTimes {
  /** Source-side time `U` of the rate automation and the pitch message. */
  readonly at: number;
  /** Context frame of `U` for `setPitch`. */
  readonly atFrame: number;
  /** `when` of the new heard anchor: `T + c(p) / sr`. */
  readonly heardWhen: number;
  /** Heard time whose old position becomes the new anchor's offset: `T + c(pOld) / sr`. */
  readonly sampleAt: number;
}

/**
 * @param input - Clock, pending change, stage delay and the two pitch factors.
 * @returns `U`, its frame, the new anchor time and the time at which the old position is sampled.
 */
export function liveChangeTimes(input: LiveChangeInput): LiveChangeTimes {
  const at = input.pendingAt ?? input.now + CHANGE_LEAD_SECONDS;
  const heard = at + input.delayFrames / input.sampleRate;
  return {
    at,
    atFrame: Math.round(at * input.sampleRate),
    heardWhen: heard + contentOffsetFrames(input.toPitch) / input.sampleRate,
    sampleAt: heard + contentOffsetFrames(input.fromPitch) / input.sampleRate,
  };
}

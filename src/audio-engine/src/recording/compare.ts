/**
 * Compare and recording gates of the take (research R-10, contract 2.5.0): an extra gate factor per channel, never a
 * change to the user's mute/solo flags. `gate = levelGate(channel) * extraGate(channel)`.
 *
 * @packageDocumentation
 */
import { parentOf } from '../gate';
import type { ChannelId, RecordingPart, TakeCompareMode } from '../types';

/** The take channel id. */
export const TAKE_CHANNEL: ChannelId = 'take';

/**
 * @param id - Channel id.
 * @param part - Recorded part.
 * @returns Whether the channel is the part's stem or one of its detail channels (never for part `none`).
 */
export function isPartChannel(id: ChannelId, part: RecordingPart): boolean {
  return part !== 'none' && (id === part || parentOf(id) === part);
}

/**
 * @param id - Channel id.
 * @param mode - Compare mode.
 * @param part - Part of the loaded take.
 * @returns 0 for the part's channels in mode `take` and for the take in mode `original`, else 1.
 */
export function compareGate(id: ChannelId, mode: TakeCompareMode, part: RecordingPart): number {
  const silent = mode === 'take' ? isPartChannel(id, part) : mode === 'original' && id === TAKE_CHANNEL;
  return silent ? 0 : 1;
}

/** Gating while a recording runs. */
export interface RecordingGating {
  readonly part: RecordingPart;
  readonly muteOriginal: boolean;
}

/**
 * @param id - Channel id.
 * @param gating - Part and `muteOriginal` of the running recording.
 * @returns 0 for the take channel, 0 for the part's channels when `muteOriginal`, else 1.
 */
export function recordingGate(id: ChannelId, gating: RecordingGating): number {
  const silent = id === TAKE_CHANNEL || (gating.muteOriginal && isPartChannel(id, gating.part));
  return silent ? 0 : 1;
}

/** Everything that decides the extra gate. */
export interface TakeGating {
  readonly mode: TakeCompareMode;
  /** Part of the loaded take. */
  readonly part: RecordingPart;
  /** The running recording, or null. */
  readonly recording: RecordingGating | null;
}

/**
 * @param gating - Compare mode, take part and recording state.
 * @returns The extra gate of every channel.
 */
export function takeGates(gating: TakeGating): (id: ChannelId) => number {
  return (id) => compareGate(id, gating.mode, gating.part) * (gating.recording === null ? 1 : recordingGate(id, gating.recording));
}

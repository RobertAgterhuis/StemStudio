/**
 * Audible-level solo/mute gate of contract 2.0.0: only collapsed parents (and stems without a
 * group) plus the detail channels of expanded parents may sound; every other channel is hidden.
 *
 * @packageDocumentation
 */
import { gateOf } from './channel';
import type { ChannelId, ChannelSettings, ParentStemName } from './types';

/** What the gate needs to know about one channel. */
export interface GateView {
  /** The channel's (validated) settings; flags of hidden channels are kept but ignored. */
  readonly settings: ChannelSettings;
  /** True when the channel is not on the audible level. */
  readonly hidden: boolean;
}

/**
 * @param id - Channel id.
 * @returns The parent of a detail channel (`drums.snare` -> `drums`), or null for a standard stem.
 */
export function parentOf(id: ChannelId): ParentStemName | null {
  const dot = id.indexOf('.');
  return dot < 0 ? null : (id.slice(0, dot) as ParentStemName);
}

/**
 * @param id - Channel id.
 * @param expanded - Parents currently expanded (only parents with a group).
 * @returns Whether the channel is hidden: an expanded parent, or a detail of a collapsed parent.
 */
export function isHidden(id: ChannelId, expanded: ReadonlySet<ParentStemName>): boolean {
  const parent = parentOf(id);
  return parent === null ? expanded.has(id as ParentStemName) : !expanded.has(parent);
}

/**
 * @param views - Channels of the mix.
 * @returns Whether any audible-level channel is soloed (hidden solo flags are ignored).
 */
export function anyAudibleSolo(views: readonly GateView[]): boolean {
  return views.some((v) => !v.hidden && v.settings.solo);
}

/**
 * Gate of one channel: `hidden || mute ? 0 : (anySolo && !solo ? 0 : 1)`.
 *
 * @param view - The channel.
 * @param anySolo - Result of {@link anyAudibleSolo} over the whole mix.
 * @returns 0 (silent) or 1 (audible).
 */
export function levelGate(view: GateView, anySolo: boolean): number {
  return view.hidden ? 0 : gateOf(view.settings, anySolo);
}

/**
 * @param views - Channels of the mix.
 * @returns The {@link levelGate} of every channel, in input order.
 */
export function gatesOf(views: readonly GateView[]): number[] {
  const anySolo = anyAudibleSolo(views);
  return views.map((v) => levelGate(v, anySolo));
}

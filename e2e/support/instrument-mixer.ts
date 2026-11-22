/**
 * Mixer assertions of the feature 016 family rule (data-model 5.3): mute flags of every channel of a track after
 * "Practise as", "Drumless" or a `play-along-<stem>` preset, read with the groups collapsed (standard strips) and expanded
 * (detail strips), and the meters of the drum family.
 *
 * @packageDocumentation
 */
import { expect } from '@playwright/test';
import type { ChannelKey, MixerPage } from '../pages/mixer-page';
import type { TrackDto } from './api';
import { isOn } from './controls';
import { silentDb } from './detail';
import { expectedMutes, splitParentsOf, trackChannels } from './instrument-family';
import type { InstrumentFamily } from './instrument-views';

/** Expects the mute toggles of `keys` to be on (`muted`) or off. */
async function expectMuteFlags(mixer: MixerPage, keys: readonly ChannelKey[], muted: boolean): Promise<void> {
  for (const key of keys) {
    await expect.poll(() => isOn(mixer.mute(key)), { message: `mute-${key}` }).toBe(muted);
    await expect.poll(() => isOn(mixer.solo(key)), { message: `solo-${key}` }).toBe(false);
  }
}

/** The keys of `all` that are visible in the given group state (standard strips collapsed, detail strips expanded). */
function visibleKeys(track: TrackDto, all: readonly ChannelKey[], expanded: boolean): ChannelKey[] {
  const details = new Set(trackChannels(track).filter((c) => c.parent !== null).map((c) => c.key));
  return all.filter((key) => details.has(key) === expanded);
}

/** Collapses or expands every split parent of the track. */
export async function setAllExpanded(mixer: MixerPage, track: TrackDto, expanded: boolean): Promise<void> {
  for (const parent of splitParentsOf(track)) {
    await mixer.setExpanded(parent, expanded);
  }
}

/**
 * Expects the family rule after a minus-one mix of `family`: its channels muted, all others unmuted, nothing soloed;
 * checked collapsed and then expanded (the flags of hidden channels are kept, 002 rule).
 */
export async function expectFamilyMuted(mixer: MixerPage, track: TrackDto, family: InstrumentFamily): Promise<void> {
  const { muted, open } = expectedMutes(track, family);
  for (const expanded of [false, true]) {
    await setAllExpanded(mixer, track, expanded);
    await expectMuteFlags(mixer, visibleKeys(track, muted, expanded), true);
    await expectMuteFlags(mixer, visibleKeys(track, open, expanded), false);
  }
}

/** The drum detail channels of the track (`drums-kick`, ...). */
export function drumDetailKeys(track: TrackDto): ChannelKey[] {
  return trackChannels(track).filter((c) => c.parent === 'drums').map((c) => c.key);
}

/** Expects every listed meter at the floor during `windowMs` of playback. */
export async function expectSilentMeters(mixer: MixerPage, keys: readonly ChannelKey[], windowMs: number): Promise<void> {
  for (const key of keys) {
    expect(await mixer.maxPeakOver(key, windowMs), `meter-${key}`).toBeLessThanOrEqual(silentDb);
  }
}

/** Expects at least one listed meter above the floor during `windowMs` of playback. */
export async function expectSomeMeterMoves(mixer: MixerPage, keys: readonly ChannelKey[], windowMs: number): Promise<void> {
  const peaks = await Promise.all(keys.map((key) => mixer.maxPeakOver(key, windowMs)));
  expect(Math.max(...peaks), `max of ${keys.join(', ')}`).toBeGreaterThan(silentDb);
}

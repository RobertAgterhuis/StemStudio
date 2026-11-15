import { Mixer } from '../../src/mixer';
import type { ChannelId, ChannelSettings } from '../../src/types';
import { MockAudioContext, type MockAudioParam } from './audio-context';

/** Channel settings with defaults (volume 1, centre, not muted or soloed). */
export function settings(partial: Partial<ChannelSettings> = {}): ChannelSettings {
  return { volume: 1, pan: 0, mute: false, solo: false, ...partial };
}

/** A mixer on a fresh mock context, built with `ids`. */
export function makeMixer(ids: readonly ChannelId[]): { ctx: MockAudioContext; mixer: Mixer } {
  const ctx = new MockAudioContext();
  const mixer = new Mixer(ctx.asAudioContext());
  mixer.build(ids);
  return { ctx, mixer };
}

/** Gain parameter of a channel: the gain node its input is. */
export function channelGain(ctx: MockAudioContext, mixer: Mixer, id: ChannelId): MockAudioParam {
  const input = mixer.input(id) as unknown;
  const node = ctx.gains.find((g) => g === input);
  if (node === undefined) {
    throw new Error(`no gain for ${id}`);
  }
  return node.gain;
}

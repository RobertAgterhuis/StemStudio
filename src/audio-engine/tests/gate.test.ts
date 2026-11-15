import { describe, expect, it } from 'vitest';
import { RAMP_TIME_CONSTANT } from '../src/channel';
import { gatesOf, isHidden, parentOf } from '../src/gate';
import { Mixer } from '../src/mixer';
import type { ChannelId, ChannelSettings, ParentStemName } from '../src/types';
import { MockAudioContext, type MockAudioParam } from './mocks/audio-context';

const CHANNELS: ChannelId[] = ['vocals', 'drums', 'bass', 'drums.kick', 'drums.snare', 'drums.rest'];

function settings(partial: Partial<ChannelSettings> = {}): ChannelSettings {
  return { volume: 1, pan: 0, mute: false, solo: false, ...partial };
}

function makeMixer(): { ctx: MockAudioContext; mixer: Mixer } {
  const ctx = new MockAudioContext();
  const mixer = new Mixer(ctx.asAudioContext());
  mixer.build(CHANNELS);
  return { ctx, mixer };
}

function gainOf(ctx: MockAudioContext, mixer: Mixer, id: ChannelId): MockAudioParam {
  const input = mixer.input(id) as unknown;
  const node = ctx.gains.find((g) => g === input);
  if (node === undefined) {
    throw new Error(`no gain for ${id}`);
  }
  return node.gain;
}

/** Gate value per channel id as heard through the mixer (volume 1 everywhere). */
function heard(ctx: MockAudioContext, mixer: Mixer): Record<string, number> {
  return Object.fromEntries(CHANNELS.map((id) => [id, gainOf(ctx, mixer, id).value]));
}

describe('channel levels', () => {
  it('parentOf returns the parent of a detail id and null for a standard stem', () => {
    expect(parentOf('drums.snare')).toBe('drums');
    expect(parentOf('other.acoustic-guitar')).toBe('other');
    expect(parentOf('drums')).toBeNull();
  });

  it('standard stems are hidden only while their parent is expanded; details only while collapsed', () => {
    const none = new Set<ParentStemName>();
    const drums = new Set<ParentStemName>(['drums']);
    expect(isHidden('drums', none)).toBe(false);
    expect(isHidden('drums.kick', none)).toBe(true);
    expect(isHidden('drums', drums)).toBe(true);
    expect(isHidden('drums.kick', drums)).toBe(false);
    expect(isHidden('vocals', drums)).toBe(false);
    expect(isHidden('vocals.lead', drums)).toBe(true);
    expect(isHidden('bass', drums)).toBe(false);
  });
});

describe('gatesOf (contract 2.0.0 table)', () => {
  it('v1 rule without hidden channels: mute wins, solo isolates', () => {
    const gates = gatesOf([
      { settings: settings({ solo: true }), hidden: false },
      { settings: settings(), hidden: false },
      { settings: settings({ solo: true, mute: true }), hidden: false },
    ]);
    expect(gates).toEqual([1, 0, 0]);
    expect(gatesOf([{ settings: settings(), hidden: false }])).toEqual([1]);
  });

  it('hidden channels are silent and their solo flag is ignored', () => {
    const gates = gatesOf([
      { settings: settings({ solo: true }), hidden: true },
      { settings: settings(), hidden: false },
    ]);
    expect(gates).toEqual([0, 1]);
  });

  it('an empty mix has no gates', () => {
    expect(gatesOf([])).toEqual([]);
  });
});

describe('mixer with detail channels', () => {
  it('collapsed by default: standard stems audible, detail channels hidden', () => {
    const { ctx, mixer } = makeMixer();
    expect(heard(ctx, mixer)).toEqual({ vocals: 1, drums: 1, bass: 1, 'drums.kick': 0, 'drums.snare': 0, 'drums.rest': 0 });
    expect(gainOf(ctx, mixer, 'drums.kick').calls).toEqual([]);
  });

  it('expanding replaces the parent by its detail channels (no doubling)', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setExpanded('drums', true, 0.5);
    expect(heard(ctx, mixer)).toEqual({ vocals: 1, drums: 0, bass: 1, 'drums.kick': 1, 'drums.snare': 1, 'drums.rest': 1 });
    mixer.setExpanded('drums', false, 0.7);
    expect(heard(ctx, mixer)).toEqual({ vocals: 1, drums: 1, bass: 1, 'drums.kick': 0, 'drums.snare': 0, 'drums.rest': 0 });
  });

  it('the swap ramps every channel at the one given context time (<= 10 ms)', () => {
    const { ctx, mixer } = makeMixer();
    ctx.currentTime = 2;
    mixer.setExpanded('drums', true, 2.01);
    for (const id of CHANNELS) {
      const calls = gainOf(ctx, mixer, id).calls;
      expect(calls.at(-2)).toEqual({ method: 'cancelScheduledValues', args: [2.01] });
      expect(calls.at(-1)?.args.slice(1)).toEqual([2.01, RAMP_TIME_CONSTANT]);
    }
    expect(mixer.isExpanded('drums')).toBe(true);
    expect(mixer.isExpanded('vocals')).toBe(false);
  });

  it('a solo on a hidden detail channel is ignored until the parent is expanded', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setChannel('drums.kick', settings({ solo: true }));
    expect(heard(ctx, mixer)).toMatchObject({ vocals: 1, drums: 1, bass: 1, 'drums.kick': 0 });
    mixer.setExpanded('drums', true, 0);
    expect(heard(ctx, mixer)).toEqual({ vocals: 0, drums: 0, bass: 0, 'drums.kick': 1, 'drums.snare': 0, 'drums.rest': 0 });
  });

  it('a solo on the expanded parent is kept but ignored, and restored on collapse (US2 AS3)', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setChannel('drums', settings({ solo: true, volume: 0.8 }));
    mixer.setExpanded('drums', true, 0);
    expect(heard(ctx, mixer)).toEqual({ vocals: 1, drums: 0, bass: 1, 'drums.kick': 1, 'drums.snare': 1, 'drums.rest': 1 });
    mixer.setExpanded('drums', false, 0);
    expect(heard(ctx, mixer)).toEqual({ vocals: 0, drums: 0.8, bass: 0, 'drums.kick': 0, 'drums.snare': 0, 'drums.rest': 0 });
  });

  it('solo across levels: a standard stem soloed next to an expanded group silences the details', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setExpanded('drums', true, 0);
    mixer.setChannel('vocals', settings({ solo: true }));
    mixer.setChannel('drums.snare', settings({ solo: true }));
    expect(heard(ctx, mixer)).toEqual({ vocals: 1, drums: 0, bass: 0, 'drums.kick': 0, 'drums.snare': 1, 'drums.rest': 0 });
  });

  it('mute on a detail channel silences only that channel and wins over its solo', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setExpanded('drums', true, 0);
    mixer.setChannel('drums.snare', settings({ mute: true }));
    expect(heard(ctx, mixer)).toEqual({ vocals: 1, drums: 0, bass: 1, 'drums.kick': 1, 'drums.snare': 0, 'drums.rest': 1 });
    mixer.setChannel('drums.snare', settings({ mute: true, solo: true }));
    expect(heard(ctx, mixer)).toMatchObject({ vocals: 0, 'drums.kick': 0, 'drums.snare': 0 });
  });

  it('detail channel settings apply volume and pan like standard channels', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setExpanded('drums', true, 0);
    mixer.setChannel('drums.kick', settings({ volume: 0.4, pan: 0.5 }));
    expect(gainOf(ctx, mixer, 'drums.kick').value).toBe(0.4);
    expect(ctx.panners[3]?.pan.value).toBe(0.5);
    expect(mixer.has('drums.kick')).toBe(true);
    expect(mixer.analyser('drums.kick')).toBe(ctx.analysers[4]);
  });

  it('rebuilding collapses every parent', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setExpanded('drums', true, 0);
    mixer.build(CHANNELS);
    expect(mixer.isExpanded('drums')).toBe(false);
    mixer.setChannel('vocals', settings());
    expect(heard(ctx, mixer)).toMatchObject({ drums: 1, 'drums.kick': 0 });
  });
});

import { describe, expect, it } from 'vitest';
import { RAMP_TIME_CONSTANT, gateOf } from '../src/channel';
import type { Mixer } from '../src/mixer';
import type { StemName } from '../src/types';
import type { MockAudioContext, MockAudioParam } from './mocks/audio-context';
import { channelGain, makeMixer as makeRigMixer, settings } from './mocks/mixer-rig';

const STEMS: StemName[] = ['vocals', 'drums', 'bass', 'other'];

function makeMixer(): { ctx: MockAudioContext; mixer: Mixer } {
  return makeRigMixer(STEMS);
}

function lastTarget(param: MockAudioParam): readonly number[] {
  const call = param.calls.filter((c) => c.method === 'setTargetAtTime').at(-1);
  return call?.args ?? [];
}

describe('mixer graph', () => {
  it('master gain -> master analyser -> destination', () => {
    const { ctx } = makeMixer();
    const [master] = ctx.gains;
    const [masterAnalyser] = ctx.analysers;
    expect(master?.connections).toEqual([masterAnalyser]);
    expect(masterAnalyser?.connections).toEqual([ctx.destination]);
  });

  it('per stem: gain -> panner -> analyser -> master gain', () => {
    const { ctx, mixer } = makeMixer();
    const master = ctx.gains[0];
    STEMS.forEach((name, i) => {
      const gain = ctx.gains[i + 1];
      const panner = ctx.panners[i];
      const analyser = ctx.analysers[i + 1];
      expect(mixer.input(name)).toBe(gain);
      expect(gain?.connections).toEqual([panner]);
      expect(panner?.connections).toEqual([analyser]);
      expect(analyser?.connections).toEqual([master]);
      expect(mixer.analyser(name)).toBe(analyser);
    });
    expect(mixer.analyser('master')).toBe(ctx.analysers[0]);
    expect(mixer.analyser('piano')).toBeUndefined();
    expect(mixer.has('master')).toBe(true);
    expect(mixer.has('vocals')).toBe(true);
    expect(mixer.has('piano')).toBe(false);
  });

  it('rebuilding disconnects the old channels and creates fresh ones', () => {
    const { ctx, mixer } = makeMixer();
    const oldVocals = ctx.gains[1];
    mixer.build(['piano']);
    expect(oldVocals?.disconnected).toBe(true);
    expect(mixer.has('vocals')).toBe(false);
    expect(mixer.has('piano')).toBe(true);
  });

  it('unknown stem names throw a readable Error', () => {
    const { mixer } = makeMixer();
    expect(() => mixer.input('guitar')).toThrow('Unknown stem "guitar"');
    expect(() => {
      mixer.setChannel('guitar', settings());
    }).toThrow('Unknown stem "guitar"');
  });
});

describe('gate', () => {
  it('gate = mute ? 0 : (anySolo && !solo ? 0 : 1)', () => {
    expect(gateOf(settings({ mute: true }), false)).toBe(0);
    expect(gateOf(settings({ mute: true, solo: true }), true)).toBe(0);
    expect(gateOf(settings(), true)).toBe(0);
    expect(gateOf(settings({ solo: true }), true)).toBe(1);
    expect(gateOf(settings(), false)).toBe(1);
  });
});

describe('channel settings', () => {
  it('gain = volume x gate, ramped with setTargetAtTime within 10 ms', () => {
    const { ctx, mixer } = makeMixer();
    ctx.currentTime = 2;
    mixer.setChannel('vocals', settings({ volume: 0.5 }));
    const gain = channelGain(ctx, mixer, 'vocals');
    expect(lastTarget(gain)).toEqual([0.5, 2, RAMP_TIME_CONSTANT]);
    expect(RAMP_TIME_CONSTANT * 3).toBeLessThanOrEqual(0.01);
    expect(gain.calls[0]).toEqual({ method: 'cancelScheduledValues', args: [2] });
  });

  it('mute silences only that channel; mute wins over solo', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setChannel('drums', settings({ volume: 0.8, mute: true, solo: true }));
    expect(channelGain(ctx, mixer, 'drums').value).toBe(0);
    expect(channelGain(ctx, mixer, 'bass').value).toBe(0);
    mixer.setChannel('drums', settings({ volume: 0.8, mute: true }));
    expect(channelGain(ctx, mixer, 'bass').value).toBe(1);
  });

  it('solo makes every non-soloed channel silent and restores them when cleared', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setChannel('bass', settings({ volume: 0.7 }));
    mixer.setChannel('vocals', settings({ volume: 0.6, solo: true }));
    expect(channelGain(ctx, mixer, 'vocals').value).toBe(0.6);
    expect(channelGain(ctx, mixer, 'bass').value).toBe(0);
    expect(channelGain(ctx, mixer, 'drums').value).toBe(0);
    mixer.setChannel('vocals', settings({ volume: 0.6 }));
    expect(channelGain(ctx, mixer, 'bass').value).toBe(0.7);
    expect(channelGain(ctx, mixer, 'drums').value).toBe(1);
  });

  it('pan -1..1 is applied (ramped) and clamped; volume is clamped to 0..1', () => {
    const { ctx, mixer } = makeMixer();
    mixer.setChannel('vocals', settings({ pan: -0.25, volume: 3 }));
    expect(ctx.panners[0]?.pan.value).toBe(-0.25);
    expect(channelGain(ctx, mixer, 'vocals').value).toBe(1);
    mixer.setChannel('vocals', settings({ pan: 5, volume: -1 }));
    expect(ctx.panners[0]?.pan.value).toBe(1);
    expect(channelGain(ctx, mixer, 'vocals').value).toBe(0);
    mixer.setChannel('vocals', settings({ pan: -5 }));
    expect(ctx.panners[0]?.pan.value).toBe(-1);
  });

  it('rejects non-finite values', () => {
    const { mixer } = makeMixer();
    expect(() => {
      mixer.setChannel('vocals', settings({ volume: Number.NaN }));
    }).toThrow('Invalid volume');
    expect(() => {
      mixer.setMaster(Number.POSITIVE_INFINITY);
    }).toThrow('Invalid volume');
  });
});

describe('master', () => {
  it('sets the master gain 0..1 with a ramp', () => {
    const { ctx, mixer } = makeMixer();
    ctx.currentTime = 1;
    mixer.setMaster(0.25);
    const master = ctx.gains[0]?.gain;
    expect(master?.value).toBe(0.25);
    expect(lastTarget(master ?? ctx.createGain().gain)).toEqual([0.25, 1, RAMP_TIME_CONSTANT]);
    mixer.setMaster(2);
    expect(master?.value).toBe(1);
  });
});

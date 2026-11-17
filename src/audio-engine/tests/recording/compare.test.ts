import { describe, expect, it } from 'vitest';
import { compareGate, recordingGate, takeGates } from '../../src/recording/compare';
import type { ChannelId, RecordingPart, TakeCompareMode } from '../../src/types';
import { channelGain, makeMixer, settings } from '../mocks/mixer-rig';

/** Compare and recording gates of the take (research R-10, contract 2.5.0). */
const CHANNELS: ChannelId[] = ['vocals', 'drums', 'bass', 'drums.kick', 'drums.snare', 'take'];

function gates(mode: TakeCompareMode, part: RecordingPart): Record<string, number> {
  return Object.fromEntries(CHANNELS.map((id) => [id, compareGate(id, mode, part)]));
}

describe('compareGate', () => {
  it('both: every channel plays', () => {
    expect(gates('both', 'drums')).toEqual({ vocals: 1, drums: 1, bass: 1, 'drums.kick': 1, 'drums.snare': 1, take: 1 });
  });

  it('take: the part stem and its detail channels are gated, the take plays', () => {
    expect(gates('take', 'drums')).toEqual({ vocals: 1, drums: 0, bass: 1, 'drums.kick': 0, 'drums.snare': 0, take: 1 });
    expect(gates('take', 'bass')).toEqual({ vocals: 1, drums: 1, bass: 0, 'drums.kick': 1, 'drums.snare': 1, take: 1 });
  });

  it('original: only the take is gated', () => {
    expect(gates('original', 'drums')).toEqual({ vocals: 1, drums: 1, bass: 1, 'drums.kick': 1, 'drums.snare': 1, take: 0 });
  });

  it('part none: take mode gates nothing, original gates the take', () => {
    expect(gates('take', 'none')).toEqual({ vocals: 1, drums: 1, bass: 1, 'drums.kick': 1, 'drums.snare': 1, take: 1 });
    expect(gates('original', 'none')).toMatchObject({ take: 0, vocals: 1 });
  });
});

describe('recordingGate', () => {
  it('silences the take while recording, and the part (with details) when muteOriginal', () => {
    const muted = Object.fromEntries(CHANNELS.map((id) => [id, recordingGate(id, { part: 'drums', muteOriginal: true })]));
    const kept = Object.fromEntries(CHANNELS.map((id) => [id, recordingGate(id, { part: 'drums', muteOriginal: false })]));
    expect(muted).toEqual({ vocals: 1, drums: 0, bass: 1, 'drums.kick': 0, 'drums.snare': 0, take: 0 });
    expect(kept).toEqual({ vocals: 1, drums: 1, bass: 1, 'drums.kick': 1, 'drums.snare': 1, take: 0 });
    expect(recordingGate('vocals', { part: 'none', muteOriginal: true })).toBe(1);
  });

  it('takeGates composes compare and recording (null = not recording)', () => {
    const gate = takeGates({ mode: 'take', part: 'drums', recording: null });
    expect([gate('drums'), gate('take')]).toEqual([0, 1]);
    const rec = takeGates({ mode: 'both', part: 'drums', recording: { part: 'vocals', muteOriginal: true } });
    expect([rec('drums'), rec('vocals'), rec('take')]).toEqual([1, 0, 0]);
  });
});

describe('mixer with the take channel', () => {
  function rig(): ReturnType<typeof makeMixer> {
    const built = makeMixer(['vocals', 'drums', 'drums.kick']);
    built.mixer.ensureTake();
    return built;
  }

  function heard(r: ReturnType<typeof makeMixer>): Record<string, number> {
    return Object.fromEntries((['vocals', 'drums', 'drums.kick', 'take'] as const).map((id) => [id, channelGain(r.ctx, r.mixer, id).value]));
  }

  it('the take is an audible-level channel: its solo isolates it, another solo silences it', () => {
    const r = rig();
    expect(heard(r)).toEqual({ vocals: 1, drums: 1, 'drums.kick': 0, take: 1 });
    r.mixer.setChannel('take', settings({ solo: true }));
    expect(heard(r)).toEqual({ vocals: 0, drums: 0, 'drums.kick': 0, take: 1 });
    r.mixer.setChannel('take', settings());
    r.mixer.setChannel('vocals', settings({ solo: true }));
    expect(heard(r)).toEqual({ vocals: 1, drums: 0, 'drums.kick': 0, take: 0 });
  });

  it('the extra gate multiplies the level gate, incl. expanded detail channels, at one context time', () => {
    const r = rig();
    r.mixer.setExpanded('drums', true, 0.2);
    r.mixer.setExtraGate((id) => compareGate(id, 'take', 'drums'), 0.5);
    expect(heard(r)).toEqual({ vocals: 1, drums: 0, 'drums.kick': 0, take: 1 });
    const kick = channelGain(r.ctx, r.mixer, 'drums.kick').calls.at(-1);
    expect(kick?.method).toBe('setTargetAtTime');
    expect(kick?.args[1]).toBe(0.5);
    r.mixer.setExtraGate(null, 0.6);
    expect(heard(r)).toEqual({ vocals: 1, drums: 0, 'drums.kick': 1, take: 1 });
  });

  it('a mute of the part beats compare: compare never un-mutes', () => {
    const r = rig();
    r.mixer.setChannel('vocals', settings({ mute: true }));
    r.mixer.setExtraGate((id) => compareGate(id, 'original', 'vocals'), 0);
    expect(heard(r)).toMatchObject({ vocals: 0, take: 0 });
  });

  it('ensureTake keeps settings given before it exists and survives no track change', () => {
    const { ctx, mixer } = makeMixer(['vocals']);
    expect(mixer.has('take')).toBe(false);
    mixer.setChannel('take', settings({ volume: 0.5 }));
    expect(mixer.has('take')).toBe(true);
    expect(mixer.analyser('take')).toBeDefined();
    mixer.ensureTake();
    expect(ctx.gains.length).toBeGreaterThan(0);
    mixer.build(['vocals']);
    expect(mixer.has('take')).toBe(false);
  });
});

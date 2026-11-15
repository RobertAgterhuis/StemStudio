/**
 * Consumer of the shared render vectors of feature 007 (`render-vectors.json` 1.0.0, T046).
 *
 * The engine owns the audible gate (mode `mix`, `channels = null`: what you hear) and the channel
 * strip `Gain(volume x gate) -> StereoPanner(pan) -> master Gain`. Every vector is checked against
 * the engine's real code: `gate.ts`, `Channel` and `Mixer`. Explicit selections and raw mode are
 * planner-only rules (no engine equivalent); for those only the channel strip of the listed
 * inputs is checked. The pan matrices are checked against the Web Audio StereoPannerNode formula
 * the engine relies on (the browser itself is checked by qa T048).
 */
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { Channel } from '../src/channel';
import { gatesOf, isHidden } from '../src/gate';
import type { ChannelId, ChannelSettings, ParentStemName } from '../src/types';
import { MockAudioContext, type MockAudioParam } from './mocks/audio-context';
import { makeMixer } from './mocks/mixer-rig';

interface VectorChannel {
  readonly stem: string;
  readonly volume: number;
  readonly pan: number;
  readonly mute: boolean;
  readonly solo: boolean;
}

interface VectorDetailChannel extends VectorChannel {
  readonly parent: string;
}

interface VectorInput {
  readonly channelId: string;
  readonly path: string;
  readonly gain: number;
  readonly pan: number;
}

interface GateVector {
  readonly name: string;
  readonly track: { readonly stems: readonly string[]; readonly split: Record<string, readonly string[]> };
  readonly mix: {
    readonly masterVolume: number;
    readonly channels: readonly VectorChannel[];
    readonly detailChannels: readonly VectorDetailChannel[];
    readonly expanded: readonly string[];
  };
  readonly mode: 'mix' | 'raw';
  readonly channels: readonly string[] | null;
  readonly expected: {
    readonly masterGain?: number;
    readonly inputs?: readonly VectorInput[];
    readonly error?: string;
  };
}

interface PanVector {
  readonly pan: number;
  readonly matrix: readonly [readonly [number, number], readonly [number, number]];
}

interface RenderVectors {
  readonly version: string;
  readonly gate: readonly GateVector[];
  readonly pan: readonly PanVector[];
}

const VECTORS_PATH = resolve(
  import.meta.dirname,
  '../../../specs/007-export-library/contracts/render-vectors.json',
);
const VECTORS = JSON.parse(readFileSync(VECTORS_PATH, 'utf8')) as RenderVectors;

/** Mixer default for a channel without an entry in the mix (data-model 007 section 4). */
const MIX_DEFAULT: ChannelSettings = { volume: 0.8, pan: 0, mute: false, solo: false };
const TOLERANCE = 1e-9;
const PAN_TOLERANCE = 1e-6;

/** Vectors the engine's audible gate decides: mode `mix` with `channels = null`. */
function isAudibleCase(v: GateVector): boolean {
  return v.mode === 'mix' && v.channels === null;
}

/** Channel ids of the vector's track in mixer order: standard stems, then detail stems. */
function channelIds(v: GateVector): ChannelId[] {
  const details = v.track.stems.flatMap((p) => (v.track.split[p] ?? []).map((d) => `${p}.${d}`));
  return [...v.track.stems, ...details] as ChannelId[];
}

function toSettings(c: VectorChannel): ChannelSettings {
  return { volume: c.volume, pan: c.pan, mute: c.mute, solo: c.solo };
}

/** Settings of every channel: the mix entry, or the mixer default. */
function settingsOf(v: GateVector): Map<ChannelId, ChannelSettings> {
  const map = new Map<ChannelId, ChannelSettings>(channelIds(v).map((id) => [id, MIX_DEFAULT]));
  v.mix.channels.forEach((c) => map.set(c.stem as ChannelId, toSettings(c)));
  v.mix.detailChannels.forEach((c) => map.set(`${c.parent}.${c.stem}` as ChannelId, toSettings(c)));
  return map;
}

function expectedInputs(v: GateVector): Map<string, VectorInput> {
  return new Map((v.expected.inputs ?? []).map((i) => [i.channelId, i]));
}

function lastTarget(param: MockAudioParam | undefined): number {
  const call = param?.calls.filter((c) => c.method === 'setTargetAtTime').at(-1);
  if (call?.args[0] === undefined) {
    throw new Error('parameter was never automated');
  }
  return call.args[0];
}

interface MixerResult {
  readonly gains: Map<ChannelId, number>;
  readonly pans: Map<ChannelId, number>;
  readonly master: number;
}

/** Drives a real Mixer with the vector's mix and returns the applied gain/pan per channel. */
function runMixer(v: GateVector): MixerResult {
  const ids = channelIds(v);
  const { ctx, mixer } = makeMixer(ids);
  for (const p of v.mix.expanded) mixer.setExpanded(p as ParentStemName, true, 0);
  for (const [id, s] of settingsOf(v)) mixer.setChannel(id, s);
  mixer.setMaster(v.mix.masterVolume);
  // gains[0] is the master bus; channel i owns gains[i + 1] and panners[i] (built in id order).
  return {
    gains: new Map(ids.map((id, i) => [id, lastTarget(ctx.gains[i + 1]?.gain)])),
    pans: new Map(ids.map((id, i) => [id, lastTarget(ctx.panners[i]?.pan)])),
    master: lastTarget(ctx.gains[0]?.gain),
  };
}

/** Web Audio StereoPannerNode, stereo input: returns `[[a, b], [c, d]]` (L' = aL + bR, R' = cL + dR). */
function stereoPannerMatrix(pan: number): number[][] {
  const x = pan <= 0 ? pan + 1 : pan;
  const gL = Math.cos((x * Math.PI) / 2);
  const gR = Math.sin((x * Math.PI) / 2);
  return pan <= 0
    ? [
        [1, gL],
        [0, gR],
      ]
    : [
        [gL, 0],
        [gR, 1],
      ];
}

/** The gate cases of 1.0.0 that the engine's audible gate decides. */
const AUDIBLE_CASES = ['no-solo', 'solo-standard', 'solo-hidden-ignored', 'mute-and-solo'].concat(
  ['expanded-muted-detail', 'collapsed-details-hidden', 'nothing-audible', 'four-stem-model'],
);

describe('render-vectors.json', () => {
  it('is version 1.0.x with gate and pan cases', () => {
    expect(VECTORS.version).toMatch(/^1\.0\.\d+$/);
    expect(VECTORS.gate.length).toBeGreaterThan(0);
    expect(VECTORS.pan.length).toBeGreaterThan(0);
  });

  it('every vector is classified: audible gate, explicit mix selection or raw', () => {
    const audible = VECTORS.gate.filter(isAudibleCase).map((v) => v.name);
    const explicitMix = VECTORS.gate.filter((v) => v.mode === 'mix' && v.channels !== null);
    const raw = VECTORS.gate.filter((v) => v.mode === 'raw');
    expect(audible.length + explicitMix.length + raw.length).toBe(VECTORS.gate.length);
    expect(audible).toEqual(AUDIBLE_CASES);
  });
});

describe.each(VECTORS.gate.filter(isAudibleCase).map((v) => [v.name, v] as const))(
  'audible gate vector %s',
  (_name, vector) => {
    it('gate.ts: gatesOf gives 1 exactly for the expected inputs', () => {
      const expanded = new Set(vector.mix.expanded as ParentStemName[]);
      const settings = settingsOf(vector);
      const ids = channelIds(vector);
      const gates = gatesOf(ids.map((id) => ({ settings: settings.get(id) ?? MIX_DEFAULT, hidden: isHidden(id, expanded) })));
      const expected = expectedInputs(vector);
      expect(Object.fromEntries(ids.map((id, i) => [id, gates[i]]))).toEqual(
        Object.fromEntries(ids.map((id) => [id, expected.has(id) ? 1 : 0])),
      );
    });

    it('Mixer: per-channel gain (volume x gate), pan and master match the vector', () => {
      const result = runMixer(vector);
      const expected = expectedInputs(vector);
      const settings = settingsOf(vector);
      result.gains.forEach((gain, id) => {
        expect(gain, `gain of ${id}`).toBeCloseTo(expected.get(id)?.gain ?? 0, 9);
      });
      expected.forEach((input, id) => {
        expect(result.pans.get(id as ChannelId), `pan of ${id}`).toBeCloseTo(input.pan, 9);
      });
      result.pans.forEach((pan, id) => {
        expect(Math.abs(pan - (settings.get(id)?.pan ?? 0))).toBeLessThan(TOLERANCE);
      });
      if (vector.expected.error === undefined) {
        expect(result.master).toBeCloseTo(vector.expected.masterGain ?? Number.NaN, 9);
      }
    });

    it('an export error means the engine is silent on every channel', () => {
      const silent = [...runMixer(vector).gains.values()].every((g) => g === 0);
      expect(silent).toBe(vector.expected.error === 'nothing-to-export');
    });
  },
);

describe.each(VECTORS.gate.filter((v) => !isAudibleCase(v)).map((v) => [v.name, v] as const))(
  'selection vector %s (planner rule)',
  (_name, vector) => {
    it('the channel strip of every listed input gives the vector gain and pan at gate 1', () => {
      const settings = settingsOf(vector);
      (vector.expected.inputs ?? []).forEach((input) => {
        const ctx = new MockAudioContext();
        const channel = new Channel(ctx.asAudioContext(), ctx.destination as unknown as AudioNode);
        const s = settings.get(input.channelId as ChannelId) ?? MIX_DEFAULT;
        channel.configure(vector.mode === 'raw' ? { ...s, volume: 1, pan: 0 } : s);
        channel.apply(1, 0);
        expect(lastTarget(ctx.gains[0]?.gain)).toBeCloseTo(input.gain, 9);
        expect(lastTarget(ctx.panners[0]?.pan)).toBeCloseTo(input.pan, 9);
      });
    });
  },
);

describe.each(VECTORS.pan.map((v) => [v.pan, v] as const))('pan vector %d', (_pan, vector) => {
  it('the engine passes the pan to the StereoPannerNode unchanged', () => {
    const ctx = new MockAudioContext();
    const channel = new Channel(ctx.asAudioContext(), ctx.destination as unknown as AudioNode);
    channel.configure({ volume: 1, pan: vector.pan, mute: false, solo: false });
    channel.apply(1, 0);
    expect(lastTarget(ctx.panners[0]?.pan)).toBe(vector.pan);
  });

  it('the matrix is the Web Audio StereoPannerNode law for stereo input (1e-6)', () => {
    const actual = stereoPannerMatrix(vector.pan);
    vector.matrix.forEach((row, r) => {
      row.forEach((value, c) => {
        expect(Math.abs((actual[r]?.[c] ?? Number.NaN) - value)).toBeLessThan(PAN_TOLERANCE);
      });
    });
  });
});

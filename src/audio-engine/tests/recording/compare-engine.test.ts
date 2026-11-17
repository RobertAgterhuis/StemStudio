import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RAMP_TIME_CONSTANT } from '../../src/channel';
import { createEngine, type AudioEngine, type RecordingPart } from '../../src/index';
import type { MockAudioBuffer, MockGainNode } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { RecordingContext } from '../mocks/media';

/** `setTakeCompare` (015 US4, research R-10, FR-013). */
const SR = 44100;
/** Byte sizes identify the decoded buffers (and so the channels their sources feed). */
const BYTES = { vocals: 101, drums: 102, kick: 103, rest: 104, take: 105 } as const;

interface Rig {
  readonly ctx: RecordingContext;
  readonly engine: AudioEngine;
}

async function makeRig(part: RecordingPart = 'drums'): Promise<Rig> {
  const ctx = new RecordingContext();
  const stub = new FetchStub();
  Object.entries(BYTES).forEach(([name, bytes]) => {
    ctx.decodeDurations.set(bytes, name === 'take' ? 5 : 20);
    stub.reply(`/${name}`, { bytes });
  });
  const engine = createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), fetch: stub.fetch });
  await engine.loadTrack({
    stems: [
      { name: 'vocals', url: '/vocals' },
      { name: 'drums', url: '/drums' },
    ],
    beatGrid: null,
    groups: [{ parent: 'drums', stems: [{ name: 'kick', url: '/kick' }, { name: 'rest', url: '/rest' }] }],
  });
  await engine.loadTake({ takeId: 't', source: { url: '/take', startSample: 0, lengthSamples: SR, channels: 1 }, part });
  await engine.play();
  return { ctx, engine };
}

/** Whether a source buffer is the decoded file of `bytes` (the take: its sliced 1 s buffer). */
function feeds(buffer: MockAudioBuffer | null, bytes: number): boolean {
  return bytes === BYTES.take ? buffer?.length === SR : buffer?.sourceBytes === bytes;
}

/** Channel gain of the channel fed by the buffer of `bytes` (the first source of it). */
function gainOf(rig: Rig, bytes: number): MockGainNode {
  const source = rig.ctx.sources.find((s) => s.connections.length > 0 && feeds(s.buffer, bytes));
  const gain = source?.connections[0] as MockGainNode | undefined;
  if (gain === undefined) {
    throw new Error(`no channel for ${String(bytes)}`);
  }
  return gain;
}

function heard(rig: Rig): Record<string, number> {
  return { vocals: gainOf(rig, BYTES.vocals).gain.value, drums: gainOf(rig, BYTES.drums).gain.value, take: gainOf(rig, BYTES.take).gain.value };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('requestAnimationFrame', () => 0);
  vi.stubGlobal('cancelAnimationFrame', () => undefined);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('setTakeCompare', () => {
  it('both / take / original gate the part and the take', async () => {
    const rig = await makeRig();
    expect(heard(rig)).toEqual({ vocals: 1, drums: 1, take: 1 });
    rig.engine.setTakeCompare('take');
    expect(heard(rig)).toEqual({ vocals: 1, drums: 0, take: 1 });
    rig.engine.setTakeCompare('original');
    expect(heard(rig)).toEqual({ vocals: 1, drums: 1, take: 0 });
    rig.engine.setTakeCompare('both');
    expect(heard(rig)).toEqual({ vocals: 1, drums: 1, take: 1 });
  });

  it('swaps every gate at one context time with ramps of <= 10 ms', async () => {
    const rig = await makeRig();
    rig.ctx.currentTime = 1.5;
    rig.engine.setTakeCompare('original');
    const steps = [BYTES.drums, BYTES.take].map((bytes) => gainOf(rig, bytes).gain.calls.at(-1));
    steps.forEach((step) => {
      expect(step).toMatchObject({ method: 'setTargetAtTime', args: [expect.any(Number), 1.5, RAMP_TIME_CONSTANT] });
    });
    expect(RAMP_TIME_CONSTANT * 3).toBeLessThanOrEqual(0.01);
  });

  it('leaves the user mute and solo flags untouched', async () => {
    const rig = await makeRig();
    rig.engine.setChannel('vocals', { volume: 1, pan: 0, mute: true, solo: false });
    rig.engine.setTakeCompare('take');
    rig.engine.setTakeCompare('both');
    expect(heard(rig)).toEqual({ vocals: 0, drums: 1, take: 1 });
    rig.engine.setChannel('take', { volume: 1, pan: 0, mute: false, solo: true });
    rig.engine.setTakeCompare('original');
    expect(heard(rig)).toEqual({ vocals: 0, drums: 0, take: 0 });
  });

  it('gates the expanded detail channels of the part', async () => {
    const rig = await makeRig();
    await rig.engine.setExpanded('drums', true);
    rig.engine.setTakeCompare('take');
    expect([gainOf(rig, BYTES.kick).gain.value, gainOf(rig, BYTES.rest).gain.value, gainOf(rig, BYTES.drums).gain.value]).toEqual([0, 0, 0]);
    rig.engine.setTakeCompare('both');
    expect([gainOf(rig, BYTES.kick).gain.value, gainOf(rig, BYTES.drums).gain.value]).toEqual([1, 0]);
  });

  it('with part none: take keeps every stem, original gates the take', async () => {
    const rig = await makeRig('none');
    rig.engine.setTakeCompare('take');
    expect(heard(rig)).toEqual({ vocals: 1, drums: 1, take: 1 });
    rig.engine.setTakeCompare('original');
    expect(heard(rig)).toEqual({ vocals: 1, drums: 1, take: 0 });
  });

  it('rejects an unknown mode; unloadTake resets to both', async () => {
    const rig = await makeRig();
    expect(() => {
      rig.engine.setTakeCompare('mix' as 'both');
    }).toThrow('Invalid compare mode: mix');
    rig.engine.setTakeCompare('take');
    rig.engine.unloadTake();
    expect(gainOf(rig, BYTES.drums).gain.value).toBe(1);
  });
});

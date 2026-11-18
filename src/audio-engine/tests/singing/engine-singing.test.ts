import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEngine, type AudioEngine, type SingingFeedbackRequest } from '../../src/index';
import { PITCH_PROCESSOR_NAME } from '../../src/pitch/worklet';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import { stubCanvas } from '../mocks/canvas';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { stubResizeObserver } from '../mocks/layout';
import { FakeMediaDevices, RecordingContext } from '../mocks/media';
import type { MockAudioWorkletNode } from '../mocks/worklet-node';
import { practice } from '../mocks/practice-rig';

/** Singing feedback through the engine (2.6.0): 015 input, pitch worklet, scorer, pitch view and callbacks. */
interface Rig {
  readonly ctx: RecordingContext;
  readonly ref: FakeDotNetRef;
  readonly engine: AudioEngine;
  readonly host: HTMLElement;
}

const REQUEST: SingingFeedbackRequest = {
  pitchViewElementId: 'pitch-view',
  reference: { hopMs: 10, frames: new Array<number>(200).fill(6900) },
  units: [
    { index: 0, startMs: 0, endMs: 1000 },
    { index: 1, startMs: 1000, endMs: 2000 },
  ],
  toleranceCents: 100,
  roundTripMs: 0,
  latencyMethod: 'loopback',
  reducedMotion: false,
};

async function makeRig(): Promise<Rig> {
  document.body.replaceChildren();
  const host = document.createElement('div');
  host.id = 'pitch-view';
  document.body.append(host);
  const ctx = new RecordingContext();
  ctx.decodeDurations.set(100, 20);
  const ref = new FakeDotNetRef();
  const engine = createEngine(ref, {
    audioContextFactory: () => ctx.asAudioContext(),
    fetch: new FetchStub().reply('/vocals', { bytes: 100 }).fetch,
    mediaDevices: new FakeMediaDevices(),
    timeStretchFactory: (context) => Promise.resolve(new FakeStretchStage(context)),
    pitchWorkletUrl: '/pitch.js',
  });
  await engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
  await engine.openInput({ deviceId: 'usb', mode: 'mono-1' });
  return { ctx, ref, engine, host };
}

function tracker(rig: Rig): MockAudioWorkletNode {
  const node = rig.ctx.workletNodes.filter((n) => n.name === PITCH_PROCESSOR_NAME).at(-1);
  if (node === undefined) {
    throw new Error('no pitch node');
  }
  return node;
}

/** Posts frames for song ms `from..to` (playing from context time 0.1 = song 0), `cents` each. */
function sing(rig: Rig, from: number, to: number, cents: number | null): void {
  for (let ms = from; ms <= to; ms += 10) {
    rig.ctx.currentTime = 0.1 + ms / 1000;
    tracker(rig).port.reply({ t: rig.ctx.currentTime, cents, clarity: 1 });
  }
}

beforeEach(() => {
  vi.stubGlobal('isSecureContext', true);
  vi.stubGlobal('requestAnimationFrame', () => 0);
  vi.stubGlobal('cancelAnimationFrame', () => undefined);
  vi.stubGlobal('AudioWorkletNode', function AudioWorkletNode(context: RecordingContext, name: string, options: AudioWorkletNodeOptions) {
    return context.createWorkletNode(name, options);
  });
  stubResizeObserver();
  stubCanvas();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('engine singing feedback', () => {
  it('starts on the open input, scores units while playing and reports them to .NET', async () => {
    const rig = await makeRig();
    await rig.engine.startSingingFeedback(REQUEST);
    expect(rig.ctx.audioWorklet.modules).toContain('/pitch.js');
    expect(rig.ref.calls.filter((c) => c.method === 'OnSingingStateChanged').map((c) => c.args)).toEqual([
      ['starting', null],
      ['running', null],
    ]);
    expect(rig.host.dataset.state).toBe('running');
    expect(rig.engine.getRoundTripLatency()).toEqual({ ms: 0, source: 'loopback' });
    rig.ctx.currentTime = 0;
    await rig.engine.play();
    sing(rig, 0, 1000, 6900);
    expect(rig.ref.calls.filter((c) => c.method === 'OnSingingUnitScored').map((c) => c.args)).toEqual([[0, 100, 1]]);
    const summary = rig.engine.stopSingingFeedback();
    expect(summary).toEqual({ unitsScored: 1, average: 100, best: { index: 0, score: 100 }, toleranceCents: 100, latencySource: 'loopback' });
    expect(rig.host.dataset.state).toBe('stopped');
    expect(rig.host.querySelector('canvas')).not.toBeNull();
  });

  it('a seek discards the partial unit; the tolerance and transpose apply to later units', async () => {
    const rig = await makeRig();
    await rig.engine.startSingingFeedback(REQUEST);
    rig.ctx.currentTime = 0;
    await rig.engine.play();
    sing(rig, 0, 500, 6900);
    rig.engine.pause();
    rig.engine.seek(0);
    rig.engine.setSingingTolerance(10);
    rig.ctx.currentTime = 0;
    await rig.engine.play();
    sing(rig, 0, 1000, 6950);
    expect(rig.ref.calls.filter((c) => c.method === 'OnSingingUnitScored').map((c) => c.args)).toEqual([[0, 0, 1]]);
    rig.engine.stopSingingFeedback();
  });

  it('re-spells nothing but moves the targets with the 012 transpose', async () => {
    const rig = await makeRig();
    await rig.engine.setPractice(practice({ transposeSemitones: 1 }));
    await rig.engine.startSingingFeedback(REQUEST);
    rig.ctx.currentTime = 0;
    await rig.engine.play();
    sing(rig, -200, 1300, 7000);
    expect(rig.ref.calls.filter((c) => c.method === 'OnSingingUnitScored').map((c) => c.args[1])).toEqual([100]);
  });

  it('dispose stops a running feedback', async () => {
    const rig = await makeRig();
    await rig.engine.startSingingFeedback(REQUEST);
    await rig.engine.dispose();
    expect(tracker(rig).disconnected).toBe(true);
    expect(rig.host.children).toHaveLength(0);
  });
});

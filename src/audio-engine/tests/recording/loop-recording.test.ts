import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEngine, type AudioEngine, type BeatGrid, type CapturedTake, type MetronomeSettings, type RecordOptions } from '../../src/index';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { practice } from '../mocks/practice-rig';
import { FakeMediaDevices, RecordingContext } from '../mocks/media';

/** Recording during an A-B loop: one take per pass (015 US3, FR-010, data-model 3.2). */
const SR = 44100;
const OPTIONS: RecordOptions = { latencySamples: 441, latencyMethod: 'loopback', part: 'vocals', muteOriginal: true, countInBars: 0 };
const GRID: BeatGrid = {
  bpm: 120,
  beats: Array.from({ length: 80 }, (_, i) => i * 0.5),
  downbeats: Array.from({ length: 20 }, (_, i) => i * 2),
  beatsPerBar: 4,
  confidence: 0.9,
  reliable: true,
};
const METRONOME: MetronomeSettings = { enabled: false, volume: 0.6, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 };

interface Rig {
  readonly ctx: RecordingContext;
  readonly ref: FakeDotNetRef;
  readonly engine: AudioEngine;
  fed: number;
}

/** A 40 s track with the input open; the capture sample at context frame f is f / 2^24. */
async function makeRig(beatGrid: BeatGrid | null = null): Promise<Rig> {
  const ctx = new RecordingContext();
  ctx.decodeDurations.set(100, 40);
  const ref = new FakeDotNetRef();
  const engine = createEngine(ref, {
    audioContextFactory: () => ctx.asAudioContext(),
    fetch: new FetchStub().reply('/vocals', { bytes: 100 }).fetch,
    mediaDevices: new FakeMediaDevices(),
    timeStretchFactory: (context) => Promise.resolve(new FakeStretchStage(context)),
  });
  await engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid });
  engine.setMetronome(METRONOME);
  await engine.openInput({ deviceId: 'usb', mode: 'mono-1' });
  return { ctx, ref, engine, fed: 0 };
}

function feed(rig: Rig): void {
  const to = Math.floor(rig.ctx.currentTime * SR);
  while (rig.fed + 2048 <= to) {
    const frame = rig.fed;
    rig.ctx.captureNodes().at(-1)?.port.reply({ type: 'block', frame, channels: [Float32Array.from({ length: 2048 }, (_, i) => (frame + i) / 2 ** 24)] });
    rig.fed += 2048;
  }
}

async function run(rig: Rig, seconds: number): Promise<void> {
  for (let i = 0; i < Math.round(seconds / 0.025); i++) {
    rig.ctx.currentTime += 0.025;
    feed(rig);
    await vi.advanceTimersByTimeAsync(25);
  }
}

/** Takes of the sessions so far (`OnTakesCaptured`). */
function sessions(rig: Rig): CapturedTake[][] {
  return rig.ref.argsOf('OnTakesCaptured') as CapturedTake[][];
}

function summary(takes: readonly CapturedTake[] | undefined): unknown[] {
  return (takes ?? []).map((t) => [t.passIndex, t.partial, t.startSample, t.lengthSamples]);
}

/** Context frame the take's first sample was captured at, decoded from its WAV (sample = frame / 2^24, so int24 * 2). */
async function sourceFrame(rig: Rig, take: CapturedTake | undefined): Promise<number> {
  const view = new DataView(await rig.engine.captureWav(take?.captureKey ?? '').arrayBuffer());
  return (view.getUint16(44, true) | (view.getUint8(46) << 16)) * 2;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('isSecureContext', true);
  vi.stubGlobal('requestAnimationFrame', () => 0);
  vi.stubGlobal('cancelAnimationFrame', () => undefined);
  vi.stubGlobal('AudioWorkletNode', function AudioWorkletNode(context: RecordingContext, name: string, options: AudioWorkletNodeOptions) {
    return context.createWorkletNode(name, options);
  });
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('loop recording', () => {
  it('records one take per pass of the loop; a partial last pass of >= 1 s is kept and marked', async () => {
    const rig = await makeRig();
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.startRecording(OPTIONS);
    await run(rig, 0.1 + 2 * 3 + 1.5);
    const stopping = rig.engine.stopRecording();
    await run(rig, 0.3);
    await stopping;
    const passes = sessions(rig)[0];
    expect(summary(passes)).toEqual([
      [1, false, 2 * SR, 2 * SR],
      [2, false, 2 * SR, 2 * SR],
      [3, false, 2 * SR, 2 * SR],
      [4, true, 2 * SR, 1.5 * SR],
    ]);
    expect(new Set(passes?.map((t) => t.sessionId)).size).toBe(1);
    const frames = await Promise.all((passes ?? []).slice(0, 2).map((take) => sourceFrame(rig, take)));
    [0.1, 2.1].forEach((heard, k) => {
      expect(Math.abs((frames[k] ?? 0) - (Math.round(heard * SR) + 441))).toBeLessThanOrEqual(1);
    });
  });

  it('drops a partial last pass shorter than 1 s', async () => {
    const rig = await makeRig();
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.startRecording(OPTIONS);
    await run(rig, 0.1 + 2 * 2 + 0.5);
    const stopping = rig.engine.stopRecording();
    await run(rig, 0.3);
    expect((await stopping).map((t) => t.passIndex)).toEqual([1, 2]);
  });

  it('does not capture the count-in before every pass (explicit passes with gaps)', async () => {
    const rig = await makeRig(GRID);
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.setPractice(practice({ countIn: { bars: 1, everyPass: true } }));
    await rig.engine.startRecording({ ...OPTIONS, countInBars: 1 });
    // Count-in (2 s) + pass (2 s) per pass.
    await run(rig, 0.1 + 4 * 3 + 0.5);
    const stopping = rig.engine.stopRecording();
    await run(rig, 0.3);
    const takes = await stopping;
    expect(summary(takes)).toEqual([
      [1, false, 2 * SR, 2 * SR],
      [2, false, 2 * SR, 2 * SR],
      [3, false, 2 * SR, 2 * SR],
    ]);
    expect(rig.engine.getPracticeStatus().countIn.beatsTotal).toBe(4);
  });

  it('stops after 50 passes by default and keeps all of them', async () => {
    const rig = await makeRig();
    rig.engine.setLoop({ start: 1, end: 1.5, enabled: true });
    await rig.engine.startRecording(OPTIONS);
    await run(rig, 0.1 + 0.5 * 50 + 0.6);
    const [takes] = sessions(rig);
    expect(takes).toHaveLength(50);
    expect(takes?.every((t) => !t.partial && t.lengthSamples === 0.5 * SR)).toBe(true);
    expect(takes?.at(-1)?.passIndex).toBe(50);
  });

  it('stops at the capture limit (maxSeconds; 1800 s by default) and keeps every pass', async () => {
    const rig = await makeRig();
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.startRecording({ ...OPTIONS, maxSeconds: 5 });
    await run(rig, 6);
    const [takes] = sessions(rig);
    expect(takes?.map((t) => [t.passIndex, t.partial])).toEqual([
      [1, false],
      [2, false],
    ]);
  });

  it('a loaded loop take replays in every pass (re-scheduled, never looped)', async () => {
    const rig = await makeRig();
    rig.engine.setLoop({ start: 2, end: 4, enabled: true });
    await rig.engine.startRecording(OPTIONS);
    await run(rig, 0.1 + 2 * 2 + 0.2);
    const stopping = rig.engine.stopRecording();
    await run(rig, 0.3);
    const [, second] = await stopping;
    await rig.engine.loadTake({ takeId: second?.captureKey ?? '', source: { captureKey: second?.captureKey ?? '' }, part: 'vocals' });
    rig.engine.seek(2);
    const start = rig.ctx.currentTime + 0.1;
    await rig.engine.play();
    const takeSources = rig.ctx.sources.filter((s) => s.buffer?.length === 2 * SR && s.connections.length > 0);
    expect(takeSources.slice(0, 3).map((s) => [s.loop, Math.round((s.startCalls[0]?.when ?? 0) * 1e6) / 1e6])).toEqual([0, 1, 2].map((k) => [false, Math.round((start + 2 * k) * 1e6) / 1e6]));
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEngine, type AudioEngine, type BeatGrid, type CapturedTake, type RecordOptions } from '../../src/index';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import type { MockAudioBuffer, MockAudioBufferSourceNode, MockGainNode } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { FakeMediaDevices, RecordingContext } from '../mocks/media';

/** The 2.5.0 recording surface wired in the engine (015 US1, T034/T035). */
const SR = 44100;
const OPTIONS: RecordOptions = { latencySamples: 441, latencyMethod: 'manual', part: 'guitar', muteOriginal: true, countInBars: 0 };
const GRID: BeatGrid = {
  bpm: 120,
  beats: Array.from({ length: 40 }, (_, i) => i * 0.5),
  downbeats: Array.from({ length: 10 }, (_, i) => i * 2),
  beatsPerBar: 4,
  confidence: 0.9,
  reliable: true,
};

interface Rig {
  readonly ctx: RecordingContext;
  readonly ref: FakeDotNetRef;
  readonly stub: FetchStub;
  readonly devices: FakeMediaDevices;
  readonly engine: AudioEngine;
  fed: number;
}

function makeRig(): Rig {
  const ctx = new RecordingContext();
  ctx.decodeDurations.set(100, 20);
  ctx.decodeDurations.set(101, 20);
  ctx.decodeDurations.set(300, 5);
  const ref = new FakeDotNetRef();
  const stub = new FetchStub().reply('/vocals', { bytes: 100 }).reply('/guitar', { bytes: 101 }).reply('/take.flac', { bytes: 300 });
  const devices = new FakeMediaDevices();
  const engine = createEngine(ref, {
    audioContextFactory: () => ctx.asAudioContext(),
    fetch: stub.fetch,
    mediaDevices: devices,
    timeStretchFactory: (context) => Promise.resolve(new FakeStretchStage(context)),
  });
  return { ctx, ref, stub, devices, engine, fed: 0 };
}

async function loaded(beatGrid: BeatGrid | null = null): Promise<Rig> {
  const rig = makeRig();
  await rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }, { name: 'guitar', url: '/guitar' }], beatGrid });
  return rig;
}

async function opened(): Promise<Rig> {
  const rig = await loaded();
  await rig.engine.openInput({ deviceId: 'usb', mode: 'mono-1' });
  return rig;
}

function feed(rig: Rig): void {
  const to = Math.floor(rig.ctx.currentTime * SR);
  while (rig.fed + 2048 <= to) {
    rig.ctx.captureNodes().at(-1)?.port.reply({ type: 'block', frame: rig.fed, channels: [new Float32Array(2048).fill(0.5)] });
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

async function record(rig: Rig, seconds = 1, options: RecordOptions = OPTIONS): Promise<CapturedTake[]> {
  await rig.engine.startRecording(options);
  await run(rig, seconds);
  const stopping = rig.engine.stopRecording();
  await run(rig, 0.3);
  return stopping;
}

/** The only take, failing the test when there is none. */
function first(takes: readonly CapturedTake[]): CapturedTake {
  const [take] = takes;
  if (take === undefined) {
    throw new Error('no take');
  }
  return take;
}

function states(rig: Rig): unknown[] {
  return rig.ref.argsOf('OnRecordingStateChanged');
}

/** Channel gain of the guitar stem: the gain its running source connects to. */
function guitarGain(rig: Rig): MockGainNode | undefined {
  const source = rig.ctx.sources.find((s) => s.connections.length > 0 && bytesOf(s.buffer) === 101);
  return source?.connections[0] as MockGainNode | undefined;
}

/** `when` of the first start call of the last source matching `match` (NaN when none). */
function startOf(rig: Rig, match: (source: MockAudioBufferSourceNode) => boolean): number {
  return rig.ctx.sources.filter(match).at(-1)?.startCalls[0]?.when ?? Number.NaN;
}

function bytesOf(buffer: MockAudioBuffer | null): number {
  return buffer?.sourceBytes ?? -1;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('isSecureContext', true);
  vi.stubGlobal('requestAnimationFrame', () => 0);
  vi.stubGlobal('cancelAnimationFrame', () => undefined);
  vi.stubGlobal(
    'AudioWorkletNode',
    function AudioWorkletNode(context: RecordingContext, name: string, options: AudioWorkletNodeOptions) {
      return context.createWorkletNode(name, options);
    },
  );
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('support and devices', () => {
  it('recordingSupport answers without opening anything', async () => {
    const rig = makeRig();
    expect(await rig.engine.recordingSupport()).toEqual({ supported: true, reasonCode: null, reason: null, permission: 'unknown' });
    vi.stubGlobal('isSecureContext', false);
    expect((await makeRig().engine.recordingSupport()).reasonCode).toBe('insecureContext');
    expect(rig.devices.requests).toEqual([]);
  });

  it('listInputDevices lists the audio inputs; devicechange calls OnInputDevicesChanged', async () => {
    const rig = makeRig();
    expect((await rig.engine.listInputDevices()).map((d) => d.deviceId)).toEqual(['usb', 'mic']);
    rig.devices.dispatchEvent(new Event('devicechange'));
    expect(rig.ref.calls.filter((c) => c.method === 'OnInputDevicesChanged')).toHaveLength(1);
  });
});

describe('input', () => {
  it('openInput opens the device, connects the capture and attaches the input meter; closeInput releases it', async () => {
    const rig = await opened();
    expect(states(rig)).toEqual(['open']);
    expect(rig.ctx.captureNodes()).toHaveLength(1);
    expect(() => {
      rig.engine.attachMeter('input', 'meter-input');
    }).not.toThrow();
    rig.engine.closeInput();
    expect([rig.devices.track?.stopped, states(rig)]).toEqual([true, ['open', 'closed']]);
    rig.engine.closeInput();
  });

  it('a failing capture worklet closes the input again', async () => {
    const rig = await loaded();
    rig.ctx.audioWorklet.error = new Error('404');
    await expect(rig.engine.openInput({ deviceId: 'usb', mode: 'stereo' })).rejects.toThrow(/^unsupported: /);
    expect(rig.devices.track?.stopped).toBe(true);
  });

  it('setMonitoring routes the input to the destination with the volume', async () => {
    const rig = await opened();
    rig.engine.setMonitoring({ enabled: true, volume: 0.6 });
    const monitorGain = rig.ctx.gains.find((g) => g.connections.includes(rig.ctx.destination) && g.gain.value === 0.6);
    expect(monitorGain).toBeDefined();
  });

  it('an unplugged device calls OnInputLost and closes', async () => {
    const rig = await opened();
    rig.devices.track?.end();
    await vi.advanceTimersByTimeAsync(10);
    expect([rig.ref.argsOf('OnInputLost'), states(rig).at(-1)]).toEqual([['ended'], 'closed']);
  });
});

describe('recording', () => {
  it('records a take: states, mute-original gate, locked controls, OnTakesCaptured and the WAV', async () => {
    const rig = await opened();
    await rig.engine.startRecording(OPTIONS);
    expect(states(rig)).toEqual(['open', 'recording']);
    const gain = guitarGain(rig)?.gain;
    expect(gain?.value).toBe(0);
    const locked = 'Stop the recording first';
    expect(() => {
      rig.engine.seek(3);
    }).toThrow(locked);
    expect(() => {
      rig.engine.setLoop({ start: 1, end: 2, enabled: true });
    }).toThrow(locked);
    expect(() => {
      rig.engine.closeInput();
    }).toThrow(locked);
    await expect(rig.engine.setPractice({ speedPercent: 90, transposeSemitones: 0, trainer: { enabled: false, startPercent: 70, stepPercent: 5, targetPercent: 100, passesPerStep: 2 }, countIn: { bars: 0, everyPass: false } })).rejects.toThrow(locked);
    await expect(rig.engine.loadTrack({ stems: [], beatGrid: null })).rejects.toThrow(locked);
    await expect(rig.engine.openInput({ deviceId: 'mic', mode: 'mono-1' })).rejects.toThrow(locked);
    await run(rig, 1);
    const stopping = rig.engine.stopRecording();
    await run(rig, 0.3);
    const takes = await stopping;
    expect(states(rig)).toEqual(['open', 'recording', 'finishing', 'open']);
    expect(rig.ref.argsOf('OnTakesCaptured')).toEqual([takes]);
    expect(rig.ref.argsOf('OnRecordingProgress').length).toBeGreaterThanOrEqual(3);
    expect(gain?.value).toBe(1);
    const take = first(takes);
    expect(rig.engine.captureWav(take.captureKey).size).toBe(44 + take.lengthSamples * 3);
    rig.engine.releaseCapture(take.captureKey);
    expect(() => rig.engine.captureWav(take.captureKey)).toThrow('Unknown capture');
  });

  it('refuses at another speed and with the trainer on', async () => {
    const rig = await opened();
    await rig.engine.setPractice({ speedPercent: 90, transposeSemitones: 0, trainer: { enabled: false, startPercent: 70, stepPercent: 5, targetPercent: 100, passesPerStep: 2 }, countIn: { bars: 0, everyPass: false } });
    await expect(rig.engine.startRecording(OPTIONS)).rejects.toThrow(/^recordingSpeed: /);
    await expect(makeRig().engine.startRecording(OPTIONS)).rejects.toThrow('Open an input first');
  });

  it("uses 012's count-in with the session's bars and restores the stored count-in", async () => {
    const rig = await loaded(GRID);
    await rig.engine.openInput({ deviceId: 'usb', mode: 'mono-1' });
    await rig.engine.startRecording({ ...OPTIONS, countInBars: 1 });
    expect(states(rig).at(-1)).toBe('countIn');
    expect(rig.engine.getPracticeStatus().countIn.active).toBe(true);
    await run(rig, 2.5);
    expect(states(rig).at(-1)).toBe('recording');
    const stopping = rig.engine.stopRecording();
    await run(rig, 0.3);
    const [take] = await stopping;
    expect(take?.startSample).toBe(0);
    expect(rig.engine.getPracticeStatus().countIn.beatsTotal).toBe(0);
  });

  it('a lost input during recording keeps a partial take', async () => {
    const rig = await opened();
    await rig.engine.startRecording(OPTIONS);
    await run(rig, 1);
    rig.devices.track?.end();
    await vi.advanceTimersByTimeAsync(3000);
    const [takes] = rig.ref.argsOf('OnTakesCaptured') as CapturedTake[][];
    expect([takes?.[0]?.partial, states(rig).at(-1)]).toEqual([true, 'closed']);
  });
});

describe('take channel', () => {
  it('loadTake from the capture plays it in the take strip sample-locked with the stems', async () => {
    const rig = await opened();
    const take = first(await record(rig));
    await rig.engine.loadTake({ takeId: take.captureKey, source: { captureKey: take.captureKey }, part: 'guitar' });
    rig.engine.seek(0);
    await rig.engine.play();
    const takeStart = startOf(rig, (s) => s.buffer?.length === take.lengthSamples);
    const stemStart = startOf(rig, (s) => bytesOf(s.buffer) === 100);
    expect(takeStart).toBeCloseTo(stemStart + take.startSample / SR, 9);
  });

  it('loadTake validates the adjustment and applies it to the song position', async () => {
    const rig = await loaded();
    const source = { url: '/take.flac', startSample: 0, lengthSamples: SR, channels: 1 } as const;
    await expect(rig.engine.loadTake({ takeId: 't', source, part: 'none', adjustmentSamples: 8821 })).rejects.toThrow('Invalid take adjustment: 8821');
    await rig.engine.loadTake({ takeId: 't', source, part: 'none', adjustmentSamples: 4410 });
    await rig.engine.play();
    expect(rig.ctx.sources.find((s) => s.buffer?.length === SR)?.startCalls[0]?.when).toBeCloseTo(0.2, 9);
  });

  it('a capture processor error is handled like a lost input; the session limit stops the recording', async () => {
    const rig = await opened();
    rig.ctx.captureNodes()[0]?.onprocessorerror?.(new Event('processorerror'));
    expect(rig.ref.argsOf('OnInputLost')).toEqual(['processorError']);
    await rig.engine.openInput({ deviceId: 'usb', mode: 'mono-1' });
    await rig.engine.startRecording({ ...OPTIONS, maxSeconds: 1 });
    await run(rig, 1.5);
    expect(rig.ref.argsOf('OnTakesCaptured')).toHaveLength(1);
  });

  it('loadTake from a URL slices the saved file; an unknown capture or HTTP error rejects', async () => {
    const rig = await loaded();
    await rig.engine.loadTake({ takeId: 't1', source: { url: '/take.flac', startSample: SR, lengthSamples: 2 * SR, channels: 1 }, part: 'bass' });
    await rig.engine.play();
    const takeSource = rig.ctx.sources.find((s) => s.buffer?.length === 2 * SR);
    expect([takeSource?.buffer?.numberOfChannels, takeSource?.startCalls[0]?.when]).toEqual([1, 0.1 + 1]);
    await expect(rig.engine.loadTake({ takeId: 'x', source: { captureKey: 'nope' }, part: 'none' })).rejects.toThrow('Unknown capture "nope"');
    await expect(rig.engine.loadTake({ takeId: 'y', source: { url: '/missing', startSample: 0, lengthSamples: 10, channels: 2 }, part: 'none' })).rejects.toThrow('Failed to load stem "take": HTTP 404');
  });

  it('setChannel and attachMeter work for the take before loadTake; unloadTake silences it', async () => {
    const rig = await loaded();
    expect(() => {
      rig.engine.setChannel('take', { volume: 0.5, pan: -0.5, mute: false, solo: false });
      rig.engine.attachMeter('take', 'meter-take');
    }).not.toThrow();
    await rig.engine.loadTake({ takeId: 't1', source: { url: '/take.flac', startSample: 0, lengthSamples: SR, channels: 2 }, part: 'vocals' });
    await rig.engine.play();
    rig.engine.unloadTake();
    expect(rig.ctx.sources.find((s) => s.buffer?.length === SR)?.stopCalls.length).toBe(1);
  });

  it('a new track unloads the take', async () => {
    const rig = await loaded();
    await rig.engine.loadTake({ takeId: 't1', source: { url: '/take.flac', startSample: 0, lengthSamples: SR, channels: 1 }, part: 'vocals' });
    await rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
    await rig.engine.play();
    expect(rig.ctx.sources.filter((s) => s.buffer?.length === SR)).toEqual([]);
  });

  it('dispose closes the input and the recording', async () => {
    const rig = await opened();
    await rig.engine.dispose();
    expect(rig.devices.track?.stopped).toBe(true);
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEngine, type AudioEngine, type CalibrationResult } from '../../src/index';
import { CALIBRATION_LEAD_SECONDS } from '../../src/recording/calibrate-run';
import { CALIBRATION_TAIL_SECONDS, chirpTemplate, clickSchedule } from '../../src/recording/calibration';
import type { MockAudioBufferSourceNode } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { FakeMediaDevices, RecordingContext } from '../mocks/media';

/** `calibrateLatency` in the engine (contract 2.5.0, research R-04, FR-007). */
const SR = 44100;
const TEMPLATE = chirpTemplate(SR);
const SEED = 7;

interface Rig {
  readonly ctx: RecordingContext;
  readonly devices: FakeMediaDevices;
  readonly engine: AudioEngine;
  /** Synthetic input on the context frame timeline. */
  signal: Float32Array;
  fed: number;
}

async function makeRig(testInput?: 'loopback'): Promise<Rig> {
  const ctx = new RecordingContext();
  ctx.decodeDurations.set(100, 20);
  ctx.baseLatency = 0.005;
  ctx.outputLatency = 0.02;
  const devices = new FakeMediaDevices();
  const engine = createEngine(new FakeDotNetRef(), {
    audioContextFactory: () => ctx.asAudioContext(),
    fetch: new FetchStub().reply('/vocals', { bytes: 100 }).fetch,
    mediaDevices: devices,
    ...(testInput === undefined ? {} : { testInput }),
  });
  await engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
  await engine.openInput({ deviceId: 'usb', mode: 'mono-1' });
  return { ctx, devices, engine, signal: new Float32Array(0), fed: 0 };
}

/** The loopback input of a calibration started at the current time: every click arrives `delay` samples late. */
function loopback(rig: Rig, delay: number, clicks = 8): void {
  const t0 = Math.round((rig.ctx.currentTime + CALIBRATION_LEAD_SECONDS) * SR);
  const schedule = clickSchedule(clicks, SEED, SR);
  const signal = new Float32Array(t0 + (schedule.at(-1) ?? 0) + Math.round(CALIBRATION_TAIL_SECONDS * SR) + 8192);
  schedule.forEach((click) => {
    signal.set(TEMPLATE, t0 + click + delay);
  });
  rig.signal = signal;
}

function feed(rig: Rig): void {
  const to = Math.floor(rig.ctx.currentTime * SR);
  while (rig.fed + 2048 <= to) {
    const block = rig.signal.slice(rig.fed, rig.fed + 2048);
    rig.ctx.captureNodes().at(-1)?.port.reply({ type: 'block', frame: rig.fed, channels: [block.length === 2048 ? block : new Float32Array(2048)] });
    rig.fed += 2048;
  }
}

/** Advances the clock, the capture and the timers by `steps` x 25 ms, or until `done()`. */
async function advance(rig: Rig, steps: number, done: () => boolean = () => false): Promise<void> {
  for (let i = 0; i < steps && !done(); i++) {
    rig.ctx.currentTime += 0.025;
    feed(rig);
    await vi.advanceTimersByTimeAsync(25);
  }
}

/** Runs the calibration with the clock advancing in 25 ms steps until it settles. */
async function calibrate(rig: Rig, options: { clicks?: number; seed?: number } = { seed: SEED }): Promise<CalibrationResult> {
  const box: { settled: boolean } = { settled: false };
  const running = rig.engine.calibrateLatency(options).finally(() => {
    box.settled = true;
  });
  await advance(rig, 600, () => box.settled);
  return running;
}

/** Click sources: buffer sources whose buffer is the chirp. */
function clickSources(rig: Rig): MockAudioBufferSourceNode[] {
  return rig.ctx.sources.filter((s) => s.buffer?.length === TEMPLATE.length);
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

describe('calibrateLatency', () => {
  it('measures the loopback latency and reports the output-latency snapshot', async () => {
    const rig = await makeRig();
    loopback(rig, 1234);
    const result = await calibrate(rig);
    expect(result).toMatchObject({ ok: true, latencySamples: 1234, detectedClicks: 8, spreadSamples: 0, reason: null, outputLatencySamples: Math.round(0.025 * SR) });
    expect(result.latencyMilliseconds).toBeCloseTo((1234 * 1000) / SR, 9);
  });

  it('schedules the seeded clicks on the destination at t0 + c_k', async () => {
    const rig = await makeRig();
    const t0 = rig.ctx.currentTime + CALIBRATION_LEAD_SECONDS;
    loopback(rig, 100);
    await calibrate(rig);
    const clicks = clickSources(rig);
    expect(clicks.map((s) => s.connections.includes(rig.ctx.destination))).toEqual(Array.from({ length: 8 }, () => true));
    clicks.forEach((source, k) => {
      expect(source.startCalls[0]?.when).toBeCloseTo(t0 + (clickSchedule(8, SEED, SR)[k] ?? 0) / SR, 9);
    });
  });

  it('forces monitoring off and mutes the mix while it runs, and restores both', async () => {
    const rig = await makeRig();
    rig.engine.setMonitoring({ enabled: true, volume: 0.6 });
    const monitor = rig.ctx.gains.find((g) => g.connections.includes(rig.ctx.destination) && g.gain.value === 0.6);
    const stem = rig.ctx.gains.find((g) => g.connections.some((n) => n.kind === 'panner'));
    loopback(rig, 300);
    const running = rig.engine.calibrateLatency({ seed: SEED });
    expect([monitor?.gain.value, stem?.gain.value]).toEqual([0, 0]);
    await advance(rig, 400);
    await running;
    expect([monitor?.gain.value, stem?.gain.value]).toEqual([0.6, 1]);
  });

  it('returns ok: false with a reason for a silent input (never throws for a failed measurement)', async () => {
    const rig = await makeRig();
    rig.signal = new Float32Array(20 * SR);
    expect(await calibrate(rig)).toMatchObject({ ok: false, reason: 'noSignal', latencySamples: null });
  });

  it('gives up waiting for the capture and evaluates what arrived', async () => {
    const rig = await makeRig();
    const running = rig.engine.calibrateLatency({ seed: SEED });
    await vi.advanceTimersByTimeAsync(20000);
    expect(await running).toMatchObject({ ok: false, reason: 'noSignal' });
  });

  it('refuses while playing, without an input, while recording and for invalid options', async () => {
    const playing = await makeRig();
    await playing.engine.play();
    await expect(playing.engine.calibrateLatency()).rejects.toThrow('Pause playback to calibrate');
    const closed = await makeRig();
    closed.engine.closeInput();
    await expect(closed.engine.calibrateLatency()).rejects.toThrow('Open an input first');
    const options = await makeRig();
    await expect(options.engine.calibrateLatency({ clicks: 13 })).rejects.toThrow('Invalid calibration options');
    const recording = await makeRig();
    await recording.engine.startRecording({ latencySamples: 0, latencyMethod: 'estimated', part: 'none', muteOriginal: false, countInBars: 0 });
    await expect(recording.engine.calibrateLatency()).rejects.toThrow('Stop the recording first');
  });

  it('a running calibration locks recording and a second calibration', async () => {
    const rig = await makeRig();
    loopback(rig, 50);
    const running = rig.engine.calibrateLatency({ seed: SEED });
    await expect(rig.engine.calibrateLatency()).rejects.toThrow('A calibration is running');
    await expect(rig.engine.startRecording({ latencySamples: 0, latencyMethod: 'estimated', part: 'none', muteOriginal: false, countInBars: 0 })).rejects.toThrow('A calibration is running');
    expect(() => {
      rig.engine.closeInput();
    }).toThrow('A calibration is running');
    await advance(rig, 400);
    await running;
    expect(() => {
      rig.engine.closeInput();
    }).not.toThrow();
  });

  it('uses a random seed by default', async () => {
    const rig = await makeRig();
    vi.spyOn(Math, 'random').mockReturnValue(0.5);
    rig.signal = new Float32Array(20 * SR);
    await calibrate(rig, {});
    expect(clickSources(rig)).toHaveLength(8);
  });
});

describe('testInput loopback (research R-13)', () => {
  it('feeds the master output and the calibration clicks into the input instead of a microphone', async () => {
    const rig = await makeRig('loopback');
    expect(rig.devices.requests).toEqual([]);
    const [destination] = rig.ctx.mediaDestinations;
    expect(destination).toBeDefined();
    const master = rig.ctx.analysers.find((a) => a.connections.includes(rig.ctx.destination));
    expect(master?.connections).toContain(destination);
    expect(rig.ctx.mediaSources[0]?.stream).toBe(destination?.stream);
    loopback(rig, 0);
    await calibrate(rig);
    expect(clickSources(rig).every((s) => s.connections.includes(destination as never))).toBe(true);
    rig.engine.closeInput();
    await rig.engine.openInput({ deviceId: null, mode: 'stereo' });
    expect(rig.ctx.mediaDestinations).toHaveLength(1);
  });
});

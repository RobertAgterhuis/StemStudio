import { describe, expect, it } from 'vitest';
import {
  ENGINE_CONTRACT_VERSION,
  createEngine,
  type AudioEngine,
  type ChannelId,
  type DotNetCallbacks,
  type EngineOptions,
  type MeterTarget,
  type RecordingState,
  type TakeCompareMode,
} from '../../src/index';
import { CAPTURE_BATCH_FRAMES, CAPTURE_PROCESSOR_NAME, registerCaptureProcessor, type CaptureProcessor } from '../../src/recording/capture-worklet';
import { notImplemented } from '../../src/recording/pending';
import { MockAudioContext } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';

/** Contract tests of the 2.5.0 recording surface (architect skeleton 015 T004; replaced by the audio-engine lane). */
function engine(options: EngineOptions = {}): AudioEngine {
  const ctx = new MockAudioContext();
  return createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), ...options });
}

const TAKE = { captureKey: 'c1', takeId: 't1', part: 'guitar' } as const;

describe('recording contract 2.5.0 (skeleton)', () => {
  it('exports version 2.5.0 with the recording names, the take channel, the input meter and the new callbacks', () => {
    const states: readonly RecordingState[] = ['closed', 'open', 'countIn', 'recording', 'finishing'];
    const modes: readonly TakeCompareMode[] = ['both', 'take', 'original'];
    const channel: ChannelId = 'take';
    const meter: MeterTarget = 'input';
    const callbacks: readonly DotNetCallbacks[] = ['OnRecordingStateChanged', 'OnRecordingProgress', 'OnTakesCaptured', 'OnInputLost', 'OnInputDevicesChanged'];

    expect(ENGINE_CONTRACT_VERSION).toBe('2.5.0');
    expect([states.length, modes.length, channel, meter, callbacks.length]).toEqual([5, 3, 'take', 'input', 5]);
  });

  it('accepts the 2.5.0 options without opening anything', () => {
    expect(() => engine({ testInput: 'loopback', captureWorkletUrl: '/w.js' })).not.toThrow();
  });

  it.each([
    ['recordingSupport', (e: AudioEngine) => e.recordingSupport()],
    ['listInputDevices', (e: AudioEngine) => e.listInputDevices()],
    ['openInput', (e: AudioEngine) => e.openInput({ deviceId: null, mode: 'mono-1' })],
    ['calibrateLatency', (e: AudioEngine) => e.calibrateLatency({ clicks: 8, seed: 1 })],
    ['startRecording', (e: AudioEngine) => e.startRecording({ latencySamples: 0, latencyMethod: 'estimated', part: 'none', muteOriginal: false, countInBars: 0 })],
    ['stopRecording', (e: AudioEngine) => e.stopRecording()],
    ['loadTake', (e: AudioEngine) => e.loadTake({ takeId: TAKE.takeId, source: { captureKey: TAKE.captureKey }, part: TAKE.part })],
  ] as const)('%s rejects with "not implemented"', async (name, call) => {
    await expect(call(engine())).rejects.toThrow(`not implemented: ${name}`);
  });

  it.each([
    ['closeInput', (e: AudioEngine) => { e.closeInput(); }],
    ['setMonitoring', (e: AudioEngine) => { e.setMonitoring({ enabled: true, volume: 0.5 }); }],
    ['captureWav', (e: AudioEngine) => { e.captureWav(TAKE.captureKey); }],
    ['releaseCapture', (e: AudioEngine) => { e.releaseCapture(TAKE.captureKey); }],
    ['unloadTake', (e: AudioEngine) => { e.unloadTake(); }],
    ['setTakeAdjustment', (e: AudioEngine) => { e.setTakeAdjustment(441); }],
    ['setTakeCompare', (e: AudioEngine) => { e.setTakeCompare('original'); }],
  ] as const)('%s throws "not implemented"', (name, call) => {
    expect(() => { call(engine()); }).toThrow(`not implemented: ${name}`);
  });

  it('refuses recording calls after dispose like every other method', async () => {
    const subject = engine();
    await subject.dispose();

    expect(() => subject.recordingSupport()).toThrow('Audio engine has been disposed');
  });

  it('has no input meter tap until the recording input exists (skeleton)', () => {
    expect(() => { engine().attachMeter('input', 'meter-input'); }).toThrow('Unknown meter target "input"');
  });

  it('names the method in the skeleton error', () => {
    expect(notImplemented('loadTake').message).toBe('not implemented: loadTake');
  });
});

describe('capture worklet skeleton', () => {
  it('registers stemstudio-capture and keeps the processor alive', () => {
    const registered = new Map<string, new () => CaptureProcessor>();
    const port = {} as MessagePort;
    registerCaptureProcessor({
      AudioWorkletProcessor: class {
        public readonly port = port;
      },
      registerProcessor: (name, processor) => {
        registered.set(name, processor);
      },
    });
    const Processor = registered.get(CAPTURE_PROCESSOR_NAME);

    expect(Processor).toBeDefined();
    expect(Processor && new Processor().process([[new Float32Array(128)]])).toBe(true);
    expect(CAPTURE_BATCH_FRAMES % 128).toBe(0);
  });
});

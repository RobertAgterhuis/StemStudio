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
import { notImplemented } from '../../src/recording/pending';
import { MockAudioContext } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';

/** Contract tests of the 2.5.0 recording surface (architect skeleton 015 T004; replaced by the audio-engine lane). */
function engine(options: EngineOptions = {}): AudioEngine {
  const ctx = new MockAudioContext();
  return createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), ...options });
}


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
    ['calibrateLatency', (e: AudioEngine) => e.calibrateLatency({ clicks: 8, seed: 1 })],
  ] as const)('%s rejects with "not implemented" until its user story (US2)', async (name, call) => {
    await expect(call(engine())).rejects.toThrow(`not implemented: ${name}`);
  });

  it.each([
    ['setTakeAdjustment', (e: AudioEngine) => { e.setTakeAdjustment(441); }],
    ['setTakeCompare', (e: AudioEngine) => { e.setTakeCompare('original'); }],
  ] as const)('%s throws "not implemented" until its user story (US4, US7)', (name, call) => {
    expect(() => { call(engine()); }).toThrow(`not implemented: ${name}`);
  });

  it('refuses recording calls after dispose like every other method', async () => {
    const subject = engine();
    await subject.dispose();

    expect(() => subject.recordingSupport()).toThrow('Audio engine has been disposed');
  });

  it('has no input meter tap until the recording input exists', () => {
    expect(() => { engine().attachMeter('input', 'meter-input'); }).toThrow('Unknown meter target "input"');
  });

  it('names the method in the skeleton error', () => {
    expect(notImplemented('loadTake').message).toBe('not implemented: loadTake');
  });
});

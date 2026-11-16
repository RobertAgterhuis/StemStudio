import { describe, expect, it } from 'vitest';
import { PRACTICE_NOT_IMPLEMENTED } from '../src/engine';
import {
  ENGINE_CONTRACT_VERSION,
  createEngine,
  type AudioEngine,
  type DotNetCallbacks,
  type EngineOptions,
  type PracticeSettings,
  type StretchState,
  type TimeStretchStage,
  type TrainerState,
} from '../src/index';
import { MockAudioContext } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';

/** Contract tests of the 2.3.0 practice surface (architect skeleton 012 T005; the stubs are replaced in T034). */
const NEUTRAL: PracticeSettings = {
  speedPercent: 100,
  transposeSemitones: 0,
  trainer: { enabled: false, startPercent: 70, stepPercent: 5, targetPercent: 100, passesPerStep: 1 },
  countIn: { bars: 0, everyPass: false },
};

function engine(options: EngineOptions = {}): AudioEngine {
  const ctx = new MockAudioContext();
  return createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), ...options });
}

describe('practice contract 2.3.0 (skeleton)', () => {
  it('exports version 2.3.0 with the practice state names and the new callback', () => {
    const stretch: readonly StretchState[] = ['bypass', 'loading', 'active', 'unavailable'];
    const trainer: readonly TrainerState[] = ['off', 'waiting', 'running', 'target'];
    const callback: DotNetCallbacks = 'OnPracticeChanged';

    expect(ENGINE_CONTRACT_VERSION).toBe('2.3.0');
    expect([stretch.length, trainer.length, callback]).toEqual([4, 4, 'OnPracticeChanged']);
  });

  it('accepts the time-stretch options and the test seam without loading anything', () => {
    const factory = (): Promise<TimeStretchStage> => Promise.reject(new Error('not used'));

    expect(() => engine({ timeStretch: { processorUrl: '/p.js', wasmUrl: '/s.wasm' }, timeStretchFactory: factory })).not.toThrow();
  });

  it('has setPractice and getPracticeStatus stubs until the audio-engine lane implements them', async () => {
    const subject = engine();

    await expect(subject.setPractice(NEUTRAL)).rejects.toThrow(PRACTICE_NOT_IMPLEMENTED);
    expect(() => subject.getPracticeStatus()).toThrow(PRACTICE_NOT_IMPLEMENTED);
  });

  it('rejects practice calls after dispose like every other method', async () => {
    const subject = engine();
    await subject.dispose();

    await expect(subject.setPractice(NEUTRAL)).rejects.toThrow('Audio engine has been disposed');
    expect(() => subject.getPracticeStatus()).toThrow('Audio engine has been disposed');
  });
});

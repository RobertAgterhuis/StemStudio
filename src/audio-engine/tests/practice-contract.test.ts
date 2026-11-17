import { DEFAULT_PRACTICE } from '../src/practice/settings';
import { describe, expect, it } from 'vitest';
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

/** Contract tests of the 2.3.0 practice surface (architect skeleton 012 T005; stubs replaced in T034). */
const NEUTRAL: PracticeSettings = DEFAULT_PRACTICE;

function engine(options: EngineOptions = {}): AudioEngine {
  const ctx = new MockAudioContext();
  return createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), ...options });
}

describe('practice contract 2.3.0 (skeleton)', () => {
  it('exports the current version with the practice state names and the new callback', () => {
    const stretch: readonly StretchState[] = ['bypass', 'loading', 'active', 'unavailable'];
    const trainer: readonly TrainerState[] = ['off', 'waiting', 'running', 'target'];
    const callback: DotNetCallbacks = 'OnPracticeChanged';

    expect(ENGINE_CONTRACT_VERSION).toBe('2.5.0');
    expect([stretch.length, trainer.length, callback]).toEqual([4, 4, 'OnPracticeChanged']);
  });

  it('accepts the time-stretch options and the test seam without loading anything', () => {
    const factory = (): Promise<TimeStretchStage> => Promise.reject(new Error('not used'));

    expect(() => engine({ timeStretch: { processorUrl: '/p.js', wasmUrl: '/s.wasm' }, timeStretchFactory: factory })).not.toThrow();
  });

  it('implements setPractice and getPracticeStatus (neutral settings load nothing)', async () => {
    let created = 0;
    const subject = engine({
      timeStretchFactory: () => {
        created += 1;
        return Promise.reject(new Error('not used'));
      },
    });

    await expect(subject.setPractice(NEUTRAL)).resolves.toBeUndefined();
    expect(subject.getPracticeStatus()).toMatchObject({ speedPercent: 100, transposeSemitones: 0, stretch: 'bypass', latencyMs: 0 });
    expect(created).toBe(0);
  });

  it('rejects practice calls after dispose like every other method', async () => {
    const subject = engine();
    await subject.dispose();

    await expect(subject.setPractice(NEUTRAL)).rejects.toThrow('Audio engine has been disposed');
    expect(() => subject.getPracticeStatus()).toThrow('Audio engine has been disposed');
  });
});

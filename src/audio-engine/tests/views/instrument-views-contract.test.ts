import { describe, expect, it, vi } from 'vitest';
import {
  ENGINE_CONTRACT_VERSION,
  createEngine,
  type AudioEngine,
  type DotNetCallbacks,
  type PitchContour,
} from '../../src/index';
import { PENDING_VIEWS_MESSAGE } from '../../src/views/pending';
import { PITCH_PROCESSOR_NAME } from '../../src/pitch/worklet';
import { FakeProcessorBase } from '../mocks/worklet-scope';
import { MockAudioContext } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';

/** Contract tests of the 2.6.0 instrument-view surface (architect skeleton 016 T004; behaviour in the lane's tests). */
function engine(): AudioEngine {
  const ctx = new MockAudioContext();
  return createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), pitchWorkletUrl: '/p.js' });
}

const reference: PitchContour = { hopMs: 10, frames: [6900] };
const units = [{ index: 0, startMs: 0, endMs: 1000 }];

describe('instrument views contract 2.6.0', () => {
  it('exports version 2.6.0 with the two singing callbacks', () => {
    const callbacks: readonly DotNetCallbacks[] = ['OnSingingUnitScored', 'OnSingingStateChanged'];

    expect(ENGINE_CONTRACT_VERSION).toBe('2.6.0');
    expect(callbacks).toHaveLength(2);
  });

  it.each([
    ['getRoundTripLatency', (e: AudioEngine) => { e.getRoundTripLatency(); }],
    ['setSingingTolerance', (e: AudioEngine) => { e.setSingingTolerance(50); }],
    ['stopSingingFeedback', (e: AudioEngine) => { e.stopSingingFeedback(); }],
  ])('%s throws the pending error until the lane builds it', (name, call) => {
    expect(() => { call(engine()); }).toThrow(`${name}: ${PENDING_VIEWS_MESSAGE}`);
  });

  it('rejects the async 2.6.0 methods with the pending error', async () => {
    const subject = engine();

    await expect(subject.startSingingFeedback({ pitchViewElementId: 'pv', reference, units, toleranceCents: 100 })).rejects.toThrow(PENDING_VIEWS_MESSAGE);
    await expect(subject.scoreTake({ takeUrl: '/t', startSample: 0, reference, units, toleranceCents: 50 })).rejects.toThrow(PENDING_VIEWS_MESSAGE);
  });

  it('refuses the 2.6.0 methods after dispose like every other method', async () => {
    const subject = engine();
    await subject.dispose();

    expect(() => { subject.detachChordLane(); }).toThrow('Audio engine has been disposed');
    await expect(subject.scoreTake({ takeUrl: '/t', startSample: 0, reference, units, toleranceCents: 50 })).rejects.toThrow('disposed');
  });
});

describe('pitch worklet module', () => {
  it('registers itself when loaded in a worklet scope', async () => {
    const names: string[] = [];
    const scope = globalThis as unknown as { registerProcessor?: (name: string) => void; AudioWorkletProcessor?: unknown; sampleRate?: number };
    scope.registerProcessor = (name) => { names.push(name); };
    scope.AudioWorkletProcessor = FakeProcessorBase;
    scope.sampleRate = 48_000;
    try {
      vi.resetModules();
      await import('../../src/pitch/worklet');
    } finally {
      delete scope.registerProcessor;
      delete scope.AudioWorkletProcessor;
      delete scope.sampleRate;
    }

    expect(names).toEqual([PITCH_PROCESSOR_NAME]);
  });
});

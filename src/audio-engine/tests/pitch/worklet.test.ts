import { describe, expect, it } from 'vitest';
import { PITCH_PROCESSOR_NAME, registerPitchProcessor, type PitchProcessor, type PitchScope } from '../../src/pitch/worklet';
import { FakeProcessorBase, type FakeProcessorPort } from '../mocks/worklet-scope';
import { centsOf, tone } from './signals';

interface Scope extends PitchScope {
  currentTime: number;
  sampleRate: number;
  processor: (PitchProcessor & { port: FakeProcessorPort }) | null;
}

function scope(sampleRate = 48_000): Scope {
  const fake: Scope = {
    AudioWorkletProcessor: FakeProcessorBase as unknown as PitchScope['AudioWorkletProcessor'],
    currentTime: 0,
    sampleRate,
    processor: null,
    registerProcessor: (name, ctor) => {
      expect(name).toBe(PITCH_PROCESSOR_NAME);
      fake.processor = new ctor() as PitchProcessor & { port: FakeProcessorPort };
    },
  };
  registerPitchProcessor(fake);
  return fake;
}

function feed(s: Scope, input: Float32Array): boolean[] {
  const kept: boolean[] = [];
  for (let i = 0; i + 128 <= input.length; i += 128) {
    s.currentTime = i / s.sampleRate;
    kept.push(s.processor?.process([[input.subarray(i, i + 128)]]) ?? false);
  }
  return kept;
}

interface Posted {
  readonly t: number;
  readonly cents: number | null;
  readonly clarity: number;
}

describe('pitch-tracker processor (research R-05, data-model 3.2)', () => {
  it('posts {t, cents, clarity} every 10 ms with t at the window centre', () => {
    const s = scope();
    feed(s, tone(48_000, 0.5, [[220, 0.4]]));
    const posted = s.processor?.port.posted as Posted[];
    expect(posted.length).toBeGreaterThanOrEqual(45);
    expect((posted[1]?.t ?? 0) - (posted[0]?.t ?? 0)).toBeCloseTo(0.01, 3);
    // first frame: window of 512 samples at 16 kHz ends at 32 ms, its centre is at 16 ms
    expect(posted[0]?.t).toBeCloseTo(0.016, 2);
    expect(Math.abs((posted[20]?.cents ?? 0) - centsOf(220))).toBeLessThan(20);
    expect(posted[20]?.clarity).toBeGreaterThan(0.8);
  });

  it('posts null cents for silence and for a missing input channel', () => {
    const s = scope(44_100);
    feed(s, new Float32Array(44_100 / 4));
    for (let i = 0; i < 40; i += 1) {
      s.processor?.process([[]]);
      s.processor?.process([]);
    }
    const posted = s.processor?.port.posted as Posted[];
    expect(posted.length).toBeGreaterThan(0);
    expect(posted.every((p) => p.cents === null)).toBe(true);
  });

  it('stops on {type: "stop"}: the next process returns false and nothing more is posted', () => {
    const s = scope();
    feed(s, tone(48_000, 0.1, [[220, 0.4]]));
    s.processor?.port.send({ type: 'stop' });
    s.processor?.port.send(null);
    const count = s.processor?.port.posted.length;
    expect(feed(s, tone(48_000, 0.1, [[220, 0.4]]))[0]).toBe(false);
    expect(s.processor?.port.posted.length).toBe(count);
  });
});

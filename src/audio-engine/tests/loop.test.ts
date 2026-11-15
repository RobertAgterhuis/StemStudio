import { describe, expect, it } from 'vitest';
import { NO_LOOP, applyLoop, isInside, setLoopChecked, validateLoopRegion, wrapPosition } from '../src/loop';
import { Transport } from '../src/transport';
import type { LoopRegion, StemName } from '../src/types';
import { MockAudioContext } from './mocks/audio-context';

const STEMS: StemName[] = ['vocals', 'drums', 'bass'];

function makeTransport(seconds = 10): { ctx: MockAudioContext; transport: Transport } {
  const ctx = new MockAudioContext();
  const transport = new Transport({
    context: ctx.asAudioContext(),
    startDelaySeconds: 0.1,
    output: () => ctx.createGain() as unknown as AudioNode,
  });
  const buffer = ctx.createBuffer(2, seconds * ctx.sampleRate, ctx.sampleRate) as unknown as AudioBuffer;
  transport.load(new Map(STEMS.map((name) => [name, buffer])));
  return { ctx, transport };
}

const region = (start: number, end: number, enabled = true): LoopRegion => ({ start, end, enabled });

describe('validateLoopRegion', () => {
  it.each([
    [region(-0.1, 2), 'start'],
    [region(2, 2), 'B <= A'],
    [region(3, 2), 'B <= A'],
    [region(1, 10.5), 'end'],
    [region(Number.NaN, 2), 'start'],
    [region(1, Number.POSITIVE_INFINITY), 'end'],
  ])('rejects %o', (loop, detail) => {
    expect(() => validateLoopRegion(loop, 10)).toThrow(/^Invalid loop region: /);
    expect(() => validateLoopRegion(loop, 10)).toThrow(detail);
  });

  it('accepts 0 <= start < end <= duration, enabled or not', () => {
    expect(validateLoopRegion(region(0, 10), 10)).toEqual(region(0, 10));
    expect(validateLoopRegion(region(2, 3, false), 10)).toEqual(region(2, 3, false));
  });
});

describe('loop helpers', () => {
  it('isInside is half-open [start, end)', () => {
    expect(isInside(2, region(2, 4))).toBe(true);
    expect(isInside(3.99, region(2, 4))).toBe(true);
    expect(isInside(4, region(2, 4))).toBe(false);
    expect(isInside(1.99, region(2, 4))).toBe(false);
  });

  it('wrapPosition folds positions past B back into [A, B)', () => {
    expect(wrapPosition(3, region(2, 4))).toBe(3);
    expect(wrapPosition(4, region(2, 4))).toBe(2);
    expect(wrapPosition(7.5, region(2, 4))).toBeCloseTo(3.5, 9);
    expect(wrapPosition(7.5, region(2, 4, false))).toBe(7.5);
    expect(wrapPosition(1, region(2, 4))).toBe(1);
  });

  it('applyLoop copies the region onto a source', () => {
    const source = new MockAudioContext().createBufferSource();
    applyLoop(source as unknown as AudioBufferSourceNode, region(1, 2));
    expect([source.loop, source.loopStart, source.loopEnd]).toEqual([true, 1, 2]);
    applyLoop(source as unknown as AudioBufferSourceNode, NO_LOOP);
    expect(source.loop).toBe(false);
  });
});

describe('setLoopChecked', () => {
  it('an invalid region throws and keeps the previous loop', async () => {
    const { ctx, transport } = makeTransport();
    setLoopChecked(transport, region(2, 4));
    await transport.play();
    expect(() => {
      setLoopChecked(transport, region(5, 4));
    }).toThrow('Invalid loop region: ');
    expect(transport.loop).toEqual(region(2, 4));
    expect(ctx.sources.every((s) => s.loop && s.loopStart === 2 && s.loopEnd === 4)).toBe(true);
  });

  it('rejects a region beyond the loaded track', () => {
    const { transport } = makeTransport(5);
    expect(() => {
      setLoopChecked(transport, region(1, 6));
    }).toThrow('Invalid loop region: ');
    expect(transport.loop).toEqual(NO_LOOP);
  });

  it('enabled sets loop/loopStart/loopEnd on every running source', async () => {
    const { ctx, transport } = makeTransport();
    await transport.play();
    ctx.currentTime = 1;
    setLoopChecked(transport, region(0.5, 3));
    expect(ctx.sources).toHaveLength(STEMS.length);
    for (const source of ctx.sources) {
      expect([source.loop, source.loopStart, source.loopEnd]).toEqual([true, 0.5, 3]);
    }
  });

  it('position outside the region restarts every stem at start', async () => {
    const { ctx, transport } = makeTransport();
    await transport.play();
    ctx.currentTime = 5;
    setLoopChecked(transport, region(1, 2));
    const restarted = ctx.sources.slice(STEMS.length);
    expect(restarted.map((s) => s.startCalls[0])).toEqual(STEMS.map(() => ({ when: 5.1, offset: 1 })));
  });

  it('position wraps inside the region', async () => {
    const { ctx, transport } = makeTransport();
    setLoopChecked(transport, region(1, 2));
    await transport.play();
    ctx.currentTime = 0.1 + 1.25 + 3;
    expect(transport.position()).toBeCloseTo(1.25, 9);
  });

  it('disable clears loop on the running sources', async () => {
    const { ctx, transport } = makeTransport();
    setLoopChecked(transport, region(1, 2));
    await transport.play();
    setLoopChecked(transport, region(1, 2, false));
    expect(ctx.sources.every((s) => !s.loop)).toBe(true);
    expect(transport.loop.enabled).toBe(false);
  });
});

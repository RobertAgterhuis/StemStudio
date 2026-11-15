import { describe, expect, it } from 'vitest';
import {
  COUNTDOWN_MS,
  DEFAULT_COUNTDOWN_GAP_MS,
  MAX_LINES,
  MAX_WORDS_PER_LINE,
  buildTimeline,
  countdownDots,
  phaseAt,
  validateCountdownGap,
} from '../../src/karaoke/timeline';
import type { KaraokeLine, KaraokeLyrics, KaraokeWord } from '../../src/types';
import { LYRICS } from '../mocks/karaoke-rig';

function word(startMs: number, endMs = startMs + 100, text = 'w'): KaraokeWord {
  return { text, startMs, endMs };
}

function line(startMs: number, endMs: number, words: readonly KaraokeWord[]): KaraokeLine {
  return { startMs, endMs, words };
}

function invalid(lyrics: unknown, reason: string): void {
  expect(() => buildTimeline(lyrics as KaraokeLyrics)).toThrow(`Invalid karaoke lyrics: ${reason}`);
}

describe('buildTimeline', () => {
  it('flattens the words in order with their line and index', () => {
    const timeline = buildTimeline(LYRICS);
    expect(timeline.lineStarts).toEqual([4000, 6000, 15_000]);
    expect(timeline.wordStarts).toEqual([4000, 4400, 4600, 6000, 6500, 6600, 15_000, 15_500]);
    expect(timeline.words[4]).toMatchObject({ line: 1, index: 1, startMs: 6500 });
    expect(timeline.firstWord).toEqual([0, 3, 6]);
  });

  it('accepts an empty document and equal starts', () => {
    expect(buildTimeline({ lines: [] }).words).toEqual([]);
    const timeline = buildTimeline({ lines: [line(0, 500, [word(0), word(0)]), line(0, 500, [word(100)])] });
    expect(timeline.wordStarts).toEqual([0, 0, 100]);
  });

  it('rejects documents that break the contract', () => {
    invalid({}, 'lines must be an array');
    invalid({ lines: Array.from({ length: MAX_LINES + 1 }, (_, i) => line(i * 10, i * 10 + 5, [word(i * 10, i * 10 + 5)])) }, 'more than 400 lines');
    invalid({ lines: [line(0, 1e6, Array.from({ length: MAX_WORDS_PER_LINE + 1 }, (_, i) => word(i * 10)))] }, 'line 0 has more than 40 words');
    invalid({ lines: [line(0, 100, [])] }, 'line 0 has no words');
    invalid({ lines: [line(0, 0, [word(0)])] }, 'line 0 ends before it starts');
    invalid({ lines: [line(Number.NaN, 100, [word(0)])] }, 'line 0 has invalid times');
    invalid({ lines: [line(500, 900, [word(500)]), line(400, 900, [word(600)])] }, 'line 1 starts before line 0');
    invalid({ lines: [line(0, 900, [word(0, 0)])] }, 'word 0.0 ends before it starts');
    invalid({ lines: [line(0, 900, [word(Number.POSITIVE_INFINITY)])] }, 'word 0.0 has invalid times');
    invalid({ lines: [line(100, 900, [word(50)])] }, 'word 0.0 lies outside its line');
    invalid({ lines: [line(0, 900, [word(800, 950)])] }, 'word 0.0 lies outside its line');
    invalid({ lines: [line(0, 900, [word(300), word(200)])] }, 'word 0.1 starts before the previous word');
    invalid({ lines: [line(0, 900, [word(500)]), line(100, 900, [word(400)])] }, 'word 1.0 starts before the previous word');
    invalid({ lines: [line(0, 900, [{ startMs: 0, endMs: 10 }])] }, 'word 0.0 has no text');
  });
});

describe('countdown', () => {
  it('validates countdownGapMs', () => {
    expect(DEFAULT_COUNTDOWN_GAP_MS).toBe(5000);
    expect(validateCountdownGap(undefined)).toBe(5000);
    expect(validateCountdownGap(0)).toBe(0);
    expect(() => validateCountdownGap(-1)).toThrow('Invalid karaoke options: countdownGapMs must be >= 0 (got -1)');
    expect(() => validateCountdownGap(Number.NaN)).toThrow('countdownGapMs');
  });

  it('shows one dot per remaining second in the last 3 s', () => {
    expect(COUNTDOWN_MS).toBe(3000);
    expect(countdownDots(10_000, 6999)).toBe(0);
    expect(countdownDots(10_000, 7000)).toBe(3);
    expect(countdownDots(10_000, 8000)).toBe(2);
    expect(countdownDots(10_000, 9999)).toBe(1);
  });
});

describe('phaseAt', () => {
  const timeline = buildTimeline(LYRICS);

  it('hidden, countdown, singing, gap countdown and ended', () => {
    expect(phaseAt(buildTimeline({ lines: [] }), -1, 0, 5000)).toEqual({ state: 'hidden', dots: 0 });
    expect(phaseAt(timeline, -1, 0, 5000)).toEqual({ state: 'countdown', dots: 0 });
    expect(phaseAt(timeline, -1, 3500, 5000)).toEqual({ state: 'countdown', dots: 1 });
    expect(phaseAt(timeline, 0, 4000, 5000)).toEqual({ state: 'singing', dots: 0 });
    expect(phaseAt(timeline, 1, 11_999, 5000)).toEqual({ state: 'singing', dots: 0 });
    expect(phaseAt(timeline, 1, 12_000, 5000)).toEqual({ state: 'countdown', dots: 3 });
    expect(phaseAt(timeline, 1, 12_000, 7000)).toEqual({ state: 'singing', dots: 0 });
    expect(phaseAt(timeline, 2, 16_999, 5000)).toEqual({ state: 'singing', dots: 0 });
    expect(phaseAt(timeline, 2, 17_000, 5000)).toEqual({ state: 'ended', dots: 0 });
  });
});

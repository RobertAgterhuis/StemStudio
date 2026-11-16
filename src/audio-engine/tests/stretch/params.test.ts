import { DEFAULT_PRACTICE } from '../../src/practice/settings';
import { describe, expect, it } from 'vitest';
import {
  CHANGE_LEAD_SECONDS,
  RENDER_QUANTUM_FRAMES,
  STRETCH_ABI_VERSION,
  STRETCH_CONTENT_FRAMES_PER_OCTAVE,
  STRETCH_DELAY_FRAMES,
  STRETCH_OVERFLOW_FRAMES,
  SOUNDTOUCH_SETTINGS,
} from '../../src/stretch/constants';
import {
  contentOffsetFrames,
  heardLatencySeconds,
  isNeutral,
  needsStretch,
  pitchFactor,
  speedRate,
} from '../../src/stretch/params';
import type { PracticeSettings } from '../../src/types';

const NEUTRAL: PracticeSettings = DEFAULT_PRACTICE;

function practice(partial: Partial<PracticeSettings>): PracticeSettings {
  return { ...NEUTRAL, ...partial };
}

describe('stretch constants (Phase 0 P0-1, ADR-021 P5/P6)', () => {
  it('fixes the delay at 5120 frames, a multiple of the render quantum and within the 230 ms limit', () => {
    expect(STRETCH_DELAY_FRAMES).toBe(5120);
    expect(STRETCH_DELAY_FRAMES % RENDER_QUANTUM_FRAMES).toBe(0);
    expect(STRETCH_DELAY_FRAMES).toBeLessThanOrEqual(10_112);
    expect(RENDER_QUANTUM_FRAMES).toBe(128);
  });

  it('has the measured content slope, the ABI version, the overflow margin and the change lead', () => {
    expect(STRETCH_CONTENT_FRAMES_PER_OCTAVE).toBe(-1100);
    expect(STRETCH_ABI_VERSION).toBe(1);
    expect(STRETCH_OVERFLOW_FRAMES).toBe(1024);
    expect(CHANGE_LEAD_SECONDS).toBe(0.02);
  });

  it('lists the SoundTouch settings of research.md R-04 as [id, value] pairs', () => {
    expect(SOUNDTOUCH_SETTINGS).toEqual([
      [2, 0],
      [0, 1],
      [1, 64],
      [5, 8],
    ]);
  });
});

describe('pitch factor p = 2^(st/12) / (speed/100) (FR-001, FR-002)', () => {
  it.each([
    [100, 0, 1],
    [50, 12, 4],
    [120, -12, 0.5 / 1.2],
    [50, 0, 2],
    [200 / 2, 12, 2],
    [80, 0, 1.25],
  ])('speed %d %% and %d semitones -> %f', (speed, semitones, expected) => {
    expect(pitchFactor(speed, semitones)).toBeCloseTo(expected, 12);
  });

  it('gives 0.4167 at the lower corner (120 %, -12 st)', () => {
    expect(pitchFactor(120, -12)).toBeCloseTo(0.4167, 4);
  });

  it('converts a speed in percent to the playback rate', () => {
    expect(speedRate(75)).toBe(0.75);
    expect(speedRate(100)).toBe(1);
  });
});

describe('neutral detection', () => {
  it('is neutral at 100 %, 0 st and trainer off, whatever the count-in', () => {
    expect(isNeutral(NEUTRAL)).toBe(true);
    expect(isNeutral(practice({ countIn: { bars: 2, everyPass: true } }))).toBe(true);
    expect(needsStretch(NEUTRAL)).toBe(false);
  });

  it.each([
    ['speed', practice({ speedPercent: 99 })],
    ['transpose', practice({ transposeSemitones: -1 })],
    ['trainer', practice({ trainer: { ...NEUTRAL.trainer, enabled: true } })],
  ])('is not neutral with a changed %s', (_name, settings) => {
    expect(isNeutral(settings)).toBe(false);
    expect(needsStretch(settings)).toBe(true);
  });
});

describe('content offset and heard latency (ADR-021 P6)', () => {
  it('is 0 at factor 1 (not -0) and follows -1100 * log2(p) rounded', () => {
    expect(Object.is(contentOffsetFrames(1), 0)).toBe(true);
    expect(contentOffsetFrames(2)).toBe(-1100);
    expect(contentOffsetFrames(0.5)).toBe(1100);
    expect(contentOffsetFrames(4)).toBe(-2200);
    expect(contentOffsetFrames(Math.SQRT2)).toBe(-550);
  });

  it('heard latency L(p) = (D + c(p)) / sampleRate', () => {
    expect(heardLatencySeconds(1, 44100)).toBeCloseTo(5120 / 44100, 12);
    expect(heardLatencySeconds(4, 44100)).toBeCloseTo((5120 - 2200) / 44100, 12);
    expect(heardLatencySeconds(0.5 / 1.2, 48000)).toBeCloseTo((5120 + Math.round(-1100 * Math.log2(0.5 / 1.2))) / 48000, 12);
  });

  it('stays within 66..147 ms over the product range at 44.1 kHz', () => {
    expect(heardLatencySeconds(4, 44100)).toBeGreaterThan(0.066);
    expect(heardLatencySeconds(0.5 / 1.2, 44100)).toBeLessThan(0.148);
  });
});

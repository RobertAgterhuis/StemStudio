/**
 * Pure formulas of the practice tempo (audio-engine.md 2.3.0 "Live changes", "Stretch stage").
 *
 * @packageDocumentation
 */
import type { PracticeSettings } from '../types';
import { STRETCH_CONTENT_FRAMES_PER_OCTAVE, STRETCH_DELAY_FRAMES } from './constants';

/**
 * @param speedPercent - Playback speed 50..120.
 * @returns Playback rate of every source (`speedPercent / 100`).
 */
export function speedRate(speedPercent: number): number {
  return speedPercent / 100;
}

/**
 * Pitch correction of the stage: the sources are resampled by `r`, so SoundTouch shifts by `2^(st/12) / r`.
 *
 * @param speedPercent - Playback speed 50..120.
 * @param transposeSemitones - Pitch shift -12..12.
 * @returns Pitch factor 0.42..4.0.
 */
export function pitchFactor(speedPercent: number, transposeSemitones: number): number {
  return 2 ** (transposeSemitones / 12) / speedRate(speedPercent);
}

/**
 * @param settings - Validated practice settings.
 * @returns Whether the settings need no time-stretch stage (100 %, 0 st, trainer off; count-in does not matter).
 */
export function isNeutral(settings: PracticeSettings): boolean {
  return settings.speedPercent === 100 && settings.transposeSemitones === 0 && !settings.trainer.enabled;
}

/**
 * @param settings - Validated practice settings.
 * @returns Whether the stage must be loaded for these settings.
 */
export function needsStretch(settings: PracticeSettings): boolean {
  return !isNeutral(settings);
}

/**
 * Mean WSOLA content offset: input frame `n` appears near SoundTouch output frame `n + c(p)` (ADR-021 P6).
 *
 * @param factor - Pitch factor.
 * @returns `round(-1100 * log2(p))` frames (0 at factor 1, never -0).
 */
export function contentOffsetFrames(factor: number): number {
  return Math.round(STRETCH_CONTENT_FRAMES_PER_OCTAVE * Math.log2(factor)) + 0;
}

/**
 * Heard latency of the engaged stage `L(p) = (D + c(p)) / sampleRate`.
 *
 * @param factor - Pitch factor.
 * @param sampleRate - Context sample rate.
 * @returns Seconds between a source frame and the moment its content is heard.
 */
export function heardLatencySeconds(factor: number, sampleRate: number): number {
  return (STRETCH_DELAY_FRAMES + contentOffsetFrames(factor)) / sampleRate;
}

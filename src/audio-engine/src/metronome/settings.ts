/**
 * Validation of {@link MetronomeSettings} (ranges of `MetronomeSettingsDto`).
 *
 * @packageDocumentation
 */
import { checkedVolume } from '../channel';
import type { MetronomeSettings } from '../types';

/** Metronome state before the first `setMetronome` call (off). */
export const DEFAULT_METRONOME: MetronomeSettings = {
  enabled: false,
  volume: 0.8,
  bpmOverride: null,
  offsetMs: 0,
  beatsPerBar: 4,
};

/**
 * @param settings - Requested settings.
 * @returns A plain, validated copy (volume clamped to 0..1).
 * @throws Error `Invalid metronome settings: ...` for an out-of-range BPM, offset or beats per bar.
 */
export function validateMetronomeSettings(settings: MetronomeSettings): MetronomeSettings {
  const problem = metronomeProblem(settings);
  if (problem !== null) {
    throw new Error(`Invalid metronome settings: ${problem}`);
  }
  return {
    enabled: settings.enabled,
    volume: checkedVolume(settings.volume),
    bpmOverride: settings.bpmOverride,
    offsetMs: settings.offsetMs,
    beatsPerBar: settings.beatsPerBar,
  };
}

function metronomeProblem(s: MetronomeSettings): string | null {
  return bpmProblem(s) ?? offsetProblem(s) ?? beatsPerBarProblem(s);
}

function bpmProblem(s: MetronomeSettings): string | null {
  const ok = s.bpmOverride === null || inRange(s.bpmOverride, 30, 300);
  return ok ? null : `bpmOverride must be 30..300 or null (got ${String(s.bpmOverride)})`;
}

function offsetProblem(s: MetronomeSettings): string | null {
  return inRange(s.offsetMs, -2000, 2000) ? null : `offsetMs must be -2000..2000 (got ${String(s.offsetMs)})`;
}

function beatsPerBarProblem(s: MetronomeSettings): string | null {
  const ok = Number.isInteger(s.beatsPerBar) && inRange(s.beatsPerBar, 1, 12);
  return ok ? null : `beatsPerBar must be an integer 1..12 (got ${String(s.beatsPerBar)})`;
}

function inRange(value: number, min: number, max: number): boolean {
  return value >= min && value <= max;
}

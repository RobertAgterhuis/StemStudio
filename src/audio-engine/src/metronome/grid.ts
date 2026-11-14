/**
 * Click grids in track time: the detected beat grid or a constant manual tempo, shifted by the
 * user's offset, with accent flags for beat 1 of each bar.
 *
 * @packageDocumentation
 */
import type { BeatGrid, MetronomeSettings } from '../types';

/** One click at a track time. */
export interface Click {
  /** Track time (s), offset included. */
  readonly time: number;
  /** Beat 1 of a bar (accent buffer). */
  readonly accent: boolean;
}

/** Source of clicks for a track-time range. */
export interface ClickGrid {
  /** Clicks with `from <= time < to`, ascending. */
  between(from: number, to: number): Click[];
}

/** A grid without clicks (no reliable grid and no override). */
export const SILENT_GRID: ClickGrid = {
  between() {
    return [];
  },
};

/** Tolerance for matching downbeat times to beat times (s). */
const DOWNBEAT_TOLERANCE = 1e-3;

/**
 * Builds the click grid for the current settings.
 *
 * @param grid - Beat grid of the track, or null.
 * @param settings - Metronome settings (validated).
 * @returns A constant grid when `bpmOverride` is set, the detected grid when it is reliable,
 *   otherwise a silent grid.
 */
export function createClickGrid(grid: BeatGrid | null, settings: MetronomeSettings): ClickGrid {
  const offset = settings.offsetMs / 1000;
  if (settings.bpmOverride !== null) {
    return constantGrid({ period: 60 / settings.bpmOverride, base: originOf(grid) + offset, beatsPerBar: settings.beatsPerBar });
  }
  return grid?.reliable === true ? detectedGrid(grid, settings.beatsPerBar, offset) : SILENT_GRID;
}

/**
 * Euclidean modulo (non-negative for negative `value`).
 *
 * @param value - Dividend.
 * @param divisor - Positive divisor.
 * @returns `value mod divisor` in `[0, divisor)`.
 */
export function mod(value: number, divisor: number): number {
  return ((value % divisor) + divisor) % divisor;
}

function originOf(grid: BeatGrid | null): number {
  return grid?.downbeats[0] ?? grid?.beats[0] ?? 0;
}

interface ConstantSpec {
  readonly period: number;
  readonly base: number;
  readonly beatsPerBar: number;
}

function constantGrid(spec: ConstantSpec): ClickGrid {
  return { between: (from, to) => constantClicks(spec, from, to) };
}

function constantClicks(spec: ConstantSpec, from: number, to: number): Click[] {
  const clicks: Click[] = [];
  for (let k = Math.ceil((from - spec.base) / spec.period) - 1; ; k++) {
    const time = spec.base + k * spec.period;
    if (time >= to) {
      return clicks;
    }
    if (time >= from) {
      clicks.push({ time, accent: mod(k, spec.beatsPerBar) === 0 });
    }
  }
}

function detectedGrid(grid: BeatGrid, beatsPerBar: number, offset: number): ClickGrid {
  const accents = accentFlags(grid, beatsPerBar);
  const clicks = grid.beats.map((beat, i) => ({ time: beat + offset, accent: accents[i] === true }));
  return { between: (from, to) => clicks.filter((c) => c.time >= from && c.time < to) };
}

function accentFlags(grid: BeatGrid, beatsPerBar: number): boolean[] {
  const isDownbeat = grid.beats.map((b) => grid.downbeats.some((d) => Math.abs(d - b) < DOWNBEAT_TOLERANCE));
  let barStart = Math.max(0, isDownbeat.indexOf(true));
  return isDownbeat.map((downbeat, i) => {
    barStart = downbeat ? i : barStart;
    return mod(i - barStart, beatsPerBar) === 0;
  });
}

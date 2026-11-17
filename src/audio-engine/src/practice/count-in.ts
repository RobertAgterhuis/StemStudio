/**
 * Count-in plan (data-model 3.3, ADR-021 D6): N = bars x beatsPerBar clicks that continue the song's click grid
 * backwards from the first beat `b >= A`, scaled by the rate, so the music enters on the beat. Pure functions; the
 * controller schedules the clicks at the returned context times.
 *
 * @packageDocumentation
 */
import { createClickGrid } from '../metronome/grid';
import type { BeatGrid, MetronomeSettings } from '../types';

/** Tempo of a count-in at the start position. */
export interface CountInTempo {
  /** Beat interval d in track seconds. */
  readonly interval: number;
  /** First click time b >= A of the grid (track seconds); A itself with a manual BPM and no grid. */
  readonly firstBeat: number;
}

/** One planned count-in click. */
export interface CountInClick {
  /** Context time. */
  readonly time: number;
  /** Beat 1 of a counted bar. */
  readonly accent: boolean;
  /** Beats left including this one (N..1). */
  readonly beat: number;
}

/** Input of {@link countInPlan}. */
export interface CountInInput {
  /** Tempo at the start position. */
  readonly tempo: CountInTempo;
  /** Start position A (track seconds). */
  readonly start: number;
  /** Playback rate. */
  readonly rate: number;
  /** Heard context time T0 of A. */
  readonly musicStart: number;
  /** Counted bars (0..2). */
  readonly bars: number;
  /** Beats per bar of the metronome settings. */
  readonly beatsPerBar: number;
}

/** Track seconds searched after A for the next two grid clicks. */
const SEARCH_SECONDS = 10;

/**
 * @param grid - Beat grid of the track.
 * @param metronome - Metronome settings (tempo source: `bpmOverride`, grid, `offsetMs`).
 * @param start - Start position A.
 * @returns The tempo at A, or null without a usable tempo (no manual BPM and no reliable grid of two beats).
 */
export function countInTempo(grid: BeatGrid | null, metronome: MetronomeSettings, start: number): CountInTempo | null {
  if (metronome.bpmOverride !== null) {
    return manualTempo(grid, metronome, metronome.bpmOverride, start);
  }
  return usableGrid(grid) ? gridTempo(grid, metronome, start) : null;
}

function usableGrid(grid: BeatGrid | null): grid is BeatGrid {
  return grid?.reliable === true && grid.beats.length > 1;
}

function manualTempo(grid: BeatGrid | null, metronome: MetronomeSettings, bpm: number, start: number): CountInTempo {
  const interval = 60 / bpm;
  const first = grid === null ? undefined : createClickGrid(grid, metronome).between(start, start + interval)[0];
  return { interval, firstBeat: first?.time ?? start };
}

function gridTempo(grid: BeatGrid, metronome: MetronomeSettings, start: number): CountInTempo {
  const clicks = createClickGrid(grid, metronome);
  const [first, second] = clicks.between(start, start + SEARCH_SECONDS);
  if (first !== undefined && second !== undefined) {
    return { interval: second.time - first.time, firstBeat: first.time };
  }
  const offset = metronome.offsetMs / 1000;
  const beats = grid.beats.map((beat) => beat + offset);
  const last = beats.length - 1;
  return { interval: Number(beats[last]) - Number(beats[last - 1]), firstBeat: first?.time ?? start };
}

/**
 * @param input - Tempo, start, rate, music start and bar settings.
 * @returns Clicks k = N..1 at `T0 + (b - A) / r - k * d / r`, accent on beat 1 of each counted bar.
 */
export function countInPlan(input: CountInInput): CountInClick[] {
  const total = input.bars * input.beatsPerBar;
  const { interval, firstBeat } = input.tempo;
  const base = input.musicStart + (firstBeat - input.start) / input.rate;
  return Array.from({ length: total }, (_, i) => {
    const beat = total - i;
    return { time: base - (beat * interval) / input.rate, accent: i % input.beatsPerBar === 0, beat };
  });
}

/**
 * @param tempo - Tempo at A.
 * @param start - Start position A.
 * @param rate - Playback rate.
 * @param beats - Number of counted beats N.
 * @returns Context seconds from the first click to the music start: `(N * d - (b - A)) / r` (0 without beats).
 */
export function countInLead(tempo: CountInTempo, start: number, rate: number, beats: number): number {
  return beats === 0 ? 0 : (beats * tempo.interval - (tempo.firstBeat - start)) / rate;
}

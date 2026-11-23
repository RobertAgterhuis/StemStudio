/**
 * Horizontal time scales of the views (contract 2.6.0): bars of the beat grid (fractional bar index, mean bar length
 * outside the grid) or, without a usable grid, seconds.
 *
 * @packageDocumentation
 */
import type { BeatGrid } from '../types';
import { lastAtOrBefore } from './cursor';

/** Maps track milliseconds to view units and back. */
export interface TimeScale {
  /** Track ms -> units (bars or seconds). */
  units(ms: number): number;
  /** Units -> track ms. */
  ms(units: number): number;
  /** Start (ms) of the unit holding `ms`. */
  barStart(ms: number): number;
}

/** One unit per second (no beat grid). */
export class SecondScale implements TimeScale {
  /** {@inheritDoc TimeScale.units} */
  public units(ms: number): number {
    return ms / 1000;
  }

  /** {@inheritDoc TimeScale.ms} */
  public ms(units: number): number {
    return units * 1000;
  }

  /** {@inheritDoc TimeScale.barStart} */
  public barStart(ms: number): number {
    return Math.floor(ms / 1000) * 1000;
  }
}

/** One unit per bar of the grid's downbeats (at least two, ascending, ms). */
export class BarScale implements TimeScale {
  private readonly mean: number;
  private readonly last: number;

  /** @param downbeats - Downbeat times in ms (length >= 2, ascending). */
  public constructor(private readonly downbeats: readonly number[]) {
    this.last = downbeats.length - 1;
    this.mean = (this.at(this.last) - this.at(0)) / this.last;
  }

  /** {@inheritDoc TimeScale.units} */
  public units(ms: number): number {
    const i = this.clamp(lastAtOrBefore(this.downbeats, ms));
    return i + (ms - this.at(i)) / this.length(i, ms < this.at(0));
  }

  /** {@inheritDoc TimeScale.ms} */
  public ms(units: number): number {
    const i = this.clamp(Math.floor(units));
    return this.at(i) + (units - i) * this.length(i, units < 0);
  }

  /** {@inheritDoc TimeScale.barStart} */
  public barStart(ms: number): number {
    return this.ms(Math.floor(this.units(ms)));
  }

  private clamp(i: number): number {
    return Math.min(Math.max(i, 0), this.last);
  }

  /** Length of bar `i`; the mean bar outside the grid (before the first or from the last downbeat). */
  private length(i: number, before: boolean): number {
    return i === this.last || before ? this.mean : this.at(i + 1) - this.at(i);
  }

  private at(i: number): number {
    return Number(this.downbeats[i]);
  }
}

/**
 * @param grid - Beat grid of the track, or null.
 * @returns A {@link BarScale} for a grid with at least two downbeats, else a {@link SecondScale}.
 */
export function scaleFor(grid: BeatGrid | null): TimeScale {
  const downbeats = grid?.downbeats ?? [];
  return downbeats.length >= 2 ? new BarScale(downbeats.map((s) => s * 1000)) : new SecondScale();
}

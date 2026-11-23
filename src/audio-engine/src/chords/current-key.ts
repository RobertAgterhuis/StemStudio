/**
 * The key shown at the song position (contract 2.6.0 chord lane host `data-current-key`, data-model 2.8): the key of
 * the region holding the position, else the global key, displayed at the current 012 transpose. The region is found
 * with a {@link Cursor} (O(1) per frame) and the text is only rebuilt when the region or the transpose changes.
 *
 * @packageDocumentation
 */
import { Cursor } from '../timeline/cursor';
import type { ChordChartData, KeyRegionData } from '../types';
import { keyAt, keyDisplay } from './spelling';

/** Index used for "outside every region" (the global key). */
const GLOBAL = -1;

/** Key display at the position of one chart. */
export class CurrentKey {
  private readonly regions: readonly KeyRegionData[];
  private readonly cursor: Cursor;
  private region = Number.NaN;
  private transpose = Number.NaN;
  private text = '';

  /** @param chart - The validated chart (ordered key regions). */
  public constructor(private readonly chart: ChordChartData) {
    this.regions = chart.keyRegions ?? [];
    this.cursor = new Cursor(this.regions.map((r) => r.startMs));
  }

  /**
   * @param ms - Track position (ms).
   * @param transpose - Current 012 transpose (semitones).
   * @returns E.g. `D major` or `E major (original D major, +2)`; `''` when the chart has no key.
   */
  public at(ms: number, transpose: number): string {
    const region = this.regionAt(ms);
    if (region !== this.region || transpose !== this.transpose) {
      this.region = region;
      this.transpose = transpose;
      const key = keyAt(this.chart, ms);
      this.text = key === null ? '' : keyDisplay(key, transpose);
    }
    return this.text;
  }

  private regionAt(ms: number): number {
    const index = this.cursor.index(ms);
    const region = this.regions[index];
    return region !== undefined && ms < region.endMs ? index : GLOBAL;
  }
}

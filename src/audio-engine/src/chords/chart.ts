/**
 * Validation of a chord chart (contract 2.6.0 errors: `Invalid chord chart: <reason>`) and its prepared form: start
 * times for the cursors and the spelling key of every segment (the key region it starts in, data-model 2.5).
 *
 * @packageDocumentation
 */
import type { ChordChartData, ChordSymbolEvent, KeyRegionData } from '../types';
import { QUALITY_SUFFIX, keyAt, type SpellingKey } from './spelling';

/** Most segments of a chart. */
export const MAX_SEGMENTS = 4000;
/** Most key regions of a chart. */
export const MAX_KEY_REGIONS = 50;

/** A validated chart. */
export interface PreparedChart {
  /** The chart as given. */
  readonly chart: ChordChartData;
  /** The segments. */
  readonly segments: readonly ChordSymbolEvent[];
  /** `startMs` of every segment. */
  readonly starts: readonly number[];
  /** Spelling key of every segment (its start's region, else the global key, else null). */
  readonly keys: readonly (SpellingKey | null)[];
}

function fail(reason: string): never {
  throw new Error(`Invalid chord chart: ${reason}`);
}

function isPitchClass(value: unknown): boolean {
  return Number.isInteger(value) && (value as number) >= 0 && (value as number) <= 11;
}

function checkOrder(segment: ChordSymbolEvent, previous: ChordSymbolEvent | undefined): void {
  const start = previous === undefined ? 0 : previous.endMs;
  const contiguous = previous === undefined ? segment.startMs >= start : segment.startMs === start;
  if (!(contiguous && segment.endMs > segment.startMs)) {
    fail('segments not ordered/contiguous');
  }
}

function isQuality(quality: unknown): boolean {
  return typeof quality === 'string' && Object.hasOwn(QUALITY_SUFFIX, quality);
}

function checkNoChord(segment: ChordSymbolEvent): void {
  if (segment.quality !== null || segment.bass !== null) {
    fail(`unknown quality ${String(segment.quality)}`);
  }
}

function checkChord(segment: ChordSymbolEvent): void {
  if (!isPitchClass(segment.root)) {
    fail('root outside 0..11');
  }
  if (!isQuality(segment.quality)) {
    fail(`unknown quality ${String(segment.quality)}`);
  }
  if (segment.bass !== null && !isPitchClass(segment.bass)) {
    fail('bass outside 0..11');
  }
}

function checkPitches(segment: ChordSymbolEvent): void {
  if (segment.root === null) {
    checkNoChord(segment);
  } else {
    checkChord(segment);
  }
}

function checkKey(tonic: unknown, mode: unknown): void {
  if (!isPitchClass(tonic)) {
    fail('key tonic outside 0..11');
  }
  if (mode !== 'major' && mode !== 'minor') {
    fail(`unknown key mode ${String(mode)}`);
  }
}

function checkRegions(regions: readonly KeyRegionData[]): void {
  if (regions.length > MAX_KEY_REGIONS) {
    fail(`more than ${String(MAX_KEY_REGIONS)} key regions`);
  }
  regions.forEach((region, i) => {
    checkKey(region.tonic, region.mode);
    const previousEnd = regions[i - 1]?.endMs ?? Number.NEGATIVE_INFINITY;
    if (!(region.startMs >= previousEnd && region.endMs > region.startMs)) {
      fail('key regions not ordered');
    }
  });
}

function checkSegments(segments: unknown): readonly ChordSymbolEvent[] {
  if (!Array.isArray(segments)) {
    fail('segments missing');
  }
  const list = segments as readonly ChordSymbolEvent[];
  if (list.length > MAX_SEGMENTS) {
    fail(`more than ${String(MAX_SEGMENTS)} segments`);
  }
  list.forEach((segment, i) => {
    checkOrder(segment, list[i - 1]);
    checkPitches(segment);
  });
  return list;
}

/**
 * @param chart - Chart from Blazor (untrusted shape).
 * @returns The prepared chart.
 * @throws Error `Invalid chord chart: <reason>`.
 */
export function prepareChart(chart: ChordChartData): PreparedChart {
  const segments = checkSegments(chart.segments);
  if (chart.keyTonic !== null || chart.keyMode !== null) {
    checkKey(chart.keyTonic, chart.keyMode);
  }
  checkRegions(chart.keyRegions ?? []);
  return {
    chart,
    segments,
    starts: segments.map((s) => s.startMs),
    keys: segments.map((s) => keyAt(chart, s.startMs)),
  };
}

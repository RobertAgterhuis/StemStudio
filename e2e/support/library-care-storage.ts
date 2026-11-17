/**
 * Helpers of the feature 013 storage and clean-up specs: the on-disk byte sums inside the
 * containers per storage category (SC-006, quickstart V8) and lookups in the overview.
 *
 * @packageDocumentation
 */
import { expect } from '@playwright/test';
import { fileBytes, fileBytesWhere } from './compose';
import type { StorageCategory, StorageOverviewDto, TrackStorageDto } from './library-care-api';

/** Bytes of a category in the overview (`null` when it could not be measured). */
export function categoryBytes(overview: StorageOverviewDto, category: StorageCategory): number | null {
  return overview.categories.find((usage) => usage.category === category)?.bytes ?? null;
}

/** The overview row of a track. */
export function trackStorage(overview: StorageOverviewDto, trackId: string): TrackStorageDto {
  const row = overview.tracks.find((track) => track.trackId === trackId);
  if (row === undefined) throw new Error(`the storage overview has no row for track ${trackId}`);
  return row;
}

/** Library categories whose files are found by their path below `/data/tracks` (api container). */
const trackPatterns: readonly (readonly [StorageCategory, string])[] = [
  ['stems', '*/stems/*'],
  ['detailStems', '*/detail/*'],
  ['artwork', '*/artwork/*'],
];

/**
 * Asserts that the path-derived library categories equal the byte sums on disk (deviation 0,
 * SC-006): stems, detail stems and artwork below `/data/tracks`, voice samples below `/data/voices`,
 * and the asset categories together equal `/data/assets` when no generation exists.
 */
export function expectLibraryCategoriesOnDisk(overview: StorageOverviewDto): void {
  for (const [category, pattern] of trackPatterns) {
    expect(categoryBytes(overview, category), category).toBe(fileBytesWhere('api', '/data/tracks', pattern));
  }
  const disk = fileBytes('api', ['/data/voices', '/data/assets', '/data/generations']);
  expect(categoryBytes(overview, 'voiceProfiles'), 'voiceProfiles').toBe(disk.get('/data/voices'));
  const assets = ['exports', 'scores', 'recordings', 'generations'] as const;
  const assetSum = assets.reduce((sum, category) => sum + (categoryBytes(overview, category) ?? 0), 0);
  expect(assetSum, 'exports + scores + recordings + generations').toBe((disk.get('/data/assets') ?? 0) + (disk.get('/data/generations') ?? 0));
}

/** Asserts that every model cache of the overview equals the files below `/models/<id>` (worker). */
export function expectModelCachesOnDisk(overview: StorageOverviewDto): void {
  for (const cache of overview.modelCaches) {
    expect(cache.bytes, `model cache ${cache.id}`).toBe(fileBytesWhere('worker', `/models/${cache.id}`, '*'));
  }
}

/** Asserts that one track's stems equal `/data/tracks/<id>/stems` on disk. */
export function expectTrackStemsOnDisk(overview: StorageOverviewDto, trackId: string): void {
  expect(trackStorage(overview, trackId).stemsBytes).toBe(fileBytesWhere('api', `/data/tracks/${trackId}/stems`, '*'));
}

/** True when `values` never increase (`descending`) or never decrease (`ascending`). */
export function isSorted(values: readonly number[], direction: 'ascending' | 'descending'): boolean {
  return values.every((value, index) => index === 0 || (direction === 'ascending' ? (values[index - 1] ?? value) <= value : (values[index - 1] ?? value) >= value));
}

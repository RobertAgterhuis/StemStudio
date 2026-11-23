/**
 * The library items of the visual and accessibility matrices: the raw bass of the seeded detail
 * track as FLAC (feature 007) and the sheet music of that bass (feature 008), each found by its
 * fixed name or created through the API, so the library screen looks the same on every run of a
 * fresh stack.
 *
 * @packageDocumentation
 */
import { request } from '@playwright/test';
import { AssetsApi, type AssetDto } from './assets-api';
import { apiContextOptions } from './env';
import { ScoreApi } from './score-api';

/** Fixed name of the seeded library item. */
export const uxLibraryItemName = 'UX bass';

/** Fixed name of the seeded score item. */
export const uxScoreItemName = 'UX bass sheet music';

const renderMs = 60_000;

/** Finds or creates the seeded item of `trackId` and waits until it is ready. */
export async function ensureLibraryItem(trackId: string): Promise<AssetDto> {
  const context = await request.newContext(apiContextOptions);
  try {
    const assets = new AssetsApi(context);
    const listed = await assets.list({ q: uxLibraryItemName });
    const existing = listed.items.find((item) => item.name === uxLibraryItemName);
    const item =
      existing ??
      (await assets.createExport({ trackId, mode: 'raw', channels: ['bass'], format: { format: 'flac' }, name: uxLibraryItemName }));
    return await assets.waitForReady(item.id, renderMs);
  } finally {
    await context.dispose();
  }
}

/** Finds or creates the seeded score item (the bass of `trackId`, profile bass) and waits until it is ready. */
export async function ensureScoreItem(trackId: string): Promise<AssetDto> {
  const context = await request.newContext(apiContextOptions);
  try {
    const assets = new AssetsApi(context);
    const listed = await assets.list({ q: uxScoreItemName });
    const existing = listed.items.find((item) => item.name === uxScoreItemName);
    const item =
      existing ?? (await new ScoreApi(context).create({ source: { trackId, channel: 'bass' }, profile: 'bass', name: uxScoreItemName }));
    return await assets.waitForReady(item.id, renderMs);
  } finally {
    await context.dispose();
  }
}

/**
 * The one library item of the visual and accessibility matrices (feature 007): the raw bass of
 * the seeded detail track as FLAC, found by its fixed name or created through the API, so the
 * library screen looks the same on every run of a fresh stack.
 *
 * @packageDocumentation
 */
import { request } from '@playwright/test';
import { AssetsApi, type AssetDto } from './assets-api';
import { apiBaseUrl } from './env';

/** Fixed name of the seeded library item. */
export const uxLibraryItemName = 'UX bass';

const renderMs = 60_000;

/** Finds or creates the seeded item of `trackId` and waits until it is ready. */
export async function ensureLibraryItem(trackId: string): Promise<AssetDto> {
  const context = await request.newContext({ baseURL: apiBaseUrl });
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

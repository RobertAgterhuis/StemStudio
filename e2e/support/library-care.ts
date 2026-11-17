/**
 * Shared set-up of the feature 013 specs (`@library-care`): the `test` object with the library
 * care page and API client, a seeded library, backup/restore helpers and the rule that every
 * spec leaves the library as it found it (TEST-STRATEGY section 19.4: a replace restore is always
 * followed by "Undo restore" and "Discard", so the specs that run later see their own data).
 *
 * @packageDocumentation
 */
import { randomUUID } from 'node:crypto';
import { test as base, expect } from '@playwright/test';
import { LibraryCarePage } from '../pages/library-care-page';
import type { MixDto, StemStudioApi, TrackDto } from './api';
import type { AssetDto, AssetsApi } from './assets-api';
import { fakeStems } from './detail';
import { apiBaseUrl, detailPhaseTimeoutMs, fakeJobTimeoutMs, runId } from './env';
import { archiveOf, LibraryCareApi, type BackupOptionsDto, type LibraryJobDto, type RestoreOptionsDto } from './library-care-api';
import { mixSettings } from './practice';
import { test as suite } from './test';

/** Timeout of one library job on the fake stack (a few small tracks; throttled runs take longer). */
export const libraryJobTimeoutMs = 180_000;

/** True when the stack slows library jobs (`StemStudio__LibraryCare__ThrottleBytesPerSecond`, e2e override). */
export const libraryThrottled: boolean = process.env.E2E_LIBRARY_THROTTLE === '1';

/**
 * Skips cases that must act while a library job is still running (cancel, write gate, blocked
 * restore, maintenance overlay): on an unthrottled fake stack a backup of the seeded library ends
 * in well under a second, so these cases would race (CTO decision 2026-11-17, 013).
 */
export function requireThrottledJobs(): void {
  base.skip(!libraryThrottled, 'needs E2E_LIBRARY_THROTTLE=1 and the e2e override that sets StemStudio__LibraryCare__ThrottleBytesPerSecond');
}

/** Fixtures of the library care specs. */
export interface LibraryCareFixtures {
  readonly care: LibraryCarePage;
  readonly careApi: LibraryCareApi;
}

/** The suite's `test` with the library care page and client. */
export const test = suite.extend<LibraryCareFixtures>({
  care: async ({ page }, use) => {
    await use(new LibraryCarePage(page));
  },
  careApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    await use(new LibraryCareApi(context));
    await context.dispose();
  },
});

export { expect };

/** What `seedLibrary` created. */
export interface SeededLibrary {
  readonly track: TrackDto;
  readonly mix: MixDto;
  readonly exported: AssetDto;
}

/**
 * A completed track (the 20 s music fixture), a mix of all six stems at equal level and a raw
 * export of every stem (FLAC), so stems, exports and the database are all non-empty. Instrument
 * neutral: no family is singled out.
 */
export async function seedLibrary(api: StemStudioApi, assetsApi: AssetsApi, salt: string): Promise<SeededLibrary> {
  const track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
  // The shared track is reused by every spec, so each seed needs its own mix name (409 "name taken" otherwise).
  const name = `Library care ${runId()} ${salt} ${randomUUID().slice(0, 8)}`;
  const mix = await api.createMix(track.id, mixSettings({ name }));
  const queued = await assetsApi.createExport({ trackId: track.id, mode: 'raw', channels: [...fakeStems], format: { format: 'flac', bitDepth: 16 }, name });
  const exported = await assetsApi.waitForReady(queued.id, fakeJobTimeoutMs);
  return { track, mix, exported };
}

/** Starts a backup through the API, waits until it is completed and returns the archive name. */
export async function backupNow(careApi: LibraryCareApi, options: BackupOptionsDto = {}): Promise<string> {
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  const job = await careApi.waitForJob((await careApi.createBackup(options)).jobId, libraryJobTimeoutMs);
  expect(job.status, job.error?.message ?? '').toBe('completed');
  return archiveOf(job);
}

/**
 * Waits until no import, detail phase, export or other job is active (`activeWork` empty), as a
 * restore requires (FR-013). New imports start an automatic detail phase (feature 002), so a spec
 * that imports right before a restore must wait for it.
 */
export async function waitForNoActiveWork(careApi: LibraryCareApi): Promise<void> {
  const busy = (): Promise<string[]> => careApi.status().then((status) => status.activeWork.map((work) => `${work.kind} ${work.title}`));
  await expect.poll(busy, { timeout: Math.max(libraryJobTimeoutMs, detailPhaseTimeoutMs * 3), intervals: [500, 1000, 2000] }).toEqual([]);
}

/** Runs a restore through the API (after active work has ended) and waits for its end; returns the terminal job. */
export async function restoreNow(careApi: LibraryCareApi, fileName: string, options: RestoreOptionsDto): Promise<LibraryJobDto> {
  await waitForNoActiveWork(careApi);
  const job = await careApi.waitForJob((await careApi.restore(fileName, options)).jobId, libraryJobTimeoutMs);
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  return job;
}

/**
 * Brings back the library that existed before a replace restore of this spec: "Undo restore"
 * (the restored library becomes the previous one), then discard it. No-op without a previous
 * library.
 */
export async function undoAndDiscard(careApi: LibraryCareApi): Promise<void> {
  if ((await careApi.previous()) === null) return;
  await waitForNoActiveWork(careApi);
  const undo = await careApi.waitForJob((await careApi.revert()).jobId, libraryJobTimeoutMs);
  expect(undo.status, undo.error?.message ?? '').toBe('completed');
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  expect(await careApi.discard()).toBe(204);
}

/** Deletes archives this spec wrote or placed (missing ones are fine). */
export async function deleteArchives(careApi: LibraryCareApi, names: Iterable<string>): Promise<void> {
  for (const name of names) {
    expect([204, 404]).toContain(await careApi.deleteBackup(name));
  }
}

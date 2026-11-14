/**
 * Steps of the constitution's main flow (import -> separate -> mix -> save -> reload), shared by
 * the fake-separator spec, the `@restart` check and the `@real` Demucs variant.
 *
 * @packageDocumentation
 */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { expect, type Page } from '@playwright/test';
import type { ImportPage } from '../pages/import-page';
import type { MixLibraryPage } from '../pages/mix-library-page';
import type { MixerPage, MixerSettings } from '../pages/mixer-page';
import type { TrackDetailPage } from '../pages/track-detail-page';
import type { StemName, StemStudioApi } from './api';
import { runId } from './env';
import { fixturePath, type FixtureName } from './fixtures';

/**
 * The "Practice" mix: touches every kind of setting and only stems present in both the 6-stem and
 * the 4-stem model (vocals, drums, bass, other).
 */
export const practiceSettings: MixerSettings = {
  faders: { vocals: 35, drums: 90 },
  pans: { bass: -40, other: 25 },
  mutes: ['other'],
  solos: ['drums', 'bass'],
  master: 70,
  metronome: { enabled: true, bpm: 120, offsetMs: 50 },
  loop: { start: 10, end: 20, enabled: true },
};

/** Page, API client and page objects the flow needs (all are suite fixtures). */
export interface FlowContext {
  readonly page: Page;
  readonly api: StemStudioApi;
  readonly importPage: ImportPage;
  readonly trackPage: TrackDetailPage;
  readonly mixer: MixerPage;
  readonly mixLibrary: MixLibraryPage;
}

/** A saved mix handed from `npm test` to `npm run test:restart` (T122, SC-005). */
export interface PersistedMix {
  readonly trackId: string;
  readonly mixId: string;
  readonly mixName: string;
  readonly stems: readonly StemName[];
  readonly settings: MixerSettings;
}

const stateDir = path.resolve(__dirname, '..', '.state');

/** A mix name unique per suite run and browser project. */
export function uniqueMixName(base: string, project: string): string {
  return `${base} ${runId()}-${project}`;
}

/** Uploads a fixture in the UI, waits for completion and opens the mixer; returns id + stems. */
export async function importAndOpenMixer(
  ctx: FlowContext,
  fixture: FixtureName,
  timeoutMs: number,
): Promise<{ trackId: string; stems: StemName[] }> {
  await ctx.importPage.goto();
  await ctx.importPage.uploadFile(fixturePath(fixture));
  const trackId = await ctx.trackPage.waitForTrackId();
  await ctx.trackPage.waitForStatus('completed', timeoutMs);
  await ctx.trackPage.openMixer.click();
  await ctx.page.waitForURL(`**/tracks/${trackId}/mixer**`);
  const stems = (await ctx.api.getTrack(trackId)).stems.map((stem) => stem.name);
  await ctx.mixer.waitForReady(stems);
  return { trackId, stems };
}

/** Saves the current mixer state as a new mix and returns its id (looked up via the API). */
export async function saveNewMix(ctx: FlowContext, trackId: string, name: string): Promise<string> {
  await expect(ctx.mixer.byId('unsaved-indicator')).toBeVisible();
  await ctx.mixer.saveNew(name);
  await expect.poll(async () => (await ctx.api.findMix(trackId, name))?.id).toBeTruthy();
  const mix = await ctx.api.findMix(trackId, name);
  return mix?.id ?? '';
}

/** Reloads the browser, loads the mix from the library and asserts every setting. */
export async function reloadAndVerify(ctx: FlowContext, saved: PersistedMix): Promise<void> {
  await ctx.page.reload();
  await ctx.mixLibrary.goto();
  await ctx.mixLibrary.load(saved.mixId);
  await ctx.mixer.waitForReady(saved.stems);
  await ctx.mixer.expectSettings(saved.settings, saved.stems);
}

/** Writes the hand-over state of a browser project. */
export function writePersistedMix(project: string, saved: PersistedMix): void {
  mkdirSync(stateDir, { recursive: true });
  writeFileSync(path.join(stateDir, `${project}-mix.json`), JSON.stringify(saved, null, 2));
}

/** Reads the hand-over state written by `npm test` for this browser project. */
export function readPersistedMix(project: string): PersistedMix {
  const file = path.join(stateDir, `${project}-mix.json`);
  return JSON.parse(readFileSync(file, 'utf-8')) as PersistedMix;
}

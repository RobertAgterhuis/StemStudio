/**
 * Steps of the Beginner journey (SC-007, quickstart V-J2): welcome -> Beginner -> import ->
 * mixer -> "Play along: drums" -> Save, using only the journey rail, primary buttons and presets.
 *
 * @packageDocumentation
 */
import { expect, type Page } from '@playwright/test';
import type { AppShell } from '../pages/app-shell';
import type { ImportPage } from '../pages/import-page';
import type { MixerPage } from '../pages/mixer-page';
import type { TrackDetailPage } from '../pages/track-detail-page';
import type { StemStudioApi } from './api';
import { isOn } from './controls';
import { fakeStems } from './detail';
import { fakeJobTimeoutMs, runId } from './env';
import { fixturePath } from './fixtures';
import { uniqueWavCopy } from './unique-audio';

/** Page, API and page objects of the journey. */
export interface JourneyContext {
  readonly page: Page;
  readonly api: StemStudioApi;
  readonly shell: AppShell;
  readonly importPage: ImportPage;
  readonly trackPage: TrackDetailPage;
  readonly mixer: MixerPage;
}

/** First visit: the welcome panel is shown at Beginner and "I am new to this" keeps Beginner. */
export async function chooseBeginnerInWelcome(ctx: JourneyContext): Promise<void> {
  await ctx.importPage.goto();
  await expect(ctx.shell.byId('welcome-panel')).toBeVisible();
  await ctx.shell.expectLevel('beginner');
  await expect(ctx.shell.byId('import-url-input')).toBeEditable();
  await ctx.shell.byId('welcome-level-beginner').click();
  await expect(ctx.shell.byId('welcome-panel')).toBeHidden();
  await ctx.shell.expectLevel('beginner');
  await ctx.shell.expectJourneyStep('import', 'current');
}

/**
 * Uploads a per-run copy of the click fixture (new hash, so the separation really runs), follows
 * the rail through Separate and returns the track id once the track is completed.
 */
export async function importForJourney(ctx: JourneyContext, salt: string): Promise<string> {
  const file = uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `${runId()}-journey-${salt}`);
  await ctx.importPage.uploadFile(file);
  const trackId = await ctx.trackPage.waitForTrackId();
  await ctx.shell.expectJourneyStep('import', 'done');
  await expectSeparateWhileProcessing(ctx);
  await ctx.trackPage.waitForStatus('completed', fakeJobTimeoutMs);
  await ctx.shell.expectJourneyStep('separate', 'done');
  return trackId;
}

/** While the track page still shows a running job, the rail's current step is Separate. */
async function expectSeparateWhileProcessing(ctx: JourneyContext): Promise<void> {
  const status = (await ctx.trackPage.status.textContent()) ?? '';
  if (!/completed/i.test(status)) {
    await expect(ctx.shell.journeyStep('separate')).toHaveAttribute('data-state', /^(current|done)$/);
  }
}

/** Primary button "Open mixer", then the rail shows Mix as the current step. */
export async function openMixerFromTrack(ctx: JourneyContext, trackId: string): Promise<void> {
  await ctx.trackPage.openMixer.click();
  await ctx.page.waitForURL(`**/tracks/${trackId}/mixer**`);
  await ctx.mixer.waitForReady(fakeStems);
  await ctx.shell.expectJourneyStep('mix', 'current');
}

/** Preset chip "Play along: drums": drums muted, the rest audible, click on, mix unsaved. */
export async function playAlongDrums(ctx: JourneyContext): Promise<void> {
  await ctx.shell.byId('preset-play-along-drums').click();
  await expect.poll(() => isOn(ctx.mixer.mute('drums'))).toBe(true);
  for (const stem of fakeStems.filter((name) => name !== 'drums')) {
    await expect.poll(() => isOn(ctx.mixer.mute(stem))).toBe(false);
    await expect.poll(() => isOn(ctx.mixer.solo(stem))).toBe(false);
  }
  await expect.poll(() => isOn(ctx.mixer.byId('metronome-toggle'))).toBe(true);
  await expect(ctx.mixer.byId('unsaved-indicator')).toBeVisible();
}

/** Primary button Save with a name; the rail marks Save done and the API has the settings. */
export async function saveJourneyMix(ctx: JourneyContext, trackId: string, name: string): Promise<void> {
  await ctx.mixer.saveNew(name);
  await ctx.shell.expectJourneyStep('save', 'done');
  await expect.poll(async () => (await ctx.api.findMix(trackId, name))?.id).toBeTruthy();
  const mix = await ctx.api.getMix((await ctx.api.findMix(trackId, name))?.id ?? '');
  expect(mix.settings.channels.find((channel) => channel.stem === 'drums')?.mute).toBe(true);
  expect(mix.settings.metronome.enabled).toBe(true);
}

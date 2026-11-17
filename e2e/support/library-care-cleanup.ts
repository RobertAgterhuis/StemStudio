/**
 * Helpers of the feature 013 clean-up and merge specs: a track of the spec's own with completed
 * detail stems (never the shared band track other specs rely on) and a mix with detail settings.
 *
 * @packageDocumentation
 */
import { expect } from '@playwright/test';
import type { MixDto, StemStudioApi, TrackDto } from './api';
import { importWithLivePhase, keptStems } from './detail';
import { detailPhaseTimeoutMs } from './env';
import { mixSettings } from './practice';

/** A unique band copy whose detail phase has completed (the spec may remove its detail stems). */
export async function ownDetailTrack(api: StemStudioApi, salt: string): Promise<TrackDto> {
  const track = await importWithLivePhase(api, salt, ['queued', 'running', 'completed']);
  const done = await api.waitForDetail(track.id, ['completed', 'failed'], detailPhaseTimeoutMs);
  expect(done.detail.status, done.detail.error ?? '').toBe('completed');
  return done;
}

/** A mix of `track` with the vocals expanded into its kept detail stems (feature 002 settings). */
export async function mixWithDetail(api: StemStudioApi, track: TrackDto, name: string): Promise<MixDto> {
  const detailChannels = keptStems('vocals').map((stem) => ({ parent: 'vocals' as const, stem, volume: 0.8, pan: 0, mute: false, solo: false }));
  return api.createMix(track.id, { ...mixSettings({ name }), expanded: ['vocals'], detailChannels });
}

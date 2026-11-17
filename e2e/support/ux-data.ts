/**
 * Shared data of the feature 004 suites (visual, a11y, help, tours, levels, keyboard): one track
 * with detail stems, one without, and one saved mix. Created through the API once per worker and
 * reused on reruns (worker cache, find-or-create mix), so names and order are stable on a fresh
 * stack (the visual suite needs one; TEST-STRATEGY.md section 5, feature 004).
 *
 * @packageDocumentation
 */
import type { MixSettingsDto, StemStudioApi, TrackDto } from './api';
import { activeStatuses, ensureDetailCompleted, fakeStems } from './detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from './env';

/** The seeded tracks and mix. */
export interface UxData {
  /** `detail_10s_band.wav` with a completed detail phase (drums, vocals, other split). */
  readonly detailTrack: TrackDto;
  /** `click_120bpm_4-4_12s.wav` whose detail phase was cancelled (no detail stems). */
  readonly plainTrack: TrackDto;
  /** Mix "UX practice" of the detail track. */
  readonly mixId: string;
}

/** Name of the seeded mix (fixed, so the mix library looks the same on every run). */
export const uxMixName = 'UX practice';

/** Creates (or finds) the shared data. */
export async function seedUxData(api: StemStudioApi): Promise<UxData> {
  const detailTrack = await ensureDetailCompleted(api);
  const plainTrack = await ensurePlainTrackNewest(api, detailTrack);
  const mixId = await ensureMix(api, detailTrack.id);
  return { detailTrack, plainTrack, mixId };
}

/**
 * The click fixture with its auto detail phase cancelled. On a stack that already finished that
 * phase the track keeps its detail stems; the visual suite therefore runs on a fresh stack.
 */
export async function ensurePlainTrack(api: StemStudioApi): Promise<TrackDto> {
  const track = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
  if (activeStatuses.includes(track.detail.status)) {
    await api.cancelDetail(track.id);
  }
  return api.waitForDetail(track.id, ['completed', 'failed', 'cancelled', 'none'], detailPhaseTimeoutMs);
}

/**
 * {@link ensurePlainTrack}, imported after the detail track. The tracks screen lists newest first, so its row order (visual
 * baselines `tracks--*`) is the import order: plain track first. A stack on which another spec imported the click fixture
 * before the detail band file would flip the two rows (the "first tracks screenshot" flake); the plain track is then removed
 * and imported again, which gives it a newer `createdAt`. A refused delete (409: the track is in use) keeps the track.
 */
async function ensurePlainTrackNewest(api: StemStudioApi, detailTrack: TrackDto): Promise<TrackDto> {
  const track = await ensurePlainTrack(api);
  if (Date.parse(track.createdAt) > Date.parse(detailTrack.createdAt) || (await api.deleteTrack(track.id)) !== 204) {
    return track;
  }
  return ensurePlainTrack(api);
}

async function ensureMix(api: StemStudioApi, trackId: string): Promise<string> {
  const existing = await api.findMix(trackId, uxMixName);
  return existing?.id ?? (await api.createMix(trackId, uxMixSettings())).id;
}

/** Settings of the seeded mix: defaults with vocals at 60 % and bass panned left. */
export function uxMixSettings(): MixSettingsDto {
  return {
    name: uxMixName,
    masterVolume: 1,
    channels: fakeStems.map((stem) => ({
      stem,
      volume: stem === 'vocals' ? 0.6 : 0.8,
      pan: stem === 'bass' ? -0.3 : 0,
      mute: false,
      solo: false,
    })),
    metronome: { enabled: false, volume: 0.5, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 },
    loop: null,
  };
}

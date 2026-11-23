/**
 * Helpers of the feature 016 singing specs (T107, TEST-STRATEGY 22.2): the reference track, the network watch of FR-047
 * ("no audio or score leaves the browser") and the unit scores the banner showed during a sung pass.
 *
 * @packageDocumentation
 */
import { expect, type Page, type Request } from '@playwright/test';
import { InstrumentViews } from '../pages/instrument-views';
import type { MixerPage } from '../pages/mixer-page';
import { RecordingPanel } from '../pages/recording-panel';
import type { StemStudioApi, TrackDto } from './api';
import { fakeJobTimeoutMs } from './env';
import type { InstrumentViewsApi } from './instrument-views';
import type { LyricsApi } from './lyrics-api';
import { distinctValues, readWatched, watchAttributes } from './practice';
import { recordingFixturePath } from './recording';

/** Time for the lyrics and the reference melody of the fake stack. */
export const referenceMs = 60_000;

/** The 015 click track: 120 BPM from 0.5 s, the grid the sung fixtures follow (TEST-STRATEGY 22.2). */
export async function referenceTrack(api: StemStudioApi, lyricsApi: LyricsApi, views: InstrumentViewsApi): Promise<TrackDto> {
  const track = await api.ensureFileCompleted(recordingFixturePath('click-track.wav'), fakeJobTimeoutMs);
  await lyricsApi.requireFeature(track.id);
  await lyricsApi.waitForStatus(track.id, ['found'], referenceMs);
  await views.ensureVocalPitch(track.id, referenceMs);
  return track;
}

/** Records every request that carries a body (anything but a plain GET) from now on. */
export function watchUploads(page: Page): Request[] {
  const uploads: Request[] = [];
  page.on('request', (request) => {
    if (request.method() !== 'GET' || request.postDataBuffer() !== null) {
      uploads.push(request);
    }
  });
  return uploads;
}

/** The result of one sung pass from the start of the track to its end. */
export interface SungPass {
  /** Every unit score the banner showed (`karaoke-score` `data-score`, empty values left out). */
  readonly unitScores: number[];
  /** `singing-summary` `data-average`. */
  readonly average: number;
  /** Requests with a body sent while singing. */
  readonly uploads: readonly Request[];
}

/** Length of the fake microphone files (tests/fixtures/generate_instrument_fixtures.py `SECONDS`); Chromium loops them. */
const fakeMicLoopMs = 20_000;

/** Records when the browser's microphone stream opened (`window.__micOpenedAt`, `performance.now()` ms). */
async function watchMicOpen(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const media = navigator.mediaDevices;
    const open = media.getUserMedia.bind(media);
    media.getUserMedia = async (constraints?: MediaStreamConstraints): Promise<MediaStream> => {
      const stream = await open(constraints);
      (window as unknown as { __micOpenedAt: number }).__micOpenedAt = performance.now();
      return stream;
    };
  });
}

/**
 * Presses play in the page when the looping fake microphone file is back at its start, so the sung notes meet the
 * reference beats (the fixture follows the song from 0 s; decision 4 of 2026-11-23: the alignment is the E2E's job).
 */
async function playAlignedWithTheFakeMic(page: Page): Promise<void> {
  await page.evaluate(async (loopMs) => {
    const opened = (window as unknown as { __micOpenedAt?: number }).__micOpenedAt ?? performance.now();
    const wait = (loopMs - ((performance.now() - opened) % loopMs)) % loopMs;
    await new Promise((resolve) => setTimeout(resolve, wait));
    document.querySelector<HTMLButtonElement>('[data-testid="transport-play"]')?.click();
  }, fakeMicLoopMs);
}

/**
 * Opens the 015 input (spec US4 scenario 2), "Practise as Vocals", starts singing and plays the track from bar 1 to its
 * end, started in step with the fake microphone loop; the summary appears at the end of the song (FR-046).
 */
export async function singThrough(page: Page, mixer: MixerPage, track: TrackDto): Promise<SungPass> {
  const ui = new InstrumentViews(page);
  await watchMicOpen(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(['vocals']);
  await new RecordingPanel(page).openWithInput();
  await ui.practiseAsFamily('vocals');
  await expect(ui.view).toHaveAttribute('data-view', 'singing');
  await watchAttributes(page, 'karaoke-score', ['data-score']);
  const uploads = watchUploads(page);
  await ui.startSinging(referenceMs);
  await playAlignedWithTheFakeMic(page);
  await expect(ui.singingSummary).toBeVisible({ timeout: fakeMicLoopMs + 1000 * (track.durationSeconds ?? 20) + 15_000 });
  const unitScores = distinctValues(await readWatched(page, 'karaoke-score'), 'data-score').filter((v) => v !== '').map(Number);
  return { unitScores, average: Number(await ui.singingSummary.getAttribute('data-average')), uploads };
}

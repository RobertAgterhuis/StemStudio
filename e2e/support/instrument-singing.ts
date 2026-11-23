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

/**
 * Opens "Practise as Vocals", starts singing from bar 1 and plays the track to its end (the summary appears on track
 * end). The fake capture file starts with the capture, so the sung notes meet the reference beats (fixture design).
 */
export async function singThrough(page: Page, mixer: MixerPage, track: TrackDto): Promise<SungPass> {
  const ui = new InstrumentViews(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(['vocals']);
  // Spec US4 scenario 2: the input is chosen and open in 015 (the recording panel) before singing starts.
  await new RecordingPanel(page).openWithInput();
  await ui.practiseAsFamily('vocals');
  await expect(ui.view).toHaveAttribute('data-view', 'singing');
  await watchAttributes(page, 'karaoke-score', ['data-score']);
  const uploads = watchUploads(page);
  await ui.startSinging(referenceMs);
  await mixer.play();
  // Play to the end of the track, then stop singing: the panel shows its summary on stop (FR-046 "at the end" is an
  // open item of the 016 integration: the engine has no end-of-song signal for the singing session).
  const transport = page.getByTestId('transport-state');
  await expect(transport).toHaveText(/playing/iu, { timeout: 15_000 });
  await expect(transport).toHaveText(/stopped/iu, { timeout: 1000 * (track.durationSeconds ?? 20) + 15_000 });
  await ui.singingToggle.click();
  await expect(ui.singingSummary).toBeVisible();
  const unitScores = distinctValues(await readWatched(page, 'karaoke-score'), 'data-score').filter((v) => v !== '').map(Number);
  return { unitScores, average: Number(await ui.singingSummary.getAttribute('data-average')), uploads };
}

/**
 * Helper that produces a failed track through the UI (corrupt upload on the fake-separator stack).
 *
 * @packageDocumentation
 */
import type { ImportPage } from '../pages/import-page';
import type { TrackDetailPage } from '../pages/track-detail-page';
import { fakeJobTimeoutMs } from './env';
import { fixturePath } from './fixtures';

/**
 * Uploads `corrupt.mp3` from the import screen and waits until its detail page shows `failed`.
 * @returns The id of the failed track.
 */
export async function uploadFailingTrack(importPage: ImportPage, trackPage: TrackDetailPage): Promise<string> {
  await importPage.goto();
  await importPage.uploadFile(fixturePath('corrupt.mp3'));
  const trackId = await trackPage.waitForTrackId();
  await trackPage.waitForStatus('failed', fakeJobTimeoutMs);
  return trackId;
}

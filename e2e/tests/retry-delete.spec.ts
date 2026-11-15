import { fakeJobTimeoutMs } from '../support/env';
import { uploadFailingTrack } from '../support/failed-track';
import { expect, test } from '../support/test';

// Retry feedback and track delete on the track detail page (ui-test-ids.md, contract 1.1.0
// `deleteTrack`), against the fake-separator stack: a corrupt upload fails within a second.
test('a failed retry says so, and the failed track can be deleted', async ({ api, importPage, page, trackList, trackPage }) => {
  const trackId = await uploadFailingTrack(importPage, trackPage);
  await expect(trackPage.retryFailedAgain).toHaveCount(0);

  await trackPage.retry.click();
  await expect(trackPage.retryFailedAgain).toBeVisible({ timeout: fakeJobTimeoutMs });
  await trackPage.waitForStatus('failed', fakeJobTimeoutMs);
  await trackPage.deleteAfterCancel();

  await page.waitForURL(/\/tracks\/?$/);
  await expect(page.getByTestId('tracks-empty').or(page.getByTestId('track-link').first())).toBeVisible();
  await expect(trackList.row(trackId)).toHaveCount(0);
  expect((await api.listTracks()).map((t) => t.id)).not.toContain(trackId);
});

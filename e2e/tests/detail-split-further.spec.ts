import {
  expectFakeOutcomesShown,
  fakeStems,
  importWithLivePhase,
  msFromStandardToDetail,
  splitParents,
  uniqueBand,
} from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';

// Feature 002 "split further" (`@detail`, T076; US4, FR-009, FR-010, SC-007, quickstart D8/D9).
// New imports always get an automatic phase, so a track without one is made by cancelling it.
// ui-test-ids 1.1.0 shows `detail-split-further` for status `none` and `detail-retry` for
// `cancelled`; the spec starts the phase with whichever of the two the page offers.
const sc007LimitMs = 5_000;

test.beforeEach(() => {
  test.setTimeout(2 * fakeJobTimeoutMs + 2 * detailPhaseTimeoutMs);
});

test('a track without a detail phase can be split further and then offers expand', { tag: '@detail' }, async ({
  api,
  trackPage,
  detail,
  mixer,
}, testInfo) => {
  const track = await importWithLivePhase(api, `split-${testInfo.project.name}`);
  expect((await api.cancelDetail(track.id)).detail.status).toBe('cancelled');

  await trackPage.goto(track.id);
  await detail.waitForStatus('cancelled', 10_000);
  await detail.splitFurther.or(detail.retry).first().click();
  await detail.waitForStatus(['queued', 'running', 'completed'], 10_000);
  await detail.waitForStatus('completed', detailPhaseTimeoutMs);
  await expectFakeOutcomesShown(detail);

  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  for (const parent of splitParents) {
    await expect(mixer.expand(parent)).toBeVisible();
  }
});

test('a re-imported source completes its detail phase from the worker cache within 5 s (SC-007)', {
  tag: '@detail',
}, async ({ api }, testInfo) => {
  const salt = `cache-${testInfo.project.name}`;
  const first = await importWithLivePhase(api, salt);
  const done = await api.waitForDetail(first.id, ['completed', 'failed'], detailPhaseTimeoutMs);
  expect(done.detail.status, done.detail.error ?? '').toBe('completed');
  expect(await api.deleteTrack(first.id)).toBe(204);

  const again = await api.uploadFile(uniqueBand(salt));
  const elapsedMs = await msFromStandardToDetail(api, again.id);
  test.info().annotations.push({ type: 'sc-007 ms', description: String(elapsedMs) });
  expect(elapsedMs).toBeLessThanOrEqual(sc007LimitMs);
});

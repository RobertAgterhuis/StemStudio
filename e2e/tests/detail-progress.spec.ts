import {
  activeStatuses,
  attemptDelete,
  expectStillActive,
  importWithLivePhase,
  uniqueBand,
} from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';

// Feature 002 "progress and cancel" (`@detail`, T071): cancel + delete (FR-016, quickstart D11)
// and standard-before-detail priority (FR-011, SC-006, quickstart D7) on the fake stack with
// STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5. The worker restart lives in detail-worker-restart.spec.ts.

test.beforeEach(() => {
  test.setTimeout(2 * fakeJobTimeoutMs + 2 * detailPhaseTimeoutMs);
});

test('an active detail phase blocks delete; after cancel the track can be deleted', { tag: '@detail' }, async ({
  api,
  trackPage,
  detail,
  page,
}, testInfo) => {
  const track = await importWithLivePhase(api, `cancel-${testInfo.project.name}`);
  await trackPage.goto(track.id);
  await detail.waitForStatus(activeStatuses, 10_000);
  await expect(detail.cancel).toBeVisible();

  await test.step('delete is refused with a reason while the phase is active', async () => {
    await attemptDelete(trackPage, detail);
    await expect(detail.deleteBlocked).toBeVisible();
    await expect(detail.deleteBlocked).not.toBeEmpty();
    expectStillActive(await api.getTrack(track.id), 'delete refused');
    expect(await api.deleteTrack(track.id), 'DELETE while the detail phase is active').toBe(409);
  });

  await test.step('cancel ends the phase; the standard stems are untouched', async () => {
    await detail.cancel.click();
    await detail.waitForStatus('cancelled', 15_000);
    await expect(detail.retry).toBeVisible();
    await expect(detail.cancel).toBeHidden();
    const cancelled = await api.getTrack(track.id);
    expect(cancelled.status).toBe('completed');
    expect(cancelled.stems).toHaveLength(6);
    expect(cancelled.detail.parents).toEqual([]);
  });

  await test.step('the track can be deleted now', async () => {
    await trackPage.deleteButton.click();
    await trackPage.deleteConfirm.click();
    await page.waitForURL(/\/tracks\/?$/);
    expect((await api.listTracks()).map((t) => t.id)).not.toContain(track.id);
  });
});

test("a new import is separated while another track's detail phase is still running", { tag: '@detail' }, async ({
  api,
}, testInfo) => {
  const project = testInfo.project.name;
  const first = await importWithLivePhase(api, `priority-a-${project}`, ['running']);
  const started = Date.now();
  const second = await api.uploadFile(uniqueBand(`priority-b-${project}`));

  const separated = await api.waitForTerminal(second.id, fakeJobTimeoutMs);
  const elapsedMs = Date.now() - started;
  test.info().annotations.push({ type: 'standard while detail runs (ms)', description: String(elapsedMs) });
  expect(separated.status, separated.error ?? '').toBe('completed');
  expectStillActive(await api.getTrack(first.id), 'priority');

  const done = await api.waitForDetail(first.id, ['completed', 'failed'], detailPhaseTimeoutMs);
  expect(done.detail.status, done.detail.error ?? '').toBe('completed');
});

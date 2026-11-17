import { fakeStems, importWithLivePhase } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs, runId } from '../support/env';
import { archiveOf, expectProblem } from '../support/library-care-api';
import { mixWithDetail, ownDetailTrack } from '../support/library-care-cleanup';
import { deleteArchives, expect, libraryJobTimeoutMs, requireThrottledJobs, seedLibrary, test } from '../support/library-care';
import { trackStorage } from '../support/library-care-storage';

// Feature 013 US4 (T061; FR-022..FR-025, SC-007) on the fake stack. Only the spec's own tracks
// (unique band copies) are cleaned, so the shared detail track of the 002 specs keeps its stems.

const twoPhases = fakeJobTimeoutMs + detailPhaseTimeoutMs * 3 + 60_000;

test.describe('remove detail stems (013 US4-1, US4-2, SC-007)', { tag: ['@library-care', '@cleanup'] }, () => {
  test('preview -> confirm frees exactly the preview, notices appear, the mixer plays the parents, Split further restores', async ({ api, care, careApi, detail, mixer, page, trackPage }, testInfo) => {
    test.setTimeout(twoPhases);
    const track = await ownDetailTrack(api, `care-clean-${runId()}-${testInfo.project.name}`);
    const mix = await mixWithDetail(api, track, `Detail mix ${runId()} ${testInfo.project.name}`);
    const before = trackStorage(await careApi.storage(true), track.id);
    expect(before.detailStemsBytes).toBeGreaterThan(0);

    await care.goto('storage');
    await care.byId(`cleanup-track-detail-${track.id}`).click();
    await expect(care.byId(`cleanup-plan-item-detail-stems-${track.id}`)).toContainText(/Split further/i);
    const previewBytes = Number(await care.byId('cleanup-plan-total').getAttribute('data-bytes'));
    expect(previewBytes).toBe(before.detailStemsBytes);
    expect(await care.confirmCleanup()).toBe(previewBytes);

    const after = trackStorage(await careApi.storage(true), track.id);
    expect([after.detailStemsBytes, after.detailFilesRemoved, after.stemsBytes]).toEqual([0, true, before.stemsBytes]);
    await trackPage.goto(track.id);
    await expect(page.getByTestId('detail-files-removed')).toBeVisible();
    await expect(detail.splitFurther).toBeVisible();
    await mixer.goto(track.id, mix.id);
    await expect(page.getByTestId('mixer-detail-removed')).toBeVisible();
    await mixer.waitForReady(fakeStems);

    await trackPage.goto(track.id);
    await detail.splitFurther.click();
    await detail.waitForStatus('completed', detailPhaseTimeoutMs * 2);
    await expect(page.getByTestId('detail-files-removed')).toHaveCount(0);
    expect(trackStorage(await careApi.storage(true), track.id).detailStemsBytes).toBe(before.detailStemsBytes);
  });

  test('the all-tracks preview lists the tracks with bytes and how to get them back; cancel removes nothing', async ({ api, care, careApi }, testInfo) => {
    test.setTimeout(twoPhases);
    const track = await ownDetailTrack(api, `care-preview-${runId()}-${testInfo.project.name}`);
    const before = await careApi.storage(true);
    await care.goto('storage');
    const total = await care.openCleanup('detail-stems');
    expect(total).toBeGreaterThanOrEqual(trackStorage(before, track.id).detailStemsBytes);
    await expect(care.byId(`cleanup-plan-item-detail-stems-${track.id}`)).toBeVisible();
    await care.byId('cleanup-dialog-cancel').click();
    await expect(care.byId('cleanup-dialog')).toHaveCount(0);
    expect(trackStorage(await careApi.storage(true), track.id).detailStemsBytes).toBe(trackStorage(before, track.id).detailStemsBytes);
  });

  test('a plan never contains originals: only detail stems, worker cache and leftovers', async ({ api, assetsApi, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + 60_000);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const plan = await careApi.preview([{ kind: 'detailStems' }, { kind: 'workerCache' }, { kind: 'leftovers' }]);
    expect(plan.items.every((item) => ['detailStems', 'workerCache', 'leftovers'].includes(item.kind))).toBe(true);
    expect(plan.items.every((item) => item.howToRegenerate.length > 0)).toBe(true);
    expect(plan.totalBytes).toBe(plan.items.reduce((sum, item) => sum + item.bytes, 0));
    expect(Date.parse(plan.expiresAt) - Date.parse(plan.createdAt)).toBe(10 * 60_000);
  });
});

test.describe('worker cache and rehydration (013 US4, FR-019)', { tag: ['@library-care', '@cleanup'] }, () => {
  test('clearing a track worker cache, then Split further rehydrates and completes', async ({ api, careApi, detail, trackPage }, testInfo) => {
    test.setTimeout(twoPhases);
    const track = await importWithLivePhase(api, `care-cache-${runId()}-${testInfo.project.name}`);
    await api.stopDetail(track.id, detailPhaseTimeoutMs);
    const plan = await careApi.preview([{ kind: 'workerCache', trackIds: [track.id] }]);
    expect(plan.items.some((item) => item.kind === 'workerCache' && item.trackId === track.id)).toBe(true);
    const result = await careApi.execute(plan.planId);
    expect(result.freedBytes).toBe(plan.totalBytes);
    expect(trackStorage(await careApi.storage(true), track.id).workerCacheBytes).toBe(0);
    await trackPage.goto(track.id);
    await detail.splitFurther.or(detail.retry).first().click();
    await detail.waitForStatus('completed', detailPhaseTimeoutMs * 2);
    expect((await api.getTrack(track.id)).detail.status).toBe('completed');
  });
});

test.describe('stale, expired and refused plans (013 US4-3..US4-5)', { tag: ['@library-care', '@cleanup'] }, () => {
  test('a plan whose items changed is refused as stale; Preview again shows the new state', async ({ api, care, careApi }, testInfo) => {
    test.setTimeout(twoPhases);
    const track = await ownDetailTrack(api, `care-stale-${runId()}-${testInfo.project.name}`);
    await care.goto('storage');
    await care.byId(`cleanup-track-detail-${track.id}`).click();
    await expect(care.byId('cleanup-plan-total')).toHaveAttribute('data-bytes', /^[1-9]\d*$/);
    const other = await careApi.preview([{ kind: 'detailStems', trackIds: [track.id] }]);
    await careApi.execute(other.planId);
    await care.byId('cleanup-confirm').click();
    await expect(care.byId('cleanup-error')).toHaveAttribute('data-code', 'plan-stale');
    await care.byId('cleanup-repreview').click();
    await expect(care.byId(`cleanup-plan-item-detail-stems-${track.id}`)).toHaveCount(0);
  });

  test('an unknown plan is refused as expired', async ({ careApi }) => {
    const problem = await expectProblem(await careApi.executeResponse('00000000-0000-4000-8000-000000000013'), 409, 'plan-expired');
    expect(problem.status).toBe(409);
  });

  test('a track whose detail phase is running is skipped with the reason', async ({ api, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs * 2);
    const busy = await importWithLivePhase(api, `care-busy-${runId()}-${testInfo.project.name}`);
    const plan = await careApi.preview([{ kind: 'detailStems', trackIds: [busy.id] }]);
    expect(plan.skipped).toContainEqual(expect.objectContaining({ trackId: busy.id, reason: 'detail-active' }));
    await api.stopDetail(busy.id, detailPhaseTimeoutMs);
  });

  test('during a backup the clean-up buttons are disabled with the reason', async ({ api, assetsApi, care, careApi }, testInfo) => {
    requireThrottledJobs();
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const job = await careApi.createBackup();
    await careApi.waitForJobStatus(job.jobId, 'running', 30_000);
    await care.goto('storage');
    await expect(care.byId('cleanup-detail-stems')).toBeDisabled();
    await expect(care.byId('cleanup-leftovers')).toBeDisabled();
    const done = await careApi.waitForJob(job.jobId, libraryJobTimeoutMs);
    await deleteArchives(careApi, [archiveOf(done)]);
    await expect(care.byId('cleanup-detail-stems')).toBeEnabled({ timeout: 10_000 });
  });
});

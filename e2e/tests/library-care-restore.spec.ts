import { openArchive } from '../support/backup-archive';
import { placeFixtureArchive, removeFromBackupDir, requireBackupFolderAccess } from '../support/backup-folder';
import { requireCompose, resetWorkerCache } from '../support/compose';
import { fakeStems, importWithLivePhase } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs, runId } from '../support/env';
import { mixSettings } from '../support/practice';
import { expectProblem } from '../support/library-care-api';
import {
  backupNow,
  deleteArchives,
  expect,
  libraryJobTimeoutMs,
  restoreNow,
  seedLibrary,
  test,
  undoAndDiscard,
} from '../support/library-care';
import {
  expectNoPreviousLibrary,
  hostileArchive,
  libraryFingerprint,
  notRestorableArchives,
  refusalCode,
  refusedArchives,
  replaceOptions,
  requireEmptyLibrary,
  restoreReportOf,
  stateArchives,
} from '../support/library-care-restore';

// Feature 013 US2 (T053; FR-010..FR-019, SC-001, SC-004, SC-008, SC-009) on the fake stack.
// Every replace restore ends with "Undo restore" + "Discard" so the library is left as found
// (TEST-STRATEGY 19.4); fixture archives come from tests/fixtures/backup (qa T012).

const written = new Set<string>();

test.beforeEach(async ({ careApi }) => {
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  await undoAndDiscard(careApi);
});

test.afterEach(async ({ careApi }) => {
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  await undoAndDiscard(careApi);
  await deleteArchives(careApi, written);
  for (const name of written) removeFromBackupDir(name);
  written.clear();
});

test.describe('replace restore (013 US2-2)', { tag: ['@library-care', '@restore'] }, () => {
  test('backup, delete a track and a mix, replace: both are back, stems play, the mix opens; undo and discard', async ({ api, assetsApi, care, careApi, mixer }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 3 + libraryJobTimeoutMs * 4);
    const seeded = await seedLibrary(api, assetsApi, testInfo.project.name);
    const extraMix = await api.createMix(seeded.track.id, mixSettings({ name: `Restore me ${runId()} ${testInfo.project.name}` }));
    const fileName = await backupNow(careApi);
    written.add(fileName);
    expect(await api.deleteMix(extraMix.id)).toBe(204);
    expect(await api.deleteMix(seeded.mix.id)).toBe(204);
    expect(await assetsApi.delete(seeded.exported.id)).toBe(204);
    const withoutThem = await libraryFingerprint(api);

    await care.goto();
    await care.openRestore(fileName);
    await expect(care.byId('restore-space')).toHaveAttribute('data-enough', 'true');
    await expect(care.byId('restore-start')).toBeDisabled();
    await care.confirmReplace();
    await care.waitForJob('restore', 'completed', libraryJobTimeoutMs);
    await expect(care.byId('library-job-report')).toBeVisible();
    await expect(care.byId('previous-library')).toBeVisible();
    expect(await api.mixStatus(extraMix.id)).toBe(200);
    expect(await api.mixStatus(seeded.mix.id)).toBe(200);
    expect((await assetsApi.get(seeded.exported.id)).status).toBe('ready');
    await mixer.goto(seeded.track.id, extraMix.id);
    await mixer.waitForReady(fakeStems);

    await care.goto();
    await care.undoRestore();
    await care.waitForJob('undoRestore', 'completed', libraryJobTimeoutMs);
    expect(await libraryFingerprint(api)).toBe(withoutThem);
    await care.discardPrevious();
    await expectNoPreviousLibrary(careApi);
  });

  test('a replace restore without the typed confirmation is refused', async ({ api, assetsApi, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const fileName = await backupNow(careApi);
    written.add(fileName);
    const problem = await expectProblem(await careApi.restoreResponse(fileName, { mode: 'replace', confirmation: 'replace' }), 422, 'confirmation-required');
    expect(problem.status).toBe(422);
    await expectNoPreviousLibrary(careApi);
  });

  test('the round trip reproduces every included file bit for bit (SC-001)', async ({ api, assetsApi, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs * 4);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const first = await backupNow(careApi);
    written.add(first);
    const job = await restoreNow(careApi, first, replaceOptions());
    expect(job.status, job.error?.message ?? '').toBe('completed');
    const second = await backupNow(careApi);
    written.add(second);
    const before = openArchive(await careApi.archiveBytes(first)).manifest;
    const after = openArchive(await careApi.archiveBytes(second)).manifest;
    const files = (manifest: typeof before): string[] => manifest.entries.filter((entry) => entry.path.startsWith('files/')).map((entry) => `${entry.path} ${entry.sha256}`).sort();
    expect(files(after)).toEqual(files(before));
    expect(after.counts).toEqual(before.counts);
  });
});

test.describe('hostile archives (013 US2-5, SC-004)', { tag: ['@library-care', '@restore'] }, () => {
  for (const archive of refusedArchives) {
    test(`${archive.vector} ${archive.file} is refused by verify and restore with ${archive.outcome}; the library is unchanged`, async ({ api, careApi }) => {
      requireBackupFolderAccess();
      test.setTimeout(libraryJobTimeoutMs * 2);
      placeFixtureArchive(archive.file);
      written.add(archive.file);
      const before = await libraryFingerprint(api);
      expect(await refusalCode(careApi, await careApi.verifyResponse(archive.file)), archive.summary).toBe(archive.outcome);
      expect(await refusalCode(careApi, await careApi.restoreResponse(archive.file, replaceOptions())), archive.summary).toBe(archive.outcome);
      expect(await libraryFingerprint(api)).toBe(before);
      await expectNoPreviousLibrary(careApi);
    });
  }

  test('H16: a restore needing more space than the library volume has is refused before extraction', async ({ api, careApi }) => {
    requireBackupFolderAccess();
    test.setTimeout(libraryJobTimeoutMs * 2);
    const archive = hostileArchive('H16');
    placeFixtureArchive(archive.file);
    written.add(archive.file);
    const before = await libraryFingerprint(api);
    expect(await refusalCode(careApi, await careApi.restoreResponse(archive.file, replaceOptions()))).toBe('insufficient-disk-space');
    expect(await libraryFingerprint(api)).toBe(before);
    await expectNoPreviousLibrary(careApi);
  });

  test('H15: a profile with a wrong consent proof is not restored, the rest is (SC-008)', async ({ api, careApi }) => {
    requireBackupFolderAccess();
    test.setTimeout(libraryJobTimeoutMs * 3);
    const archive = hostileArchive('H15');
    expect(archive.outcome).toBe('profile-dropped');
    placeFixtureArchive(archive.file);
    written.add(archive.file);
    const job = await restoreNow(careApi, archive.file, replaceOptions());
    expect(job.status).toBe('completed');
    expect(restoreReportOf(job).skipped).toContainEqual(expect.objectContaining({ category: 'voiceProfiles', reason: 'consent-invalid' }));
    expect((await api.listTracks()).some((track) => track.title === 'Library care fixture')).toBe(true);
  });
});

test.describe('archive states (013 US2-4, FR-010, SC-009)', { tag: ['@library-care', '@restore'] }, () => {
  test('older, newer, other-format and unrecognised archives are listed with their state', async ({ care, careApi }) => {
    requireBackupFolderAccess();
    for (const archive of stateArchives) {
      placeFixtureArchive(archive.file);
      written.add(archive.file);
    }
    await care.goto();
    for (const archive of stateArchives) {
      await expect(care.row(archive.file), archive.summary).toHaveAttribute('data-state', archive.state);
    }
    for (const archive of notRestorableArchives) {
      await expect(care.byId(`backup-restore-${archive.file}`)).toBeDisabled();
      await expect(care.byId(`backup-row-status-${archive.file}`)).not.toBeEmpty();
    }
    const listed = (await careApi.listBackups()).archives.filter((item) => written.has(item.fileName));
    expect(listed.filter((item) => !item.recognised).map((item) => item.fileName).sort()).toEqual(['not-a-backup.zip', 'not-a-zip.zip']);
  });

  test('an archive of the 010 schema is migrated in staging and restored', async ({ api, careApi }) => {
    requireBackupFolderAccess();
    test.setTimeout(libraryJobTimeoutMs * 3);
    placeFixtureArchive('schema-010.zip');
    written.add('schema-010.zip');
    const job = await restoreNow(careApi, 'schema-010.zip', replaceOptions());
    expect(job.status).toBe('completed');
    expect(restoreReportOf(job).migratedFrom).toBe('20261115200748_AddLyricsAndVoices');
    expect((await api.listTracks()).map((track) => track.title)).toContain('Library care fixture');
  });

  test('the details panel shows the consent proof of an archived voice profile', async ({ care }) => {
    requireBackupFolderAccess();
    placeFixtureArchive('valid-base.zip');
    written.add('valid-base.zip');
    await care.goto();
    await care.byId('backup-details-valid-base.zip').click();
    const proof = care.byId('backup-details-voice-7d3c2b1a-0f9e-4d8c-b7a6-5e4d3c2b1a09');
    await expect(proof).toHaveAttribute('data-valid', 'true');
    await expect(proof).toContainText('Alex Example');
    await expect(proof).toContainText('consent.v1');
  });
});

test.describe('restore blockers and rehydration (013 US2-3, US2-7)', { tag: ['@library-care', '@restore'] }, () => {
  test('a restore is refused while a detail phase runs, with the active work listed', async ({ api, care, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + detailPhaseTimeoutMs + libraryJobTimeoutMs);
    const fileName = await backupNow(careApi);
    written.add(fileName);
    const busy = await importWithLivePhase(api, `care-blocked-${runId()}-${testInfo.project.name}`);
    const problem = await expectProblem(await careApi.restoreResponse(fileName, replaceOptions()), 409, 'jobs-active');
    expect(problem.activeWork?.some((work) => work.kind === 'detail' || work.kind === 'import')).toBe(true);
    await care.goto();
    await care.openRestore(fileName);
    await expect(care.byId('restore-blockers')).toBeVisible();
    await expect(care.byId('restore-start')).toBeDisabled();
    await api.stopDetail(busy.id, detailPhaseTimeoutMs);
  });

  test('"Split further" after a worker cache reset rehydrates the cache and completes', { tag: '@compose' }, async ({ api, detail, trackPage }, testInfo) => {
    requireCompose();
    test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs * 3);
    const track = await importWithLivePhase(api, `care-rehydrate-${runId()}-${testInfo.project.name}`);
    expect((await api.cancelDetail(track.id)).detail.status).toBe('cancelled');
    await api.waitForDetail(track.id, ['cancelled'], detailPhaseTimeoutMs);
    resetWorkerCache();
    await trackPage.goto(track.id);
    await detail.splitFurther.or(detail.retry).first().click();
    await detail.waitForStatus('completed', detailPhaseTimeoutMs * 2);
    expect((await api.getTrack(track.id)).detail.status).toBe('completed');
  });
});

test.describe('empty library (013 US2-1)', { tag: ['@library-care', '@restore', '@fresh-library'] }, () => {
  test('an empty library is restored directly, without a mode or a typed confirmation', async ({ care, careApi }) => {
    requireBackupFolderAccess();
    await requireEmptyLibrary(careApi);
    test.setTimeout(libraryJobTimeoutMs * 4);
    const emptyArchive = await backupNow(careApi);
    written.add(emptyArchive);
    placeFixtureArchive('valid-base.zip');
    written.add('valid-base.zip');
    await care.goto();
    await care.openRestore('valid-base.zip');
    await expect(care.byId('restore-mode-replace')).toHaveCount(0);
    await expect(care.byId('restore-mode-merge')).toHaveCount(0);
    await care.byId('restore-start').click();
    await care.waitForJob('restore', 'completed', libraryJobTimeoutMs);
    const report = (await careApi.listBackups()).archives.find((item) => item.fileName === 'valid-base.zip');
    expect(report?.counts?.tracks).toBe(1);
    expect(await careApi.previous()).toBeNull();
    // Back to the empty library: replace with the empty archive, discard the fixture library.
    expect((await restoreNow(careApi, emptyArchive, replaceOptions())).status).toBe('completed');
    expect(await careApi.discard()).toBe(204);
    expect((await careApi.status()).libraryEmpty).toBe(true);
  });
});

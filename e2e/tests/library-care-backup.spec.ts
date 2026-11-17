import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { archiveProblems, openArchive, type BackupManifest } from '../support/backup-archive';
import { backupDirFiles } from '../support/backup-folder';
import { axeViolations } from '../support/axe';
import { ensureDetailCompleted, fakeStems, splitParents } from '../support/detail';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { fixturePath } from '../support/fixtures';
import { archiveOf, expectProblem } from '../support/library-care-api';
import { backupNow, deleteArchives, expect, libraryJobTimeoutMs, requireThrottledJobs, seedLibrary, test } from '../support/library-care';
import { expectNoIdWithPrefix, expectNotRendered, expectRendered } from '../support/levels';
import { expertState, levelState } from '../support/preferences';
import { uniqueWavCopy } from '../support/unique-audio';

// Feature 013 US1 (T041; FR-001..FR-009, FR-031, FR-032, SC-003): backups on the fake stack.
// Archives are read back with support/backup-archive.ts (manifest schema 1.0.0, every SHA-256).
// Archives written here are deleted after each case; no case changes the library.

/** Archives written by the current case (deleted in afterEach). */
const written = new Set<string>();

test.afterEach(async ({ careApi }) => {
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  await deleteArchives(careApi, written);
  written.clear();
});

/** Every entry outside the four allowed prefixes (backup-archive 1.0.0 section 2, FR-004). */
function foreignEntries(manifest: BackupManifest): string[] {
  return manifest.entries.map((entry) => entry.path).filter((name) => !/^(db\/stemstudio\.db|files\/(tracks|assets|generations|voices|uploads)\/)/.test(name));
}

/**
 * `Tracks.DetailPhase` JSON of one track inside the archived database (read-only copy).
 * `node:sqlite` is imported on use, so its experimental warning appears only in this case.
 */
async function archivedDetailPhase(archiveBytes: Buffer, trackId: string): Promise<string> {
  const { DatabaseSync } = await import('node:sqlite');
  const folder = mkdtempSync(path.join(tmpdir(), 'stemstudio-archive-'));
  const file = path.join(folder, 'stemstudio.db');
  writeFileSync(file, openArchive(archiveBytes).entry('db/stemstudio.db'));
  const database = new DatabaseSync(file, { readOnly: true });
  try {
    const row = database.prepare('SELECT "DetailPhase" AS phase FROM "Tracks" WHERE upper("Id") = upper(?)').get(trackId) as { phase: string | null } | undefined;
    return row?.phase ?? '';
  } finally {
    database.close();
  }
}

test.describe('backup (013 US1)', { tag: ['@library-care', '@backup'] }, () => {
  test('a complete backup writes a verified, schema-valid archive with every stem, the export and the database', async ({ api, assetsApi, care, careApi, page }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs);
    const seeded = await seedLibrary(api, assetsApi, testInfo.project.name);
    await care.goto();
    await expect(care.byId('backup-folder-state')).toHaveAttribute('data-available', 'true');
    await expect(care.byId('backup-folder-state')).toHaveAttribute('data-writable', 'true');
    await expect(care.byId('backup-folder-error')).toHaveCount(0);

    await care.openBackupDialog();
    await care.chooseProfile('complete');
    await expect(care.byId('backup-estimate')).toHaveAttribute('data-enough', 'true');
    await care.startBackup();
    await care.waitForJob('backup', 'completed', libraryJobTimeoutMs);
    await expect(care.byId('library-job-progress')).toHaveAttribute('aria-valuenow', '100');
    await expect(care.byId('library-job-report')).toBeVisible();
    await expect(care.root).toHaveAttribute('data-gate', 'open');

    const newest = (await careApi.listBackups()).archives[0];
    expect(newest?.fileName).toMatch(/^stemstudio-backup-\d{8}-\d{6}(-\d+)?\.zip$/);
    const fileName = newest?.fileName ?? '';
    written.add(fileName);
    await expect(care.row(fileName)).toHaveAttribute('data-state', 'restorable');
    expect(backupDirFiles().filter((name) => name.endsWith('.partial'))).toEqual([]);

    const bytes = await care.download(fileName);
    expect(archiveProblems(bytes)).toEqual([]);
    const { manifest } = openArchive(bytes);
    const paths = manifest.entries.map((entry) => entry.path);
    for (const stem of fakeStems) expect(paths).toContain(`files/tracks/${seeded.track.id}/stems/${stem}.flac`);
    expect(paths.some((name) => name.startsWith(`files/assets/${seeded.exported.id}/`))).toBe(true);
    expect(paths[0]).toBe('db/stemstudio.db');
    expect(foreignEntries(manifest)).toEqual([]);
    expect(manifest.contents.included).toEqual(expect.arrayContaining(['database', 'stems', 'artwork', 'pendingUploads', 'detailStems', 'exports', 'scores', 'generations', 'voiceProfiles']));
    expect(manifest.counts.tracks).toBeGreaterThanOrEqual(1);
    expect(manifest.counts.mixes).toBeGreaterThanOrEqual(1);
    await expect(page).toHaveURL(/\/care/);
  });

  test('the archive downloads with Content-Disposition, ETag and resumable byte ranges', async ({ api, assetsApi, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const fileName = await backupNow(careApi);
    written.add(fileName);
    const whole = await careApi.download(fileName);
    expect(whole.headers()['content-disposition']).toMatch(new RegExp(`attachment;.*${fileName.replace(/\./g, '\\.')}`));
    expect(whole.headers().etag).toBeTruthy();
    expect(whole.headers()['accept-ranges']).toBe('bytes');
    const body = await whole.body();
    const part = await careApi.download(fileName, 'bytes=100-1123');
    expect(part.status()).toBe(206);
    expect(Buffer.compare(await part.body(), body.subarray(100, 1124))).toBe(0);
    expect((await careApi.download(fileName, `bytes=${String(body.length + 10)}-`)).status()).toBe(416);
  });

});

test.describe('compact backup (013 US1-5)', { tag: ['@library-care', '@backup'] }, () => {
  test('a compact backup leaves out the detail stems and marks them removed in the archived database', async ({ api, care, careApi }) => {
    test.setTimeout(fakeJobTimeoutMs + libraryJobTimeoutMs * 2);
    const track = await ensureDetailCompleted(api);
    await care.goto();
    await care.openBackupDialog();
    await care.chooseProfile('compact');
    await care.startBackup();
    await care.waitForJob('backup', 'completed', libraryJobTimeoutMs);
    const fileName = (await careApi.listBackups()).archives[0]?.fileName ?? '';
    written.add(fileName);

    const bytes = await careApi.archiveBytes(fileName);
    expect(archiveProblems(bytes)).toEqual([]);
    const { manifest } = openArchive(bytes);
    expect(manifest.entries.filter((entry) => entry.path.includes('/detail/'))).toEqual([]);
    expect(manifest.entries.some((entry) => entry.path === `files/tracks/${track.id}/stems/${splitParents[0] ?? 'vocals'}.flac`)).toBe(true);
    expect(manifest.contents.excluded).toContainEqual({ category: 'detailStems', reason: 'option' });
    expect(await archivedDetailPhase(bytes, track.id)).toMatch(/"FilesRemovedAt":"\d{4}-\d{2}-\d{2}T/);
  });

});

test.describe('running backup (013 US1-2, US1-3; throttled stack)', { tag: ['@library-care', '@backup'] }, () => {
  test('cancel ends the running backup within 5 seconds and leaves no partial file', async ({ api, assetsApi, care, careApi }, testInfo) => {
    requireThrottledJobs();
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const before = new Set((await careApi.listBackups()).archives.map((archive) => archive.fileName));
    await care.goto();
    await care.openBackupDialog();
    await care.startBackup();
    await care.waitForJob('backup', 'running', 30_000);
    await expect(care.byId('library-job-progress')).toHaveAttribute('aria-valuetext', /\d+ % - .+/);
    const started = Date.now();
    await care.byId('library-job-cancel').click();
    await care.waitForJob('backup', 'cancelled', 5_000);
    expect(Date.now() - started).toBeLessThanOrEqual(5_000);
    await expect(care.root).toHaveAttribute('data-gate', 'open');
    const after = (await careApi.listBackups()).archives.map((archive) => archive.fileName);
    expect(after.filter((name) => !before.has(name))).toEqual([]);
    expect(backupDirFiles().filter((name) => name.endsWith('.partial'))).toEqual([]);
  });

});

test.describe('write gate (013 FR-002)', { tag: ['@library-care', '@backup'] }, () => {
  test('while a backup runs, deletes and clean-ups are refused with backup-running and an export still runs', async ({ api, assetsApi, careApi, trackPage, page }, testInfo) => {
    requireThrottledJobs();
    test.setTimeout(fakeJobTimeoutMs * 3 + libraryJobTimeoutMs);
    const seeded = await seedLibrary(api, assetsApi, testInfo.project.name);
    const victim = await api.ensureFileCompleted(uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `care-gate-${runId()}-${testInfo.project.name}`), fakeJobTimeoutMs);
    const job = await careApi.createBackup();
    await careApi.waitForJobStatus(job.jobId, 'running', 30_000);
    expect((await careApi.status()).gate).toBe('backupRunning');

    const refused = await expectProblem(await api.deleteTrackResponse(victim.id), 409, 'backup-running');
    expect(refused.detail ?? refused.title).toMatch(/backup is running/i);
    await expectProblem(await careApi.previewResponse([{ kind: 'detailStems' }]), 409, 'backup-running');
    await trackPage.goto(victim.id);
    await trackPage.deleteButton.click();
    await trackPage.deleteConfirm.click();
    await expect(page.locator('body')).toContainText(/A backup is running; try again when it has finished/i);

    const exportJob = await assetsApi.createExport({ trackId: seeded.track.id, mode: 'raw', channels: ['bass'], format: { format: 'wav', bitDepth: 16 }, name: `During backup ${runId()} ${testInfo.project.name}` });
    const done = await careApi.waitForJob(job.jobId, libraryJobTimeoutMs);
    expect(done.status).toBe('completed');
    const fileName = archiveOf(done);
    written.add(fileName);
    expect((await assetsApi.waitForReady(exportJob.id, fakeJobTimeoutMs)).status).toBe('ready');
    const { manifest } = openArchive(await careApi.archiveBytes(fileName));
    expect(manifest.entries.some((entry) => entry.path.startsWith(`files/assets/${exportJob.id}/`))).toBe(false);
    expect((await api.getTrack(victim.id)).status).toBe('completed');
  });

  test('a second backup while one runs is refused with library-job-running', async ({ api, assetsApi, careApi }, testInfo) => {
    requireThrottledJobs();
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const job = await careApi.createBackup();
    await expectProblem(await careApi.createBackupResponse(), 409, 'library-job-running');
    const done = await careApi.waitForJob(job.jobId, libraryJobTimeoutMs);
    expect(done.status).toBe('completed');
    written.add(archiveOf(done));
  });

});

test.describe('library care accessibility (013 FR-032)', { tag: ['@library-care', '@backup'] }, () => {
  test('the library care page has no axe violations on either tab', async ({ care, page }) => {
    await care.goto('backups');
    expect(await axeViolations(page)).toEqual([]);
    await care.selectTab('storage');
    expect(await axeViolations(page)).toEqual([]);
  });
});

test.describe('backup dialog levels (013 FR-031)', { tag: ['@library-care', '@backup'] }, () => {
  test.describe('Beginner', () => {
    test.use({ storageState: levelState('beginner') });

    test('shows the two profiles and the estimate, hides the category options and rows', async ({ care, page }) => {
      await care.goto();
      await care.openBackupDialog();
      await expectRendered(page, ['backup-profile-complete', 'backup-profile-compact', 'backup-estimate', 'backup-start', 'backup-dialog-cancel']);
      await expectNotRendered(page, ['backup-profile-custom']);
      await expectNoIdWithPrefix(page, 'backup-option-');
      await expectNoIdWithPrefix(page, 'backup-estimate-');
      await care.byId('backup-dialog-cancel').click();
      await expect(care.byId('backup-dialog')).toHaveCount(0);
    });
  });

  test.describe('Advanced', () => {
    test('shows every option and estimate row; changing an option selects Custom', async ({ care }) => {
      await care.goto();
      await care.openBackupDialog();
      for (const option of ['detail-stems', 'exports', 'scores', 'generations', 'recordings', 'voice-profiles'] as const) {
        await expect(care.byId(`backup-option-${option}`)).toBeChecked();
      }
      await expect(care.byId('backup-estimate-stems')).toHaveAttribute('data-included', 'true');
      await expect(care.byId('backup-estimate-recordings')).toHaveAttribute('data-bytes', /^\d+$/);
      await care.setOption('exports', false);
      await expect(care.byId('backup-profile-custom')).toBeChecked();
      await expect(care.byId('backup-estimate-exports')).toHaveAttribute('data-included', 'false');
    });
  });

  test.describe('Expert', () => {
    test.use({ storageState: expertState });

    test('the details panel shows the manifest view', async ({ api, assetsApi, care, careApi }, testInfo) => {
      test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs);
      await seedLibrary(api, assetsApi, testInfo.project.name);
      const fileName = await backupNow(careApi);
      written.add(fileName);
      await care.goto();
      await care.byId(`backup-details-${fileName}`).click();
      await expect(care.byId('backup-details')).toBeVisible();
      await expect(care.byId('backup-details-contents')).toBeVisible();
      await expect(care.byId('backup-details-manifest')).toContainText('1.0.0');
      await expect(care.byId(`backup-verify-${fileName}`)).toBeVisible();
    });
  });
});

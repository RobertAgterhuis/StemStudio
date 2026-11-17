import { GenerationApi, type StyleDto, type StyleInputDto } from '../support/generation-api';
import { apiBaseUrl, fakeJobTimeoutMs, runId } from '../support/env';
import { fixturePath } from '../support/fixtures';
import { okJson } from '../support/http';
import { mixSettings } from '../support/practice';
import { backupNow, deleteArchives, expect, libraryJobTimeoutMs, restoreNow, test, waitForNoActiveWork } from '../support/library-care';
import { restoreReportOf } from '../support/library-care-restore';
import { levelState } from '../support/preferences';
import { uniqueWavCopy } from '../support/unique-audio';

// Feature 013 US5 (T065, P3; FR-012 merge rules) on the fake stack, at Advanced (merge is not
// offered at Beginner). Library B is the library at the time of the backup (tracks 2 and 3, style
// S); library A is made from it afterwards: track 3 and style S are deleted, a new style with S's
// name and a new track 1 are added, and track 2's mix is renamed. Merging B into A must add track
// 3 with its mix, keep A's track 2 and mix name untouched, and add S as "S (restored)".

function style(name: string): StyleInputDto {
  return { name, genreFamily: 'jazz', tempoFeel: 'keep', instrumentation: ['piano'], moods: ['calm'], vocalHandling: 'keep', defaultStrength: 'balanced', description: 'Library care merge fixture' };
}

const written = new Set<string>();

test.afterEach(async ({ careApi }) => {
  await careApi.waitForGateOpen(libraryJobTimeoutMs);
  await deleteArchives(careApi, written);
  written.clear();
});

test.describe('merge restore (013 US5)', { tag: ['@library-care', '@merge'] }, () => {
  test('adds what is missing, keeps what exists, renames a clashing style to "(restored)"', async ({ api, care, careApi, playwright }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 4 + libraryJobTimeoutMs * 2);
    const salt = `${runId()}-${testInfo.project.name}`;
    const styles = new GenerationApi(await playwright.request.newContext({ baseURL: apiBaseUrl }));
    const styleName = `Merge style ${salt}`;
    const track2 = await api.ensureFileCompleted(uniqueWavCopy(fixturePath('click_90bpm_3-4_12s.wav'), `merge-2-${salt}`), fakeJobTimeoutMs);
    const track3 = await api.ensureFileCompleted(uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `merge-3-${salt}`), fakeJobTimeoutMs);
    const mix2 = await api.createMix(track2.id, mixSettings({ name: `Track two ${salt}` }));
    const mix3 = await api.createMix(track3.id, mixSettings({ name: `Track three ${salt}` }));
    const styleB = await okJson<StyleDto>(await styles.createStyle(style(styleName)));
    const archive = await backupNow(careApi);
    written.add(archive);

    // New imports start an automatic detail phase; a track is deleted only once it is idle (409 otherwise).
    await waitForNoActiveWork(careApi);
    expect(await api.deleteMix(mix3.id)).toBe(204);
    expect(await api.deleteTrack(track3.id)).toBe(204);
    expect(await styles.deleteStyle(styleB.id)).toBe(204);
    const styleA = await okJson<StyleDto>(await styles.createStyle(style(styleName)));
    const track1 = await api.ensureFileCompleted(uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `merge-1-${salt}`), fakeJobTimeoutMs);
    const renamed = { ...(await api.getMix(mix2.id)).settings, name: `Track two in A ${salt}` };
    await api.updateMix(mix2.id, renamed);
    await waitForNoActiveWork(careApi);

    await care.goto();
    await care.openRestore(archive);
    await care.confirmMerge();
    await care.waitForJob('restore', 'completed', libraryJobTimeoutMs);

    expect(await api.trackStatus(track3.id)).toBe(200);
    expect(await api.mixStatus(mix3.id)).toBe(200);
    expect(await api.trackStatus(track1.id)).toBe(200);
    expect((await api.getMix(mix2.id)).settings.name).toBe(`Track two in A ${salt}`);
    const names = (await styles.listStyles({ kind: 'user' })).map((item) => item.name);
    expect(names).toEqual(expect.arrayContaining([styleName, `${styleName} (restored)`]));
    expect((await styles.listStyles({ kind: 'user' })).find((item) => item.name === styleName)?.id).toBe(styleA.id);
    expect(await careApi.previous()).toBeNull();
  });

  test('the merge report lists every skipped item with its reason', async ({ api, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + libraryJobTimeoutMs * 2);
    const salt = `${runId()}-${testInfo.project.name}`;
    const track = await api.ensureFileCompleted(uniqueWavCopy(fixturePath('click_90bpm_3-4_12s.wav'), `merge-skip-${salt}`), fakeJobTimeoutMs);
    const archive = await backupNow(careApi);
    written.add(archive);
    const job = await restoreNow(careApi, archive, { mode: 'merge', voiceProfiles: false });
    expect(job.status, job.error?.message ?? '').toBe('completed');
    const report = restoreReportOf(job);
    expect(report.skipped).toContainEqual(expect.objectContaining({ id: track.id, reason: 'same-id' }));
    expect(report.skipped.every((item) => ['same-id', 'same-source', 'dependency-skipped', 'consent-invalid', 'option', 'missing-file'].includes(item.reason))).toBe(true);
  });
});

test.describe('merge at Beginner (013 FR-031)', { tag: ['@library-care', '@merge'] }, () => {
  test.use({ storageState: levelState('beginner') });

  test('the restore dialog offers no merge below Advanced', async ({ api, care, careApi }) => {
    test.setTimeout(fakeJobTimeoutMs + libraryJobTimeoutMs);
    await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
    const archive = await backupNow(careApi);
    written.add(archive);
    await waitForNoActiveWork(careApi);
    await care.goto();
    await care.openRestore(archive);
    await expect(care.byId('restore-mode-replace')).toBeVisible();
    await expect(care.byId('restore-mode-merge')).toHaveCount(0);
  });
});

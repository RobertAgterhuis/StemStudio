import { fakeJobTimeoutMs } from '../support/env';
import {
  importAndOpenMixer,
  practiceSettings,
  readPersistedMix,
  reloadAndVerify,
  saveNewMix,
  uniqueMixName,
  writePersistedMix,
  type FlowContext,
  type PersistedMix,
} from '../support/main-flow';
import { expect, test } from '../support/test';

// The constitution's main flow on the fake-separator stack (T107; SC-001, SC-005, US4).

test('import -> separate -> mix -> save -> reload restores every setting', async ({
  page,
  api,
  importPage,
  trackPage,
  mixer,
  mixLibrary,
}, testInfo) => {
  const ctx: FlowContext = { page, api, importPage, trackPage, mixer, mixLibrary };

  const { trackId, stems } = await test.step('import and separate', () =>
    importAndOpenMixer(ctx, 'music_100bpm_20s.mp3', fakeJobTimeoutMs));

  await test.step('mix', () => mixer.apply(practiceSettings));

  const mixName = uniqueMixName('Practice', testInfo.project.name);

  const mixId = await test.step('save "Practice"', () => saveNewMix(ctx, trackId, mixName));

  const saved: PersistedMix = { trackId, mixId, mixName, stems, settings: practiceSettings };

  await test.step('reload and load', () => reloadAndVerify(ctx, saved));

  writePersistedMix(testInfo.project.name, saved);

  await test.step('save as "Practice 2"', async () => {
    const copyName = uniqueMixName('Practice 2', testInfo.project.name);
    await mixer.saveAs(copyName);
    await expect.poll(async () => (await api.findMix(trackId, copyName))?.id).toBeTruthy();
  });

  await test.step('duplicate and delete the duplicate', async () => {
    await mixLibrary.goto();
    await mixLibrary.duplicate(mixId);
    await expect.poll(async () => (await api.findMix(trackId, `${mixName} (copy)`))?.id).toBeTruthy();
    const copy = await api.findMix(trackId, `${mixName} (copy)`);
    await mixLibrary.delete(copy?.id ?? '');
    expect(await api.findMix(trackId, `${mixName} (copy)`)).toBeUndefined();
    expect((await api.getTrack(trackId)).status).toBe('completed');
  });
});

// Run by T122 after `docker compose restart api web` (npm run test:restart; G2, SC-005).
test(
  'a saved mix survives an application restart',
  { tag: '@restart' },
  async ({ page, api, importPage, trackPage, mixer, mixLibrary }, testInfo) => {
    const saved = readPersistedMix(testInfo.project.name);
    const ctx: FlowContext = { page, api, importPage, trackPage, mixer, mixLibrary };
    expect((await api.getTrack(saved.trackId)).status).toBe('completed');
    await reloadAndVerify(ctx, saved);
  },
);

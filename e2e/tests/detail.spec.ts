import type { StemName } from '../support/api';
import {
  activeStatuses,
  expectFakeOutcomesShown,
  fakeDetailOutcomes,
  outcomesOf,
  sampleProgressUntilDone,
  uniqueBand,
} from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { expect, test } from '../support/test';

// Feature 002 detail separation against the fake stack (STEMSTUDIO_SEPARATOR=fake,
// STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5; docs/testing/TEST-STRATEGY.md section 5).
const sixStems: readonly StemName[] = ['vocals', 'drums', 'bass', 'guitar', 'piano', 'other'];
const silentDb = -60;

test.describe('auto', { tag: '@detail' }, () => {
  // T057: US1 AS1-AS3, FR-001, FR-007, FR-017.
  test('a new import gets its detail phase automatically and the mixer plays meanwhile', async ({
    api,
    importPage,
    trackPage,
    detail,
    mixer,
    page,
  }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs + 2 * detailPhaseTimeoutMs);
    await importPage.goto();
    await importPage.uploadFile(uniqueBand(`auto-${testInfo.project.name}`));
    const trackId = await trackPage.waitForTrackId();
    await trackPage.waitForStatus('completed', fakeJobTimeoutMs);

    await test.step('the detail phase is shown separately while it is active', async () => {
      await expect(detail.section).toBeVisible();
      await detail.waitForStatus(activeStatuses, 10_000);
      await expect(detail.longNotice).toBeVisible();
      await expect(detail.limitationHelp).toBeVisible();
      await detail.waitForStatus('running', detailPhaseTimeoutMs);
      await expect(detail.stage).not.toBeEmpty();
    });

    await test.step('the standard stems play in the mixer during the phase', async () => {
      await trackPage.openMixer.click();
      await page.waitForURL(`**/tracks/${trackId}/mixer**`);
      await mixer.waitForReady(sixStems);
      await mixer.play();
      await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
      expect(activeStatuses).toContain((await api.getTrack(trackId)).detail.status);
    });

    await test.step('the phase completes with the outcome table of the fake models', async () => {
      await trackPage.goto(trackId);
      const progress = await sampleProgressUntilDone(detail);
      expect(progress, 'progress never decreases').toEqual([...progress].sort((a, b) => a - b));
      await detail.waitForStatus('completed', 1_000);
      await expect(detail.progress).toHaveAttribute('aria-valuenow', '100');
      await expect(detail.longNotice).toBeHidden();
      await expectFakeOutcomesShown(detail);
      expect(outcomesOf(await api.getTrack(trackId))).toEqual(fakeDetailOutcomes);
      await expect(trackPage.status).toHaveText(/completed/i);
    });
  });
});

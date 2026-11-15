import { expectFakeOutcomesShown, fakeStems, importWithLivePhase, silentDb } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { restartWorker } from '../support/stack';
import { expect, test } from '../support/test';

// Feature 002 "worker restart" (T071; US3 AS3, FR-019, quickstart D6). Opt-in: needs Docker on
// the host (`npm run test:worker-restart`, E2E_COMPOSE_FILE); excluded from `npm test`.

test('a worker restart mid-phase marks it interrupted; Retry completes and the stems stay playable', {
  tag: ['@detail', '@worker-restart'],
}, async ({ api, trackPage, detail, mixer }, testInfo) => {
  test.setTimeout(fakeJobTimeoutMs + 3 * detailPhaseTimeoutMs + 120_000);
  const track = await importWithLivePhase(api, `restart-${testInfo.project.name}`, ['running']);

  await test.step('restart the worker container while the phase runs', () => {
    restartWorker();
  });

  await test.step('the phase is failed "interrupted by restart" with Retry', async () => {
    await trackPage.goto(track.id);
    await detail.waitForStatus('failed', detailPhaseTimeoutMs);
    await expect(detail.error).toContainText(/interrupted by restart/i);
    await expect(detail.retry).toBeVisible();
    await expect(trackPage.status).toHaveText(/completed/i);
  });

  await test.step('the standard stems still play', async () => {
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await mixer.play();
    await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
  });

  await test.step('Retry runs only the detail phase to completion', async () => {
    await trackPage.goto(track.id);
    await detail.retry.click();
    await detail.waitForStatus('completed', detailPhaseTimeoutMs);
    await expectFakeOutcomesShown(detail);
    expect((await api.getTrack(track.id)).status).toBe('completed');
  });
});

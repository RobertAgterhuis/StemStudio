import { requireCompose, startService, stopService } from '../support/compose';
import { fakeJobTimeoutMs } from '../support/env';
import { expect, seedLibrary, test } from '../support/library-care';
import {
  categoryBytes,
  expectLibraryCategoriesOnDisk,
  expectModelCachesOnDisk,
  expectTrackStemsOnDisk,
  isSorted,
} from '../support/library-care-storage';
import { expectNoIdWithPrefix, expectNotRendered, expectRendered } from '../support/levels';
import { expertState, levelState } from '../support/preferences';

// Feature 013 US3 (T057; FR-020, FR-021, FR-031, SC-006): the storage overview on the fake stack.
// The byte comparisons and the stopped worker need docker compose access (`@compose`,
// E2E_COMPOSE=1, E2E_COMPOSE_PROJECT); everything else runs with `npm run test:library-care`.

test.describe('storage numbers (013 US3-1, SC-006)', { tag: ['@library-care', '@storage'] }, () => {
  test('every library category, a track row and the model caches equal the bytes on disk', { tag: '@compose' }, async ({ api, assetsApi, care, careApi }, testInfo) => {
    requireCompose();
    test.setTimeout(fakeJobTimeoutMs * 2 + 60_000);
    const seeded = await seedLibrary(api, assetsApi, testInfo.project.name);
    const overview = await careApi.storage(true);
    expectLibraryCategoriesOnDisk(overview);
    expectTrackStemsOnDisk(overview, seeded.track.id);
    expectModelCachesOnDisk(overview);
    await care.goto('storage');
    expect(await care.categoryBytes('stems')).toBe(categoryBytes(overview, 'stems'));
  });

  test('the Storage tab shows totals, classes and free space within 2 seconds', async ({ api, assetsApi, care, careApi }, testInfo) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + 60_000);
    await seedLibrary(api, assetsApi, testInfo.project.name);
    const overview = await careApi.storage(true);
    await care.goto('storage');
    await expect(care.byId('storage-category-stems')).toBeVisible({ timeout: 2_000 });
    await expect(care.byId('storage-volume-library')).toHaveAttribute('data-free-bytes', /^\d+$/, { timeout: 2_000 });
    await expect(care.byId('storage-volume-library')).toHaveAttribute('role', 'meter');
    await expect(care.byId('storage-measured-at')).not.toBeEmpty();
    for (const usage of overview.categories.filter((item) => ['stems', 'detailStems', 'exports', 'voiceProfiles'].includes(item.category))) {
      const id = usage.category.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);
      await expect(care.byId(`storage-category-${id}`), id).toHaveAttribute('data-class', usage.regenerability);
    }
    await expect(care.byId('storage-category-detail-stems')).toHaveAttribute('data-class', 'regenerable');
    await expect(care.byId('storage-category-stems')).toHaveAttribute('data-class', 'original');
  });

  test('original categories link to the screens where items are deleted one by one', async ({ care, page }) => {
    await care.goto('storage');
    await expect(care.byId('storage-manage-exports')).toHaveAttribute('href', /\/library/);
    await expect(care.byId('storage-manage-stems')).toHaveAttribute('href', /\/tracks/);
    await expect(care.byId('storage-manage-recordings')).toHaveAttribute('href', /\/tracks/);
    await expectNoIdWithPrefix(page, 'storage-manage-detail-stems');
  });
});

test.describe('worker unreachable (013 US3-2)', { tag: ['@library-care', '@storage', '@compose'] }, () => {
  test('with the worker stopped the library part is shown and the worker part says why', async ({ care, careApi }) => {
    requireCompose();
    test.setTimeout(6 * 60_000);
    stopService('worker');
    try {
      const overview = await careApi.storage(true);
      expect(overview.worker.available).toBe(false);
      expect(overview.worker.reason).toBeTruthy();
      expect(categoryBytes(overview, 'stems')).not.toBeNull();
      await care.goto('storage');
      await expect(care.byId('storage-worker-unavailable')).toBeVisible();
      await expect(care.byId('storage-category-stems')).toHaveAttribute('data-bytes', /^\d+$/);
    } finally {
      startService('worker');
    }
    await expect.poll(async () => (await careApi.storage(true)).worker.available, { timeout: 120_000 }).toBe(true);
  });
});

test.describe('per-track table (013 US3-3)', { tag: ['@library-care', '@storage'] }, () => {
  test('sorting by total orders the rows, the title opens the track page', async ({ api, care, page }) => {
    test.setTimeout(fakeJobTimeoutMs * 2 + 60_000);
    const big = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
    await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
    await care.goto('storage');
    await expect(care.byId('storage-tracks-table')).toBeVisible();
    for (const direction of ['descending', 'ascending'] as const) {
      await care.sortBy('total', direction);
      const totals = await Promise.all((await care.trackOrder()).map(async (id) => Number(await care.trackRow(id).getAttribute('data-total-bytes'))));
      expect(isSorted(totals, direction), `${direction}: ${totals.join(', ')}`).toBe(true);
    }
    await care.sortBy('stems', 'descending');
    await care.trackRow(big.id).locator('a').first().click();
    await expect(page).toHaveURL(new RegExp(`/tracks/${big.id}$`));
  });
});

test.describe('storage levels and model caches (013 FR-031, US3-4)', { tag: ['@library-care', '@storage'] }, () => {
  test.describe('Beginner', () => {
    test.use({ storageState: levelState('beginner') });

    test('shows the five largest tracks and the three clean-ups, not the table', async ({ care, page }) => {
      await care.goto('storage');
      await expect(care.byId('storage-top-tracks')).toBeVisible();
      await expectRendered(page, ['storage-top-tracks', 'cleanup-detail-stems', 'cleanup-worker-cache', 'cleanup-leftovers', 'storage-unattached', 'storage-backups']);
      await expectNotRendered(page, ['storage-tracks-table', 'storage-worker-entries']);
      await expectNoIdWithPrefix(page, 'cleanup-track-detail-');
    });
  });

  test.describe('Expert', () => {
    test.use({ storageState: expertState });

    test('shows the worker cache entries; model caches have no delete control', async ({ care, careApi, page }) => {
      await care.goto('storage');
      await expectRendered(page, ['storage-tracks-table', 'storage-worker-entries']);
      const overview = await careApi.storage(true);
      await expect(care.byId('storage-worker-entries').locator('tbody tr')).toHaveCount(Math.max(1, overview.workerEntries.length));
      for (const cache of overview.modelCaches) {
        const row = care.byId(`storage-model-cache-${cache.id}`);
        await expect(row).toContainText(/Downloaded again at first use/i);
        await expect(row.locator('button')).toHaveCount(0);
      }
    });
  });
});

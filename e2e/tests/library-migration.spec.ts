import { LibraryDatabaseSwap, requireDatabaseFixture } from '../support/library-db';
import { readLibraryScenarios } from '../support/library-seed';
import { expect, test } from '../support/library-test';

// Feature 022 US1-6 (T039; FR-010, SC-003): a library from before the feature (5,000 items, the
// backend seeder's pre-022 database) is filed by the start-up backfill within 10 s while the root
// shows "Sorting your library". Opt-in (`@library-db`, E2E_COMPOSE=1): the spec swaps the api's
// database and puts the original back afterwards.
const { files, items, targets } = readLibraryScenarios().seed5000;
let swap: LibraryDatabaseSwap | undefined;

test.describe.configure({ mode: 'serial' });

test.afterAll(() => {
  swap?.restore();
});

test('a pre-022 library is filed after start: the pending note shows, then every item has a folder', { tag: ['@library', '@library-db'] }, async ({ libraryApi, folders, page }) => {
  test.setTimeout(10 * 60_000);
  const fixture = requireDatabaseFixture(files.pre022);
  // The note can only be seen while the backfill runs; on a fast machine it may be over before the
  // page renders, so its visibility is recorded (annotation), and its disappearance is asserted.
  swap = LibraryDatabaseSwap.start(fixture);
  const started = Date.now();
  await page.goto('/library');
  // Wait until the view has loaded: before that the note is absent too, and the count of 0 would
  // pass at once while the backfill has not even started (r22 integration).
  await expect(folders.counts).toBeVisible();
  const pending = folders.filingPending;
  const sawPending = await pending.isVisible();
  await libraryApi.waitUntilFiled(targets.filingSeconds * 1000 + 5_000);
  const elapsedMs = Date.now() - started;
  await expect(pending).toHaveCount(0);

  const root = await libraryApi.root();
  expect(root.pendingFiling).toBe(0);
  expect(root.totalItems).toBe(items);
  // SC-003: within 10 s of start (the api start itself is not counted: measured from healthy).
  expect(elapsedMs, `filing took ${String(elapsedMs)} ms`).toBeLessThanOrEqual(targets.filingSeconds * 1000);
  test.info().annotations.push({ type: 'SC-003', description: `filed ${String(items)} items in <= ${String(elapsedMs)} ms; pending note seen: ${String(sawPending)}` });
});

import type { Page } from '@playwright/test';
import type { LibrarySearch } from '../pages/library-search';
import type { LibraryApi } from '../support/library-api';
import { LibraryDatabaseSwap, requireDatabaseFixture } from '../support/library-db';
import { readLibraryScenarios } from '../support/library-seed';
import { expect, test } from '../support/library-test';

// Feature 022 US3-1/US3-9 (T071; SC-004): on the 5,000-item database fixture the results show
// within 0.4 s of the last key press (p95 over 30 typed queries), and no older result replaces the
// newest one. Opt-in (`@library-db`, E2E_COMPOSE=1, Chromium): the spec swaps the api's database.
// Measured: last key press -> response for the final text -> two animation frames (rendered).
// `npm run test:library-db` runs it in Chromium only (the SC-004 reference browser).
const { files, searchQueries, targets } = readLibraryScenarios().seed5000;
const instrumentWords = ['snare', 'bass', 'lead vocals', 'strings', 'piano', 'guitar', 'kick', 'brass', 'drum', 'other'];
let swap: LibraryDatabaseSwap | undefined;

test.describe.configure({ mode: 'serial' });

test.afterAll(() => {
  swap?.restore();
});

/**
 * 30 queries: instrument words, prefixes of artist names on the root (3-6 letters), then whole
 * artist names. The seeder's names share their prefixes ("Artist 000".."Artist 149", some with
 * "The"), so the prefixes alone give only 8 distinct queries; the whole names fill up to 30.
 */
async function queries(libraryApi: LibraryApi): Promise<string[]> {
  const names = (await libraryApi.root()).children.map((folder) => folder.name.toLowerCase());
  const prefixes = names.map((name, index) => name.slice(0, 3 + (index % 4)));
  return [...new Set([...instrumentWords, ...prefixes, ...names])].filter((query) => query.trim().length > 0).slice(0, searchQueries);
}

/**
 * Counts DOM changes after the final results rendered (stale results arriving late); the polite
 * live region is left out (its count is announced after the typing pause by design).
 */
async function watchResults(page: Page): Promise<void> {
  await page.evaluate(() => {
    const state = window as unknown as { qaLate: number; qaFinal: boolean };
    state.qaLate = 0;
    state.qaFinal = false;
    new MutationObserver((records) => {
      const relevant = records.filter((record) => (record.target instanceof Element ? record.target : record.target.parentElement)?.closest('[data-testid="library-search-status"]') === null);
      if (state.qaFinal) state.qaLate += relevant.length;
    }).observe(document.body, { subtree: true, childList: true, characterData: true });
  });
}

/** DOM changes counted after the final results, 300 ms after they rendered. */
async function lateChanges(page: Page): Promise<number> {
  return page.evaluate(() => new Promise<number>((resolve) => setTimeout(() => { resolve((window as unknown as { qaLate: number }).qaLate); }, 300)));
}

/** Types one query; returns the ms from the last key press to the rendered final results. */
async function timeQuery(page: Page, search: LibrarySearch, query: string): Promise<number> {
  await search.input.fill('');
  await page.evaluate(() => {
    Object.assign(window, { qaFinal: false, qaLate: 0 });
  });
  await search.input.pressSequentially(query.slice(0, -1), { delay: 40 });
  const final = page.waitForResponse((response) => response.url().includes('/api/library/search') && new URL(response.url()).searchParams.get('q') === query);
  const started = Date.now();
  await search.input.press(query.slice(-1));
  await final;
  await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  const elapsed = Date.now() - started;
  await page.evaluate(() => {
    (window as unknown as { qaFinal: boolean }).qaFinal = true;
  });
  await expect(search.results.or(search.empty).first()).toBeVisible();
  return elapsed;
}

function p95(values: readonly number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.min(sorted.length - 1, Math.ceil(0.95 * sorted.length) - 1)] ?? Number.NaN;
}

test('p95 last key to results <= 400 ms over 30 typed queries; no late stale results', { tag: ['@library', '@library-db'] }, async ({ libraryApi, folders, librarySearch, page }) => {
  test.setTimeout(10 * 60_000);
  swap = LibraryDatabaseSwap.start(requireDatabaseFixture(files.current));
  await libraryApi.waitUntilFiled(60_000);
  const list = await queries(libraryApi);
  expect(list).toHaveLength(searchQueries);
  await folders.gotoRoot();
  await watchResults(page);
  const timings: number[] = [];
  for (const query of list) {
    timings.push(await timeQuery(page, librarySearch, query));
    // Stale-result check: nothing in the page changes in the 300 ms after the final results.
    expect(await lateChanges(page), `late changes after "${query}"`).toBe(0);
  }
  test.info().annotations.push({ type: 'SC-004', description: `p95 ${String(p95(timings))} ms; timings ${timings.join(', ')}` });
  expect(p95(timings)).toBeLessThanOrEqual(targets.searchP95Ms);
});

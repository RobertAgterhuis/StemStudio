import type { TestInfo } from '@playwright/test';
import type { LibraryFoldersPage } from '../pages/library-folders-page';
import type { LibrarySearch } from '../pages/library-search';
import type { LibraryApi } from '../support/library-api';
import {
  type FamilyCase,
  type LibrarySeedClients,
  type SeededFamilies,
  ensureFamilySet,
  familySeedTimeoutMs,
  readLibraryScenarios,
  salted,
} from '../support/library-seed';
import { expect, test } from '../support/library-test';

// Feature 022 US3 (T070; FR-022..FR-029, SC-006): live search. The families set (one item per
// standard and detail stem, a "What I hear" mix, a Motörhead track) is seeded once per project and
// run; family cases are checked inside the set's artist folder ("Only in this folder") so other
// runs' items cannot interfere, and against the API with `within` for the exact sets.
const families = readLibraryScenarios().families;
const cases: readonly FamilyCase[] = [...families.standard, ...families.detail];

/**
 * Parents whose items a query may also hit through an alias of research R-08 although they do
 * not contain the queried stem: "bass guitar" is an alias of Bass, so "guitar" finds the bass item.
 * Reported to the CTO (SC-006 "no item without that stem" vs R-08); see the qa hand-off.
 */
const aliasOverlap: Readonly<Record<string, readonly string[]>> = { guitar: ['bass'] };

async function familySet(seed: LibrarySeedClients, testInfo: TestInfo): Promise<SeededFamilies> {
  test.setTimeout(familySeedTimeoutMs + 120_000);
  return ensureFamilySet(seed, testInfo);
}

const parentOf = (stem: string): string => stem.split('.')[0] ?? stem;

/** Item ids a case must find: items of the queried stems (a standard stem also finds its detail stems) and mixes containing the parent. */
function mustFind(set: SeededFamilies, entry: FamilyCase): string[] {
  const standardCase = entry.stems.every((stem) => !stem.includes('.'));
  const hit = (stem: string): boolean => entry.stems.some((wanted) => stem === wanted || (standardCase && parentOf(stem) === wanted));
  const stems = [...set.byStem.entries()].filter(([stem]) => hit(stem)).map(([, item]) => item.asset.id);
  const mixes = families.mixes.filter((mix) => standardCase && mix.audible.some((stem) => entry.stems.includes(stem)));
  return [...stems, ...mixes.map((mix) => set.mixes.get(mix.name)?.asset.id ?? '')];
}

/** Item ids a case must not find: items of other parents (minus documented alias overlaps). */
function mustNotFind(set: SeededFamilies, entry: FamilyCase): string[] {
  const parents = new Set(entry.stems.map(parentOf));
  const allowed = new Set(aliasOverlap[entry.query] ?? []);
  return [...set.byStem.entries()].filter(([stem]) => !parents.has(parentOf(stem)) && !allowed.has(parentOf(stem))).map(([, item]) => item.asset.id);
}

function artistOf(set: SeededFamilies): string {
  return set.all[0]?.placement.artist.folderId ?? '';
}

/** API check of one case within the set's artist folder. */
async function expectApiCase(libraryApi: LibraryApi, set: SeededFamilies, entry: FamilyCase): Promise<void> {
  const found = (await libraryApi.search(entry.query, { within: artistOf(set) })).items.map((hit) => hit.asset.id);
  expect(found, `"${entry.query}" finds`).toEqual(expect.arrayContaining(mustFind(set, entry)));
  expect(found.filter((id) => mustNotFind(set, entry).includes(id)), `"${entry.query}" must not find`).toEqual([]);
}

/** Opens the set's artist folder with "Only in this folder" ticked. */
async function openScoped(folders: LibraryFoldersPage, search: LibrarySearch, set: SeededFamilies): Promise<void> {
  await folders.gotoFolder(artistOf(set));
  await search.input.fill('x');
  await search.scope.check();
  await search.input.fill('');
}

test('one query per instrument family and per detail group finds every item with that stem (SC-006)', { tag: '@library' }, async ({ seedClients, libraryApi, folders, librarySearch }, testInfo) => {
  const set = await familySet(seedClients, testInfo);
  await openScoped(folders, librarySearch, set);
  for (const entry of cases) {
    await expectApiCase(libraryApi, set, entry);
    await librarySearch.search(entry.query);
    for (const id of mustFind(set, entry)) {
      await expect(librarySearch.itemHit(id), `"${entry.query}" shows ${id}`).toBeVisible();
    }
    for (const id of mustNotFind(set, entry)) {
      await expect(librarySearch.itemHit(id), `"${entry.query}" hides ${id}`).toHaveCount(0);
    }
  }
});

test('typing one character at a time shows grouped results with counts for the current text', { tag: '@library' }, async ({ seedClients, folders, librarySearch, page }, testInfo) => {
  const set = await familySet(seedClients, testInfo);
  await folders.gotoRoot();
  await librarySearch.type(set.salt.slice(0, 3));
  await expect(librarySearch.input).toHaveValue(set.salt.slice(0, 3));
  await expect(librarySearch.results).toBeVisible();
  await expect(librarySearch.status).toHaveText(/^\d+ results?$/u);
  await expect(librarySearch.status).toHaveAttribute('aria-live', 'polite');
  await expect(page.locator('[data-testid^="search-group-"]').first()).toContainText(/\d/u);
  await librarySearch.type(set.salt.slice(3));
  await expect(librarySearch.itemHit(set.accented.asset.id)).toBeVisible();
  await expect(librarySearch.group('artists')).toBeVisible();
});

test('"motorhead" finds Motörhead with the matched part highlighted; results carry their path as links', { tag: '@library' }, async ({ seedClients, folders, librarySearch }, testInfo) => {
  const set = await familySet(seedClients, testInfo);
  const { placement, asset } = set.accented;
  await folders.gotoRoot();
  await librarySearch.search(`motorhead ${set.salt}`);
  const hit = librarySearch.folderHit(placement.artist.folderId);
  await expect(hit).toBeVisible();
  await expect(librarySearch.marks(hit).first()).toHaveText(/^mot[oö]rhead$/iu);
  expect(await librarySearch.marks(hit).first().evaluate((el) => el.tagName)).toBe('MARK');
  await expect(librarySearch.itemHit(asset.id)).toBeVisible();
  await expect(folders.itemPath(asset.id).getByRole('link')).toHaveText([placement.artist.name, placement.album.name, placement.song.name]);
});

test('two words narrow to items matching both; item actions work in the results', { tag: '@library' }, async ({ seedClients, folders, librarySearch, library, page }, testInfo) => {
  const set = await familySet(seedClients, testInfo);
  const bass = set.byStem.get('bass')?.asset.id ?? '';
  const drums = set.byStem.get('drums')?.asset.id ?? '';
  await folders.gotoRoot();
  await librarySearch.search(`${set.salt} bass`);
  await expect(librarySearch.itemHit(bass)).toBeVisible();
  await expect(librarySearch.itemHit(drums)).toHaveCount(0);
  const download = page.waitForEvent('download');
  await library.download(bass).click();
  expect((await download).suggestedFilename()).toMatch(/\.flac$/u);
  const renamed = salted('Found and renamed', set.salt);
  await library.rename(bass, renamed);
  await expect(librarySearch.input).toHaveValue(`${set.salt} bass`);
});

test('Escape and the clear button restore the folder with focus in the search box', { tag: '@library' }, async ({ seedClients, folders, librarySearch, page }, testInfo) => {
  const set = await familySet(seedClients, testInfo);
  const { placement } = set.accented;
  await folders.gotoFolder(placement.song.folderId);
  await librarySearch.search('bass');
  await expect(folders.view).toBeHidden();
  await page.keyboard.press('Escape');
  await expect(folders.view).toHaveAttribute('data-level', 'song');
  await expect(folders.title).toHaveText(placement.song.name);
  await expect(librarySearch.input).toBeFocused();
  await librarySearch.search('bass');
  await librarySearch.clear.click();
  await expect(folders.title).toHaveText(placement.song.name);
  await expect(librarySearch.input).toBeFocused();
});

test('no matches: the empty state names the query and clears it', { tag: '@library' }, async ({ seedClients, folders, librarySearch }, testInfo) => {
  const set = await familySet(seedClients, testInfo);
  const query = `zzz${set.salt}`;
  await folders.gotoRoot();
  await librarySearch.search(query);
  await expect(librarySearch.empty).toContainText(query);
  await librarySearch.emptyClear.click();
  await expect(librarySearch.input).toHaveValue('');
  await expect(folders.view).toHaveAttribute('data-level', 'root');
});

test('keyboard: "/" focuses the search, Arrow Down moves to the first result, the count is announced', { tag: '@library' }, async ({ seedClients, folders, librarySearch, page }, testInfo) => {
  const set = await familySet(seedClients, testInfo);
  await folders.gotoRoot();
  await folders.title.click();
  await page.keyboard.press('/');
  await expect(librarySearch.input).toBeFocused();
  await page.keyboard.type(`${set.salt} bass`);
  await expect(librarySearch.status).toHaveText(/^\d+ results?$/u);
  await page.keyboard.press('ArrowDown');
  const focusedInResults = await librarySearch.results.evaluate((el) => el.contains(document.activeElement));
  expect(focusedInResults).toBe(true);
});

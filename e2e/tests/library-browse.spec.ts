import type { Page, TestInfo } from '@playwright/test';
import type { LibraryFoldersPage } from '../pages/library-folders-page';
import { present } from '../support/generative';
import type { LibraryApi, LibraryFolderDto } from '../support/library-api';
import {
  type LibrarySeedClients,
  type SeededBrowseTrack,
  browseSalt,
  browseSeedTimeoutMs,
  browseTrack,
  ensureBrowseSet,
  readLibraryScenarios,
  salted,
} from '../support/library-seed';
import { expect, test } from '../support/library-test';
import { fakeOnlineMode } from '../support/online';
import { levelState } from '../support/preferences';

// Feature 022 US2 (T054; FR-013..FR-021, SC-005): browse the folders with art. One browse set per
// browser project and run (tests/fixtures/library/scenarios.json "browse"), seeded on first use.
const scenarios = readLibraryScenarios().browse;

/** The browse set of this project and run (seeded once per worker). */
async function browseSet(seed: LibrarySeedClients, testInfo: TestInfo): Promise<Map<string, SeededBrowseTrack>> {
  test.setTimeout(browseSeedTimeoutMs);
  return ensureBrowseSet(seed, testInfo);
}

const seeded = browseTrack;

/** Folder of an artist on the API root. */
async function artistFolder(libraryApi: LibraryApi, artistId: string): Promise<LibraryFolderDto> {
  return present((await libraryApi.root()).children.find((child) => child.id === artistId), `artist ${artistId}`);
}

/** Without a band info match the artist without pictures shows the lettered placeholder (mode found gives every track the fake cover). */
function expectedPlaceholderArt(apiKind: string): string {
  return fakeOnlineMode === 'found' ? apiKind : 'placeholder';
}

/** Tile indices of the given artist folders on the open root page. */
async function tileOrder(folders: LibraryFoldersPage, ids: readonly string[]): Promise<number[]> {
  const all = await folders.tiles.evaluateAll((tiles) => tiles.map((tile) => tile.getAttribute('data-testid') ?? ''));
  return ids.map((id) => all.indexOf(`folder-tile-${id}`));
}

async function imgCount(page: Page, folderId: string): Promise<number> {
  return page.getByTestId(`folder-art-${folderId}`).locator('img').count();
}

test('the root lists artists A-Z ("The" ignored) with mosaic, cover and placeholder art and their counts', { tag: '@library' }, async ({ seedClients, libraryApi, folders, page }, testInfo) => {
  const set = await browseSet(seedClients, testInfo);
  const nora = seeded(set, 'nora-harbour').item.placement.artist.folderId;
  const meters = seeded(set, 'meters').item.placement.artist.folderId;
  const abel = seeded(set, 'abel').item.placement.artist.folderId;
  await folders.gotoRoot();
  const order = await tileOrder(folders, [abel, nora, meters]);
  expect(order.every((index) => index >= 0), 'all three artists on the root page').toBe(true);
  expect([...order].sort((a, b) => a - b)).toEqual(order);

  for (const [key, images] of [['nora-harbour', 3], ['duo-1', 2], ['four-1', 4]] as const) {
    const id = seeded(set, key).item.placement.artist.folderId;
    await expect(folders.tile(id), key).toHaveAttribute('data-art', 'mosaic');
    expect(await imgCount(page, id), key).toBe(images);
  }
  await expect(folders.tile(meters)).toHaveAttribute('data-art', 'cover');
  expect(await imgCount(page, meters)).toBe(1);
  await expect(folders.tile(abel)).toHaveAttribute('data-art', expectedPlaceholderArt((await artistFolder(libraryApi, abel)).art.kind));
  await expect(folders.tileCounts(nora)).toContainText(/3 albums/u);
  await expect(folders.tileCounts(nora)).toContainText(/3 items/u);
  // US2-7: one link per tile, named "<name>, artist, <counts>" (the name is a block in the grid, so
  // the accessible name computation may put a space before the screen-reader-only comma).
  await expect(folders.tile(nora)).toHaveAccessibleName(new RegExp(`^${salted('Nora Fieldings', browseSalt(testInfo))} ?, artist, 3 albums`, 'u'));
});

test('artist > album > song: header art, breadcrumb landmark with the current page, items with 007 actions', { tag: '@library' }, async ({ seedClients, folders, library, page }, testInfo) => {
  const set = await browseSet(seedClients, testInfo);
  const { placement, asset } = seeded(set, 'nora-lanterns').item;
  await folders.openSongFolder({ artistId: placement.artist.folderId, albumId: placement.album.folderId, songId: placement.song.folderId });

  await expect(folders.byId('library-folder-art')).toHaveAttribute('data-art', 'cover');
  await expect(folders.breadcrumb).toHaveRole('navigation');
  await expect(folders.breadcrumb.locator('[aria-current="page"]')).toHaveText(placement.song.name);
  await expect(folders.crumb(placement.artist.folderId)).toHaveText(placement.artist.name);
  await expect(folders.crumb(placement.album.folderId)).toHaveText(placement.album.name);
  await expect(folders.row(asset.id)).toHaveAttribute('data-status', 'ready');

  const renamed = salted('Renamed in folder', browseSalt(testInfo));
  await library.rename(asset.id, renamed);
  const download = page.waitForEvent('download');
  await library.download(asset.id).click();
  expect((await download).suggestedFilename()).toBe(`${renamed}.flac`);

  await folders.crumb(placement.artist.folderId).click();
  await expect(folders.view).toHaveAttribute('data-level', 'artist');
  await folders.byId('library-crumb-root').click();
  await expect(folders.view).toHaveAttribute('data-level', 'root');
});

test('keyboard only: Tab reaches a tile, Enter opens it, focus is visible', { tag: '@library' }, async ({ seedClients, folders, page }, testInfo) => {
  const set = await browseSet(seedClients, testInfo);
  const { placement } = seeded(set, 'meters').item;
  await folders.gotoRoot();
  const tile = folders.tile(placement.artist.folderId);
  await tile.focus();
  await expect(tile).toBeFocused();
  const outline = await tile.evaluate((el) => getComputedStyle(el).outlineStyle);
  expect(outline).not.toBe('none');
  await page.keyboard.press('Enter');
  await expect(folders.view).toHaveAttribute('data-level', 'artist');
  await expect(folders.title).toHaveText(placement.artist.name);
});

test('grid and list show the same folders; the choice is remembered after a reload', { tag: '@library' }, async ({ seedClients, folders, page }, testInfo) => {
  const set = await browseSet(seedClients, testInfo);
  const { placement } = seeded(set, 'nora-static').item;
  await folders.gotoFolder(placement.artist.folderId);
  await folders.setLayout('grid');
  const inGrid = await folders.tileNames();
  await folders.setLayout('list');
  expect(await folders.tileNames()).toEqual(inGrid);
  await expect(folders.tileArt(placement.album.folderId)).toBeVisible();
  await page.reload();
  await expect(folders.byId('library-layout-list')).toHaveAttribute('aria-pressed', 'true');
  await folders.setLayout('grid');
});

test('Advanced sorts the albums by name and by recently added', { tag: '@library' }, async ({ seedClients, folders }, testInfo) => {
  const set = await browseSet(seedClients, testInfo);
  const salt = browseSalt(testInfo);
  await folders.gotoFolder(seeded(set, 'nora-harbour').item.placement.artist.folderId);
  for (const [sort, names] of Object.entries(scenarios.expectAlbumOrder)) {
    await folders.sortBy(sort as 'name' | 'recent');
    await expect.poll(() => folders.tileNames()).toEqual(names.map((name) => salted(name, salt)));
  }
});

test.describe('Beginner', () => {
  test.use({ storageState: levelState('beginner') });

  test('browses to a song folder without sort or All items; library info is offered', { tag: '@library' }, async ({ seedClients, folders }, testInfo) => {
    const set = await browseSet(seedClients, testInfo);
    const { placement, asset } = seeded(set, 'meters').item;
    await folders.gotoRoot();
    await expect(folders.byId('library-sort')).toHaveCount(0);
    await expect(folders.byId('library-view-all')).toHaveCount(0);
    await folders.openTile(placement.artist.folderId, 'artist');
    await folders.openTile(placement.album.folderId, 'album');
    await folders.openTile(placement.song.folderId, 'song');
    await expect(folders.row(asset.id)).toBeVisible();
    await expect(folders.byId('library-info-edit')).toBeVisible();
    await expect(folders.byId(`asset-library-info-${asset.id}`)).toBeVisible();
    await expect(folders.byId('library-search-scope')).toHaveCount(0);
  });
});

/** Same box size up to sub-pixel rounding (Firefox lays the scrolled list out at fractional offsets). */
function expectSameSize(after: { width: number; height: number }, before: { width: number; height: number }): void {
  expect(after.width).toBeCloseTo(before.width, 0);
  expect(after.height).toBeCloseTo(before.height, 0);
}

test('a broken picture shows the placeholder without moving the layout', { tag: '@library' }, async ({ seedClients, folders, page }, testInfo) => {
  const set = await browseSet(seedClients, testInfo);
  const meters = seeded(set, 'meters');
  const id = meters.item.placement.artist.folderId;
  await folders.gotoRoot();
  const before = present(await folders.tile(id).boundingBox(), 'tile box before');
  await page.route(`**/api/tracks/${meters.track.id}/artwork/**`, (route) => route.fulfill({ status: 404, body: '' }));
  await page.reload();
  // Lazy pictures load (and fail) only near the viewport; the root of a busy stack lists many artists.
  await folders.tile(id).scrollIntoViewIfNeeded();
  await expect(folders.tileArt(id).locator('img:visible')).toHaveCount(0);
  await expect(folders.tileArt(id)).toContainText(/\S/u);
  const after = present(await folders.tile(id).boundingBox(), 'tile box after');
  expectSameSize(after, before);
});

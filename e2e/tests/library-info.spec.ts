import type { TestInfo } from '@playwright/test';
import type { TrackDto } from '../support/api';
import { fakeJobTimeoutMs } from '../support/env';
import type { LibraryApi, PlacementDto } from '../support/library-api';
import { type LibrarySeedClients, type SeededItem, importTagged, librarySalt, requireOnlineMode, salted, saveRawItem, testSalt } from '../support/library-seed';
import { expect, test } from '../support/library-test';

// Feature 022 US4 (T076; FR-030..FR-037): library info moves items. The dialog is driven in the UI
// (every level renders it); the folders are checked through `Asset.placement` and the folder view.
const renderMs = 60_000;

interface SeededSong {
  readonly salt: string;
  readonly track: TrackDto;
  readonly items: readonly SeededItem[];
}

/** A track filed under salted names with two items (bass, drums). */
async function songWithItems(seed: LibrarySeedClients, testInfo: TestInfo, names: { artist: string; album: string; song: string }): Promise<SeededSong> {
  test.setTimeout(fakeJobTimeoutMs + 4 * renderMs + 60_000);
  const salt = testSalt(testInfo);
  const track = await importTagged(seed, salt, salted(names.song, salt), null);
  await seed.libraryApi.setTrackInfo(track.id, { artist: salted(names.artist, salt), album: salted(names.album, salt), song: salted(names.song, salt) });
  const items = [await saveRawItem(seed, track.id, ['bass'], salted('Info one', salt)), await saveRawItem(seed, track.id, ['drums'], salted('Info two', salt))];
  return { salt, track, items };
}

/** The current placement of an item (fails when it has none). */
async function placementOf(libraryApi: LibraryApi, assetId: string): Promise<PlacementDto> {
  const placement = (await libraryApi.asset(assetId)).placement;
  if (placement === null) throw new Error(`asset ${assetId} has no placement`);
  return placement;
}

function first(song: SeededSong): SeededItem {
  const [item] = song.items;
  if (item === undefined) throw new Error('seeded song without items');
  return item;
}

test('setting the album of a track moves all its items; the old album folder vanishes and the view follows', { tag: '@library' }, async ({ seedClients, folders, libraryInfo, libraryApi, page }, testInfo) => {
  const song = await songWithItems(seedClients, testInfo, { artist: 'Motörhead', album: 'Unknown pressing', song: 'Ace of Spades' });
  const old = first(song).placement;
  await folders.gotoFolder(old.song.folderId);
  await libraryInfo.openForTrack();
  const album = salted('Ace of Spades LP', song.salt);
  await libraryInfo.save({ album });

  const moved = await Promise.all(song.items.map(async (item) => (await libraryApi.asset(item.asset.id)).placement));
  expect(moved.map((placement) => placement?.album.name)).toEqual([album, album]);
  const newSong = moved[0]?.song.folderId ?? '';
  await expect(page).toHaveURL(new RegExp(`/library/${newSong}`, 'u'));
  await expect(folders.breadcrumb).toContainText(album);
  expect((await libraryApi.folderResponse(old.album.folderId)).status()).toBe(404);
  expect(moved.map((placement) => placement?.album.source)).toEqual(['track', 'track']);
});

test('an own song name on one item moves only that item; Automatic puts it back', { tag: '@library' }, async ({ seedClients, folders, libraryInfo, libraryApi }, testInfo) => {
  const song = await songWithItems(seedClients, testInfo, { artist: 'Item Mover', album: 'Moves', song: 'Origin' });
  const [one, two] = song.items as [SeededItem, SeededItem];
  await folders.gotoFolder(one.placement.song.folderId);
  await libraryInfo.openForItem(one.asset.id);
  const own = salted('Jazz take', song.salt);
  await libraryInfo.save({ song: own });
  const movedOne = await placementOf(libraryApi, one.asset.id);
  expect([movedOne.song.name, movedOne.song.source]).toEqual([own, 'item']);
  expect((await placementOf(libraryApi, two.asset.id)).song.folderId).toBe(one.placement.song.folderId);

  await folders.gotoFolder(movedOne.song.folderId);
  await libraryInfo.openForItem(one.asset.id);
  await expect(libraryInfo.automatic('song')).toBeVisible();
  await libraryInfo.save({ song: 'auto' });
  expect((await placementOf(libraryApi, one.asset.id)).song.folderId).toBe(one.placement.song.folderId);
});

test('Reset makes every level automatic again; the labels show the automatic names', { tag: '@library' }, async ({ seedClients, folders, libraryInfo, libraryApi }, testInfo) => {
  const song = await songWithItems(seedClients, testInfo, { artist: 'Resetter', album: 'Before', song: 'Reset Song' });
  await folders.gotoFolder(first(song).placement.song.folderId);
  await libraryInfo.openForTrack();
  await expect(libraryInfo.automatic('artist')).not.toBeChecked();
  await libraryInfo.reset();
  const info = await libraryApi.trackInfo(song.track.id);
  expect([info.artist.value, info.album.value, info.song.value]).toEqual([null, null, null]);
  expect(info.artist.source).not.toBe('track');
  expect((await libraryApi.asset(first(song).asset.id)).placement?.artist.name).toBe(info.artist.automatic);
});

test('invalid names show a readable reason and change nothing', { tag: '@library' }, async ({ seedClients, folders, libraryInfo, libraryApi }, testInfo) => {
  const song = await songWithItems(seedClients, testInfo, { artist: 'Validator', album: 'Rules', song: 'Checked' });
  const before = first(song).placement;
  await folders.gotoFolder(before.song.folderId);
  // The dialog checks as the API does and marks the field (aria-invalid + its reason) before any request.
  for (const invalid of ['   ', 'tab\tname']) {
    await libraryInfo.openForTrack();
    await libraryInfo.fill({ album: invalid });
    await libraryInfo.byId('library-info-save').click();
    await expect(libraryInfo.input('album'), JSON.stringify(invalid)).toHaveAttribute('aria-invalid', 'true');
    await expect(libraryInfo.problem('album'), JSON.stringify(invalid)).toBeVisible();
    await libraryInfo.byId('library-info-cancel').click();
  }
  // More than 200 characters cannot be typed (maxlength); the API rejects them.
  await libraryInfo.openForTrack();
  await libraryInfo.fill({ album: 'x'.repeat(201) });
  await expect(libraryInfo.input('album')).toHaveValue('x'.repeat(200));
  await libraryInfo.byId('library-info-cancel').click();
  expect((await libraryApi.asset(first(song).asset.id)).placement?.album.folderId).toBe(before.album.folderId);
  for (const album of [' ', 'x'.repeat(201)]) {
    expect((await libraryApi.setTrackInfoResponse(song.track.id, { artist: null, album, song: null })).status()).toBe(400);
  }
});

test('the track dialog offers "Look up band info again" (005 correction on the track page)', { tag: '@library' }, async ({ seedClients, folders, libraryInfo, page }, testInfo) => {
  const song = await songWithItems(seedClients, testInfo, { artist: 'Looker', album: 'Lookup', song: 'Again' });
  await folders.gotoFolder(first(song).placement.song.folderId);
  await libraryInfo.openForTrack();
  await libraryInfo.lookup.click();
  await expect(page).toHaveURL(new RegExp(`/tracks/${song.track.id}`, 'u'));
});

test('a later band info match moves automatic items only; own names stay (mode found)', { tag: '@library' }, async ({ seedClients, metadataApi, libraryApi }, testInfo) => {
  requireOnlineMode('found');
  test.setTimeout(fakeJobTimeoutMs + 4 * renderMs + 120_000);
  const salt = librarySalt(testInfo, 'm');
  const track = await importTagged(seedClients, salt, salted('Before Match', salt), { artist: salted('MetallicaTV', salt), album: null, title: salted('Before Match', salt) });
  await metadataApi.waitForMetadata(track.id, 60_000);
  const automatic = await saveRawItem(seedClients, track.id, ['bass'], salted('Auto item', salt));
  const own = await saveRawItem(seedClients, track.id, ['drums'], salted('Own item', salt));
  await libraryApi.setItemInfo(own.asset.id, { artist: null, album: null, song: salted('My own name', salt) });

  // The fake provider's recording title follows the looked-up title (tests/fixtures/metadata/fake-provider.json).
  const corrected = salted('After Match', salt);
  expect(await metadataApi.lookup(track.id, { title: corrected })).toBeLessThan(300);
  await expect.poll(async () => (await libraryApi.asset(automatic.asset.id)).placement?.song.name, { timeout: 60_000 }).toBe(corrected);
  const ownAfter = (await libraryApi.asset(own.asset.id)).placement;
  expect([ownAfter?.song.name, ownAfter?.song.source]).toEqual([salted('My own name', salt), 'item']);
  expect((await libraryApi.asset(automatic.asset.id)).placement?.song.source).toBe('match');
});

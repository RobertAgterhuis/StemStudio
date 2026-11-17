import type { TestInfo } from '@playwright/test';
import type { TrackDto } from '../support/api';
import type { AssetsApi } from '../support/assets-api';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { generationTimeoutMs, restyledTake, songTake, takeOf } from '../support/generative-flow';
import { present, readOwnLyrics, requireGeneratorMode } from '../support/generative';
import { type PlacedAssetDto, type PlacementDto, folderPath, pathText } from '../support/library-api';
import {
  type FilingSong,
  type LibrarySeedClients,
  type NormalisationGroup,
  type SeedTags,
  type SeededItem,
  importTagged,
  librarySalt,
  readLibraryScenarios,
  requireOnlineMode,
  salted,
  saltedTemplate,
  saveMixItem,
  saveRawItem,
} from '../support/library-seed';
import { expect, test } from '../support/library-test';
import type { ScoreApi } from '../support/score-api';

// Feature 022 US1 (T038; FR-001..FR-012, SC-001, SC-002): saved items are filed at once. Items are
// created through the API (the dialogs are 007/008 specs) except the confirmation case; folders are
// checked in the UI and through `Asset.placement`. Every name carries the run salt.
const renderMs = 60_000;

interface SongNames {
  readonly artist: string;
  readonly album: string;
  readonly song: string;
}

const battery: SongNames = { artist: 'Metallica', album: 'Master of Puppets', song: 'Battery' };

function saltOf(testInfo: TestInfo): string {
  return librarySalt(testInfo, testInfo.title.length.toString(36));
}

/** Imports a tagged copy and gives the track salted library info (mode independent). */
async function namedTrack(seed: LibrarySeedClients, salt: string, names: SongNames): Promise<TrackDto> {
  const track = await importTagged(seed, salt, salted(names.song, salt), null);
  await seed.libraryApi.setTrackInfo(track.id, { artist: salted(names.artist, salt), album: salted(names.album, salt), song: salted(names.song, salt) });
  return track;
}

/** The placement a created item carries in its create response (FR-002: filed in the same save). */
function placementOnCreate(asset: { readonly id: string }): PlacementDto | null {
  return (asset as Partial<PlacedAssetDto>).placement ?? null;
}

/** One item of every kind the fake stack makes without the generator (stem, detail stem, selected channels, range, stem pack, What I hear, sheet music). */
async function everyKind(seed: LibrarySeedClients, assetsApi: AssetsApi, scoreApi: ScoreApi, track: TrackDto): Promise<{ readonly id: string }[]> {
  const raw = (channels: string[], extra: object = {}): ReturnType<AssetsApi['createExport']> =>
    assetsApi.createExport({ trackId: track.id, mode: 'raw', channels, format: { format: 'flac' }, ...extra });
  return [
    await raw(['bass']),
    await raw(['drums.snare']),
    await raw(['drums', 'bass']),
    await raw(['vocals'], { range: { startSeconds: 2, endSeconds: 8 } }),
    ...(await assetsApi.createStemPack(track.id, false, { format: 'flac' })),
    (await saveMixItem(seed, track.id, ['drums', 'bass'], `What I hear ${track.id}`)).asset,
    await scoreApi.create({ source: { trackId: track.id, channel: 'bass' } }),
  ];
}

/** Files one item per spelling (track library info) and returns the items. */
async function fileSpellings(seed: LibrarySeedClients, salt: string, spellings: readonly string[]): Promise<SeededItem[]> {
  const items: SeededItem[] = [];
  for (const [index, spelling] of spellings.entries()) {
    const track = await importTagged(seed, salt, salted(`Norm ${spelling.length.toString(36)}${String(index)}`, salt), null);
    await seed.libraryApi.setTrackInfo(track.id, { artist: saltedTemplate(spelling, salt), album: null, song: null });
    items.push(await saveRawItem(seed, track.id, ['bass'], salted(`Norm item ${String(index)}`, salt)));
  }
  return items;
}

/** Expected placement name of a filing song (`Unknown ...` stays, `<track title>` is the track's title). */
function expectedName(value: string, salt: string, track: TrackDto): string {
  if (value.startsWith('Unknown ')) return value;
  return value === '<track title>' ? track.title : salted(value, salt);
}

/** The upload title and tags of a filing song, salted. */
function saltedTags(song: FilingSong, salt: string): { readonly title: string; readonly tags: SeedTags | null } {
  const title = salted(song.tags?.title ?? `Untitled ${song.key}`, salt);
  const tag = (value: string | null): string | null => (value === null ? null : salted(value, salt));
  return { title, tags: song.tags === null ? null : { artist: tag(song.tags.artist), album: tag(song.tags.album), title } };
}

/** Imports one tagged filing song, saves an item and checks its placement against the fixture. */
async function fileTaggedSong(seed: LibrarySeedClients, salt: string, song: FilingSong): Promise<PlacementDto> {
  const { title, tags } = saltedTags(song, salt);
  const track = await importTagged(seed, salt, title, tags);
  const { placement } = await saveRawItem(seed, track.id, ['bass'], salted(`Filing ${song.key}`, salt));
  const fallback = song.expect.artist.startsWith('Unknown ');
  expect(placement.artist.name, `${song.key} artist`).toBe(expectedName(song.expect.artist, salt, track));
  expect(placement.album.name, `${song.key} album`).toBe(expectedName(song.expect.album, salt, track));
  expect(placement.song.name, `${song.key} song`).toBe(song.expect.song === '<track title>' ? track.title : title);
  expect(placement.artist.source, `${song.key} artist source`).toBe(fallback ? 'fallback' : 'import');
  return placement;
}

test('every item kind of a track lands in its song folder at once, with the folder path on the item', { tag: '@library' }, async ({ api, seedClients, assetsApi, scoreApi, libraryApi, folders }, testInfo) => {
  test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 8 * renderMs);
  const salt = saltOf(testInfo);
  const track = await namedTrack(seedClients, salt, battery);
  await scoreApi.requireFeature(track.id);
  await api.waitForDetail(track.id, ['completed'], detailPhaseTimeoutMs);
  const created = await everyKind(seedClients, assetsApi, scoreApi, track);

  const expected = `${salted(battery.artist, salt)} > ${salted(battery.album, salt)} > ${salted(battery.song, salt)}`;
  const onCreate = created.map((asset) => placementOnCreate(asset));
  // SC-001: no unfiled item, not even in the 202 of the save.
  expect(onCreate.map((placement) => placement?.filed)).toEqual(created.map(() => true));
  expect(onCreate.map((placement) => pathText(present(placement, 'placement')))).toEqual(created.map(() => expected));
  const songId = present(onCreate[0], 'placement').song.folderId;
  for (const asset of created) {
    expect((await libraryApi.waitForPlacement(asset.id)).song.folderId).toBe(songId);
  }

  await folders.gotoFolder(songId);
  await expect(folders.view).toHaveAttribute('data-level', 'song');
  await expect(folders.title).toHaveText(salted(battery.song, salt));
  for (const asset of created) {
    await expect(folders.row(asset.id)).toBeVisible();
    await expect(folders.itemPath(asset.id).getByRole('link')).toHaveCount(3);
  }
  await expect(folders.byId('song-open-track')).toBeVisible();
  await expect(folders.byId('song-open-mixer')).toBeVisible();
});

test('the save confirmation names Artist > Album > Song and links to the song folder', { tag: '@library' }, async ({ seedClients, trackPage, exportDialog, folders, libraryApi, page }, testInfo) => {
  test.setTimeout(fakeJobTimeoutMs + 2 * renderMs);
  const track = await namedTrack(seedClients, saltOf(testInfo), battery);
  await trackPage.goto(track.id);
  await exportDialog.openForStem('bass');
  await exportDialog.save();
  const id = await exportDialog.savedAssetId();
  const placement = await libraryApi.waitForPlacement(id);
  await expect(exportDialog.byId('export-saved-folder').getByRole('link')).toHaveText([placement.artist.name, placement.album.name, placement.song.name]);
  await expect(exportDialog.savedOpen).toHaveAttribute('href', new RegExp(`/library/${placement.song.folderId}#asset-${id}$`, 'u'));
  await exportDialog.savedOpen.click();
  await expect(page).toHaveURL(new RegExp(`/library/${placement.song.folderId}`, 'u'));
  await expect(folders.row(id)).toBeVisible();
});

test('more items of one song share its folder; "metallica " joins the existing Metallica folder', { tag: '@library' }, async ({ seedClients, libraryApi, folders }, testInfo) => {
  test.setTimeout(2 * fakeJobTimeoutMs + 4 * renderMs);
  const salt = saltOf(testInfo);
  const first = await namedTrack(seedClients, salt, battery);
  const bass = await saveRawItem(seedClients, first.id, ['bass'], salted('Seed one', salt));
  const drums = await saveRawItem(seedClients, first.id, ['drums'], salted('Seed two', salt));
  expect(drums.placement.song.folderId).toBe(bass.placement.song.folderId);

  // US1-3 / SC-002: lower case and a trailing space give the same key.
  const second = await importTagged(seedClients, salt, salted('Orion', salt), null);
  await libraryApi.setTrackInfo(second.id, { artist: `metallica ${salt} `, album: salted(battery.album, salt), song: salted('Orion', salt) });
  const orion = await saveRawItem(seedClients, second.id, ['bass'], salted('Seed three', salt));
  expect(orion.placement.artist.folderId).toBe(bass.placement.artist.folderId);
  expect(orion.placement.album.folderId).toBe(bass.placement.album.folderId);
  expect(orion.placement.artist.name).toBe(salted(battery.artist, salt));

  await folders.gotoFolder(bass.placement.album.folderId);
  await expect(folders.tiles).toHaveCount(2);
  await expect(folders.tile(bass.placement.song.folderId)).toBeVisible();
  await expect(folders.tile(orion.placement.song.folderId)).toBeVisible();
});

for (const group of readLibraryScenarios().normalisation.groups) {
  test(`normalisation: the spellings of "${group.key}" share one artist folder`, { tag: '@library' }, async ({ seedClients }, testInfo) => {
    test.setTimeout((group.spellings.length + 1) * (fakeJobTimeoutMs + renderMs));
    const salt = saltOf(testInfo);
    const items = await fileSpellings(seedClients, salt, group.spellings);
    expect(new Set(items.map((item) => item.placement.artist.folderId)).size, group.spellings.join(' | ')).toBe(1);
    const others = await fileSpellings(seedClients, salt, distinctNames(group));
    expect(others.filter((item) => item.placement.artist.folderId === items[0]?.placement.artist.folderId)).toEqual([]);
  });
}

function distinctNames(group: NormalisationGroup): readonly string[] {
  return group.distinctFrom ?? [];
}

test('without band info the import names decide: tags, unknown album, unknown artist, two albums (mode not-found)', { tag: '@library' }, async ({ seedClients }, testInfo) => {
  requireOnlineMode('not-found');
  const songs = readLibraryScenarios().filing.songs;
  test.setTimeout(songs.length * (fakeJobTimeoutMs + renderMs));
  const salt = saltOf(testInfo);
  const placements = new Map<string, PlacementDto>();
  for (const song of songs) {
    placements.set(song.key, await fileTaggedSong(seedClients, salt, song));
  }
  // The same song on two albums: one artist ("The" ignored), two album and two song folders.
  const [a, b] = [present(placements.get('two-albums-a'), 'a'), present(placements.get('two-albums-b'), 'b')];
  expect(b.artist.folderId).toBe(a.artist.folderId);
  expect(b.album.folderId).not.toBe(a.album.folderId);
  expect(b.song.folderId).not.toBe(a.song.folderId);
});

test('items stay in their folders when the track is deleted', { tag: '@library' }, async ({ api, seedClients, folders, libraryApi }, testInfo) => {
  test.setTimeout(fakeJobTimeoutMs + detailPhaseTimeoutMs + 2 * renderMs);
  const salt = saltOf(testInfo);
  const track = await namedTrack(seedClients, salt, { artist: 'Keeper', album: 'Kept', song: 'Kept Song' });
  const item = await saveRawItem(seedClients, track.id, ['bass'], salted('Kept item', salt));
  await api.stopDetail(track.id, detailPhaseTimeoutMs);
  expect(await api.deleteTrack(track.id)).toBe(204);

  expect((await libraryApi.asset(item.asset.id)).placement?.song.folderId).toBe(item.placement.song.folderId);
  await folders.gotoFolder(item.placement.song.folderId);
  await expect(folders.row(item.asset.id)).toBeVisible();
  await expect(folders.byId(`asset-source-${item.asset.id}`)).toHaveAttribute('data-track-available', 'false');
  await expect(folders.byId('song-open-track')).toHaveCount(0);
});

test('deleting the last item removes the song, album and artist folders; the stale link shows the empty-folder page', { tag: '@library' }, async ({ seedClients, library, libraryApi, folders }, testInfo) => {
  test.setTimeout(fakeJobTimeoutMs + 2 * renderMs);
  const salt = saltOf(testInfo);
  const track = await namedTrack(seedClients, salt, { artist: 'Lonely', album: 'Single', song: 'Last One' });
  const item = await saveRawItem(seedClients, track.id, ['bass'], salted('Last item', salt));
  const ids = folderPath(item.placement);

  await folders.gotoFolder(ids.songId);
  await library.delete(item.asset.id);
  const statuses = await Promise.all([ids.songId, ids.albumId, ids.artistId].map(async (id) => (await libraryApi.folderResponse(id)).status()));
  expect(statuses).toEqual([404, 404, 404]);
  await folders.gotoFolder(ids.songId);
  await expect(folders.folderMissing).toBeVisible();
  await expect(folders.folderMissing.getByRole('link')).not.toHaveCount(0);
  await folders.gotoRoot();
  await expect(folders.tile(ids.artistId)).toHaveCount(0);
});

test('a restyled take is filed with its source track; an own-lyrics song under My songs', { tag: ['@library', '@generative'] }, async ({ seedClients, generationApi, voiceApi, libraryApi }, testInfo) => {
  requireGeneratorMode('ready', 'oom');
  test.setTimeout(fakeJobTimeoutMs + 2 * generationTimeoutMs + 2 * renderMs);
  const salt = saltOf(testInfo);
  const source = await namedTrack(seedClients, salt, { artist: 'Restyle Source', album: 'Source Album', song: 'Source Song' });
  const stem = await saveRawItem(seedClients, source.id, ['bass'], salted('Source item', salt));
  const restyled = takeOf(await restyledTake(generationApi, source.id, 'Jazz trio'));
  expect((await libraryApi.waitForPlacement(present(restyled.libraryAssetId, 'library item'))).song.folderId).toBe(stem.placement.song.folderId);

  const song = takeOf(await songTake(voiceApi, generationApi, { lyricsText: readOwnLyrics('sample-en.txt'), language: 'en', leadVoice: 'any', durationSeconds: 20 }));
  const placement = await libraryApi.waitForPlacement(present(song.libraryAssetId, 'library item'));
  expect([placement.artist.name, placement.album.name, placement.artist.source]).toEqual(['My songs', 'Songs from my lyrics', 'fallback']);
});

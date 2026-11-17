/**
 * Seeding of the feature 022 library specs over the public API (qa T023; fixture facts in
 * `tests/fixtures/library/scenarios.json`). Every name gets the run salt so reruns on one data
 * volume never share folders; folder ids are read from `Asset.placement`, never computed.
 *
 * @packageDocumentation
 */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test, type TestInfo } from '@playwright/test';
import type { StemName, StemStudioApi, TrackDto } from './api';
import type { AssetDto, AssetsApi, ExportMixDto } from './assets-api';
import { fakeStems } from './detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs, runId } from './env';
import { fixturePath } from './fixtures';
import type { LibraryApi, PlacementDto } from './library-api';
import type { MetadataApi } from './metadata-api';
import { artworkFixturePath, type ArtworkFixtureName } from './artwork-fixtures';
import { type FakeOnlineMode, fakeOnlineMode, modeSkipReason } from './online';
import { uniqueWavCopy } from './unique-audio';

/** Tags written into a seeded upload (RIFF LIST/INFO). */
export interface SeedTags {
  readonly artist: string | null;
  readonly album: string | null;
  readonly title: string;
}

/** One normalisation group of `scenarios.json`. */
export interface NormalisationGroup {
  readonly key: string;
  readonly spellings: readonly string[];
  readonly display: string;
  readonly distinctFrom?: readonly string[];
}

/** One tagged filing song of `scenarios.json`. */
export interface FilingSong {
  readonly key: string;
  readonly tags: SeedTags | null;
  readonly expect: { readonly artist: string; readonly album: string; readonly song: string };
}

/** One track of the browse set. */
export interface BrowseTrack {
  readonly key: string;
  readonly artist: string;
  readonly album: string;
  readonly song: string;
  readonly picture: ArtworkFixtureName | null;
}

/** One search case of the families set. */
export interface FamilyCase {
  readonly query: string;
  readonly stems: readonly string[];
}

/** Shape of `tests/fixtures/library/scenarios.json` (the parts the suite reads). */
export interface LibraryScenarios {
  readonly normalisation: { readonly groups: readonly NormalisationGroup[] };
  readonly filing: { readonly songs: readonly FilingSong[] };
  readonly browse: {
    readonly tracks: readonly BrowseTrack[];
    readonly expectOrder: readonly string[];
    readonly expectAlbumOrder: { readonly name: readonly string[]; readonly recent: readonly string[] };
  };
  readonly families: {
    readonly standard: readonly FamilyCase[];
    readonly detail: readonly FamilyCase[];
    readonly mixes: readonly { readonly name: string; readonly audible: readonly StemName[]; readonly foundBy: readonly string[] }[];
  };
  readonly seed5000: { readonly files: { readonly pre022: string; readonly current: string }; readonly items: number; readonly searchQueries: number; readonly targets: { readonly filingSeconds: number; readonly searchP95Ms: number } };
}

/** Root of the 022 fixtures. */
export const libraryFixtureDir = path.resolve(__dirname, '..', '..', 'tests', 'fixtures', 'library');

/** Reads the committed scenarios. */
export function readLibraryScenarios(): LibraryScenarios {
  return JSON.parse(readFileSync(path.join(libraryFixtureDir, 'scenarios.json'), 'utf-8')) as LibraryScenarios;
}

/**
 * Run salt of one browser project: a single lower-case word (so it is one search token), unique
 * per run and project.
 */
export function librarySalt(testInfo: TestInfo, extra = ''): string {
  return `qa${runId()}${testInfo.project.name.slice(0, 2)}${extra}`.toLowerCase().replace(/[^a-z0-9]/gu, '');
}

/** Skips the test unless the stack runs one of the fake online `modes` (tag-based filing needs `not-found`). */
export function requireOnlineMode(...modes: FakeOnlineMode[]): void {
  test.skip(!modes.includes(fakeOnlineMode), modeSkipReason(modes));
}

/** `name` with the salt appended (the form every seeded name takes). */
export function salted(name: string, salt: string): string {
  return `${name} ${salt}`;
}

/**
 * A fixture name with the salt: at `{salt}` when the name has the placeholder (names whose tail the
 * normalisation drops, such as featuring parts), else appended.
 */
export function saltedTemplate(name: string, salt: string): string {
  return name.includes('{salt}') ? name.replace('{salt}', salt) : salted(name, salt);
}

const outDir = path.resolve(__dirname, '..', '.state', 'library');

/** One RIFF sub-chunk: id, little-endian size, data padded to an even length. */
function riffChunk(id: string, data: Buffer): Buffer {
  const header = Buffer.alloc(8);
  header.write(id, 0, 'ascii');
  header.writeUInt32LE(data.length, 4);
  return Buffer.concat([header, data, data.length % 2 === 1 ? Buffer.alloc(1) : Buffer.alloc(0)]);
}

/** A `LIST/INFO` chunk with zero-terminated UTF-8 values. */
function infoChunk(tags: SeedTags): Buffer {
  const fields: [string, string | null][] = [['INAM', tags.title], ['IART', tags.artist], ['IPRD', tags.album]];
  const parts = fields.filter((field): field is [string, string] => field[1] !== null).map(([id, value]) => riffChunk(id, Buffer.from(`${value}\0`, 'utf-8')));
  return riffChunk('LIST', Buffer.concat([Buffer.from('INFO', 'ascii'), ...parts]));
}

/**
 * A unique copy of the 12 s click WAV with the given tags (none for an untagged upload). The
 * file name (the raw track title) is `fileTitle`.
 */
export function taggedWav(salt: string, fileTitle: string, tags: SeedTags | null): string {
  const unique = uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `lib-${salt}-${fileTitle}`);
  const bytes = readFileSync(unique);
  const body = tags === null ? bytes : Buffer.concat([bytes, infoChunk(tags)]);
  body.writeUInt32LE(body.length - 8, 4);
  mkdirSync(outDir, { recursive: true });
  const target = path.join(outDir, `${fileTitle.replace(/[^\p{L}\p{N} -]/gu, '-')}.wav`);
  writeFileSync(target, body);
  return target;
}

/** Clients the seeding uses. */
export interface LibrarySeedClients {
  readonly api: StemStudioApi;
  readonly assetsApi: AssetsApi;
  readonly libraryApi: LibraryApi;
  readonly metadataApi?: MetadataApi;
}

/** A saved, filed item. */
export interface SeededItem {
  readonly asset: AssetDto;
  readonly placement: PlacementDto;
}

const renderMs = 60_000;

/** Saves `channels` of a track as one raw FLAC item, waits until ready and filed. */
export async function saveRawItem(clients: LibrarySeedClients, trackId: string, channels: readonly string[], name: string): Promise<SeededItem> {
  const created = await clients.assetsApi.createExport({ trackId, mode: 'raw', channels, format: { format: 'flac' }, name });
  const asset = await clients.assetsApi.waitForReady(created.id, renderMs);
  return { asset, placement: await clients.libraryApi.waitForPlacement(asset.id) };
}

/** The mixer state in which only `audible` stems are heard ("What I hear"). */
export function audibleMix(audible: readonly StemName[]): ExportMixDto {
  return { masterVolume: 1, channels: fakeStems.map((stem) => ({ stem, volume: 0.8, pan: 0, mute: !audible.includes(stem), solo: false })) };
}

/** Saves "What I hear" with `audible` stems, waits until ready and filed. */
export async function saveMixItem(clients: LibrarySeedClients, trackId: string, audible: readonly StemName[], name: string): Promise<SeededItem> {
  const created = await clients.assetsApi.createExport({ trackId, mode: 'mix', mix: audibleMix(audible), format: { format: 'flac' }, name });
  const asset = await clients.assetsApi.waitForReady(created.id, renderMs);
  return { asset, placement: await clients.libraryApi.waitForPlacement(asset.id) };
}

/** Imports a tagged copy and waits until separated. */
export async function importTagged(clients: LibrarySeedClients, salt: string, fileTitle: string, tags: SeedTags | null): Promise<TrackDto> {
  return clients.api.ensureFileCompleted(taggedWav(salt, fileTitle, tags), fakeJobTimeoutMs);
}

/** A browse track with its first item. */
export interface SeededBrowseTrack {
  readonly spec: BrowseTrack;
  readonly track: TrackDto;
  readonly item: SeededItem;
}

/**
 * Seeds the browse set (US2): per track an import, the library info (salted names), the own
 * picture and one `bass` item.
 */
export async function seedBrowse(clients: LibrarySeedClients, salt: string): Promise<Map<string, SeededBrowseTrack>> {
  const seeded = new Map<string, SeededBrowseTrack>();
  for (const spec of readLibraryScenarios().browse.tracks) {
    const track = await importTagged(clients, salt, salted(spec.song, salt), null);
    const album = spec.album === 'Unknown album' ? null : salted(spec.album, salt);
    await clients.libraryApi.setTrackInfo(track.id, { artist: salted(spec.artist, salt), album, song: salted(spec.song, salt) });
    if (spec.picture !== null && clients.metadataApi !== undefined) {
      await clients.metadataApi.setArtwork(track.id, artworkFixturePath(spec.picture));
    }
    const item = await saveRawItem(clients, track.id, ['bass'], salted(`${spec.song} bass`, salt));
    seeded.set(spec.key, { spec, track, item });
  }
  return seeded;
}

/** Items of the families set by stem (standard and detail) plus the mixes by name. */
export interface SeededFamilies {
  readonly salt: string;
  readonly byStem: ReadonlyMap<string, SeededItem>;
  readonly mixes: ReadonlyMap<string, SeededItem>;
  readonly all: readonly SeededItem[];
  /** The "Motörhead" item (accent case, US3-2). */
  readonly accented: SeededItem;
}

/**
 * Seeds the families set (US3-3, SC-006): a track with one item per standard stem and the
 * "What I hear" mixes, and a band copy with a completed detail phase with one item per detail
 * stem. Both tracks get salted library info.
 */
export async function seedFamilies(clients: LibrarySeedClients, salt: string): Promise<SeededFamilies> {
  const scenarios = readLibraryScenarios().families;
  const byStem = new Map<string, SeededItem>();
  const mixes = new Map<string, SeededItem>();
  const standard = await importTagged(clients, salt, salted('Family Song', salt), null);
  await clients.libraryApi.setTrackInfo(standard.id, { artist: salted('Family Band', salt), album: salted('Family Album', salt), song: salted('Family Song', salt) });
  // Item names carry no instrument word, so a hit proves the instrument field (FR-022).
  let n = 0;
  const neutralName = (): string => salted(`Seed item ${String(++n)}`, salt);
  for (const stem of fakeStems) {
    byStem.set(stem, await saveRawItem(clients, standard.id, [stem], neutralName()));
  }
  for (const mix of scenarios.mixes) {
    mixes.set(mix.name, await saveMixItem(clients, standard.id, mix.audible, neutralName()));
  }
  const band = await clients.api.ensureFileCompleted(uniqueWavCopy(fixturePath('detail_10s_band.wav'), `lib-detail-${salt}`), fakeJobTimeoutMs);
  await clients.api.waitForDetail(band.id, ['completed'], detailPhaseTimeoutMs);
  await clients.libraryApi.setTrackInfo(band.id, { artist: salted('Family Band', salt), album: salted('Detail Album', salt), song: salted('Detail Song', salt) });
  for (const stem of scenarios.detail.flatMap((entry) => entry.stems)) {
    byStem.set(stem, await saveRawItem(clients, band.id, [stem], neutralName()));
  }
  const accented = await seedAccented(clients, salt);
  return { salt, byStem, mixes, all: [...byStem.values(), ...mixes.values()], accented };
}

/** A track filed under "Motörhead <salt>" with one item (US3-2: found by "motorhead"). */
async function seedAccented(clients: LibrarySeedClients, salt: string): Promise<SeededItem> {
  const track = await importTagged(clients, salt, salted('Ace of Spades', salt), null);
  await clients.libraryApi.setTrackInfo(track.id, { artist: salted('Motörhead', salt), album: null, song: salted('Ace of Spades', salt) });
  return saveRawItem(clients, track.id, ['bass'], salted('Seed accented', salt));
}

const familySeeds = new Map<string, Promise<SeededFamilies>>();

/** Time a first {@link ensureFamilySet} may take (three imports, a detail phase, 15 items). */
export const familySeedTimeoutMs = 3 * fakeJobTimeoutMs + detailPhaseTimeoutMs + 15 * renderMs;

/** The families set of this browser project and run, seeded on first use (memoised per worker). */
export function ensureFamilySet(clients: LibrarySeedClients, testInfo: TestInfo): Promise<SeededFamilies> {
  const salt = librarySalt(testInfo, 'f');
  const existing = familySeeds.get(salt) ?? seedFamilies(clients, salt);
  familySeeds.set(salt, existing);
  return existing;
}

const browseSeeds = new Map<string, Promise<Map<string, SeededBrowseTrack>>>();

/** Time a first {@link ensureBrowseSet} may take (one import and one item per browse track). */
export const browseSeedTimeoutMs = readLibraryScenarios().browse.tracks.length * (fakeJobTimeoutMs + renderMs) + 120_000;

/** The browse set of this browser project and run, seeded on first use (memoised per worker). */
export function ensureBrowseSet(clients: LibrarySeedClients, testInfo: TestInfo): Promise<Map<string, SeededBrowseTrack>> {
  const salt = browseSalt(testInfo);
  const existing = browseSeeds.get(salt) ?? seedBrowse(clients, salt);
  browseSeeds.set(salt, existing);
  return existing;
}

/** Salt of the browse set of this project and run. */
export function browseSalt(testInfo: TestInfo): string {
  return librarySalt(testInfo, 'b');
}

/** One seeded browse track by fixture key. */
export function browseTrack(set: ReadonlyMap<string, SeededBrowseTrack>, key: string): SeededBrowseTrack {
  const track = set.get(key);
  if (track === undefined) {
    throw new Error(`browse track ${key} is not in scenarios.json`);
  }
  return track;
}

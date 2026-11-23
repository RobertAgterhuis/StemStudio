/**
 * Seeding helper of the 020 handout specs (qa T029, SC-006): makes the per-family fixture song and the three-song
 * setlist of `tests/fixtures/families/manifest.json` on the fake stack through the API only. The fixture song is 014's
 * `practice-song.wav`; it gets sections (014 fake analyser), chords (016 fake engine), word-timed lyrics (006 fake
 * LRCLIB `word`), a detail split (002 fake, for the melodic part on `other.woodwinds`), one notation item per score
 * request of the plan and one practice note per family. Reruns reuse the cached track and the notation items of the
 * same request (the score requests are idempotent per source and options on the backend; a new item per run is fine).
 *
 * @packageDocumentation
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, type APIRequestContext } from '@playwright/test';
import type { StemStudioApi, TrackDto } from './api';
import { AssetsApi } from './assets-api';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from './env';
import { practiceFlowFixturePath } from './fixtures';
import { okJson } from './http';
import { readyScoreId, type InstrumentScoreRequest } from './instrument-scores';
import { InstrumentViewsApi } from './instrument-views';
import { InsightsApi, type HandoutFamily, type IngredientCode, type QualityRating, type Transposition } from './insights-api';
import { LyricsApi } from './lyrics-api';
import { PracticeFlowApi, sectionsReadyTimeoutMs, type SetlistDto } from './practice-flow';
import { ScoreApi } from './score-api';

/** One family of the plan (manifest `families.<family>`). */
export interface FamilyPlan {
  readonly label: string;
  readonly scores: readonly InstrumentScoreRequestPlan[];
  readonly expectedIncluded: readonly IngredientCode[];
  readonly transposition: Transposition | null;
  readonly transpositionNote: string | null;
  readonly needsDetail: boolean;
  readonly practiceNote: string;
}

/** A score request of the plan: the channel of the fixture song plus the 016 options. */
export interface InstrumentScoreRequestPlan extends Omit<InstrumentScoreRequest, 'source'> {
  readonly channel: string;
}

/** One setlist entry of the plan. */
export interface SetlistEntryPlan {
  readonly file: string;
  readonly folder: 'practice-flow' | 'families';
  readonly note: string;
}

/** The committed plan (`tests/fixtures/families/manifest.json`). */
export interface FamilyManifest {
  readonly song: { readonly file: string; readonly bpm: number; readonly beatsPerBar: number; readonly sections: readonly string[] };
  readonly families: Readonly<Record<HandoutFamily, FamilyPlan>>;
  readonly fakeQuality: {
    readonly analyzerVersion: string;
    readonly stems: Readonly<Record<string, { readonly rating: QualityRating; readonly bleedSource: string | null; readonly artefactKinds: readonly string[] }>>;
    readonly detail: { readonly rating: QualityRating; readonly rest: QualityRating; readonly restKind: string };
  };
  readonly setlist: { readonly name: string; readonly note: string; readonly entries: readonly SetlistEntryPlan[] };
}

/** The seeded fixture song. */
export interface SeededSong {
  readonly track: TrackDto;
  /** Notation item ids per family, in the order of the plan's score requests. */
  readonly scores: Readonly<Record<HandoutFamily, readonly string[]>>;
}

const familiesFolder = path.resolve(__dirname, '..', '..', 'tests', 'fixtures', 'families');

/** Time for the whole seed on a fresh stack: separation, detail, analyses and nine score items. */
export const seedTimeoutMs = fakeJobTimeoutMs + detailPhaseTimeoutMs + 9 * 90_000;

/** Reads the plan. */
export function familyManifest(): FamilyManifest {
  return JSON.parse(readFileSync(path.join(familiesFolder, 'manifest.json'), 'utf-8')) as FamilyManifest;
}

/** The local file of a setlist entry (the 014 song or a generated setlist song). */
export function entryFile(entry: SetlistEntryPlan): string {
  if (entry.folder === 'practice-flow') {
    return practiceFlowFixturePath(entry.file as 'practice-song.wav');
  }
  const file = path.join(familiesFolder, entry.file);
  if (!existsSync(file)) {
    throw new Error(`Fixture ${entry.file} is missing; run: uv run --project worker python tests/fixtures/families/generate_family_fixtures.py`);
  }
  return file;
}

/** The clients the seed needs, all on one API context. */
class Clients {
  readonly flow: PracticeFlowApi;
  readonly views: InstrumentViewsApi;
  readonly lyrics: LyricsApi;
  readonly scores: ScoreApi;
  readonly assets: AssetsApi;
  readonly insights: InsightsApi;

  constructor(readonly context: APIRequestContext) {
    this.flow = new PracticeFlowApi(context);
    this.views = new InstrumentViewsApi(context);
    this.lyrics = new LyricsApi(context);
    this.scores = new ScoreApi(context);
    this.assets = new AssetsApi(context);
    this.insights = new InsightsApi(context);
  }
}

/** Sections, chords and word-timed lyrics of a completed track (each started only when the stack has none). */
async function seedAnalyses(c: Clients, trackId: string): Promise<void> {
  if ((await c.flow.sections(trackId)).status === 'none') {
    expect((await c.flow.detect(trackId)).status()).toBe(202);
  }
  await c.flow.waitForSections(trackId, sectionsReadyTimeoutMs);
  await c.views.waitForChords(trackId, fakeJobTimeoutMs);
  if ((await c.lyrics.get(trackId)).status !== 'found') {
    expect([202, 409]).toContain(await c.lyrics.search(trackId));
  }
  const lyrics = await c.lyrics.waitForStatus(trackId, ['found'], fakeJobTimeoutMs);
  expect(lyrics.status).toBe('found');
}

/** The detail split of the fixture song (needed for the melodic part on `other.woodwinds`). */
async function seedDetail(api: StemStudioApi, trackId: string): Promise<void> {
  const settled = await api.waitForDetail(trackId, ['completed', 'failed', 'cancelled', 'none'], detailPhaseTimeoutMs);
  if (settled.detail.status !== 'completed') {
    await api.startDetail(trackId);
  }
  const done = await api.waitForDetail(trackId, ['completed', 'failed'], detailPhaseTimeoutMs);
  expect(done.detail.status, done.detail.error ?? '').toBe('completed');
}

/** The notation items and the practice note of one family. */
async function seedFamily(c: Clients, trackId: string, family: HandoutFamily, plan: FamilyPlan): Promise<readonly string[]> {
  const ids: string[] = [];
  for (const { channel, ...options } of plan.scores) {
    ids.push(await readyScoreId(c.scores, c.assets, { ...options, source: { trackId, channel } }, 90_000));
  }
  expect(await c.insights.setPracticeNote(trackId, family, plan.practiceNote)).toBe(200);
  return ids;
}

/**
 * Makes the fixture song with every ingredient of every family (idempotent: reruns reuse the cached track; notes are
 * overwritten with the plan's text).
 */
export async function seedFixtureSong(api: StemStudioApi, context: APIRequestContext): Promise<SeededSong> {
  const plan = familyManifest();
  const c = new Clients(context);
  const track = await api.ensureFileCompleted(practiceFlowFixturePath('practice-song.wav'), fakeJobTimeoutMs);
  await seedAnalyses(c, track.id);
  await seedDetail(api, track.id);
  const scores: Partial<Record<HandoutFamily, readonly string[]>> = {};
  for (const [family, familyPlan] of Object.entries(plan.families) as [HandoutFamily, FamilyPlan][]) {
    scores[family] = await seedFamily(c, track.id, family, familyPlan);
  }
  return { track: await api.getTrack(track.id), scores: scores as Record<HandoutFamily, readonly string[]> };
}

/** The setlist of the plan with its three songs in order (found by name or created), notes as in the plan. */
export async function seedSetlist(api: StemStudioApi, context: APIRequestContext): Promise<SetlistDto> {
  const { setlist } = familyManifest();
  const tracks: TrackDto[] = [];
  for (const entry of setlist.entries) {
    tracks.push(await api.ensureFileCompleted(entryFile(entry), fakeJobTimeoutMs));
  }
  const listed = await okJson<readonly { readonly id: string; readonly name: string }[]>(await context.get('/api/setlists'));
  const id = listed.find((s) => s.name === setlist.name)?.id ?? (await okJson<SetlistDto>(await context.post('/api/setlists', { data: { name: setlist.name } }))).id;
  await okJson<SetlistDto>(await context.patch(`/api/setlists/${id}`, { data: { note: setlist.note } }));
  const entries = setlist.entries.map((entry, index) => ({ trackId: tracks[index]?.id, mixId: null, note: entry.note }));
  return okJson<SetlistDto>(await context.put(`/api/setlists/${id}/entries`, { data: { entries } }));
}

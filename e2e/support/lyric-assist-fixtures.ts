/**
 * Fixture sheets, facts and the corpus track of the feature 023 specs
 * (`tests/fixtures/lyrics/assist/`, scripts in `e2e/lyric-scripts/`). The fake assistant picks a
 * script by sheet title (`<title-slug>-<kind>.json`, else `<kind>.json`; DELTA.md section 5), so
 * sheets keep the exact titles of `expected.json`.
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, test } from '@playwright/test';
import type { StemStudioApi, TrackDto } from './api';
import type { LyricAssistApi, LyricAssistStatusDto, LyricsSheetDto } from './lyric-assist-api';
import type { LyricsApi } from './lyrics-api';
import { taggedWav } from './library-seed';
import { fakeJobTimeoutMs, runId } from './env';

const assistDir = path.resolve(__dirname, '..', '..', 'tests', 'fixtures', 'lyrics', 'assist');

/** One planted correction or licence of `expected.json`. */
export interface PlantedFact {
  readonly line: number;
  readonly original: string;
  readonly proposal: string | null;
  readonly category: string;
}

/** One sheet of `expected.json` (the parts the suite reads). */
export interface SheetFacts {
  readonly file: string;
  readonly title: string;
  readonly language: string;
  readonly planted?: readonly PlantedFact[];
  readonly licences?: readonly { readonly line: number; readonly span: string; readonly licence: string }[];
  readonly unknownWords?: readonly { readonly line: number; readonly word: string }[];
  readonly rhythm?: { readonly bpm: number; readonly beatsPerBar: number; readonly barsPerLine: number; readonly syllablesPerBeat: number; readonly targetSyllables: number; readonly deviatingLines: readonly number[]; readonly syllablesLine2: number };
}

/** Sheet keys of `expected.json` the specs use. */
export type SheetKey = 'zomeravond' | 'avondlied' | 'radio' | 'inject-en' | 'verbatim' | 'not-own';

/** Shape of `expected.json` (the parts the suite reads). */
export interface AssistExpected {
  readonly sheets: Readonly<Record<SheetKey, SheetFacts>>;
  readonly corpus: Readonly<Record<'t-fake-song', { readonly file: string; readonly title: string; readonly verbatimRun: string }>>;
}

/** Reads `tests/fixtures/lyrics/assist/expected.json`. */
export function readAssistExpected(): AssistExpected {
  return JSON.parse(readFileSync(path.join(assistDir, 'expected.json'), 'utf-8')) as AssistExpected;
}

/** Text of a fixture sheet (line 1 is the first line of the file). */
export function sheetText(key: SheetKey): string {
  return readFileSync(path.join(assistDir, readAssistExpected().sheets[key].file), 'utf-8').replace(/\r\n/gu, '\n');
}

/** Creates the fixture sheet `key` with its exact title (the fake script key) and language. */
export async function createFixtureSheet(lyricAssistApi: LyricAssistApi, key: SheetKey): Promise<LyricsSheetDto> {
  const facts = readAssistExpected().sheets[key];
  return lyricAssistApi.createSheet(facts.title, facts.language, sheetText(key));
}

/** Title prefix of the corpus track (its upload file name). */
export const corpusTrackTitle = 'Paper Lanterns';

/** 006 lyrics states in which a paste is accepted (not `searching` or `aligning`). */
const settledLyrics = ['none', 'found', 'instrumental', 'notFound', 'alignPending', 'failed', 'disabled', 'removed'] as const;

/** Upper bound of the automatic 006 lookup and CPU alignment of the fake worker (as tests/lyrics.spec.ts). */
const lyricsSettleMs = 150_000;

let corpusTrack: Promise<TrackDto> | undefined;

async function seedCorpusTrack(api: StemStudioApi, lyricsApi: LyricsApi): Promise<TrackDto> {
  const facts = readAssistExpected().corpus['t-fake-song'];
  const text = readFileSync(path.join(assistDir, facts.file), 'utf-8');
  const track = await api.ensureFileCompleted(taggedWav(`corpus-${runId()}`, `${corpusTrackTitle} qa${runId()}`, null), fakeJobTimeoutMs);
  // Decision 023-P0-10: the corpus text goes into a track through 006 setLyrics (the fake 006
  // lyrics have 3-word lines and never trip the guard). The automatic 006 lookup of the new track
  // refuses a paste while it searches or aligns (409 lyrics-busy), so wait until it settled.
  await lyricsApi.waitForStatus(track.id, settledLyrics, lyricsSettleMs);
  expect(await lyricsApi.paste(track.id, text, 'en')).toBe(202);
  return track;
}

/** The track whose 006 lyrics are the `t-fake-song` corpus text (seeded once per worker and run). */
export function ensureCorpusTrack(api: StemStudioApi, lyricsApi: LyricsApi): Promise<TrackDto> {
  corpusTrack ??= seedCorpusTrack(api, lyricsApi);
  return corpusTrack;
}

/** Skips unless the stack's assistant model backend is `backend` (the fake stack runs `fake`). */
export async function requireModelBackend(lyricAssistApi: LyricAssistApi, backend: LyricAssistStatusDto['model']['backend']): Promise<LyricAssistStatusDto> {
  const status = await lyricAssistApi.status();
  test.skip(status.model.backend !== backend, `needs the assistant backend "${backend}" (STEMSTUDIO_AGENT_BACKEND); the stack runs "${status.model.backend}"`);
  return status;
}

/** Skips unless the stack reports the Claude notice as required (the fake Claude double, quickstart Q6 step 3). */
export async function requireClaudeDouble(lyricAssistApi: LyricAssistApi): Promise<void> {
  const status = await lyricAssistApi.status();
  test.skip(status.model.backend !== 'claude', `needs the fake Claude double (backend "claude" with the test handler, devops T069); the stack runs "${status.model.backend}"`);
  await lyricAssistApi.setClaudeNotice(false);
}

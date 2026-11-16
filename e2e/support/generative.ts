/**
 * Fake generator modes of the stack under test (features 009/010, data-model 1) and access to
 * the generative fixtures: the voice samples (`tests/fixtures/voice-manifest.json`), the own
 * lyrics (`tests/fixtures/lyrics/sample-*.txt`, `sample.lrc`), the ethics cases and the fake
 * generator expectations (`tests/fixtures/generative/`).
 *
 * @packageDocumentation
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from '@playwright/test';

/** `STEMSTUDIO_FAKE_GENERATOR_MODE` of a fake generator (and fake singer). */
export type FakeGeneratorMode = 'ready' | 'no-gpu' | 'oom' | 'oom-twice' | 'busy' | 'slow' | 'unvoiced-sample';

/** Files written by `tests/fixtures/generate_voice_fixtures.py`. */
export type VoiceFixtureName = 'own-voice-20s.wav' | 'own-voice-6s.wav' | 'own-voice-70s.wav' | 'unvoiced-20s.wav' | 'sung-target-10s.wav';

/** Problem type the API answers for a voice sample (`ok` = accepted). */
export type SampleCheck = 'ok' | 'too-short' | 'too-long' | 'unvoiced';

/** Known facts of one voice fixture. */
export interface VoiceFixtureFacts {
  readonly kind: 'sung-voice' | 'noise';
  readonly durationSeconds: number;
  readonly voicedShare: number;
  readonly expectedCheck: SampleCheck;
}

/** Shape of `voice-manifest.json`. */
export interface VoiceManifest {
  readonly licence: string;
  readonly limits: { readonly minSeconds: number; readonly maxSeconds: number; readonly minVoicedRatio: number };
  readonly fixtures: Readonly<Record<VoiceFixtureName, VoiceFixtureFacts>>;
}

/** One style text the imitation guard must refuse. */
export interface RefusedText {
  readonly field: 'name' | 'description' | 'subGenre' | 'freeCaption' | 'delivery';
  readonly text: string;
  readonly source: 'spec' | 'qa-proposal';
}

/** Shape of `generative/ethics-cases.json`. */
export interface EthicsCases {
  readonly imitationRefused: readonly RefusedText[];
  readonly songTextsRefused: readonly RefusedText[];
  readonly genreNamesAccepted: readonly string[];
  readonly presetForbiddenPhrases: readonly string[];
  readonly libraryArtist: { readonly fixture: string; readonly artist: string; readonly refusedDescription: string };
  readonly aiLabels: {
    readonly flacTags: readonly string[];
    readonly songVoiceTag: string;
    readonly badgeText: string;
    readonly convertedProfileDeletedSuffix: string;
  };
  readonly consent: { readonly statementVersion: string; readonly textEn: string; readonly textNl: string; readonly outdatedVersion: string };
}

/** Shape of `lyrics/own-lyrics-expected.json`. */
export interface OwnLyricsExpected {
  readonly 'sample-en.txt': {
    readonly language: string;
    readonly words: number;
    readonly proposedMarkers: readonly string[];
    readonly wordsPerSecondAt120s: number;
    readonly densityWarningBelowSeconds: number;
  };
  readonly 'sample-nl.txt': { readonly language: string; readonly markers: readonly string[] };
  readonly 'sample.lrc': { readonly normalisedText: string };
  readonly maxCharacters: number;
}

const modes: readonly FakeGeneratorMode[] = ['ready', 'no-gpu', 'oom', 'oom-twice', 'busy', 'slow', 'unvoiced-sample'];
const fixtureRoot = path.resolve(__dirname, '..', '..', 'tests', 'fixtures');

/**
 * Parses a fake generator mode; unset or empty means `ready` (the generator default).
 * @throws Error naming the allowed values when `value` is none of them.
 */
export function parseFakeGeneratorMode(value: string | undefined): FakeGeneratorMode {
  const mode = value === undefined || value === '' ? 'ready' : value;
  const known = modes.find((candidate) => candidate === mode);
  if (known === undefined) {
    throw new Error(`fake generator mode "${mode}" is not one of ${modes.join(', ')}`);
  }
  return known;
}

/** Mode of the stack: `E2E_FAKE_GENERATOR_MODE`, else `STEMSTUDIO_FAKE_GENERATOR_MODE`, else `ready`. */
export const fakeGeneratorMode: FakeGeneratorMode = parseFakeGeneratorMode(
  process.env.E2E_FAKE_GENERATOR_MODE ?? process.env.STEMSTUDIO_FAKE_GENERATOR_MODE,
);

/** Skips unless the stack's fake generator runs one of `wanted`. */
export function requireGeneratorMode(...wanted: FakeGeneratorMode[]): void {
  test.skip(!wanted.includes(fakeGeneratorMode), `needs STEMSTUDIO_FAKE_GENERATOR_MODE=${wanted.join('|')}; the stack runs "${fakeGeneratorMode}"`);
}

function readJson(...parts: string[]): unknown {
  return JSON.parse(readFileSync(path.join(fixtureRoot, ...parts), 'utf-8'));
}

/**
 * `value`, or an error naming `what` when it is null or undefined (instead of a non-null
 * assertion, so a missing element fails with a readable message).
 */
export function present<T>(value: T | null | undefined, what: string): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is missing`);
  }
  return value;
}

/**
 * The artist name the guard knows for a library track: the track's own artist, else the artist of
 * its band info lookup (005; the fake online mode `found` answers "The Fixture Band", no real
 * artist). Skips when there is none.
 */
export function requireLibraryArtist(names: readonly (string | null | undefined)[]): string {
  const name = names.find((candidate) => (candidate ?? '').trim().length > 1);
  test.skip(name === undefined, 'the track has no artist name (neither its own nor from the band info lookup)');
  return name ?? '';
}

/** Reads the committed voice manifest. */
export function readVoiceManifest(): VoiceManifest {
  return readJson('voice-manifest.json') as VoiceManifest;
}

/** Reads the ethics test cases (imitation guard, presets, labels). */
export function readEthicsCases(): EthicsCases {
  return readJson('generative', 'ethics-cases.json') as EthicsCases;
}

/** The parts of `generative/fake-generator.json` the specs read. */
export interface FakeGeneratorExpectations {
  readonly modes: {
    readonly 'no-gpu': { readonly reasonPattern: string; readonly createProblem: string };
    readonly 'oom-twice': { readonly restyle: { readonly errorCode: string; readonly messagePattern: string } };
    readonly busy: { readonly restyle: { readonly errorCode: string; readonly messagePattern: string } };
  };
}

/** Reads the fake generator expectations. */
export function readFakeGenerator(): FakeGeneratorExpectations {
  return readJson('generative', 'fake-generator.json') as FakeGeneratorExpectations;
}

/** Reads the expected facts of the own-lyrics fixtures. */
export function readOwnLyricsExpected(): OwnLyricsExpected {
  return readJson('lyrics', 'own-lyrics-expected.json') as OwnLyricsExpected;
}

/** Text of an own-lyrics fixture (`sample-en.txt`, `sample-nl.txt`, `sample.lrc`), LF line ends. */
export function readOwnLyrics(name: 'sample-en.txt' | 'sample-nl.txt' | 'sample.lrc'): string {
  return readFileSync(ownLyricsPath(name), 'utf-8').replace(/\r\n/gu, '\n');
}

/** Absolute path of an own-lyrics fixture (for file inputs). */
export function ownLyricsPath(name: 'sample-en.txt' | 'sample-nl.txt' | 'sample.lrc'): string {
  return path.join(fixtureRoot, 'lyrics', name);
}

/**
 * Absolute path of a generated voice fixture.
 * @throws Error naming the generator command when the audio has not been generated yet.
 */
export function voiceFixturePath(name: VoiceFixtureName): string {
  const file = voiceFixtureFile(name);
  if (!existsSync(file)) {
    throw new Error(`Fixture ${name} is missing; run: uv run --project worker python tests/fixtures/generate_voice_fixtures.py`);
  }
  return file;
}

/** Where a voice fixture is written (not checked: for launch options set before any test runs). */
export function voiceFixtureFile(name: VoiceFixtureName): string {
  return path.join(fixtureRoot, 'audio', name);
}

/** Words of a lyrics text (whitespace tokens), section-marker lines left out. */
export function lyricWords(text: string): number {
  const lines = text.split('\n').filter((line) => !/^\s*\[[^\]]*\]\s*$/u.test(line));
  return lines.join(' ').split(/\s+/u).filter((word) => word !== '').length;
}

/** A text of `length` characters made of self-written placeholder words (for the limit cases). */
export function placeholderLyrics(length: number): string {
  return 'la la la\n'.repeat(Math.ceil(length / 9)).slice(0, length);
}

/** A display or style name unique to this run, browser project and case. */
export function uniqueName(prefix: string, project: string, runIdValue: string): string {
  return `${prefix} ${project} ${runIdValue}`.slice(0, 60);
}

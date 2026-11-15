/**
 * Fake lyrics modes of the stack under test (feature 006, data-model.md 1) and the expectations
 * of the fake lyrics provider (`tests/fixtures/lyrics/fake-lyrics.json`, qa proposal for
 * audio-ingest 006:T028 and stem-separation 006:T041).
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from '@playwright/test';
import { fakeDeviceMode } from './env';

/** `STEMSTUDIO_FAKE_LYRICS` of a fake stack. */
export type FakeLyricsMode = 'word' | 'line' | 'plain' | 'instrumental' | 'not-found' | 'failed';

/** `Lyrics.status` / `Track.lyricsStatus` (backend-api 1.6.0 `LyricsStatus`). */
export type LyricsStatus =
  | 'none'
  | 'searching'
  | 'found'
  | 'instrumental'
  | 'notFound'
  | 'alignPending'
  | 'aligning'
  | 'failed'
  | 'disabled'
  | 'removed';

/** One timed word (ms from the start of the track). */
export interface TimedWord {
  readonly text: string;
  readonly startMs: number;
  readonly endMs: number;
}

/** One timed line. */
export interface TimedLine {
  readonly startMs: number;
  readonly endMs: number;
  readonly text: string;
  readonly words: readonly TimedWord[];
}

/** One mode row of the behaviour table. */
export interface FakeLyricsModeRow {
  readonly status: LyricsStatus;
  readonly credit?: string;
  readonly dataSync?: 'word' | 'line' | 'aligned';
  readonly alignedCredit?: string;
  readonly error?: string;
}

/** Shape of `fake-lyrics.json`. */
export interface FakeLyrics {
  readonly text: { readonly plainText: string; readonly pastedText: string };
  readonly modes: Readonly<Record<FakeLyricsMode, FakeLyricsModeRow>>;
  readonly fakeAlignment: { readonly firstWordMs: number; readonly wordMs: number };
  readonly countdownGapMs: number;
  readonly lines: readonly TimedLine[];
}

const modes: readonly FakeLyricsMode[] = ['word', 'line', 'plain', 'instrumental', 'not-found', 'failed'];
const fakeFile = path.resolve(__dirname, '..', '..', 'tests', 'fixtures', 'lyrics', 'fake-lyrics.json');

/**
 * Parses a fake lyrics mode; unset or empty means `word` (the worker default).
 * @throws Error naming the allowed values when `value` is none of them.
 */
export function parseFakeLyricsMode(value: string | undefined): FakeLyricsMode {
  const mode = value === undefined || value === '' ? 'word' : value;
  const known = modes.find((candidate) => candidate === mode);
  if (known === undefined) {
    throw new Error(`fake lyrics mode "${mode}" is not one of ${modes.join(', ')}`);
  }
  return known;
}

/** Mode of the stack under test: `E2E_FAKE_LYRICS`, else `STEMSTUDIO_FAKE_LYRICS`, else `word`. */
export const fakeLyricsMode: FakeLyricsMode = parseFakeLyricsMode(process.env.E2E_FAKE_LYRICS ?? process.env.STEMSTUDIO_FAKE_LYRICS);

/** Reads the committed behaviour table of the fake lyrics provider. */
export function readFakeLyrics(): FakeLyrics {
  return JSON.parse(readFileSync(fakeFile, 'utf-8')) as FakeLyrics;
}

/** Skips unless the stack's `STEMSTUDIO_FAKE_LYRICS` is one of `wanted`. */
export function requireLyricsMode(...wanted: FakeLyricsMode[]): void {
  test.skip(!wanted.includes(fakeLyricsMode), `needs STEMSTUDIO_FAKE_LYRICS=${wanted.join('|')}; the stack runs "${fakeLyricsMode}"`);
}

/** Skips unless the stack's fake device (`STEMSTUDIO_FAKE_DEVICE`, feature 003) is `device`. */
export function requireFakeDevice(device: 'cpu' | 'gpu'): void {
  test.skip(fakeDeviceMode !== device, `needs STEMSTUDIO_FAKE_DEVICE=${device}; the stack runs "${fakeDeviceMode}"`);
}

/** Words of `text` timed by the fake aligner: word k at `firstWordMs + k * wordMs`, line breaks kept. */
export function fakeAlignedLines(text: string, rule: FakeLyrics['fakeAlignment']): TimedLine[] {
  let index = 0;
  return text.split('\n').map((line) => {
    const words = line.split(/\s+/u).filter((w) => w !== '').map((word) => {
      const startMs = rule.firstWordMs + rule.wordMs * index++;
      return { text: word, startMs, endMs: startMs + rule.wordMs };
    });
    return { startMs: words[0]?.startMs ?? 0, endMs: words.at(-1)?.endMs ?? 0, text: line, words };
  });
}

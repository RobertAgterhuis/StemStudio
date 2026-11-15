/**
 * Validated, flattened karaoke timeline and the banner phase (countdown, singing, ended)
 * at a given time.
 *
 * @packageDocumentation
 */
import type { KaraokeLine, KaraokeLyrics, KaraokeState, KaraokeWord } from '../types';

/** Most lines in one document. */
export const MAX_LINES = 400;
/** Most words in one line. */
export const MAX_WORDS_PER_LINE = 40;
/** The countdown starts this long before a line (ms), one dot per second. */
export const COUNTDOWN_MS = 3000;
/** Gaps between lines longer than this show the countdown by default (ms). */
export const DEFAULT_COUNTDOWN_GAP_MS = 5000;

/** One word with its place in the document. */
export interface TimedWord extends KaraokeWord {
  /** Line index. */
  readonly line: number;
  /** Word index inside the line. */
  readonly index: number;
}

/** The document flattened for lookups. */
export interface Timeline {
  /** Lines as given. */
  readonly lines: readonly KaraokeLine[];
  /** All words in document order (starts never decrease). */
  readonly words: readonly TimedWord[];
  /** `startMs` of every line. */
  readonly lineStarts: readonly number[];
  /** `startMs` of every word in {@link Timeline.words}. */
  readonly wordStarts: readonly number[];
  /** Index in {@link Timeline.words} of the first word of every line. */
  readonly firstWord: readonly number[];
}

/** Banner state plus the number of lit countdown dots (0..3). */
export interface Phase {
  /** Host `data-state`. */
  readonly state: KaraokeState;
  /** Countdown dots shown. */
  readonly dots: number;
}

/**
 * Validates the document and flattens its words.
 *
 * @param lyrics - Lyrics passed to `attachKaraoke`.
 * @returns The timeline.
 * @throws Error `Invalid karaoke lyrics: <reason>` when the document breaks the contract.
 */
export function buildTimeline(lyrics: KaraokeLyrics): Timeline {
  const lines = checkedLines(lyrics);
  const words: TimedWord[] = [];
  const firstWord: number[] = [];
  lines.forEach((line, l) => {
    checkLine(line, l, lines[l - 1]);
    firstWord.push(words.length);
    line.words.forEach((word, index) => {
      checkWord({ word, line, label: `${String(l)}.${String(index)}` }, words.at(-1));
      words.push({ text: word.text, startMs: word.startMs, endMs: word.endMs, line: l, index });
    });
  });
  return { lines, words, lineStarts: lines.map((l) => l.startMs), wordStarts: words.map((w) => w.startMs), firstWord };
}

/**
 * @param gapMs - `KaraokeOptions.countdownGapMs`.
 * @returns The gap, {@link DEFAULT_COUNTDOWN_GAP_MS} when undefined.
 * @throws Error `Invalid karaoke options: ...` unless it is a number >= 0.
 */
export function validateCountdownGap(gapMs: number | undefined): number {
  const gap = gapMs ?? DEFAULT_COUNTDOWN_GAP_MS;
  if (!(gap >= 0)) {
    throw new Error(`Invalid karaoke options: countdownGapMs must be >= 0 (got ${String(gap)})`);
  }
  return gap;
}

/**
 * @param startMs - Start of the coming line.
 * @param t - Karaoke time (ms).
 * @returns Lit dots: 3, 2, 1 in the last three seconds before `startMs`, else 0.
 */
export function countdownDots(startMs: number, t: number): number {
  const remaining = startMs - t;
  return remaining > COUNTDOWN_MS || remaining <= 0 ? 0 : Math.ceil(remaining / 1000);
}

/**
 * @param timeline - The document.
 * @param line - Current line index (last line started, -1 before the first).
 * @param t - Karaoke time (ms).
 * @param gapMs - Gaps longer than this show the countdown.
 * @returns The banner phase.
 */
export function phaseAt(timeline: Timeline, line: number, t: number, gapMs: number): Phase {
  const { lines } = timeline;
  const last = lines.at(-1);
  if (last === undefined) {
    return { state: 'hidden', dots: 0 };
  }
  if (t >= last.endMs) {
    return { state: 'ended', dots: 0 };
  }
  const next = lines[line + 1];
  if (next !== undefined && countsDown(lines[line], next, t, gapMs)) {
    return { state: 'countdown', dots: countdownDots(next.startMs, t) };
  }
  return { state: 'singing', dots: 0 };
}

function countsDown(current: KaraokeLine | undefined, next: KaraokeLine, t: number, gapMs: number): boolean {
  if (current === undefined) {
    return true;
  }
  return next.startMs - current.endMs > gapMs && next.startMs - t <= COUNTDOWN_MS;
}

function fail(reason: string): never {
  throw new Error(`Invalid karaoke lyrics: ${reason}`);
}

function checkedLines(lyrics: KaraokeLyrics): readonly KaraokeLine[] {
  const lines: unknown = lyrics.lines;
  if (!Array.isArray(lines)) {
    fail('lines must be an array');
  }
  if (lines.length > MAX_LINES) {
    fail(`more than ${String(MAX_LINES)} lines`);
  }
  return lines as readonly KaraokeLine[];
}

function checkSpan(span: { startMs: number; endMs: number }, label: string): void {
  if (!Number.isFinite(span.startMs) || !Number.isFinite(span.endMs)) {
    fail(`${label} has invalid times`);
  }
  if (span.endMs <= span.startMs) {
    fail(`${label} ends before it starts`);
  }
}

function checkLine(line: KaraokeLine, l: number, previous: KaraokeLine | undefined): void {
  const label = `line ${String(l)}`;
  checkSpan(line, label);
  if (line.words.length === 0) {
    fail(`${label} has no words`);
  }
  if (line.words.length > MAX_WORDS_PER_LINE) {
    fail(`${label} has more than ${String(MAX_WORDS_PER_LINE)} words`);
  }
  if (previous !== undefined && line.startMs < previous.startMs) {
    fail(`${label} starts before line ${String(l - 1)}`);
  }
}

function checkWord(item: { word: KaraokeWord; line: KaraokeLine; label: string }, previous: TimedWord | undefined): void {
  const { word, line } = item;
  const label = `word ${item.label}`;
  checkSpan(word, label);
  if (word.startMs < line.startMs || word.endMs > line.endMs) {
    fail(`${label} lies outside its line`);
  }
  checkOrderAndText(word, label, previous);
}

function checkOrderAndText(word: KaraokeWord, label: string, previous: TimedWord | undefined): void {
  if (previous !== undefined && word.startMs < previous.startMs) {
    fail(`${label} starts before the previous word`);
  }
  const text: unknown = word.text;
  if (typeof text !== 'string') {
    fail(`${label} has no text`);
  }
}

/**
 * Small text-level readers for the MusicXML, PDF and MIDI files of a score item (feature 008,
 * T045-T070) and the bar count the fake transcriber must produce on a beat grid (data-model 2.4,
 * 2.6). Regular expressions over `score-partwise` output are enough for the facts the specs
 * assert; no XML library is added to the suite.
 *
 * @packageDocumentation
 */

/** A `<clef>` of the score. */
export interface Clef {
  readonly sign: string;
  readonly line: number | null;
  readonly octaveChange: number;
}

/** The beat grid of a track (backend `BeatGrid`). */
export interface GridDto {
  readonly beats: readonly number[];
  readonly downbeats: readonly number[];
  readonly beatsPerBar: number;
}

/** The first `<part>` of a `score-partwise` document. */
export function firstPart(xml: string): string {
  return /<part\b[^>]*>([\s\S]*?)<\/part>/u.exec(xml)?.[1] ?? '';
}

/** Number of `<measure>` elements of the first part (a pickup bar counts). */
export function measureCount(xml: string): number {
  return (firstPart(xml).match(/<measure\b/gu) ?? []).length;
}

/** Is the first measure a pickup (`implicit="yes"`)? */
export function hasPickup(xml: string): boolean {
  return /<measure\b[^>]*implicit="yes"/u.test(firstPart(xml).slice(0, 400));
}

function firstNumber(xml: string, tag: string): number | null {
  const match = new RegExp(`<${tag}>\\s*(-?\\d+)\\s*</${tag}>`, 'u').exec(xml);
  return match?.[1] === undefined ? null : Number(match[1]);
}

/** The first key signature (`<fifths>`), `null` without one (percussion). */
export function fifths(xml: string): number | null {
  return firstNumber(xml, 'fifths');
}

/** The first time signature as `beats/beat-type`, `null` without one. */
export function timeSignature(xml: string): string | null {
  const beats = firstNumber(xml, 'beats');
  const type = firstNumber(xml, 'beat-type');
  return beats === null || type === null ? null : `${String(beats)}/${String(type)}`;
}

/** Number of staves (`<staves>`, 1 when absent). */
export function staves(xml: string): number {
  return firstNumber(xml, 'staves') ?? 1;
}

/** Every `<clef>` in document order. */
export function clefs(xml: string): Clef[] {
  return [...xml.matchAll(/<clef\b[^>]*>([\s\S]*?)<\/clef>/gu)].map(([, body = '']) => ({
    sign: /<sign>(\w+)<\/sign>/u.exec(body)?.[1] ?? '',
    line: firstNumber(body, 'line'),
    octaveChange: firstNumber(body, 'clef-octave-change') ?? 0,
  }));
}

/** The distinct `<type>` values of notes and rests (e.g. `quarter`, `16th`). */
export function noteTypes(xml: string): Set<string> {
  return new Set([...xml.matchAll(/<type\b[^>]*>([\w-]+)<\/type>/gu)].map(([, t = '']) => t));
}

/** The distinct `<voice>` numbers. */
export function voices(xml: string): Set<number> {
  return new Set([...xml.matchAll(/<voice>(\d+)<\/voice>/gu)].map(([, v = '0']) => Number(v)));
}

/** Number of `<notehead>x</notehead>` elements (hi-hat and cymbals). */
export function crossNoteheads(xml: string): number {
  return (xml.match(/<notehead\b[^>]*>x<\/notehead>/gu) ?? []).length;
}

/** Index of the beat nearest to the first downbeat (`k0`, data-model 2.4). */
export function firstDownbeatIndex(grid: GridDto): number {
  const first = grid.downbeats[0] ?? grid.beats[0] ?? 0;
  let best = 0;
  grid.beats.forEach((beat, index) => {
    if (Math.abs(beat - first) < Math.abs((grid.beats[best] ?? 0) - first)) {
      best = index;
    }
  });
  return best;
}

/**
 * Bars the fake transcriber's score must have on `grid`: one note on every beat (data-model
 * 2.6), bar 1 at the first downbeat, a pickup bar when beats precede it (data-model 2.4).
 */
export function expectedFakeBars(grid: GridDto): number {
  const k0 = firstDownbeatIndex(grid);
  const pickup = k0 > 0 ? 1 : 0;
  return pickup + Math.ceil((grid.beats.length - k0) / grid.beatsPerBar);
}

/** Does the file start with the PDF signature? */
export function isPdf(bytes: Buffer): boolean {
  return bytes.subarray(0, 4).toString('latin1') === '%PDF';
}

/** Does the file start with the Standard MIDI File signature? */
export function isMidi(bytes: Buffer): boolean {
  return bytes.subarray(0, 4).toString('latin1') === 'MThd';
}

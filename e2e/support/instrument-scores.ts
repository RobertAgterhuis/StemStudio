/**
 * Score requests with the feature 016 extensions (backend-api 1.15.0: `tablature`, `part`, `includeChords`, the
 * profiles `strings`/`brass`/`woodwinds`) and MusicXML/MIDI readers for the tab, part and chord specs.
 *
 * @packageDocumentation
 */
import type { AssetsApi } from './assets-api';
import type { AudioSourceDto, ScoreApi } from './score-api';

/** Contract `ScoreProfile` with the 016 profiles. */
export type InstrumentScoreProfile = 'vocals' | 'bass' | 'guitar' | 'keys' | 'piano' | 'drums' | 'strings' | 'brass' | 'woodwinds';

/** Contract `ScoreTablature`. */
export interface ScoreTablatureDto {
  readonly tuning: string;
  readonly capo?: number;
  readonly layout?: 'notation-and-tab' | 'tab-only';
}

/** Contract `ScorePart`. */
export interface ScorePartDto {
  readonly instrument: string;
  readonly clef?: 'auto' | 'treble' | 'bass' | 'alto' | 'tenor' | 'treble-8vb' | 'bass-8vb';
}

/** Contract `CreateScoreRequest` with the 016 fields. */
export interface InstrumentScoreRequest {
  readonly source: AudioSourceDto;
  readonly profile?: InstrumentScoreProfile;
  readonly tablature?: ScoreTablatureDto;
  readonly part?: ScorePartDto;
  readonly includeChords?: boolean;
}

/** Creates a score item, waits until it is ready and returns its id. */
export async function readyScoreId(scoreApi: ScoreApi, assetsApi: AssetsApi, request: InstrumentScoreRequest, timeoutMs: number): Promise<string> {
  // The 008 client types the body without the 016 fields; the API takes them (additive, backend 1.15.0).
  const created = await scoreApi.create(request as Parameters<ScoreApi['create']>[0]);
  await assetsApi.waitForReady(created.id, timeoutMs);
  return created.id;
}

/** Creates a score item and returns its MusicXML once ready. */
export async function musicXmlOf(scoreApi: ScoreApi, assetsApi: AssetsApi, request: InstrumentScoreRequest, timeoutMs: number): Promise<string> {
  return scoreApi.musicXml(await readyScoreId(scoreApi, assetsApi, request, timeoutMs));
}

/** Every `<fret>` value of a MusicXML document. */
export function frets(xml: string): number[] {
  return [...xml.matchAll(/<fret>(\d+)<\/fret>/g)].map((m) => Number(m[1]));
}

/** Every `<staff-tuning>` of a MusicXML document as `{line, step, octave}`. */
export function staffTunings(xml: string): { readonly line: number; readonly step: string; readonly octave: number }[] {
  return [...xml.matchAll(/<staff-tuning line="(\d+)">\s*<tuning-step>([A-G])<\/tuning-step>(?:\s*<tuning-alter>-?\d+<\/tuning-alter>)?\s*<tuning-octave>(\d+)<\/tuning-octave>/g)]
    .map((m) => ({ line: Number(m[1]), step: m[2] ?? '', octave: Number(m[3]) }));
}

/** The `<staff-lines>` value of the first TAB staff, or null. */
export function tabStaffLines(xml: string): number | null {
  const match = /<staff-lines>(\d+)<\/staff-lines>/.exec(xml);
  return match ? Number(match[1]) : null;
}

/**
 * True when a Standard MIDI File holds a note-on of `note` on `channel` (1-16) with an explicit status byte (the first
 * note-on of a track always has one; the 008 writer does not use running status across channels).
 */
export function hasNoteOn(bytes: Buffer, channel: number, note: number): boolean {
  const status = 0x90 + channel - 1;
  return bytes.some((byte, i) => byte === status && bytes[i + 1] === note && (bytes[i + 2] ?? 0) > 0);
}

/** A note of a TAB staff: its pitch (MIDI) and the string and fret it was given. */
export interface TabNote {
  readonly midi: number;
  readonly string: number;
  readonly fret: number;
}

const stepSemitones: Readonly<Record<string, number>> = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 };

/** MIDI number of a MusicXML `step`/`alter`/`octave`. */
export function midiOf(step: string, alter: number, octave: number): number {
  return 12 * (octave + 1) + (stepSemitones[step] ?? 0) + alter;
}

function pitchOf(body: string): number | null {
  const pitch = /<pitch>\s*<step>([A-G])<\/step>(?:\s*<alter>(-?\d+)<\/alter>)?\s*<octave>(\d+)<\/octave>/u.exec(body);
  return pitch ? midiOf(pitch[1] ?? 'C', Number(pitch[2] ?? 0), Number(pitch[3])) : null;
}

function tabNote(body: string): TabNote[] {
  const midi = pitchOf(body);
  const string = /<string>(\d+)<\/string>/u.exec(body);
  const fret = /<fret>(\d+)<\/fret>/u.exec(body);
  return midi !== null && string && fret ? [{ midi, string: Number(string[1]), fret: Number(fret[1]) }] : [];
}

/** Every note with `<string>` and `<fret>` (the TAB staff notes), in document order. */
export function tabNotes(xml: string): TabNote[] {
  return [...xml.matchAll(/<note\b[^>]*>([\s\S]*?)<\/note>/gu)].flatMap(([, body = '']) => tabNote(body));
}

/** Open-string pitches (MIDI) of the first TAB staff, lowest line (= highest string number) first. */
export function tuningPitches(xml: string): number[] {
  const tunings = [...xml.matchAll(/<staff-tuning line="(\d+)">\s*<tuning-step>([A-G])<\/tuning-step>(?:\s*<tuning-alter>(-?\d+)<\/tuning-alter>)?\s*<tuning-octave>(\d+)<\/tuning-octave>/gu)];
  return tunings
    .map((m) => ({ line: Number(m[1]), midi: midiOf(m[2] ?? 'C', Number(m[3] ?? 0), Number(m[4])) }))
    .sort((a, b) => a.line - b.line)
    .map((t) => t.midi);
}

/** The sounding pitch of a TAB note: string 1 is the highest string (the top line). */
export function soundingPitch(note: TabNote, tuning: readonly number[], capo: number): number {
  return (tuning[tuning.length - note.string] ?? Number.NaN) + capo + note.fret;
}

/** The `<chromatic>` of the first `<transpose>` (written -> sounding), or null without one. */
export function chromaticTranspose(xml: string): number | null {
  const match = /<transpose\b[^>]*>[\s\S]*?<chromatic>(-?\d+)<\/chromatic>/u.exec(xml);
  return match ? Number(match[1]) : null;
}

/** The `<part-name>` of the first part. */
export function partName(xml: string): string {
  return /<part-name\b[^>]*>([^<]*)<\/part-name>/u.exec(xml)?.[1] ?? '';
}

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

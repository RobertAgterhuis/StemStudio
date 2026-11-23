/**
 * Backend client and helpers for the feature 016 instrument-view routes (backend-api 1.15.0, tag `instrument-views`;
 * `specs/016-instrument-views/contracts/backend-api.openapi.yaml`). Used for set-up and server-state assertions; the UI is
 * driven through `pages/instrument-views.ts`. Waits always poll the analysis the spec started or the track's own
 * analysis until a terminal status (LESSONS qa: never trust a status already set at load).
 *
 * @packageDocumentation
 */
import { expect, test, type APIRequestContext } from '@playwright/test';
import type { StemStudioApi, TrackDto } from './api';
import { apiBaseUrl } from './env';
import { okJson } from './http';

/** Contract `AnalysisStatus`. */
export type AnalysisStatus = 'none' | 'queued' | 'running' | 'ready' | 'failed' | 'unavailable' | 'stale';

/** Display suffix per contract `ChordQuality` (data-model 2.1). */
const suffix = {
  maj: '', min: 'm', dim: 'dim', aug: 'aug', min6: 'm6', maj6: '6', min7: 'm7', minmaj7: 'm(maj7)', maj7: 'maj7', '7': '7', dim7: 'dim7', hdim7: 'm7b5', sus2: 'sus2', sus4: 'sus4',
} as const;

/** Contract `ChordQuality`. */
export type ChordQuality = keyof typeof suffix;

/** Contract `KeyMode`. */
export type KeyMode = 'major' | 'minor';

/** Contract `InstrumentFamily` (ui-test-ids placeholder `<family>`). */
export type InstrumentFamily = 'vocals' | 'drums' | 'bass' | 'guitar' | 'keys' | 'orchestral';

/** Contract `ChordSegment`. */
export interface ChordSegmentDto {
  readonly startMs: number;
  readonly endMs: number;
  readonly label: string;
  readonly root: number | null;
  readonly quality: ChordQuality | null;
  readonly bass: number | null;
  readonly confidence: number;
  readonly edited: boolean;
}

/** Contract `KeyRegion`. */
export interface KeyRegionDto {
  readonly startMs: number;
  readonly endMs: number;
  readonly tonic: number;
  readonly mode: KeyMode;
  readonly confidence: number;
}

/** Contract `ChordChart` document of `ChordsAnalysis`. */
export interface ChordChartDto {
  readonly vocabulary: 'simple' | 'full';
  readonly key: { readonly tonic: number; readonly mode: KeyMode; readonly confidence: number } | null;
  readonly keyRegions: readonly KeyRegionDto[];
  readonly noChords: boolean;
  readonly hasEdits: boolean;
  readonly segments: readonly ChordSegmentDto[];
}

/** Contract `TrackAnalysisBase` with a document. */
export interface TrackAnalysisDto<T> {
  readonly kind: 'chords' | 'drumHits' | 'vocalPitch';
  readonly status: AnalysisStatus;
  readonly reason: string | null;
  readonly progress: number;
  readonly limits: string;
  readonly document: T | null;
}

/** Contract `DrumHits` document. */
export interface DrumHitsDto {
  readonly pieces: readonly string[];
  readonly hits: readonly { readonly timeMs: number; readonly piece: string; readonly strength: number }[];
}

/** Contract `VocalPitch` document. */
export interface VocalPitchDto {
  readonly hopMs: number;
  readonly frames: readonly number[];
  readonly source: 'vocals.lead' | 'vocals';
}

/** Contract `ChordSheet`. */
export interface ChordSheetDto {
  readonly transpose: number;
  readonly capo: number;
  readonly rows: readonly { readonly text: string; readonly chords: readonly { readonly charIndex: number; readonly symbol: string; readonly lead: boolean }[] }[];
}

/** Contract `KeyView`. */
export interface KeyViewDto {
  readonly status: AnalysisStatus;
  readonly transpose: number;
  readonly display: string | null;
  readonly writtenKey: string | null;
  readonly regions: readonly { readonly startMs: number; readonly startBar: number | null; readonly display: string }[];
}

/** Contract `CapoSuggestions`. */
export interface CapoSuggestionsDto {
  readonly transpose: number;
  readonly shortcut: { readonly capo: number; readonly reason: string } | null;
  readonly suggestions: readonly { readonly capo: number; readonly shapeKey: string; readonly openShare: number; readonly fewOpenShapes: boolean }[];
}

/** Terminal statuses of an analysis. */
const settled: readonly AnalysisStatus[] = ['ready', 'failed', 'unavailable', 'stale', 'none'];

/** Sharp spelling of a pitch class (C major / D major regions of the fake chords, data-model 2.5). */
const sharpNames = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'] as const;

/** Returns the value, or fails the test naming what was missing. */
export function required<T>(value: T | null | undefined, what: string): T {
  if (value === null || value === undefined) {
    throw new Error(`${what} is missing`);
  }

  return value;
}

/** The symbol of a segment in a sharps key (the fake song is in C major, then D major), "N.C." for no chord. */
export function sharpSymbol(segment: Pick<ChordSegmentDto, 'root' | 'quality' | 'bass'>): string {
  if (segment.root === null || segment.quality === null) {
    return 'N.C.';
  }

  return `${noteName(segment.root)}${suffix[segment.quality]}${slash(segment.root, segment.bass)}`;
}

function noteName(pitchClass: number): string {
  return sharpNames[pitchClass] ?? '';
}

function slash(root: number, bass: number | null): string {
  return bass === null || bass === root ? '' : `/${noteName(bass)}`;
}

/** Polls `read` until `done` holds and returns the last value. */
async function pollUntil<T>(read: () => Promise<T>, done: (value: T) => boolean, timeoutMs: number): Promise<T> {
  let last: T | undefined;
  await expect.poll(async () => {
    last = await read();
    return done(last);
  }, { timeout: timeoutMs, intervals: [500, 1000] }).toBe(true);
  return required(last, 'polled value');
}

/** Expects a ready analysis and returns its document. */
function readyDocument<T>(analysis: TrackAnalysisDto<T>, what: string): T {
  expect(analysis.status, analysis.reason ?? what).toBe('ready');
  return required(analysis.document, `${what} document`);
}

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class InstrumentViewsApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/tracks/{id}/chords`. */
  async chords(trackId: string): Promise<TrackAnalysisDto<ChordChartDto>> {
    return okJson<TrackAnalysisDto<ChordChartDto>>(await this.request.get(`/api/tracks/${trackId}/chords`));
  }

  /** Polls the track's chords until a terminal status and expects `ready` (the backend detects them on its own, FR-010). */
  async waitForChords(trackId: string, timeoutMs: number): Promise<ChordChartDto> {
    const analysis = await pollUntil(() => this.chords(trackId), (a) => settled.includes(a.status) && a.status !== 'none', timeoutMs);
    return readyDocument(analysis, 'chords');
  }

  /** `PUT /api/tracks/{id}/chords/segments/{index}`. */
  async editSegment(trackId: string, index: number, label: string): Promise<TrackAnalysisDto<ChordChartDto>> {
    return okJson<TrackAnalysisDto<ChordChartDto>>(await this.request.put(`/api/tracks/${trackId}/chords/segments/${String(index)}`, { data: { label } }));
  }

  /** `GET /api/tracks/{id}/chords/sheet`. */
  async sheet(trackId: string, transpose = 0, capo = 0): Promise<ChordSheetDto> {
    return okJson<ChordSheetDto>(await this.request.get(`/api/tracks/${trackId}/chords/sheet`, { params: { transpose, capo } }));
  }

  /** `GET` of an absolute or relative ChordPro URL (the UI's download link), as text. */
  async text(url: string): Promise<string> {
    const response = await this.request.get(url);
    expect(response.ok(), `${url} -> ${String(response.status())}`).toBe(true);
    return response.text();
  }

  /** `GET /api/tracks/{id}/key`. */
  async key(trackId: string, transpose = 0, instrument?: string): Promise<KeyViewDto> {
    const params: Record<string, string | number> = instrument === undefined ? { transpose } : { transpose, instrument };
    return okJson<KeyViewDto>(await this.request.get(`/api/tracks/${trackId}/key`, { params }));
  }

  /** `GET /api/tracks/{id}/chords/capo-suggestions`. */
  async capo(trackId: string, transpose: number): Promise<CapoSuggestionsDto> {
    return okJson<CapoSuggestionsDto>(await this.request.get(`/api/tracks/${trackId}/chords/capo-suggestions`, { params: { transpose } }));
  }

  /** `GET /api/tracks/{id}/drum-hits`. */
  async drumHits(trackId: string): Promise<TrackAnalysisDto<DrumHitsDto>> {
    return okJson<TrackAnalysisDto<DrumHitsDto>>(await this.request.get(`/api/tracks/${trackId}/drum-hits`));
  }

  /** Polls the drum hits until ready (started by the backend after the drum split). */
  async waitForDrumHits(trackId: string, timeoutMs: number): Promise<DrumHitsDto> {
    const analysis = await pollUntil(() => this.drumHits(trackId), (a) => a.status === 'ready' || a.status === 'failed', timeoutMs);
    return readyDocument(analysis, 'drum hits');
  }

  /** `GET /api/tracks/{id}/vocal-pitch`. */
  async vocalPitch(trackId: string): Promise<TrackAnalysisDto<VocalPitchDto>> {
    return okJson<TrackAnalysisDto<VocalPitchDto>>(await this.request.get(`/api/tracks/${trackId}/vocal-pitch`));
  }

  /** `POST /api/tracks/{id}/vocal-pitch` when needed, then polls until ready. */
  async ensureVocalPitch(trackId: string, timeoutMs: number): Promise<VocalPitchDto> {
    if ((await this.vocalPitch(trackId)).status !== 'ready') {
      const started = await this.request.post(`/api/tracks/${trackId}/vocal-pitch`);
      expect([202, 409]).toContain(started.status());
    }

    const analysis = await pollUntil(() => this.vocalPitch(trackId), (a) => a.status === 'ready' || a.status === 'failed', timeoutMs);
    return readyDocument(analysis, 'vocal pitch');
  }

  /** Skips the test while the stack has no 016 backend (`/api/instruments/catalogue` answers 404). */
  async requireFeature(): Promise<void> {
    const status = (await this.request.get('/api/instruments/catalogue')).status();
    test.skip(status === 404 || status === 405, `instrument views not deployed on this stack (catalogue -> ${String(status)})`);
  }
}

/** Accessors of the per-test API context and instrument-views client registered by {@link useInstrumentApi}. */
export interface InstrumentApiSuite {
  readonly views: () => InstrumentViewsApi;
  readonly context: () => APIRequestContext;
}

/**
 * Registers the `beforeEach`/`afterEach` hooks of an instrument spec: test timeout, an API context, the client, and the
 * skip while the stack has no 016 backend. Returns accessors valid inside a test.
 */
export function useInstrumentApi(timeoutMs: number): InstrumentApiSuite {
  let context: APIRequestContext | undefined;
  let views: InstrumentViewsApi | undefined;
  test.beforeEach(async ({ playwright }) => {
    test.setTimeout(timeoutMs);
    context = await playwright.request.newContext({ baseURL: apiBaseUrl });
    views = new InstrumentViewsApi(context);
    await views.requireFeature();
  });
  test.afterEach(async () => {
    await context?.dispose();
  });
  return { views: () => required(views, 'instrument views client'), context: () => required(context, 'API context') };
}

/** A completed track and its ready chord chart (the backend detects the chords after the separation). */
export async function chordTrack(api: StemStudioApi, views: InstrumentViewsApi, file: string, timeoutMs: number): Promise<{ readonly track: TrackDto; readonly chart: ChordChartDto }> {
  const track = await api.ensureFileCompleted(file, timeoutMs);
  return { track, chart: await views.waitForChords(track.id, timeoutMs) };
}

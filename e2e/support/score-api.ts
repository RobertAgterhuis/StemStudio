/**
 * Backend client for the feature 008 sheet music routes (`specs/008-sheet-music/contracts/backend-api.openapi.yaml`
 * 1.7.0): `GET /api/scores/options`, `POST /api/assets/scores` and the score files of an asset
 * (`/api/assets/{id}/files/{pdf|musicxml|midi}`). Used for set-up, downloads and server-state
 * assertions; the UI is driven through the page objects.
 *
 * @packageDocumentation
 */
import { expect, test, type APIRequestContext } from '@playwright/test';
import type { AssetDto } from './assets-api';
import { okJson } from './http';

/** Contract `ScoreProfile`. */
export type ScoreProfile = 'vocals' | 'bass' | 'guitar' | 'keys' | 'piano' | 'drums';

/** Score file roles (`AssetFileRole` 1.7.0 additions). */
export type ScoreFileRole = 'pdf' | 'musicxml' | 'midi';

/** Contract `AudioSource`. */
export type AudioSourceDto = { readonly trackId: string; readonly channel: string } | { readonly assetId: string };

/** Contract `ScoreFacts` (the parts the suite reads). */
export interface ScoreFactsDto {
  readonly profile: ScoreProfile;
  readonly engine: string;
  readonly key: { readonly tonic: string; readonly mode: string; readonly confidence: number; readonly source: string } | null;
  readonly bpm: number;
  readonly timeSignature: string;
  readonly bars: number;
  readonly notes: number;
  readonly resolution: 'sixteenth' | 'eighth';
  readonly warnings: readonly { readonly code: string; readonly text: string }[];
}

/** An asset of kind `score` (the 007 `Asset` plus `score` and the provenance links). */
export interface ScoreAssetDto extends AssetDto {
  readonly score: ScoreFactsDto | null;
  readonly provenance: AssetDto['provenance'] & { readonly sourceAssetIds: readonly string[] };
}

/** Contract `ScoreOptions` (the parts the suite reads). */
export interface ScoreOptionsDto {
  readonly suggestedProfile: ScoreProfile | null;
  readonly allowedProfiles: readonly ScoreProfile[];
  readonly drumsRequireSplit: boolean;
  readonly gridStatus: string;
}

/** Contract `CreateScoreRequest`. */
export interface CreateScoreDto {
  readonly source: AudioSourceDto;
  readonly profile?: ScoreProfile | null;
  readonly resolution?: 'sixteenth' | 'eighth' | null;
  readonly keyOverride?: string | null;
  readonly name?: string | null;
}

/** A downloaded score file. */
export interface ScoreFile {
  readonly bytes: Buffer;
  readonly contentType: string | undefined;
  readonly disposition: string | undefined;
}

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class ScoreApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/scores/options` for a track channel. */
  async options(trackId: string, channel: string): Promise<ScoreOptionsDto> {
    return okJson<ScoreOptionsDto>(await this.request.get('/api/scores/options', { params: { trackId, channel } }));
  }

  /** `POST /api/assets/scores`; returns the created (202) item. */
  async create(body: CreateScoreDto): Promise<ScoreAssetDto> {
    return okJson<ScoreAssetDto>(await this.request.post('/api/assets/scores', { data: body }));
  }

  /** `GET /api/assets/{id}` as a score item. */
  async get(id: string): Promise<ScoreAssetDto> {
    return okJson<ScoreAssetDto>(await this.request.get(`/api/assets/${id}`));
  }

  /** `GET /api/assets/{id}/files/{role}` (attachment unless `inline`), asserting 200. */
  async file(id: string, role: ScoreFileRole, inline = false): Promise<ScoreFile> {
    const url = `/api/assets/${id}/files/${role}?inline=${String(inline)}`;
    const response = await this.request.get(url);
    expect(response.ok(), `${url} -> ${String(response.status())}`).toBe(true);
    const { 'content-type': contentType, 'content-disposition': disposition } = response.headers();
    return { bytes: await response.body(), contentType, disposition };
  }

  /** The MusicXML of a ready item as text. */
  async musicXml(id: string): Promise<string> {
    return (await this.file(id, 'musicxml')).bytes.toString('utf-8');
  }

  /** Skips the test while the stack has no 008 backend (`/api/scores/options` answers 404 or 405). */
  async requireFeature(trackId: string): Promise<void> {
    const status = (await this.request.get('/api/scores/options', { params: { trackId, channel: 'bass' } })).status();
    test.skip(status === 404 || status === 405, `sheet music API not deployed on this stack (options -> ${String(status)})`);
  }
}

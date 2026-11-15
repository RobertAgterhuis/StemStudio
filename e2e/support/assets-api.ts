/**
 * Backend client for the feature 007 library routes (`specs/007-export-library/contracts/backend-api.openapi.yaml`
 * 1.5.0, `/api/assets/**`). Used for set-up, for downloading exported files and for asserting
 * server state the UI cannot show (for example that a deleted item is gone).
 *
 * @packageDocumentation
 */
import { expect, type APIRequestContext } from '@playwright/test';
import type { ChannelSettingsDto, DetailChannelSettingsDto, ParentStemName } from './api';
import { okJson } from './http';
import { oneOf } from './text';

/** `AssetStatus`; `ready` and `failed` are terminal. */
export type AssetStatus = 'queued' | 'rendering' | 'ready' | 'failed';

/** `AudioFileFormat`. */
export type AudioFileFormat = 'flac' | 'wav' | 'mp3';

/** Contract `ExportFormat`. */
export interface ExportFormatDto {
  readonly format: AudioFileFormat;
  readonly bitDepth?: 16 | 24 | null;
  readonly mp3BitrateKbps?: 192 | 256 | 320 | null;
}

/** Contract `ExportMix` (current mixer state). */
export interface ExportMixDto {
  readonly masterVolume: number;
  readonly channels: readonly ChannelSettingsDto[];
  readonly expanded?: readonly ParentStemName[];
  readonly detailChannels?: readonly DetailChannelSettingsDto[];
}

/** Contract `CreateAudioExportRequest`. */
export interface CreateAudioExportDto {
  readonly trackId: string;
  readonly mode: 'raw' | 'mix';
  readonly channels?: readonly string[] | null;
  readonly mix?: ExportMixDto | null;
  readonly mixId?: string | null;
  readonly range?: { readonly startSeconds: number; readonly endSeconds: number } | null;
  readonly format: ExportFormatDto;
  readonly name?: string | null;
}

/** Contract `AudioFacts`. */
export interface AudioFactsDto {
  readonly format: AudioFileFormat;
  readonly bitDepth: number | null;
  readonly durationSeconds: number;
  readonly peakDbfs: number;
  readonly appliedGainDb: number;
}

/** Contract `Asset` (the parts the suite reads). */
export interface AssetDto {
  readonly id: string;
  readonly name: string;
  readonly status: AssetStatus;
  readonly progress: number;
  readonly error: string | null;
  readonly files: readonly { readonly role: 'audio'; readonly contentType: string; readonly sizeBytes: number; readonly url: string }[];
  readonly audio: AudioFactsDto | null;
  readonly provenance: { readonly trackId: string | null; readonly trackAvailable: boolean; readonly contents: string };
  readonly sizeBytes: number;
}

/** Contract `AssetList`. */
export interface AssetListDto {
  readonly items: readonly AssetDto[];
  readonly total: number;
  readonly totalSizeBytes: number;
}

/** A downloaded file: its bytes and the `Content-Disposition` file name. */
export interface DownloadedFile {
  readonly bytes: Buffer;
  readonly contentType: string | undefined;
  readonly disposition: string | undefined;
}

const terminal: readonly AssetStatus[] = ['ready', 'failed'];
const pollIntervals = [500, 1000];

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class AssetsApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `POST /api/assets/audio-exports`; returns the created (202) item. */
  async createExport(body: CreateAudioExportDto): Promise<AssetDto> {
    return okJson<AssetDto>(await this.request.post('/api/assets/audio-exports', { data: body }));
  }

  /** `POST /api/assets/stem-packs`; returns the created items. */
  async createStemPack(trackId: string, includeDetail: boolean, format: ExportFormatDto): Promise<AssetDto[]> {
    const data = { trackId, includeDetail, format };
    return (await okJson<{ items: AssetDto[] }>(await this.request.post('/api/assets/stem-packs', { data }))).items;
  }

  /** `GET /api/assets` with optional filters. */
  async list(filters: { trackId?: string; q?: string } = {}): Promise<AssetListDto> {
    return okJson<AssetListDto>(await this.request.get('/api/assets', { params: { ...filters } }));
  }

  /** `GET /api/assets/{id}`; `undefined` when the item does not exist (404). */
  async find(id: string): Promise<AssetDto | undefined> {
    const response = await this.request.get(`/api/assets/${id}`);
    return response.status() === 404 ? undefined : okJson<AssetDto>(response);
  }

  /** `GET /api/assets/{id}`. */
  async get(id: string): Promise<AssetDto> {
    return okJson<AssetDto>(await this.request.get(`/api/assets/${id}`));
  }

  /** Polls until the item is ready or failed; returns it. */
  async waitForTerminal(id: string, timeoutMs: number): Promise<AssetDto> {
    const status = (): Promise<string> => this.get(id).then((asset) => asset.status);
    await expect.poll(status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(oneOf(terminal));
    return this.get(id);
  }

  /** {@link waitForTerminal} and assert `ready`. */
  async waitForReady(id: string, timeoutMs: number): Promise<AssetDto> {
    const asset = await this.waitForTerminal(id, timeoutMs);
    expect(asset.status, `${asset.name}: ${asset.error ?? ''}`).toBe('ready');
    return asset;
  }

  /** `GET /api/assets/{id}/files/audio` (attachment). */
  async download(id: string): Promise<DownloadedFile> {
    const response = await this.request.get(`/api/assets/${id}/files/audio`);
    expect(response.status(), response.url()).toBe(200);
    const h = response.headers();
    return { bytes: await response.body(), contentType: h['content-type'], disposition: h['content-disposition'] };
  }

  /** Body of any API URL (for example a stem `url` of a track), asserting 200. */
  async bytes(url: string): Promise<Buffer> {
    const response = await this.request.get(url);
    expect(response.status(), response.url()).toBe(200);
    return response.body();
  }

  /** `DELETE /api/assets/{id}`; returns the HTTP status. */
  async delete(id: string): Promise<number> {
    return (await this.request.delete(`/api/assets/${id}`)).status();
  }
}

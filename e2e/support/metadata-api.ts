/**
 * Backend client for the feature 005 routes (`specs/005-artwork-band-info/contracts/backend-api.openapi.yaml`
 * 1.4.0): track artwork, band info and the metadata lookup. Used for set-up and for asserting
 * server state; the UI is driven through the page objects.
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, type APIRequestContext } from '@playwright/test';
import type { TrackDto } from './api';
import type { MetadataStatus } from './online';
import { okJson } from './http';
import { oneOf } from './text';

/** `ArtworkSource` (1.4.0) plus `none` for "no picture" (ui-test-ids 1.4.0 `<artworkSource>`). */
export type ArtworkSource = 'user' | 'spotify' | 'embedded' | 'coverartarchive' | 'youtube';

/** Contract `Artwork`. */
export interface ArtworkDto {
  readonly source: ArtworkSource;
  readonly version: string;
  readonly coverUrl: string;
  readonly thumbUrl: string;
}

/** Contract `TrackMetadata`. */
export interface TrackMetadataDto {
  readonly status: MetadataStatus;
  readonly reason?: string | null;
  readonly album?: string | null;
  readonly year?: number | null;
  readonly artistName?: string | null;
  readonly correctedByUser: boolean;
}

/** `Track` with the 1.4.0 members. */
export type TrackWithArtworkDto = TrackDto & {
  readonly artwork?: ArtworkDto | null;
  readonly metadata: TrackMetadataDto;
};

/** Contract `BandInfo` (the parts the suite reads). */
export interface BandInfoDto {
  readonly status: MetadataStatus;
  readonly reason?: string | null;
  readonly recording?: { readonly title: string; readonly album?: string | null; readonly year?: number | null } | null;
  readonly artist?: { readonly name: string; readonly type?: string | null; readonly members: readonly { readonly name: string }[] } | null;
  readonly sources: readonly { readonly name: string; readonly url: string; readonly licence?: string | null }[];
}

/** Result of an artwork upload: the HTTP status and, on 200, the track. */
export interface ArtworkUploadResult {
  readonly status: number;
  readonly track: TrackWithArtworkDto | null;
}

/** Response headers and size of one artwork variant. */
export interface ArtworkResponse {
  readonly status: number;
  readonly contentType: string | undefined;
  readonly etag: string | undefined;
  readonly cacheControl: string | undefined;
  readonly bytes: number;
}

const settled: readonly MetadataStatus[] = ['found', 'notFound', 'failed', 'disabled'];
const pollIntervals = [500, 1000];

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class MetadataApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/tracks/{id}` with the 1.4.0 members. */
  async getTrack(id: string): Promise<TrackWithArtworkDto> {
    return okJson<TrackWithArtworkDto>(await this.request.get(`/api/tracks/${id}`));
  }

  /** `GET /api/tracks/{id}/band-info`. */
  async getBandInfo(id: string): Promise<BandInfoDto> {
    return okJson<BandInfoDto>(await this.request.get(`/api/tracks/${id}/band-info`));
  }

  /** `POST /api/tracks/{id}/metadata/lookup`; returns the HTTP status (202, 409, 422). */
  async lookup(id: string, names: { title?: string; artist?: string } = {}): Promise<number> {
    return (await this.request.post(`/api/tracks/${id}/metadata/lookup`, { data: names })).status();
  }

  /** Polls until the metadata status is settled (found, notFound, failed, disabled). */
  async waitForMetadata(id: string, timeoutMs: number): Promise<TrackWithArtworkDto> {
    const status = (): Promise<string> => this.getTrack(id).then((t) => t.metadata.status);
    await expect.poll(status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(oneOf(settled));
    return this.getTrack(id);
  }

  /** `PUT /api/tracks/{id}/artwork` with a local picture file. */
  async setArtwork(id: string, file: string, mimeType = 'application/octet-stream'): Promise<ArtworkUploadResult> {
    const multipart = { file: { name: path.basename(file), mimeType, buffer: readFileSync(file) } };
    const response = await this.request.put(`/api/tracks/${id}/artwork`, { multipart });
    const track = response.ok() ? ((await response.json()) as TrackWithArtworkDto) : null;
    return { status: response.status(), track };
  }

  /** `DELETE /api/tracks/{id}/artwork` (idempotent). */
  async resetArtwork(id: string): Promise<TrackWithArtworkDto> {
    return okJson<TrackWithArtworkDto>(await this.request.delete(`/api/tracks/${id}/artwork`));
  }

  /** `GET` of an artwork URL (`coverUrl`/`thumbUrl`), optionally conditional. */
  async fetchArtwork(url: string, ifNoneMatch?: string): Promise<ArtworkResponse> {
    const headers = ifNoneMatch === undefined ? {} : { 'If-None-Match': ifNoneMatch };
    const response = await this.request.get(url, { headers });
    const body = response.status() === 200 ? await response.body() : Buffer.alloc(0);
    const h = response.headers();
    return { status: response.status(), contentType: h['content-type'], etag: h.etag, cacheControl: h['cache-control'], bytes: body.length };
  }
}

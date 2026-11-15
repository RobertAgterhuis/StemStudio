/**
 * Backend client for the feature 006 lyrics routes (`specs/006-synced-lyrics-karaoke/contracts/backend-api.openapi.yaml`
 * 1.6.0, `/api/tracks/{trackId}/lyrics/**`). Used for set-up and for asserting server state; the
 * UI is driven through the page objects. Lyrics responses are `no-store`.
 *
 * @packageDocumentation
 */
import { expect, test, type APIRequestContext, type APIResponse } from '@playwright/test';
import { okJson } from './http';
import type { LyricsStatus, TimedLine } from './lyrics';

/** Contract `JobDevice` (003) as the lyrics alignment reports it. */
export interface LyricsDeviceDto {
  readonly kind: 'cpu' | 'gpu';
  readonly name?: string | null;
}

/** Contract `LyricsAlignment`. */
export interface LyricsAlignmentDto {
  readonly estimateSeconds: number | null;
  readonly device: LyricsDeviceDto | null;
  readonly progress: number | null;
}

/** Contract `Lyrics` (the parts the suite reads). */
export interface LyricsDto {
  readonly status: LyricsStatus;
  readonly reason: string | null;
  readonly syncLevel: 'word' | 'line' | 'plain' | null;
  readonly origin: 'source' | 'interpolated' | 'aligned' | 'none' | null;
  readonly source: 'lrclib' | 'user' | null;
  readonly credit: string | null;
  readonly offsetMs: number;
  readonly instrumental: boolean;
  readonly lines: readonly TimedLine[];
  readonly alignment: LyricsAlignmentDto | null;
}

const pollIntervals = [500, 1000];

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class LyricsApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/tracks/{id}/lyrics`. */
  async get(trackId: string): Promise<LyricsDto> {
    return okJson<LyricsDto>(await this.request.get(this.url(trackId)));
  }

  /** Raw `GET` response, for header checks (`Cache-Control: no-store`). */
  async getResponse(trackId: string): Promise<APIResponse> {
    return this.request.get(this.url(trackId));
  }

  /** Polls until the lyrics status is one of `statuses`; returns the document. */
  async waitForStatus(trackId: string, statuses: readonly LyricsStatus[], timeoutMs: number): Promise<LyricsDto> {
    const status = (): Promise<LyricsStatus> => this.get(trackId).then((lyrics) => lyrics.status);
    await expect.poll(status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(new RegExp(`^(${statuses.join('|')})$`, 'u'));
    return this.get(trackId);
  }

  /** `PUT .../lyrics` (paste); returns the HTTP status. */
  async paste(trackId: string, text: string, language: string | null = 'en'): Promise<number> {
    return (await this.request.put(this.url(trackId), { data: { text, language } })).status();
  }

  /** `POST .../lyrics/align`; returns the HTTP status. */
  async align(trackId: string): Promise<number> {
    return (await this.request.post(`${this.url(trackId)}/align`)).status();
  }

  /** `POST .../lyrics/search`; returns the HTTP status. */
  async search(trackId: string): Promise<number> {
    return (await this.request.post(`${this.url(trackId)}/search`)).status();
  }

  /** `DELETE .../lyrics`; returns the HTTP status. */
  async remove(trackId: string): Promise<number> {
    return (await this.request.delete(this.url(trackId))).status();
  }

  /** `PUT .../lyrics/offset`; returns the HTTP status. */
  async setOffset(trackId: string, offsetMs: number): Promise<number> {
    return (await this.request.put(`${this.url(trackId)}/offset`, { data: { offsetMs } })).status();
  }

  /** Skips the test while the stack has no 006 backend (the lyrics route answers 404 or 405). */
  async requireFeature(trackId: string): Promise<void> {
    const status = (await this.getResponse(trackId)).status();
    test.skip(status === 404 || status === 405, `lyrics API not deployed on this stack (GET lyrics -> ${String(status)})`);
  }

  private url(trackId: string): string {
    return `/api/tracks/${trackId}/lyrics`;
  }
}

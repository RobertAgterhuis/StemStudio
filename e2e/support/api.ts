/**
 * Thin client for the backend HTTP API (`contracts/backend-api.openapi.yaml`), used only for test
 * set-up (a completed fixture track) and for asserting server state the UI cannot show
 * (for example "no track was created").
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { fixturePath, type FixtureName } from './fixtures';

/** Track status values of the contract (`TrackStatus`). */
export type TrackStatus = 'queued' | 'downloading' | 'separating' | 'analysing' | 'completed' | 'failed';

/** Stem names of the contract (`StemName`). */
export type StemName = 'vocals' | 'drums' | 'bass' | 'guitar' | 'piano' | 'other';

/** Subset of the contract `Track` the suite reads. */
export interface TrackDto {
  readonly id: string;
  readonly title: string;
  readonly status: TrackStatus;
  readonly progress: number;
  readonly error?: string | null;
  readonly durationSeconds?: number | null;
  readonly model?: 'htdemucs_6s' | 'htdemucs' | 'fake' | null;
  readonly stems: readonly { readonly name: StemName; readonly url: string }[];
  readonly beatGrid?: { readonly bpm: number; readonly beatsPerBar: number } | null;
  readonly updatedAt: string;
}

/** Contract `MixSummary`. */
export interface MixSummaryDto {
  readonly id: string;
  readonly trackId: string;
  readonly trackTitle: string;
  readonly name: string;
  readonly updatedAt: string;
}

const terminal: readonly TrackStatus[] = ['completed', 'failed'];

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class StemStudioApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/tracks`. */
  async listTracks(): Promise<TrackDto[]> {
    return json<TrackDto[]>(await this.request.get('/api/tracks'));
  }

  /** `GET /api/tracks/{id}`. */
  async getTrack(id: string): Promise<TrackDto> {
    return json<TrackDto>(await this.request.get(`/api/tracks/${id}`));
  }

  /** `POST /api/imports/upload` with a fixture; returns the created (202) or cached (200) track. */
  async upload(name: FixtureName): Promise<TrackDto> {
    const file = fixturePath(name);
    const buffer = readFileSync(file);
    const multipart = { file: { name: path.basename(file), mimeType: 'application/octet-stream', buffer } };
    return json<TrackDto>(await this.request.post('/api/imports/upload', { multipart }));
  }

  /** `POST /api/tracks/{id}/retry`. */
  async retry(id: string): Promise<TrackDto> {
    return json<TrackDto>(await this.request.post(`/api/tracks/${id}/retry`));
  }

  /** Polls `GET /api/tracks/{id}` until the track is completed or failed. */
  async waitForTerminal(id: string, timeoutMs: number): Promise<TrackDto> {
    await expect
      .poll(async () => (await this.getTrack(id)).status, { timeout: timeoutMs, intervals: [500, 1000] })
      .toMatch(new RegExp(`^(${terminal.join('|')})$`));
    return this.getTrack(id);
  }

  /**
   * Uploads a fixture through the API and waits until its track is completed, retrying once if a
   * previous run left it failed. Used as set-up by specs that do not test the import itself.
   */
  async ensureCompleted(name: FixtureName, timeoutMs: number): Promise<TrackDto> {
    let track = await this.waitForTerminal((await this.upload(name)).id, timeoutMs);
    if (track.status === 'failed') {
      track = await this.waitForTerminal((await this.retry(track.id)).id, timeoutMs);
    }
    expect(track.status, `fixture ${name}: ${track.error ?? ''}`).toBe('completed');
    return track;
  }

  /** `GET /api/mixes` (optionally of one track). */
  async listMixes(trackId?: string): Promise<MixSummaryDto[]> {
    const params = trackId === undefined ? undefined : { trackId };
    return json<MixSummaryDto[]>(await this.request.get('/api/mixes', params ? { params } : {}));
  }

  /** The mix of a track with exactly this name, or undefined. */
  async findMix(trackId: string, name: string): Promise<MixSummaryDto | undefined> {
    return (await this.listMixes(trackId)).find((mix) => mix.name === name);
  }
}

async function json<T>(response: APIResponse): Promise<T> {
  expect(response.ok(), `${response.url()} -> ${String(response.status())}`).toBe(true);
  return (await response.json()) as T;
}

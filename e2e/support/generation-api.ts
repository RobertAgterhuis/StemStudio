/**
 * Backend client for the feature 009 routes (`specs/010-lyrics-to-song/contracts/backend-api.openapi.yaml`
 * 1.9.0, a superset of 009's 1.8.0): generation status, the style library, restyles, generations
 * and their takes. Used for set-up and for asserting server state; the UI is driven through the
 * page objects.
 *
 * @packageDocumentation
 */
import { expect, test, type APIRequestContext, type APIResponse } from '@playwright/test';
import type { StemName, TrackDto } from './api';
import { present } from './generative';
import { okJson } from './http';

/** Contract `GenerationAvailability`. */
export type GenerationAvailability = 'notInstalled' | 'unavailable' | 'ready';

/** Contract `GenerationStatus` (the parts the suite reads). */
export interface GenerationStatusDto {
  readonly state: GenerationAvailability;
  readonly reason: string | null;
  readonly downloadBytesNeeded: number;
  readonly voice: { readonly state: GenerationAvailability; readonly reason: string | null };
}

/** Contract `Strength`. */
export type Strength = 'subtle' | 'light' | 'balanced' | 'strong' | 'free';

/** Contract `StyleInput`. */
export interface StyleInputDto {
  readonly name: string;
  readonly genreFamily: string;
  readonly subGenre?: string | null;
  readonly tempoFeel: 'keep' | 'laidback' | 'driving' | 'halfTime' | 'swing';
  readonly instrumentation: readonly string[];
  readonly moods: readonly string[];
  readonly vocalHandling: 'keep' | 'instrumental';
  readonly defaultStrength: Strength;
  readonly description?: string | null;
}

/** Contract `Style`. */
export interface StyleDto extends StyleInputDto {
  readonly id: string;
  readonly kind: 'preset' | 'user';
  readonly presetKey: string | null;
}

/** Contract `GenerationState`. */
export type GenerationState = 'queued' | 'waitingForStems' | 'waitingForGpu' | 'downloadingModel' | 'running' | 'completed' | 'failed' | 'cancelled';

/** Contract `VocalLabel`. */
export type VocalLabel = 'none' | 'synthetic' | 'converted';

/** Contract `GenerationTake`. */
export interface TakeDto {
  readonly index: number;
  readonly seed: number;
  readonly durationSeconds: number;
  readonly audioUrl: string;
  readonly libraryAssetId: string | null;
  readonly importedTrackId: string | null;
  readonly model: { readonly id: string; readonly revision: string };
  readonly aiGenerated: true;
  readonly vocal: VocalLabel | null;
  readonly voiceProfileName: string | null;
  readonly vocalAudioUrl: string | null;
}

/** Contract `Generation` (the parts the suite reads; details members as loose records). */
export interface GenerationDto {
  readonly id: string;
  readonly kind: 'restyle' | 'song' | 'voiceConversion';
  readonly status: GenerationState;
  readonly progress: number;
  readonly stage: string | null;
  readonly restyle?: {
    readonly sourceTrackId: string | null;
    readonly sourceTrackTitle: string;
    readonly includedStems: readonly StemName[];
    readonly style: { readonly name: string; readonly kind: string };
    readonly strength: Strength;
    readonly range: { readonly startSeconds: number; readonly endSeconds: number; readonly mode: string };
  } | null;
  readonly song?: { readonly lyrics: { readonly text: string; readonly title: string; readonly language: string }; readonly instrumental: boolean; readonly durationSeconds: number } | null;
  readonly voiceConversion?: { readonly voiceProfileId: string | null; readonly voiceProfileName: string; readonly importedTrackId: string | null } | null;
  readonly takes: readonly TakeDto[];
  readonly warnings: readonly string[];
  readonly error: { readonly code: string; readonly message: string } | null;
}

/** Contract `RestyleRequest`. */
export interface RestyleRequestDto {
  readonly styleId: string;
  readonly mode: 'range' | 'inPlace';
  readonly rangeStartSeconds?: number | null;
  readonly rangeEndSeconds?: number | null;
  readonly includedStems?: readonly StemName[] | null;
  readonly strength?: Strength | null;
  readonly takes?: number;
}

/** Contract `TrackOrigin` (1.8.0/1.9.0). */
export interface TrackOriginDto {
  readonly kind: 'imported' | 'generated';
  readonly generationId: string | null;
  readonly takeIndex: number | null;
  readonly sourceTrackId: string | null;
  readonly aiGenerated: boolean;
  readonly vocal: VocalLabel | null;
  readonly voiceProfileName: string | null;
  readonly lyricsSheetId: string | null;
}

/** A track with the 1.8.0 `origin` member (absent on imported tracks) and the 005 artist. */
export type GeneratedTrackDto = TrackDto & { readonly origin?: TrackOriginDto; readonly artist?: string | null };

/** RFC 9457 problem body (`type` is the kebab-case problem type). */
export interface ProblemDto {
  readonly type?: string;
  readonly title: string;
  readonly status: number;
  readonly detail?: string | null;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

/** Terminal generation states. */
export const terminalStates: readonly GenerationState[] = ['completed', 'failed', 'cancelled'];

const pollIntervals = [500, 1000];

/** Reads a problem body and asserts its HTTP status. */
export async function expectProblem(response: APIResponse, status: number): Promise<ProblemDto> {
  expect(response.status(), `${response.url()} -> ${String(response.status())}`).toBe(status);
  return (await response.json()) as ProblemDto;
}

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class GenerationApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/generation/status`. */
  async status(): Promise<GenerationStatusDto> {
    return okJson<GenerationStatusDto>(await this.request.get('/api/generation/status'));
  }

  /** Skips the test while the stack has no 009 backend (the status route answers 404 or 405). */
  async requireFeature(): Promise<void> {
    const code = (await this.request.get('/api/generation/status')).status();
    test.skip(code === 404 || code === 405, `generation API not deployed on this stack (GET /api/generation/status -> ${String(code)})`);
  }

  /** {@link requireFeature}, then skips unless the generator's availability is `state`. */
  async requireState(state: GenerationAvailability): Promise<GenerationStatusDto> {
    await this.requireFeature();
    const status = await this.status();
    test.skip(status.state !== state, `needs a generator in state "${state}"; the stack reports "${status.state}"`);
    return status;
  }

  /** {@link requireFeature}, then skips unless the singer's availability is `state`. */
  async requireVoiceState(state: GenerationAvailability): Promise<GenerationStatusDto> {
    await this.requireFeature();
    const status = await this.status();
    test.skip(status.voice.state !== state, `needs a singer in state "${state}"; the stack reports "${status.voice.state}"`);
    return status;
  }

  /** `GET /api/styles` with optional filters (`kind`, `genreFamily`, `mood`, `instrument`, `q`). */
  async listStyles(params: Readonly<Record<string, string>> = {}): Promise<StyleDto[]> {
    return okJson<StyleDto[]>(await this.request.get('/api/styles', { params: { ...params } }));
  }

  /** The preset named `name`. */
  async preset(name: string): Promise<StyleDto> {
    const found = (await this.listStyles({ kind: 'preset' })).find((style) => style.name === name);
    return present(found, `preset "${name}"`);
  }

  /** `POST /api/styles`; the raw response (201 or a problem). */
  async createStyle(input: StyleInputDto): Promise<APIResponse> {
    return this.request.post('/api/styles', { data: input });
  }

  /** `PUT /api/styles/{id}`; the raw response. */
  async updateStyle(id: string, input: StyleInputDto): Promise<APIResponse> {
    return this.request.put(`/api/styles/${id}`, { data: input });
  }

  /** `DELETE /api/styles/{id}`; the HTTP status. */
  async deleteStyle(id: string): Promise<number> {
    return (await this.request.delete(`/api/styles/${id}`)).status();
  }

  /** `POST /api/styles/{id}/duplicate`; the copy. */
  async duplicateStyle(id: string): Promise<StyleDto> {
    return okJson<StyleDto>(await this.request.post(`/api/styles/${id}/duplicate`));
  }

  /** `POST /api/tracks/{id}/restyles`; the raw response (202 or a problem). */
  async restyle(trackId: string, body: RestyleRequestDto): Promise<APIResponse> {
    return this.request.post(`/api/tracks/${trackId}/restyles`, { data: body });
  }

  /** {@link restyle} asserting 202; the queued generation. */
  async restyleOk(trackId: string, body: RestyleRequestDto): Promise<GenerationDto> {
    return okJson<GenerationDto>(await this.restyle(trackId, body));
  }

  /** `GET /api/generations/{id}`. */
  async get(id: string): Promise<GenerationDto> {
    return okJson<GenerationDto>(await this.request.get(`/api/generations/${id}`));
  }

  /** `GET /api/generations/{id}`; `undefined` on 404. */
  async find(id: string): Promise<GenerationDto | undefined> {
    const response = await this.request.get(`/api/generations/${id}`);
    return response.status() === 404 ? undefined : okJson<GenerationDto>(response);
  }

  /** `GET /api/generations` (newest first) with optional `kind` / `sourceTrackId`. */
  async list(params: { kind?: GenerationDto['kind']; sourceTrackId?: string } = {}): Promise<GenerationDto[]> {
    return okJson<GenerationDto[]>(await this.request.get('/api/generations', { params: { ...params } }));
  }

  /** Polls until the newest generation matching `params` exists and is not `before`; returns it. */
  async waitForNewest(params: { kind?: GenerationDto['kind']; sourceTrackId?: string }, before: readonly string[], timeoutMs = 30_000): Promise<GenerationDto> {
    const newest = (): Promise<string> => this.list(params).then((all) => all.find((g) => !before.includes(g.id))?.id ?? '');
    await expect.poll(newest, { timeout: timeoutMs, intervals: pollIntervals }).not.toBe('');
    return this.get(await newest());
  }

  /** Polls until the generation's status is one of `statuses`; returns it. */
  async waitForStatus(id: string, statuses: readonly GenerationState[], timeoutMs: number): Promise<GenerationDto> {
    const status = (): Promise<string> => this.get(id).then((g) => g.status);
    await expect.poll(status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(new RegExp(`^(${statuses.join('|')})$`, 'u'));
    return this.get(id);
  }

  /** {@link waitForStatus} on the terminal states, asserting `completed` (the message names the error). */
  async waitForCompleted(id: string, timeoutMs: number): Promise<GenerationDto> {
    const generation = await this.waitForStatus(id, terminalStates, timeoutMs);
    expect(generation.status, `${generation.error?.code ?? ''}: ${generation.error?.message ?? ''}`).toBe('completed');
    return generation;
  }

  /** `POST /api/generations/{id}/cancel`; the HTTP status. */
  async cancel(id: string): Promise<number> {
    return (await this.request.post(`/api/generations/${id}/cancel`)).status();
  }

  /** `POST /api/generations/{id}/retry`; the new generation. */
  async retry(id: string): Promise<GenerationDto> {
    return okJson<GenerationDto>(await this.request.post(`/api/generations/${id}/retry`));
  }

  /** `DELETE /api/generations/{id}`; the HTTP status. */
  async delete(id: string): Promise<number> {
    return (await this.request.delete(`/api/generations/${id}`)).status();
  }

  /** Raw `GET` of any API URL (take audio, take vocal, sample). */
  async fetch(url: string): Promise<APIResponse> {
    return this.request.get(url);
  }

  /** Bytes of a take's audio file, asserting 200. */
  async takeBytes(take: TakeDto): Promise<Buffer> {
    const response = await this.request.get(take.audioUrl);
    expect(response.status(), response.url()).toBe(200);
    return response.body();
  }

  /** `POST /api/generations/{id}/takes/{n}/import`; the HTTP status (202 new, 200 existing) and the track. */
  async importTake(id: string, index: number): Promise<{ readonly status: number; readonly track: GeneratedTrackDto }> {
    const response = await this.request.post(`/api/generations/${id}/takes/${String(index)}/import`);
    return { status: response.status(), track: await okJson<GeneratedTrackDto>(response) };
  }

  /** `GET /api/tracks/{id}` with `origin` and `artist`. */
  async track(id: string): Promise<GeneratedTrackDto> {
    return okJson<GeneratedTrackDto>(await this.request.get(`/api/tracks/${id}`));
  }

  /**
   * `GET /api/assets/{id}` as loose JSON (provenance origin and generation link) plus the HTTP
   * status as `httpStatus` (the asset's own `status` member is its `ready`/`pending` state).
   */
  async libraryItem(assetId: string): Promise<{ readonly httpStatus: number; readonly provenance?: { readonly origin: string; readonly generation?: { readonly generationId: string } } }> {
    const response = await this.request.get(`/api/assets/${assetId}`);
    const body = response.ok() ? ((await response.json()) as object) : {};
    return { ...body, httpStatus: response.status() };
  }
}

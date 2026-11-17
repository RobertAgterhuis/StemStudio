/**
 * Thin client for the track, import, mix and device endpoints of the backend HTTP API
 * (`specs/007-export-library/contracts/backend-api.openapi.yaml` 1.5.0, which keeps every 1.3.0
 * path of feature 003 unchanged), used only for test set-up (a completed fixture track, an
 * old-style mix) and for asserting server state the UI cannot show (for example "no track was
 * created"). The metadata/artwork (1.4.0) and asset (1.5.0) endpoints live in
 * `metadata-api.ts` and `assets-api.ts`.
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { fixturePath, type DetailFixtureName, type FixtureName } from './fixtures';
import { oneOf } from './text';

/** Track status values of the contract (`TrackStatus`). */
export type TrackStatus = 'queued' | 'downloading' | 'separating' | 'analysing' | 'completed' | 'failed';

/** Stem names of the contract (`StemName`). */
export type StemName = 'vocals' | 'drums' | 'bass' | 'guitar' | 'piano' | 'other';

/** Standard stems that can be split further (`ParentStemName`, 1.2.0). */
export type ParentStemName = 'vocals' | 'drums' | 'guitar' | 'piano' | 'other';

/** Detail stem names of backend-api 1.2.0 in display order per family; `rest` is always last. */
export const detailStemNames = [
  ...['kick', 'snare', 'toms', 'hihat', 'cymbals'],
  ...['lead', 'backing'],
  ...['strings', 'brass', 'woodwinds', 'keys', 'synth', 'acoustic-guitar', 'electric-guitar', 'percussion'],
  'rest',
] as const;

/** Detail stem name (`DetailStemName`, 1.2.0). */
export type DetailStemName = (typeof detailStemNames)[number];

/** Detail phase status (`DetailPhaseStatus`, 1.2.0). */
export type DetailPhaseStatus = 'none' | 'queued' | 'running' | 'completed' | 'failed' | 'cancelled';

/** Per-parent outcome kind (`ParentOutcomeKind`, 1.2.0). */
export type ParentOutcomeKind = 'split' | 'skipped-silent' | 'nothing-further' | 'not-reliable' | 'not-supported';

/** Contract `ParentOutcome`. */
export interface ParentOutcomeDto {
  readonly parent: StemName;
  readonly outcome: ParentOutcomeKind;
  readonly stems: readonly { readonly name: DetailStemName; readonly url: string }[];
}

/** Processing device kind (`ProcessingDeviceKind`, 1.3.0). */
export type ProcessingDeviceKind = 'cpu' | 'gpu';

/** Contract `JobDevice` (1.3.0): where a standard or detail job ran. */
export interface JobDeviceDto {
  readonly kind: ProcessingDeviceKind;
  readonly name?: string | null;
  readonly cpuFallback: boolean;
  readonly fallbackReason?: string | null;
}

/** Contract `ProcessingDevice` (1.3.0, `GET /api/device`). */
export interface ProcessingDeviceDto {
  readonly reachable: boolean;
  readonly kind?: ProcessingDeviceKind | null;
  readonly name?: string | null;
  readonly totalMemoryMiB?: number | null;
  readonly reason?: string | null;
}

/** Contract `DetailPhase`. */
export interface DetailPhaseDto {
  readonly status: DetailPhaseStatus;
  readonly stage?: string | null;
  readonly currentParent?: ParentStemName | null;
  readonly progress: number;
  readonly error?: string | null;
  readonly parents: readonly ParentOutcomeDto[];
  readonly device?: JobDeviceDto | null;
}

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
  readonly beatGrid?: BeatGridDto | null;
  readonly detail: DetailPhaseDto;
  readonly device?: JobDeviceDto | null;
  readonly createdAt: string;
  readonly updatedAt: string;
}

/** Contract `BeatGrid`; the 012 specs read the beat times (the older specs only bpm and bars). */
export interface BeatGridDto {
  readonly bpm: number;
  readonly beatsPerBar: number;
  readonly beats?: readonly number[];
  readonly downbeats?: readonly number[];
  readonly reliable?: boolean;
}

/** Contract `SpeedTrainerSettings` (backend-api 1.11.0, feature 012). */
export interface SpeedTrainerSettingsDto {
  readonly enabled: boolean;
  readonly startPercent: number;
  readonly stepPercent: number;
  readonly targetPercent: number;
  readonly passesPerStep: number;
}

/** Contract `CountInSettings` (1.11.0): `bars` 0 (off), 1 or 2. */
export interface CountInSettingsDto {
  readonly bars: number;
  readonly everyPass: boolean;
}

/** Contract `PracticeSettings` (1.11.0): optional in requests, always present in responses. */
export interface PracticeSettingsDto {
  readonly speedPercent: number;
  readonly transposeSemitones: number;
  readonly trainer: SpeedTrainerSettingsDto;
  readonly countIn: CountInSettingsDto;
}

/** Contract `ChannelSettings` (volume 0..1, pan -1..1). */
export interface ChannelSettingsDto {
  readonly stem: StemName;
  readonly volume: number;
  readonly pan: number;
  readonly mute: boolean;
  readonly solo: boolean;
}

/** Contract `DetailChannelSettings` (1.2.0). */
export interface DetailChannelSettingsDto {
  readonly parent: ParentStemName;
  readonly stem: DetailStemName;
  readonly volume: number;
  readonly pan: number;
  readonly mute: boolean;
  readonly solo: boolean;
}

/** Contract `MixSettings`; `expanded`/`detailChannels` are absent in the 1.1.0 shape. */
export interface MixSettingsDto {
  readonly name: string;
  readonly masterVolume: number;
  readonly channels: readonly ChannelSettingsDto[];
  readonly metronome: {
    readonly enabled: boolean;
    readonly volume: number;
    readonly bpmOverride: number | null;
    readonly offsetMs: number;
    readonly beatsPerBar: number;
  };
  readonly loop: { readonly start: number; readonly end: number; readonly enabled: boolean } | null;
  readonly expanded?: readonly ParentStemName[];
  readonly detailChannels?: readonly DetailChannelSettingsDto[];
  /** 1.11.0 (feature 012); absent in requests of the older shapes. */
  readonly practice?: PracticeSettingsDto;
}

/** Contract `Mix`. */
export interface MixDto {
  readonly id: string;
  readonly trackId: string;
  readonly settings: MixSettingsDto;
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
const pollIntervals = [500, 1000];

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class StemStudioApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/device` (1.3.0): the worker's active processing device. */
  async getDevice(): Promise<ProcessingDeviceDto> {
    return json<ProcessingDeviceDto>(await this.request.get('/api/device'));
  }

  /** `GET /api/tracks`. */
  async listTracks(): Promise<TrackDto[]> {
    return json<TrackDto[]>(await this.request.get('/api/tracks'));
  }

  /** `GET /api/tracks/{id}`. */
  async getTrack(id: string): Promise<TrackDto> {
    return json<TrackDto>(await this.request.get(`/api/tracks/${id}`));
  }

  /** `POST /api/imports/upload` with a fixture; returns the created (202) or cached (200) track. */
  async upload(name: FixtureName | DetailFixtureName): Promise<TrackDto> {
    return this.uploadFile(fixturePath(name));
  }

  /** `POST /api/imports/upload` with any local file (for example a unique copy of a fixture). */
  async uploadFile(file: string): Promise<TrackDto> {
    const buffer = readFileSync(file);
    const multipart = { file: { name: path.basename(file), mimeType: 'application/octet-stream', buffer } };
    return json<TrackDto>(await this.request.post('/api/imports/upload', { multipart }));
  }

  /** `POST /api/tracks/{id}/retry`. */
  async retry(id: string): Promise<TrackDto> {
    return json<TrackDto>(await this.request.post(`/api/tracks/${id}/retry`));
  }

  /** `DELETE /api/tracks/{id}`; returns the HTTP status (204 deleted, 409 refused). */
  async deleteTrack(id: string): Promise<number> {
    return (await this.request.delete(`/api/tracks/${id}`)).status();
  }

  /** Polls `GET /api/tracks/{id}` until the track is completed or failed. */
  async waitForTerminal(id: string, timeoutMs: number): Promise<TrackDto> {
    const status = (): Promise<string> => this.getTrack(id).then((track) => track.status);
    await expect.poll(status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(oneOf(terminal));
    return this.getTrack(id);
  }

  /**
   * Uploads a fixture through the API and waits until its track is completed, retrying once if a
   * previous run left it failed. Used as set-up by specs that do not test the import itself.
   */
  async ensureCompleted(name: FixtureName | DetailFixtureName, timeoutMs: number): Promise<TrackDto> {
    return this.ensureFileCompleted(fixturePath(name), timeoutMs);
  }

  /** {@link ensureCompleted} for any local file. */
  async ensureFileCompleted(file: string, timeoutMs: number): Promise<TrackDto> {
    let track = await this.waitForTerminal((await this.uploadFile(file)).id, timeoutMs);
    if (track.status === 'failed') {
      track = await this.waitForTerminal((await this.retry(track.id)).id, timeoutMs);
    }
    expect(track.status, `${path.basename(file)}: ${track.error ?? ''}`).toBe('completed');
    return track;
  }

  /** `POST /api/tracks/{id}/detail` (Split further / retry of the detail phase). */
  async startDetail(id: string): Promise<TrackDto> {
    return json<TrackDto>(await this.request.post(`/api/tracks/${id}/detail`));
  }

  /** `POST /api/tracks/{id}/detail/cancel`. */
  async cancelDetail(id: string): Promise<TrackDto> {
    return json<TrackDto>(await this.request.post(`/api/tracks/${id}/detail/cancel`));
  }

  /**
   * Cancels the track's detail phase when it is queued or running and waits until it has ended,
   * so the track can be deleted (a new import starts its detail phase, feature 002).
   */
  async stopDetail(id: string, timeoutMs: number): Promise<TrackDto> {
    if (['queued', 'running'].includes((await this.getTrack(id)).detail.status)) await this.cancelDetail(id);
    return this.waitForDetail(id, ['none', 'completed', 'failed', 'cancelled'], timeoutMs);
  }

  /** Polls until the track's detail phase has one of `statuses`; returns the track. */
  async waitForDetail(id: string, statuses: readonly DetailPhaseStatus[], timeoutMs: number): Promise<TrackDto> {
    const status = (): Promise<string> => this.getTrack(id).then((track) => track.detail.status);
    await expect.poll(status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(oneOf(statuses));
    return this.getTrack(id);
  }

  /** `POST /api/mixes`; returns the created mix. */
  async createMix(trackId: string, settings: MixSettingsDto): Promise<MixDto> {
    return json<MixDto>(await this.request.post('/api/mixes', { data: { trackId, settings } }));
  }

  /** `GET /api/mixes/{id}`. */
  async getMix(id: string): Promise<MixDto> {
    return json<MixDto>(await this.request.get(`/api/mixes/${id}`));
  }

  /** `POST /api/mixes/{id}/duplicate`; returns the copy. */
  async duplicateMix(id: string): Promise<MixDto> {
    return json<MixDto>(await this.request.post(`/api/mixes/${id}/duplicate`));
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

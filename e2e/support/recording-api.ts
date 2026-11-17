/**
 * Backend client for the feature 015 recording routes (`specs/015-play-along-recording/contracts/backend-api.openapi.yaml`
 * 1.14.0, tag `recording`): takes and input calibrations. Used for set-up, clean-up and server-state assertions; the UI is
 * driven through `pages/recording-panel.ts`.
 *
 * @packageDocumentation
 */
import { expect, type APIRequestContext } from '@playwright/test';
import { okJson } from './http';

/** `RecordingPart` values (ui-test-ids 1.14.0 `<family>`). */
export type RecordingPart = 'none' | 'vocals' | 'guitar' | 'bass' | 'piano' | 'drums' | 'other';

/** Contract `TakeStatus`. */
export type TakeStatus = 'saving' | 'ready' | 'failed';

/** Contract `LatencyMethod`. */
export type LatencyMethod = 'loopback' | 'manual' | 'estimated';

/** Contract `RecordingTake` (the fields the suite reads). */
export interface RecordingTakeDto {
  readonly id: string;
  readonly trackId: string;
  readonly number: number;
  readonly name: string;
  readonly part: RecordingPart;
  readonly status: TakeStatus;
  readonly error: string | null;
  readonly sessionId: string;
  readonly passIndex: number | null;
  readonly partial: boolean;
  readonly startSample: number;
  readonly lengthSamples: number;
  readonly durationSeconds: number;
  readonly inputChannels: number;
  readonly latency: { readonly samples: number; readonly milliseconds: number; readonly method: LatencyMethod };
  readonly favorite: boolean;
  readonly adjustmentSamples: number;
  readonly alignmentRevision: number;
  readonly sizeBytes: number;
  readonly assetId: string;
  readonly audioUrl: string | null;
}

/** Contract `RecordingTakeList`. */
export interface RecordingTakeListDto {
  readonly items: readonly RecordingTakeDto[];
  readonly totalSizeBytes: number;
}

/** Contract `InputCalibration` (the fields the suite reads). */
export interface InputCalibrationDto {
  readonly id: string;
  readonly deviceLabel: string;
  readonly latencySamples: number;
  readonly method: 'loopback' | 'manual';
}

const pollIntervals = [500, 1000];

/** Typed access to `/api/tracks/{id}/takes`, `/api/takes/**` and `/api/recording/calibrations`. */
export class RecordingApi {
  /** @param request An API request context with `baseURL` = the API origin. */
  constructor(private readonly request: APIRequestContext) {}

  /** `listRecordingTakes`: newest first. */
  async listTakes(trackId: string): Promise<RecordingTakeListDto> {
    return okJson<RecordingTakeListDto>(await this.request.get(`/api/tracks/${trackId}/takes`));
  }

  /** `getRecordingTake`. */
  async getTake(takeId: string): Promise<RecordingTakeDto> {
    return okJson<RecordingTakeDto>(await this.request.get(`/api/takes/${takeId}`));
  }

  /** Polls one take (by its id, never by a list position) until it has `status`. */
  async waitForTakeStatus(takeId: string, status: TakeStatus, timeoutMs: number): Promise<RecordingTakeDto> {
    await expect.poll(async () => (await this.getTake(takeId)).status, { timeout: timeoutMs, intervals: pollIntervals }).toBe(status);
    return this.getTake(takeId);
  }

  /** `deleteRecordingTake`; returns the status code (204, or 404 when gone). */
  async deleteTake(takeId: string): Promise<number> {
    return (await this.request.delete(`/api/takes/${takeId}`)).status();
  }

  /** Deletes every take of a track (test clean-up). */
  async deleteAllTakes(trackId: string): Promise<void> {
    const ids = (await this.listTakes(trackId)).items.map((t) => t.id);
    if (ids.length > 0) {
      await okJson<{ deleted: number }>(await this.request.post(`/api/tracks/${trackId}/takes/delete`, { data: { takeIds: ids } }));
    }
  }

  /** Bytes of an API file URL (take audio, stem) relative to the API origin. */
  async audio(url: string): Promise<Buffer> {
    const response = await this.request.get(url);
    expect(response.ok(), `${url} -> ${String(response.status())}`).toBe(true);
    return response.body();
  }

  /** `listInputCalibrations`. */
  async listCalibrations(): Promise<InputCalibrationDto[]> {
    return okJson<InputCalibrationDto[]>(await this.request.get('/api/recording/calibrations'));
  }

  /** Forgets every calibration (test set-up: the next take uses the estimate). */
  async deleteAllCalibrations(): Promise<void> {
    for (const calibration of await this.listCalibrations()) {
      await this.request.delete(`/api/recording/calibrations/${calibration.id}`);
    }
  }
}

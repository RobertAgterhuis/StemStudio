/**
 * Backend client and helpers for the feature 014 practice-flow routes (backend-api 1.13.0, tags `sections`,
 * `score-follow`, `setlists`, `practice`; `specs/014-practice-flow/contracts/practice-api.openapi.yaml`). Used for
 * set-up and server-state assertions; the UI is driven through the page objects.
 *
 * @packageDocumentation
 */
import { expect, test, type APIRequestContext, type APIResponse } from '@playwright/test';
import { okJson } from './http';

/** Contract `SectionKind`. */
export type SectionKind = 'intro' | 'verse' | 'pre-chorus' | 'chorus' | 'bridge' | 'solo' | 'instrumental' | 'outro' | 'other';

/** Contract `SectionSetStatus`. */
export type SectionSetStatus = 'none' | 'queued' | 'detecting' | 'ready' | 'failed';

/** Contract `Section`. */
export interface SectionDto {
  readonly id: string;
  readonly kind: SectionKind;
  readonly label: string;
  readonly startSeconds: number;
  readonly endSeconds: number;
  readonly startBar?: number;
  readonly instrument: string | null;
  readonly confidence: number | null;
  readonly origin: 'detected' | 'user';
}

/** Contract `SectionWarning`. */
export interface SectionWarningDto {
  readonly code: string;
  readonly message: string;
}

/** Contract `SectionSet`. */
export interface SectionSetDto {
  readonly trackId: string;
  readonly status: SectionSetStatus;
  readonly origin: 'detected' | 'edited';
  readonly revision: number;
  readonly analyzer: string | null;
  readonly snap: 'downbeat' | 'beat' | 'time' | null;
  readonly warnings: readonly SectionWarningDto[];
  readonly error: string | null;
  readonly sections: readonly SectionDto[];
  readonly updatedAt: string | null;
}

/** Contract `SectionInput`. */
export interface SectionInputDto {
  readonly id?: string;
  readonly kind: SectionKind;
  readonly label: string;
  readonly startSeconds: number;
  readonly endSeconds: number;
  readonly instrument?: string;
}

/** Contract `ScoreViewStatus`. */
export type ScoreViewStatus = 'none' | 'queued' | 'rendering' | 'ready' | 'failed';

/** Contract `ScoreBar`. */
export interface ScoreBarDto {
  readonly index: number;
  readonly number: number;
  readonly page: number;
  readonly startSeconds: number;
  readonly endSeconds: number;
}

/** Contract `ScoreView` (the parts the suite reads). */
export interface ScoreViewDto {
  readonly assetId: string;
  readonly status: ScoreViewStatus;
  readonly offsetSeconds: number;
  readonly pages: readonly { readonly page: number; readonly url: string }[];
  readonly bars: readonly ScoreBarDto[];
  readonly error: string | null;
}

/** The bar a track time falls in, or undefined outside the score. */
export function barAt(view: ScoreViewDto, seconds: number): ScoreBarDto | undefined {
  return view.bars.find((bar) => bar.startSeconds <= seconds && seconds < bar.endSeconds);
}

/** Contract `SetlistEntry` (the parts the suite reads). */
export interface SetlistEntryDto {
  readonly id: string;
  readonly position: number;
  readonly trackId: string;
  readonly trackTitle: string;
  readonly mixId: string | null;
  readonly mixName: string | null;
  readonly note: string;
}

/** Contract `Setlist` (the parts the suite reads). */
export interface SetlistDto {
  readonly id: string;
  readonly name: string;
  readonly note: string;
  readonly entries: readonly SetlistEntryDto[];
  readonly totalSeconds: number;
  readonly removedEntries: number;
}

/** The status text the section lane shows for an unreliable grid (ui-test-ids 1.13.0 `section-status`). */
export const unreliableGridText = 'Tempo grid uncertain - boundaries may be off';

/** Sections must be ready this long after the separation finished (014 US1 scenario 1). */
export const sectionsReadyTimeoutMs = 30_000;

/** Seconds of a section as the lane writes them (`data-start`, `data-end`: 1 decimal). */
export function laneSeconds(seconds: number): string {
  return seconds.toFixed(1);
}

/** Whether the stack detects sections right after a separation (`E2E_SECTION_DETECT_ON_COMPLETE`, default on). */
export const detectOnComplete: boolean = process.env.E2E_SECTION_DETECT_ON_COMPLETE !== 'false';

/**
 * Skips unless the stack's `StemStudio__Practice__SectionDetectOnComplete` matches `on` (014 US1 scenario 4 needs a
 * stack with it off; `E2E_SECTION_DETECT_ON_COMPLETE=false` says the stack runs that way).
 */
export function requireDetectOnComplete(on: boolean): void {
  test.skip(detectOnComplete !== on, on ? 'this stack does not detect sections after separation' : 'needs a stack with StemStudio__Practice__SectionDetectOnComplete=false');
}

/** Client of the 014 section routes. */
export class PracticeFlowApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/tracks/{id}/sections`. */
  async sections(trackId: string): Promise<SectionSetDto> {
    return okJson<SectionSetDto>(await this.request.get(`/api/tracks/${trackId}/sections`));
  }

  /** `POST /api/tracks/{id}/sections/detect` (raw response: 202, 409 or 503). */
  async detect(trackId: string, overwrite = false): Promise<APIResponse> {
    return this.request.post(`/api/tracks/${trackId}/sections/detect`, { data: { overwrite } });
  }

  /** `PUT /api/tracks/{id}/sections` (raw response: 200, 409 or 422). */
  async replace(trackId: string, revision: number, sections: readonly SectionInputDto[]): Promise<APIResponse> {
    return this.request.put(`/api/tracks/${trackId}/sections`, { data: { revision, sections } });
  }

  /** `GET /api/assets/{id}/score-view`. */
  async scoreView(assetId: string): Promise<ScoreViewDto> {
    return okJson<ScoreViewDto>(await this.request.get(`/api/assets/${assetId}/score-view`));
  }

  /** Polls until the score view is `ready` (asks for it once when it is `none`). */
  async waitForScoreView(assetId: string, timeoutMs: number): Promise<ScoreViewDto> {
    if ((await this.scoreView(assetId)).status === 'none') {
      await this.request.post(`/api/assets/${assetId}/score-view`);
    }
    await expect.poll(async () => (await this.scoreView(assetId)).status, { timeout: timeoutMs, intervals: [250, 500, 1000] }).toBe('ready');
    return this.scoreView(assetId);
  }

  /** `GET /api/setlists/{id}`. */
  async setlist(setlistId: string): Promise<SetlistDto> {
    return okJson<SetlistDto>(await this.request.get(`/api/setlists/${setlistId}`));
  }

  /** `PATCH /api/setlists/{id}` (the note). */
  async setSetlistNote(setlistId: string, note: string): Promise<SetlistDto> {
    return okJson<SetlistDto>(await this.request.patch(`/api/setlists/${setlistId}`, { data: { note } }));
  }

  /** Polls until the set is `ready` (or fails the test when it becomes `failed`). */
  async waitForSections(trackId: string, timeoutMs = sectionsReadyTimeoutMs): Promise<SectionSetDto> {
    await expect.poll(async () => {
      const set = await this.sections(trackId);
      expect(set.status, `sections of ${trackId} failed: ${set.error ?? ''}`).not.toBe('failed');
      return set.status;
    }, { timeout: timeoutMs, intervals: [250, 500, 1000] }).toBe('ready');
    return this.sections(trackId);
  }
}

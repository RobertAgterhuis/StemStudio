/**
 * Backend API 1.19.0 client of feature 020 (tags `insights` and `handouts`): stem quality reports,
 * handout options, handout items and practice notes. Types are the contract shapes the suite reads
 * (`specs/020-insights-handouts/contracts/backend-api.delta.openapi.yaml`). Every wait polls the job
 * the caller started: a report by its `measuredAt` (a new report replaces the old one), a handout by
 * its own asset id (LESSONS 013/022).
 *
 * @packageDocumentation
 */
import { expect, test, type APIRequestContext, type APIResponse } from '@playwright/test';
import type { AssetDto } from './assets-api';
import { okJson } from './http';

/** Contract `QualityReportStatus`. */
export type QualityReportStatus = 'none' | 'queued' | 'running' | 'ready' | 'failed';

/** Contract `QualityRating`. */
export type QualityRating = 'good' | 'fair' | 'poor' | 'not-present' | 'unknown';

/** Contract `QualityLevel`. */
export type QualityLevel = 'low' | 'medium' | 'high' | 'unknown';

/** Contract `TipCode` (1.19.0: no `rerun-high-quality`, G2 deferred). */
export type TipCode = 'not-in-song' | 'use-detail-stem' | 'try-detail-separation' | 'listen-to-rest' | 'hard-to-isolate' | 'clean';

/** Contract `TipActionKind` (1.19.0: no `reseparate`). */
export type TipActionKind = 'mute' | 'open-detail' | 'split-detail';

/** Contract `Tip`. */
export interface TipDto {
  readonly code: TipCode;
  readonly text: string;
  readonly action: { readonly kind: TipActionKind; readonly channel: string; readonly parent: string | null; readonly child: string | null } | null;
}

/** Contract `StemQualityView`. */
export interface StemQualityDto {
  readonly channel: string;
  readonly parent: string | null;
  readonly rating: QualityRating;
  readonly reason: string;
  readonly previousRating: QualityRating | null;
  readonly bleed: { readonly level: QualityLevel; readonly index: number; readonly mainSource: string | null; readonly coOccurrence: boolean; readonly reliability: string };
  readonly artefacts: { readonly level: QualityLevel; readonly index: number; readonly kinds: readonly string[]; readonly reliability: string };
  readonly tips: readonly TipDto[];
}

/** Contract `QualityReport`. */
export interface QualityReportDto {
  readonly status: QualityReportStatus;
  readonly progress: number;
  readonly error: string | null;
  readonly analyzerVersion: string | null;
  readonly measuredAt: string | null;
  readonly outdated: boolean;
  readonly disclaimer: string;
  readonly summary: { readonly good: number; readonly fair: number; readonly poor: number; readonly notPresent: number; readonly unknown: number };
  readonly stems: readonly StemQualityDto[];
}

/** The fixed disclaimer of every report (contract `QualityReport.disclaimer`, a `const`). */
export const qualityDisclaimer = 'These ratings are estimates made from the stems alone, without the original recording. They can be wrong.';

/** Contract `HandoutFamily`, in the contract's order. */
export const handoutFamilies = ['overview', 'vocals', 'guitar', 'bass', 'keys', 'drums', 'melodic'] as const;

/** Contract `HandoutFamily`. */
export type HandoutFamily = (typeof handoutFamilies)[number];

/** Contract `IngredientCode`. */
export type IngredientCode = 'facts' | 'form-chart' | 'chords-over-lyrics' | 'lyrics' | 'chord-chart' | 'notation' | 'practice-notes' | 'practice-progress' | 'setlist-notes';

/** Contract `Transposition`. */
export type Transposition = 'c' | 'b-flat' | 'e-flat' | 'f';

/** Contract `HandoutOptions` (the parts the suite reads). */
export interface HandoutOptionsDto {
  readonly kind: 'song' | 'setlist';
  readonly family: HandoutFamily;
  readonly familyLabel: string;
  readonly songs: number;
  readonly ingredients: readonly { readonly code: IngredientCode; readonly status: 'available' | 'missing' | 'not-applicable'; readonly defaultIncluded: boolean; readonly text: string; readonly action: string | null }[];
  readonly notation: readonly { readonly assetId: string; readonly kind: 'score' | 'tab' | 'drums' | 'transposed'; readonly transposition: Transposition | null; readonly defaultSelected: boolean }[];
  readonly transpositions: readonly Transposition[];
  readonly estimateSeconds: number;
}

/** Contract `HandoutFacts`. */
export interface HandoutFactsDto {
  readonly kind: 'song' | 'setlist';
  readonly family: HandoutFamily;
  readonly transposition: Transposition | null;
  readonly paper: 'a4' | 'letter';
  readonly pages: number;
  readonly songs: number;
  readonly included: readonly IngredientCode[];
  readonly omitted: readonly { readonly code: IngredientCode; readonly reason: 'not-available' | 'not-selected' | 'no-timing' }[];
  readonly warnings: readonly { readonly code: string; readonly text: string }[];
  readonly outline: readonly string[];
}

/** A library item of kind `handout` (contract `Asset` with the 1.19.0 additions). */
export interface HandoutAssetDto extends Omit<AssetDto, 'files' | 'provenance'> {
  readonly kind: string;
  readonly files: readonly { readonly role: string; readonly contentType: string; readonly sizeBytes: number; readonly url: string }[];
  readonly handout: HandoutFactsDto | null;
  readonly provenance: AssetDto['provenance'] & { readonly handoutRecipe?: Record<string, unknown> | null };
}

/** Contract `CreateHandoutRequest`. */
export interface CreateHandoutDto {
  readonly source: { readonly trackId: string } | { readonly setlistId: string };
  readonly family: HandoutFamily;
  readonly include?: readonly IngredientCode[] | null;
  readonly notationAssetIds?: readonly string[] | null;
  readonly transposition?: Transposition | null;
  readonly paper?: 'a4' | 'letter' | null;
  readonly name?: string | null;
}

/** Contract `PracticeNote`. */
export interface PracticeNoteDto {
  readonly family: HandoutFamily;
  readonly text: string;
  readonly updatedAt: string;
}

/** Fake analyzer job length is 0.5 s (data-model 2.6); 60 s covers queueing behind other lanes. */
export const qualityTimeoutMs = 60_000;

/** Handout target 15 s on the reference CPU (SC-004); the fake stack gets head room. */
export const handoutTimeoutMs = 90_000;

const intervals = [250, 500, 1000];

/** Client of the 020 routes. */
export class InsightsApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/tracks/{id}/quality`. */
  async quality(trackId: string): Promise<QualityReportDto> {
    return okJson<QualityReportDto>(await this.request.get(`/api/tracks/${trackId}/quality`));
  }

  /** `POST /api/tracks/{id}/quality`; the raw response (202, 409 `quality-running`, 409 `track-not-ready`). */
  async requestQualityResponse(trackId: string): Promise<APIResponse> {
    return this.request.post(`/api/tracks/${trackId}/quality`);
  }

  /** Waits for a ready (or failed) report; with `after`, only for one measured later than that report. */
  async waitForQuality(trackId: string, timeoutMs = qualityTimeoutMs, after: QualityReportDto | null = null): Promise<QualityReportDto> {
    const fresh = (r: QualityReportDto): boolean => (r.status === 'ready' || r.status === 'failed') && r.measuredAt !== after?.measuredAt;
    await expect.poll(async () => fresh(await this.quality(trackId)), { timeout: timeoutMs, intervals }).toBe(true);
    const report = await this.quality(trackId);
    expect(report.status, report.error ?? '').toBe('ready');
    return report;
  }

  /** The automatic report of a completed track (FR-001), requested once if the stack has none yet. */
  async ensureQuality(trackId: string, timeoutMs = qualityTimeoutMs): Promise<QualityReportDto> {
    const current = await this.quality(trackId);
    if (current.status === 'none' || current.status === 'failed') {
      expect([202, 409]).toContain((await this.requestQualityResponse(trackId)).status());
    }
    return this.waitForQuality(trackId, timeoutMs);
  }

  /** Requests a new report and waits for it (not for the previous one). */
  async remakeQuality(trackId: string, timeoutMs = qualityTimeoutMs): Promise<QualityReportDto> {
    const before = await this.waitForQuality(trackId, timeoutMs);
    expect((await this.requestQualityResponse(trackId)).status()).toBe(202);
    return this.waitForQuality(trackId, timeoutMs, before);
  }

  /** `GET /api/handouts/options`. */
  async handoutOptions(source: { readonly trackId: string } | { readonly setlistId: string }, family: HandoutFamily, transposition?: Transposition): Promise<HandoutOptionsDto> {
    const params: Record<string, string> = { ...source, family, ...(transposition ? { transposition } : {}) };
    return okJson<HandoutOptionsDto>(await this.request.get('/api/handouts/options', { params }));
  }

  /** `POST /api/assets/handouts` (202, the queued item). */
  async createHandout(body: CreateHandoutDto): Promise<HandoutAssetDto> {
    return okJson<HandoutAssetDto>(await this.request.post('/api/assets/handouts', { data: body }));
  }

  /** `GET /api/assets/{id}` of a handout item. */
  async handout(id: string): Promise<HandoutAssetDto> {
    return okJson<HandoutAssetDto>(await this.request.get(`/api/assets/${id}`));
  }

  /** Polls the item the caller created until it is ready (fails on `failed`). */
  async waitForHandout(id: string, timeoutMs = handoutTimeoutMs): Promise<HandoutAssetDto> {
    await expect.poll(async () => (await this.handout(id)).status, { timeout: timeoutMs, intervals }).toMatch(/^(ready|failed)$/u);
    const item = await this.handout(id);
    expect(item.status, item.error ?? '').toBe('ready');
    return item;
  }

  /** `PUT /api/tracks/{id}/practice-notes/{family}`; returns the HTTP status (200 stored, 204 deleted). */
  async setPracticeNote(trackId: string, family: HandoutFamily, text: string): Promise<number> {
    const response = await this.request.put(`/api/tracks/${trackId}/practice-notes/${family}`, { data: { text } });
    expect([200, 204], await response.text()).toContain(response.status());
    return response.status();
  }

  /** `GET /api/tracks/{id}/practice-notes`. */
  async practiceNotes(trackId: string): Promise<readonly PracticeNoteDto[]> {
    return (await okJson<{ readonly notes: readonly PracticeNoteDto[] }>(await this.request.get(`/api/tracks/${trackId}/practice-notes`))).notes;
  }

  /** Skips the test while the stack has no 020 backend (the route answers 404 or 405). */
  async requireFeature(): Promise<void> {
    const status = (await this.request.get('/api/handouts/options', { params: { family: 'bass' } })).status();
    test.skip(status === 404 || status === 405, `insights and handouts not deployed on this stack (options -> ${String(status)})`);
  }
}

/** The test id of a ChannelId (`other.rest` -> `other-rest`, ui-test-ids R20-3). */
export function channelTestId(channel: string): string {
  return channel.replaceAll('.', '-');
}

/** The stem of `channel` in a report. */
export function stemOf(report: QualityReportDto, channel: string): StemQualityDto {
  const stem = report.stems.find((s) => s.channel === channel);
  if (stem === undefined) {
    throw new Error(`no quality row for ${channel} (rows: ${report.stems.map((s) => s.channel).join(', ')})`);
  }
  return stem;
}

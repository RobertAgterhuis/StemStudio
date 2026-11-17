/**
 * Backend client for the feature 023 lyric assistant (`specs/023-lyric-writing-assistant/contracts/backend-api.delta.openapi.yaml`,
 * full document 1.23.0) and the 010 lyrics sheets it works on (`/api/lyrics/**`). Used for set-up
 * and for asserting server state; the UI is driven through `pages/lyric-assist-pane.ts`.
 *
 * @packageDocumentation
 */
import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { okJson } from './http';

/** `AssistKind`. */
export type AssistKind = 'check' | 'devices' | 'alternatives' | 'continue' | 'titles';

/** `SuggestionCategory`. */
export type SuggestionCategory = 'spelling' | 'grammar' | 'flow' | 'licence' | 'device' | 'alternative' | 'continuation' | 'title';

/** `SuggestionStatus`. */
export type SuggestionStatus = 'open' | 'accepted' | 'rejected' | 'intentional' | 'stale' | 'superseded';

/** `Sensitivity`. */
export type Sensitivity = 'loose' | 'balanced' | 'strict';

/** `LyricSuggestion` (the parts the suite reads). */
export interface LyricSuggestionDto {
  readonly suggestionId: string;
  readonly assistId: string;
  readonly category: SuggestionCategory;
  readonly type: string;
  readonly severity: 'error' | 'improvement' | 'idea';
  readonly anchor: { readonly line: number; readonly start: number; readonly length: number; readonly original: string };
  readonly proposal?: string | null;
  readonly groupId?: string | null;
  readonly source: 'dictionary' | 'rules' | 'model';
  readonly syllablesBefore?: number | null;
  readonly syllablesAfter?: number | null;
  readonly rhymeWith?: { readonly line: number; readonly word: string } | null;
  readonly status: SuggestionStatus;
}

/** `OwnWorkOverride`. */
export interface OwnWorkOverrideDto {
  readonly statementVersion: string;
  readonly confirmedBy: string;
  readonly confirmedAt: string;
  readonly matchedTrackId: string;
  readonly matchedTrackTitle: string;
}

/** `SheetAssistInfo`. */
export interface SheetAssistInfoDto {
  readonly sensitivity: Sensitivity;
  readonly openSuggestions: number;
  readonly assistedLines: readonly { readonly line: number; readonly suggestionId: string }[];
  readonly intentionalMarkCount: number;
  readonly canUndo: boolean;
  readonly ownWorkOverride: OwnWorkOverrideDto | null;
}

/** `LyricsSheet` 1.23.0. */
export interface LyricsSheetDto {
  readonly id: string;
  readonly title: string;
  readonly language: string;
  readonly text: string;
  readonly revision: number;
  readonly assist: SheetAssistInfoDto;
}

/** `LyricAssistSummary` / `LyricAssist` (the parts the suite reads). */
export interface LyricAssistDto {
  readonly assistId: string;
  readonly lyricsId: string;
  readonly kind: AssistKind;
  readonly status: 'queued' | 'running' | 'completed' | 'failed' | 'cancelled';
  readonly backend?: string | null;
  readonly counts: Readonly<Record<SuggestionCategory, number>>;
  readonly discarded: { readonly invalidAnchor: number; readonly invalidSchema: number; readonly failedVerification: number; readonly filtered: number; readonly verbatim: number };
  readonly suggestions?: readonly LyricSuggestionDto[];
  readonly usage?: { readonly inputTokens: number; readonly outputTokens: number } | null;
}

/** `LyricAssistStatus` (the parts the suite reads). */
export interface LyricAssistStatusDto {
  readonly languages: readonly string[];
  readonly model: { readonly backend: 'ollama' | 'claude' | 'none' | 'fake'; readonly ready: boolean };
  readonly claudeNoticeRequired: boolean;
  readonly assistActive: string | null;
}

/** `LyricAssistSettings` (the parts the suite reads). */
export interface LyricAssistSettingsDto {
  readonly claudeNoticeAcknowledged: boolean;
  readonly ownWorkStatementVersion: string;
}

/** `AssistProblem` (the parts the suite reads). */
export interface AssistProblemDto {
  readonly status: number;
  readonly code: string;
  readonly matchedTrackTitle?: string | null;
  readonly matchedTrackId?: string | null;
  readonly overridable?: boolean | null;
}

const pollIntervals = [250, 500, 1000];

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class LyricAssistApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `POST /api/lyrics` (010 `LyricsInput`); returns the sheet. */
  async createSheet(title: string, language: string, text: string): Promise<LyricsSheetDto> {
    return okJson<LyricsSheetDto>(await this.request.post('/api/lyrics', { data: { title, language, text } }));
  }

  /** `GET /api/lyrics/{id}`. */
  async sheet(lyricsId: string): Promise<LyricsSheetDto> {
    return okJson<LyricsSheetDto>(await this.request.get(`/api/lyrics/${lyricsId}`));
  }

  /** `GET /api/lyric-assist/status`. */
  async status(): Promise<LyricAssistStatusDto> {
    return okJson<LyricAssistStatusDto>(await this.request.get('/api/lyric-assist/status'));
  }

  /** `PUT /api/lyric-assist/settings`: acknowledge or withdraw the Claude notice. */
  async setClaudeNotice(acknowledge: boolean): Promise<LyricAssistSettingsDto> {
    return okJson<LyricAssistSettingsDto>(await this.request.put('/api/lyric-assist/settings', { data: { acknowledgeClaudeNotice: acknowledge } }));
  }

  /** Raw `POST /api/lyrics/{id}/assists` (202, or a 400/409 problem). */
  async startResponse(lyricsId: string, kind: AssistKind, body: Readonly<Record<string, unknown>> = {}): Promise<APIResponse> {
    const { revision } = await this.sheet(lyricsId);
    return this.request.post(`/api/lyrics/${lyricsId}/assists`, { data: { kind, expectedRevision: revision, ...body } });
  }

  /** The problem body of a refused start (fails when the start was accepted). */
  async startProblem(lyricsId: string, kind: AssistKind, body: Readonly<Record<string, unknown>> = {}): Promise<AssistProblemDto> {
    const response = await this.startResponse(lyricsId, kind, body);
    expect(response.status(), `${kind} start`).toBeGreaterThanOrEqual(400);
    return { ...((await response.json()) as AssistProblemDto), status: response.status() };
  }

  /** `GET /api/lyric-assists/{id}`. */
  async assist(assistId: string): Promise<LyricAssistDto> {
    return okJson<LyricAssistDto>(await this.request.get(`/api/lyric-assists/${assistId}`));
  }

  /** Polls an assist until it ended; returns it. */
  async waitForEnd(assistId: string, timeoutMs = 60_000): Promise<LyricAssistDto> {
    await expect.poll(async () => (await this.assist(assistId)).status, { timeout: timeoutMs, intervals: pollIntervals }).toMatch(/^(completed|failed|cancelled)$/u);
    return this.assist(assistId);
  }

  /** `GET /api/lyrics/{id}/assists` (newest first). */
  async assists(lyricsId: string): Promise<LyricAssistDto[]> {
    return okJson<LyricAssistDto[]>(await this.request.get(`/api/lyrics/${lyricsId}/assists`));
  }

  /** `GET /api/lyrics/{id}/suggestions`. */
  async suggestions(lyricsId: string, filter: { readonly status?: SuggestionStatus; readonly category?: SuggestionCategory } = {}): Promise<LyricSuggestionDto[]> {
    const response = await this.request.get(`/api/lyrics/${lyricsId}/suggestions`, { params: { ...filter } });
    return (await okJson<{ readonly suggestions: LyricSuggestionDto[] }>(response)).suggestions;
  }

  /** The open suggestion whose anchor text is `original` (fails when there is none). */
  async openSuggestionFor(lyricsId: string, original: string): Promise<LyricSuggestionDto> {
    const found = (await this.suggestions(lyricsId, { status: 'open' })).find((suggestion) => suggestion.anchor.original === original);
    if (found === undefined) {
      throw new Error(`no open suggestion on "${original}"`);
    }
    return found;
  }

  /** `GET /api/lyric-assist/rhymes`; returns the result and the round-trip time in ms. */
  async rhymes(word: string, language: 'nl' | 'en'): Promise<{ readonly rhymes: readonly { readonly word: string; readonly kind: string }[]; readonly elapsedMs: number }> {
    const started = Date.now();
    const result = await okJson<{ readonly rhymes: { readonly word: string; readonly kind: string }[] }>(await this.request.get('/api/lyric-assist/rhymes', { params: { word, language } }));
    return { rhymes: result.rhymes, elapsedMs: Date.now() - started };
  }

  /** Removes `word` from the personal word list of `language` when present (set-up: the list is global). */
  async forgetWord(language: 'nl' | 'en', word: string): Promise<void> {
    const words = await okJson<{ readonly wordId: string; readonly word: string }[]>(await this.request.get('/api/lyric-assist/words', { params: { language } }));
    for (const entry of words.filter((candidate) => candidate.word.toLowerCase() === word.toLowerCase())) {
      expect((await this.request.delete(`/api/lyric-assist/words/${entry.wordId}`)).status()).toBe(204);
    }
  }

  /** `DELETE /api/lyrics/{id}/own-work-override` (revoke; 204, or 404 when there is none). */
  async revokeOwnWork(lyricsId: string): Promise<number> {
    return (await this.request.delete(`/api/lyrics/${lyricsId}/own-work-override`)).status();
  }
}

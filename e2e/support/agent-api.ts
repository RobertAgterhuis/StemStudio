/**
 * Thin client for the agent endpoints of backend API 1.10.0 (`/api/agent/*`, feature 011) and the
 * raw status probes the agent specs need: set-up (stored backend), and asserting server state the
 * panel cannot show (a mix or track is gone, a conversation was stored).
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import type { APIRequestContext, APIResponse } from '@playwright/test';
import { okJson } from './http';

/** Agent backend of the contract (`AgentStatus.backend`); `fake` is test-only and never stored. */
export type AgentBackend = 'ollama' | 'claude' | 'none' | 'fake';

/** Backends a settings row may hold (`AgentSettings.backend`). */
export type StoredAgentBackend = Exclude<AgentBackend, 'fake'>;

/** Subset of the contract `AgentStatus`. */
export interface AgentStatusDto {
  readonly backend: AgentBackend;
  readonly ready: boolean;
  readonly activeModel: string | null;
  readonly usingFallback: boolean;
  readonly reason: string | null;
  readonly fix: string | null;
  readonly ollama: { readonly reachable: boolean };
  readonly claude: { readonly keyConfigured: boolean; readonly noticeAcknowledged: boolean; readonly model: string };
}

/** Subset of the contract `AgentSettings`. */
export interface AgentSettingsDto {
  readonly backend: AgentBackend;
  readonly ollamaModel: string;
  readonly ollamaFallbackModel: string;
}

/** Contract `ConversationSummary`. */
export interface ConversationSummaryDto {
  readonly conversationId: string;
  readonly title: string;
  readonly lastRunStatus: string | null;
}

/** Subset of the contract `Conversation`. */
export interface ConversationDto extends ConversationSummaryDto {
  readonly messages: readonly { readonly seq: number; readonly role: 'user' | 'assistant'; readonly text: string }[];
  readonly runs: readonly { readonly runId: string; readonly status: string }[];
}

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class AgentApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/agent/status`. */
  async status(): Promise<AgentStatusDto> {
    return okJson<AgentStatusDto>(await this.request.get('/api/agent/status'));
  }

  /** `GET /api/agent/settings`. */
  async settings(): Promise<AgentSettingsDto> {
    return okJson<AgentSettingsDto>(await this.request.get('/api/agent/settings'));
  }

  /** `PUT /api/agent/settings` with only the backend (raw response: 200, 409 or 422). */
  async putBackend(backend: StoredAgentBackend): Promise<APIResponse> {
    return this.request.put('/api/agent/settings', { data: { backend } });
  }

  /** `GET /api/agent/conversations` (newest first). */
  async conversations(): Promise<ConversationSummaryDto[]> {
    return okJson<ConversationSummaryDto[]>(await this.request.get('/api/agent/conversations'));
  }

  /** `GET /api/agent/conversations/{id}`. */
  async conversation(conversationId: string): Promise<ConversationDto> {
    return okJson<ConversationDto>(await this.request.get(`/api/agent/conversations/${conversationId}`));
  }

  /** HTTP status of `GET <path>` (for "still exists" / "is gone" assertions). */
  async statusOf(path: string): Promise<number> {
    return (await this.request.get(path)).status();
  }

  /**
   * `POST /api/imports/upload` of the bytes of `file` under the file name `<title>.wav`: an upload's
   * track title is its file name (001), which is how the injection carrier `t-inject` gets its title.
   */
  async uploadTitled(file: string, title: string): Promise<{ readonly id: string }> {
    const multipart = { file: { name: `${title}.wav`, mimeType: 'audio/wav', buffer: readFileSync(file) } };
    return okJson<{ readonly id: string }>(await this.request.post('/api/imports/upload', { multipart }));
  }
}

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

/** Contract `ClaudeKeyStatus` (backend API 1.21.0, 011 follow-up claude-key): never the key. */
export interface ClaudeKeyStatusDto {
  readonly configured: boolean;
  readonly source: 'environment' | 'stored' | null;
  readonly state: 'not-configured' | 'environment' | 'stored' | 'stored-unreadable';
  readonly lastFour: string | null;
  readonly stored: 'absent' | 'readable' | 'unreadable';
  readonly storedLastFour: string | null;
  readonly storedAt: string | null;
  readonly storeAvailable: boolean;
  readonly reason: string | null;
  readonly fix: string | null;
}

/** Subset of the contract `AgentStatus`. */
export interface AgentStatusDto {
  readonly backend: AgentBackend;
  readonly ready: boolean;
  readonly activeModel: string | null;
  readonly usingFallback: boolean;
  readonly reason: string | null;
  readonly fix: string | null;
  readonly ollama: { readonly reachable: boolean };
  readonly claude: {
    readonly keyConfigured: boolean;
    readonly noticeAcknowledged: boolean;
    readonly model: string;
    /** 1.21.0 (claude-key). */
    readonly key: ClaudeKeyStatusDto;
  };
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

/** An `Origin` that is not the Web app's (a page on another device or site). */
const foreignOrigin = { Origin: 'http://lan-device.invalid:8080' } as const;

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

  /** `GET /api/agent/settings/claude-key` (1.21.0): the raw response, so a test can scan its body. */
  async claudeKeyResponse(): Promise<APIResponse> {
    return this.request.get('/api/agent/settings/claude-key');
  }

  /**
   * `PUT /api/agent/settings/claude-key` sent with a foreign `Origin`: the local-only filter must
   * refuse it (`403 local-only`) in every topology, Docker or not (CK-FR-012, CK-SC-005).
   */
  async putClaudeKeyFromElsewhere(apiKey: string): Promise<APIResponse> {
    return this.request.put('/api/agent/settings/claude-key', { data: { apiKey }, headers: foreignOrigin });
  }

  /** `DELETE /api/agent/settings/claude-key` with a foreign `Origin` (must be `403 local-only`). */
  async deleteClaudeKeyFromElsewhere(): Promise<APIResponse> {
    return this.request.delete('/api/agent/settings/claude-key', { headers: foreignOrigin });
  }

  /** `GET /api/agent/conversations` (newest first). */
  async conversations(): Promise<ConversationSummaryDto[]> {
    return okJson<ConversationSummaryDto[]>(await this.request.get('/api/agent/conversations'));
  }

  /**
   * Id of the newest conversation.
   * @throws Error when there is none.
   */
  async latestConversationId(): Promise<string> {
    const [latest] = await this.conversations();
    if (latest === undefined) throw new Error('no agent conversation is stored');
    return latest.conversationId;
  }

  /** `GET /api/agent/conversations/{id}`. */
  async conversation(conversationId: string): Promise<ConversationDto> {
    return okJson<ConversationDto>(await this.request.get(`/api/agent/conversations/${conversationId}`));
  }

  /** `POST /api/agent/conversations`; returns the new conversation id. */
  async createConversation(title: string): Promise<string> {
    return (await okJson<ConversationSummaryDto>(await this.request.post('/api/agent/conversations', { data: { title } }))).conversationId;
  }

  /** `POST /api/agent/conversations/{id}/messages` (raw response: 202, 409, 422 or 503). */
  async sendMessage(conversationId: string, text: string, page: string): Promise<APIResponse> {
    return this.request.post(`/api/agent/conversations/${conversationId}/messages`, { data: { text, context: { page } } });
  }

  /** `DELETE /api/agent/conversations/{id}`; returns the HTTP status. */
  async deleteConversation(conversationId: string): Promise<number> {
    return (await this.request.delete(`/api/agent/conversations/${conversationId}`)).status();
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

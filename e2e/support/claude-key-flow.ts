/**
 * Run conditions and API-side checks of `e2e/tests/agent-claude-key.spec.ts` (011 follow-up
 * claude-key, T046/T047): skips with a reason when the topology cannot show a case, and the
 * "never in a response" scans of the key status endpoints.
 *
 * @packageDocumentation
 */
import { expect, request, test, type APIResponse, type Browser, type BrowserContext } from '@playwright/test';
import { ClaudeKeySection } from '../pages/claude-key-section';
import type { AgentApi, AgentStatusDto, ClaudeKeyStatusDto } from './agent-api';
import { allSentinels, findLeaks } from './claude-key';
import { apiBaseUrl, webBaseUrl } from './env';
import { isLoopbackUrl, nonLoopbackBaseUrl } from './lan-origin';
import { preferencesRecord, storageStateFor } from './preferences';

/** Skips unless the suite opens the Web app under a loopback host name (only there can a key be entered). */
export function requireLoopbackBaseUrl(): void {
  test.skip(!isLoopbackUrl(webBaseUrl), `E2E_BASE_URL ${webBaseUrl} is not a loopback host; key entry is local-only`);
}

/**
 * The non-loopback base URL of the local-only case; skips when there is none.
 * @returns `E2E_LAN_BASE_URL`, or this computer's LAN address with the web port.
 */
export function requireLanBaseUrl(): string {
  const url = nonLoopbackBaseUrl() ?? '';
  test.skip(url === '', 'no non-loopback address of the Web app (set E2E_LAN_BASE_URL)');
  return url;
}

/**
 * Fails the run when the api already has a key from `ANTHROPIC_API_KEY`: the specs must start
 * without one, so that no real key is ever displayed, replaced or tested by them.
 */
export async function expectNoEnvironmentKey(): Promise<void> {
  const context = await request.newContext({ baseURL: apiBaseUrl });
  try {
    const status = (await (await context.get('/api/agent/status')).json()) as AgentStatusDto;
    expect(status.claude.key.source, 'start the stack without ANTHROPIC_API_KEY in the shell').not.toBe('environment');
  } finally {
    await context.dispose();
  }
}

/** A browser context on `baseUrl` at level Advanced with the welcome dismissed (storage for that origin). */
export async function contextAt(browser: Browser, baseUrl: string): Promise<BrowserContext> {
  const origin = new URL(baseUrl).origin;
  return browser.newContext({ baseURL: baseUrl, storageState: storageStateFor(preferencesRecord({ level: 'advanced' }), origin) });
}

/** Asserts that a response body contains no sentinel and returns its text. */
export async function expectBodyWithout(response: APIResponse, source: string): Promise<string> {
  const body = await response.text();
  expect(findLeaks(source, body, allSentinels), `sentinel in ${source}`).toEqual([]);
  return body;
}

/**
 * Reads the key status from both endpoints that carry it (`GET /api/agent/settings/claude-key`
 * and `claude.key` of `GET /api/agent/status`), asserts that neither body holds a sentinel and
 * that both agree, and returns it (CK-FR-008, US2-4).
 */
export async function keyStatusFromApi(agentApi: AgentApi): Promise<ClaudeKeyStatusDto> {
  const keyResponse = await agentApi.claudeKeyResponse();
  expect(keyResponse.status()).toBe(200);
  const key = JSON.parse(await expectBodyWithout(keyResponse, 'GET claude-key')) as ClaudeKeyStatusDto;
  const status = await agentApi.status();
  expect(findLeaks('GET status', JSON.stringify(status), allSentinels)).toEqual([]);
  expect(status.claude.key).toEqual(key);
  expect(status.claude.keyConfigured).toBe(key.configured);
  return key;
}

/** Asserts a `403 local-only` problem without the sentinel (CK-FR-012, CK-SC-005). */
export async function expectLocalOnlyRefusal(response: APIResponse, source: string): Promise<void> {
  expect(response.status(), source).toBe(403);
  const problem = JSON.parse(await expectBodyWithout(response, source)) as { readonly type?: string };
  expect(problem.type ?? '').toMatch(/\/local-only$/);
}

/**
 * Leaves the stack as the suite found it: no stored key (agent.spec.ts "settings" expects Claude
 * without one). Deletes through the UI on a fresh context; a no-op when the base URL is not a
 * loopback host (the specs were skipped and the controls are disabled there).
 */
export async function removeStoredKey(browser: Browser): Promise<void> {
  if (!isLoopbackUrl(webBaseUrl)) {
    return;
  }
  const context = await contextAt(browser, webBaseUrl);
  try {
    await new ClaudeKeySection(await context.newPage()).openAndDeleteIfStored();
  } finally {
    await context.close();
  }
}

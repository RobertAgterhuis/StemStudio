import type { Page, Request } from '@playwright/test';
import { apiBaseUrl, webBaseUrl } from '../support/env';
import { createFixtureSheet, requireClaudeDouble, requireModelBackend } from '../support/lyric-assist-fixtures';
import { runAssist } from '../support/lyric-assist-flow';
import { expect, test } from '../support/lyric-assist-test';

// Feature 023 US5 (T065; quickstart Q6; SC-006, FR-021..FR-023): local-only and degraded modes.
// `@lyric-assist-none` runs on a stack with STEMSTUDIO_AGENT_BACKEND=none
// (`npm run test:lyric-assist-none`), `@lyric-assist-claude` on the fake Claude double
// (`npm run test:lyric-assist-claude`, devops T069). The browser-side outbound recorder below
// complements the api's failing test handler (DELTA.md 4): the page itself talks only to the app.
const appHosts = new Set([new URL(webBaseUrl).host, new URL(apiBaseUrl).host]);

/** Records every request of the page to a host other than the app's (web, api). */
function recordOutbound(page: Page): Request[] {
  const outbound: Request[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.protocol.startsWith('http') && !appHosts.has(url.host)) outbound.push(request);
  });
  return outbound;
}

test('backend none: Check runs the dictionary layer, the model layers show the fix, model buttons are disabled', { tag: ['@lyric-assist', '@lyric-assist-none'] }, async ({ lyricAssistApi, assistPane, page }) => {
  await requireModelBackend(lyricAssistApi, 'none');
  const outbound = recordOutbound(page);
  const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  await assistPane.goto(sheet.id);
  await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
  const found = await lyricAssistApi.suggestions(sheet.id, { status: 'open' });
  expect(found.length).toBeGreaterThan(0);
  expect(found.filter((suggestion) => suggestion.source === 'model')).toEqual([]);
  await expect(assistPane.byId('lyric-assist-unavailable')).toContainText(/grammar/iu);
  await expect(assistPane.byId('lyric-assist-unavailable')).toContainText(/flow/iu);
  for (const button of ['alternatives', 'continue', 'devices'] as const) {
    await expect(assistPane.byId(`lyric-assist-${button}`), button).toBeDisabled();
  }
  expect(outbound.map((request) => request.url())).toEqual([]);
});

test('Claude: the one-time notice; cancel sends nothing, acknowledge runs the assist with usage', { tag: ['@lyric-assist', '@lyric-assist-claude'] }, async ({ lyricAssistApi, assistPane, page }) => {
  await requireClaudeDouble(lyricAssistApi);
  const outbound = recordOutbound(page);
  const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  await assistPane.goto(sheet.id);
  await assistPane.press('check');
  await expect(assistPane.byId('lyric-assist-claude-notice')).toBeVisible();
  await assistPane.byId('lyric-assist-claude-notice-cancel').click();
  await expect(assistPane.byId('lyric-assist-claude-notice')).toBeHidden();
  expect(await lyricAssistApi.assists(sheet.id)).toEqual([]);

  await assistPane.press('check');
  await assistPane.byId('lyric-assist-claude-notice-ack').click();
  await expect.poll(async () => (await lyricAssistApi.assists(sheet.id)).length).toBe(1);
  const [summary] = await lyricAssistApi.assists(sheet.id);
  const assist = await lyricAssistApi.waitForEnd(summary?.assistId ?? '');
  expect([assist.status, assist.backend]).toEqual(['completed', 'claude']);
  expect(assist.usage?.inputTokens).toBeGreaterThan(0);
  expect((await lyricAssistApi.status()).claudeNoticeRequired).toBe(false);
  expect(outbound.map((request) => request.url())).toEqual([]);
});

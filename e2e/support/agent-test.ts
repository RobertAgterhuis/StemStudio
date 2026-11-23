/**
 * The `test` object of the feature 011 agent specs: the 009/010 `test` (support/generative-test.ts,
 * which holds every fixture of 001-010) extended with the agent client and the agent page objects.
 *
 * @packageDocumentation
 */
import { AgentPanel } from '../pages/agent-panel';
import { ClaudeKeySection } from '../pages/claude-key-section';
import { SettingsPage } from '../pages/settings-page';
import { AgentApi } from './agent-api';
import { apiContextOptions } from './env';
import { test as generative } from './generative-test';

/** Fixtures added by feature 011. */
export interface AgentFixtures {
  readonly agentApi: AgentApi;
  readonly agent: AgentPanel;
  readonly settings: SettingsPage;
  /** The Claude key group of the Settings page (011 follow-up claude-key). */
  readonly claudeKey: ClaudeKeySection;
}

/** Extended Playwright test. */
export const test = generative.extend<AgentFixtures>({
  agentApi: async ({ playwright }, use) => {
    const context = await playwright.request.newContext(apiContextOptions);
    await use(new AgentApi(context));
    await context.dispose();
  },
  agent: async ({ page }, use) => use(new AgentPanel(page)),
  settings: async ({ page }, use) => use(new SettingsPage(page)),
  claudeKey: async ({ page }, use) => use(new ClaudeKeySection(page)),
});

export { expect } from '@playwright/test';

import { expect, type Locator, type Page } from '@playwright/test';
import type { StoredAgentBackend } from '../support/agent-api';
import { isOn } from '../support/controls';

/** The agent section of the settings page `/settings` (ui-test-ids 1.10.0, feature 011). */
export class SettingsPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Element with the given contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Opens `/settings` through the rail link `nav-settings`. */
  async openFromRail(): Promise<void> {
    await this.byId('nav-settings').click();
    await expect(this.page).toHaveURL(/\/settings$/);
    await expect(this.byId('settings-agent-backend')).toBeVisible();
  }

  /** Radio of one backend (`settings-agent-backend-<backend>`). */
  backend(backend: StoredAgentBackend): Locator {
    return this.byId(`settings-agent-backend-${backend}`);
  }

  /** Claude status (`data-key-configured`; never the key). */
  get claudeStatus(): Locator {
    return this.byId('settings-agent-claude-status');
  }

  /** Readable error of the settings endpoint (409/422). */
  get error(): Locator {
    return this.byId('settings-agent-error');
  }

  /** Selects `backend` and saves; asserts that no error is shown and the radio stays selected. */
  async saveBackend(backend: StoredAgentBackend): Promise<void> {
    await this.backend(backend).click();
    await expect.poll(() => isOn(this.backend(backend))).toBe(true);
    await this.byId('settings-agent-save').click();
    await expect(this.error).toHaveCount(0);
  }

  /** Asserts which backend radio is selected (after a reload: the stored value). */
  async expectSelected(backend: StoredAgentBackend): Promise<void> {
    await expect.poll(() => isOn(this.backend(backend))).toBe(true);
  }
}

import { expect, type Locator } from '@playwright/test';
import { allSentinels, findLeaks } from '../support/claude-key';
import { TestIdPage } from './test-id-page';

/** Values of `data-state` on `settings-agent-claude-key-status` (ui-test-ids 1.21.0). */
export type ClaudeKeyState = 'not-configured' | 'environment' | 'stored' | 'stored-unreadable';

/** The status attributes of `settings-agent-claude-key-status`. */
export interface ClaudeKeyStatusAttributes {
  readonly state: ClaudeKeyState;
  /** `environment`, `stored` or empty. */
  readonly source: string;
  readonly stored: 'absent' | 'readable' | 'unreadable';
}

/**
 * The Claude API key group of the Settings page (`settings-agent-claude-key*`, ui-test-ids 1.21.0,
 * 011 follow-up claude-key). Every action goes through the UI, because the API refuses key writes
 * that do not come from the Web app (`403 local-only`).
 */
export class ClaudeKeySection extends TestIdPage {
  /** The whole group (the scope of the axe scan). */
  get group(): Locator {
    return this.byId('settings-agent-claude-key');
  }

  /** The status line. */
  get status(): Locator {
    return this.byId('settings-agent-claude-key-status');
  }

  /** The password field. */
  get input(): Locator {
    return this.byId('settings-agent-claude-key-input');
  }

  /** "Save key" / "Save new key". */
  get save(): Locator {
    return this.byId('settings-agent-claude-key-save');
  }

  /** The readable error of a failed key action. */
  get error(): Locator {
    return this.byId('settings-agent-claude-key-error');
  }

  /** The delete confirmation. */
  get confirmation(): Locator {
    return this.byId('settings-agent-claude-key-delete-confirm');
  }

  /** Opens `/settings` (relative to the context's `baseURL`) and waits for the key status. */
  async open(): Promise<void> {
    await this.page.goto('/settings');
    await expect(this.status).toBeVisible();
    await expect(this.group).toHaveAttribute('role', 'group');
  }

  /** Asserts the status attributes (and, when given, that the text names `lastFour`). */
  async expectStatus(expected: ClaudeKeyStatusAttributes, lastFour?: string): Promise<void> {
    await expect(this.status).toHaveAttribute('data-state', expected.state);
    await expect(this.status).toHaveAttribute('data-source', expected.source);
    await expect(this.status).toHaveAttribute('data-stored', expected.stored);
    if (lastFour !== undefined) {
      await expect(this.status).toContainText(`...${lastFour}`);
    }
  }

  /**
   * Types `key` into the empty field and presses Save; asserts that the field is emptied
   * afterwards (it stays, empty, after a refused value and is hidden once a key is stored).
   */
  async saveKey(key: string): Promise<void> {
    await expect(this.input).toHaveValue('');
    await this.input.fill(key);
    await expect(this.save).toBeEnabled();
    await this.save.click();
    await expect.poll(() => this.fieldIsEmptyOrHidden(), 'field emptied after the save attempt').toBe(true);
  }

  private async fieldIsEmptyOrHidden(): Promise<boolean> {
    return (await this.input.count()) === 0 || (await this.input.inputValue()) === '';
  }

  /** Presses "Replace key" and saves `key` through the field that opens. */
  async replaceKey(key: string): Promise<void> {
    await this.byId('settings-agent-claude-key-replace').click();
    await expect(this.save).toHaveText(/Save new key/);
    await this.saveKey(key);
  }

  /** Opens the delete confirmation and asserts it took the focus on "Keep key". */
  async openDeleteConfirmation(): Promise<void> {
    await this.byId('settings-agent-claude-key-delete').click();
    await expect(this.confirmation).toBeVisible();
    await expect(this.confirmation).toHaveAttribute('role', 'alertdialog');
    await expect(this.byId('settings-agent-claude-key-delete-confirm-no')).toBeFocused();
  }

  /** Deletes the stored key through the confirmation. */
  async deleteKey(): Promise<void> {
    await this.openDeleteConfirmation();
    await this.byId('settings-agent-claude-key-delete-confirm-yes').click();
    await expect(this.confirmation).toHaveCount(0);
  }

  /** Opens Settings and deletes a stored key when one is shown (set-up and clean-up; a no-op without one). */
  async openAndDeleteIfStored(): Promise<void> {
    await this.open();
    if ((await this.status.getAttribute('data-stored')) !== 'absent') {
      await this.deleteKey();
      await expect(this.status).toHaveAttribute('data-stored', 'absent');
    }
  }

  /** Asserts that neither the page HTML nor the key field's value contains a sentinel (any search form). */
  async expectDomWithout(keys: readonly string[] = allSentinels): Promise<void> {
    expect(findLeaks('DOM', await this.page.content(), keys), 'sentinel in the rendered DOM').toEqual([]);
    if ((await this.input.count()) > 0) {
      expect(findLeaks('key field', await this.input.inputValue(), keys), 'sentinel in the key field').toEqual([]);
    }
  }
}

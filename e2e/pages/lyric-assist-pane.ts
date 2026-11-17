import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** `lyric-assist-pane[data-state]` (ui-test-ids 1.23.0). */
export type PaneState = 'idle' | 'running' | 'done' | 'unavailable';

/** The assistant buttons of the pane. */
export type AssistButton = 'check' | 'devices' | 'alternatives' | 'continue' | 'titles';

/** The lyric assistant pane of the 010 lyrics editor `/lyrics/{id}` (ui-test-ids 1.23.0, feature 023). */
export class LyricAssistPane extends TestIdPage {
  /** The pane region (`data-state`). */
  get pane(): Locator {
    return this.byId('lyric-assist-pane');
  }

  /** The editor's text area (010). */
  get text(): Locator {
    return this.byId('lyrics-text-input');
  }

  /** The suggestion list (`role="listbox"`). */
  get list(): Locator {
    return this.byId('lyric-assist-list');
  }

  /** Polite live region of the announcements. */
  get live(): Locator {
    return this.byId('lyric-assist-live');
  }

  /** "Undo" of the newest accept. */
  get undo(): Locator {
    return this.byId('lyric-assist-undo');
  }

  /** Refusal text of a sheet that matches library lyrics. */
  get notOwn(): Locator {
    return this.byId('lyric-assist-not-own');
  }

  /** "n suggestions withheld (too close to existing lyrics)". */
  get withheld(): Locator {
    return this.byId('lyric-assist-withheld');
  }

  /** The own-work badge. */
  get ownWorkBadge(): Locator {
    return this.byId('lyric-own-work-badge');
  }

  /** Every suggestion entry. */
  get entries(): Locator {
    return this.page.getByTestId(/^lyric-suggestion-[0-9a-f-]{36}$/u);
  }

  /** One suggestion entry (`data-category`, `data-status`, `data-line`, ...). */
  entry(suggestionId: string): Locator {
    return this.byId(`lyric-suggestion-${suggestionId}`);
  }

  /** A part of an entry: `accept`, `reject`, `intentional`, `add-word`, `original`, `proposal`, `syllables`, ... */
  part(part: string, suggestionId: string): Locator {
    return this.byId(`lyric-suggestion-${part}-${suggestionId}`);
  }

  /** Opens the editor of a sheet with the pane shown. */
  async goto(lyricsId: string): Promise<void> {
    await this.page.goto(`/lyrics/${lyricsId}`);
    await expect(this.text).toBeVisible();
    if (!(await this.pane.isVisible())) {
      await this.byId('lyric-assist-toggle').click();
    }
    await expect(this.pane).toBeVisible();
  }

  /**
   * Presses an assistant button without waiting for the outcome (specs wait on the id of the
   * assist it started: `support/lyric-assist-flow.ts` `runAssist`).
   */
  async press(button: AssistButton): Promise<void> {
    await this.byId(`lyric-assist-${button}`).click();
  }

  /** Sets the sensitivity radio (Advanced/Expert). */
  async setSensitivity(sensitivity: 'loose' | 'balanced' | 'strict'): Promise<void> {
    await this.byId(`lyric-assist-sensitivity-${sensitivity}`).check();
  }

  /** Toggles one category filter on (or `all`). */
  async filter(category: string): Promise<void> {
    await this.byId(`lyric-assist-filter-${category}`).click();
    await expect(this.byId(`lyric-assist-filter-${category}`)).toHaveAttribute('aria-pressed', 'true');
  }

  /** Accepts one suggestion and waits until it left the list (the list shows open suggestions only). */
  async accept(suggestionId: string): Promise<void> {
    await this.part('accept', suggestionId).click();
    await expect(this.entry(suggestionId)).toHaveCount(0);
  }

  /** Puts the editor cursor at the start of line `line` (1-based). */
  async cursorToLine(line: number): Promise<void> {
    await this.text.evaluate((el, n) => {
      const area = el as HTMLTextAreaElement;
      const offset = area.value.split('\n').slice(0, n - 1).reduce((sum, row) => sum + row.length + 1, 0);
      area.focus();
      area.setSelectionRange(offset, offset);
      area.dispatchEvent(new Event('select', { bubbles: true }));
    }, line);
  }

  /** Confirms own work in the dialog (name + tick box) and waits for the badge. */
  async confirmOwnWork(name: string): Promise<void> {
    await this.byId('lyric-assist-own-work-open').click();
    const confirm = this.byId('lyric-own-work-confirm');
    await expect(this.byId('lyric-own-work-dialog')).toBeVisible();
    await expect(confirm).toBeDisabled();
    await this.byId('lyric-own-work-name').fill(name);
    await expect(confirm).toBeDisabled();
    await this.byId('lyric-own-work-check').check();
    await expect(confirm).toBeEnabled();
    await confirm.click();
    await expect(this.ownWorkBadge).toBeVisible();
  }

  /** Revokes the own-work confirmation through the shared confirm dialog. */
  async revokeOwnWork(): Promise<void> {
    await this.byId('lyric-own-work-revoke').click();
    await this.byId('confirm-dialog-ok').click();
    await expect(this.ownWorkBadge).toHaveCount(0);
  }
}

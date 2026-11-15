/**
 * Counted keyboard input for the Expert keyboard journey (SC-008: at most 25 key presses after
 * the import URL is typed).
 *
 * Counting rule (docs/testing/TEST-STRATEGY.md section 5, feature 004): every `press` is one key
 * press, a chord such as `ControlOrMeta+K` included; typing a text into a field (palette query,
 * mix name) counts as one entry, like typing the URL, which SC-008 excludes from the budget.
 *
 * @packageDocumentation
 */
import type { Page } from '@playwright/test';

/** Sends keys to a page and counts them. */
export class KeyCounter {
  private presses = 0;
  private entries = 0;

  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Presses a key or chord (Playwright key syntax) and counts one press. */
  async press(key: string): Promise<void> {
    this.presses += 1;
    await this.page.keyboard.press(key);
  }

  /** Types `text` into the focused field and counts one text entry. */
  async type(text: string): Promise<void> {
    this.entries += 1;
    await this.page.keyboard.type(text);
  }

  /** Key presses plus text entries so far (the SC-008 budget). */
  get total(): number {
    return this.presses + this.entries;
  }
}

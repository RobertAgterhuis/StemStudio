import { expect, type Locator, type Page } from '@playwright/test';

/** Ways to open the help panel (FR-023, quickstart V-H1). */
export type HelpOpener = 'button' | 'nav' | 'question-key' | 'f1-key';

const testIdPrefix = 'help-result-';

/** Contextual help panel (ui-test-ids 1.3.0 "Help"). */
export class HelpPanel {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Element with the given contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** The panel (`aside`, `data-topic-id` of the shown topic). */
  get panel(): Locator {
    return this.byId('help-panel');
  }

  /** Opens the panel with one of the openers and waits until it is visible. */
  async open(opener: HelpOpener): Promise<void> {
    await this.trigger(opener);
    await expect(this.panel).toBeVisible();
  }

  private async trigger(opener: HelpOpener): Promise<void> {
    const actions: Record<HelpOpener, () => Promise<void>> = {
      button: () => this.byId('help-toggle').click(),
      nav: () => this.byId('nav-help').click(),
      'question-key': () => this.page.keyboard.press('?'),
      'f1-key': () => this.page.keyboard.press('F1'),
    };
    await actions[opener]();
  }

  /** Waits until the panel shows `topicId`. */
  async expectTopic(topicId: string): Promise<void> {
    await expect(this.panel).toHaveAttribute('data-topic-id', topicId);
  }

  /** Types a query into the panel's search field. */
  async search(query: string): Promise<void> {
    await this.byId('help-search-input').fill(query);
  }

  /** All search result buttons, in display order. */
  get results(): Locator {
    return this.page.getByTestId(new RegExp(`^${testIdPrefix}`));
  }

  /** Topic id of the first search result (waits until there is one). */
  async firstResultTopic(): Promise<string> {
    const first = this.results.first();
    await expect(first).toBeVisible();
    return ((await first.getAttribute('data-testid')) ?? '').slice(testIdPrefix.length);
  }

  /** Opens a topic through search: query, then the result of `topicId`. */
  async openTopic(query: string, topicId: string): Promise<void> {
    await this.search(query);
    await this.byId(`${testIdPrefix}${topicId}`).click();
    await this.expectTopic(topicId);
  }

  /** Related-topic links of the shown topic. */
  get relatedLinks(): Locator {
    return this.page.getByTestId(/^help-related-/);
  }

  /** Closes the panel with Escape and waits until it is gone. */
  async closeWithEscape(): Promise<void> {
    await this.page.keyboard.press('Escape');
    await expect(this.panel).toBeHidden();
  }
}

import { expect, type Locator, type Page } from '@playwright/test';
import { laneSeconds, type SectionDto } from '../support/practice-flow';

/**
 * The section lane of the mixer (ui-test-ids 1.13.0, feature 014): the list of section buttons, its status text,
 * "Try again" and the transport-row section buttons.
 */
export class SectionLane {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** The lane (`role="list"`, `data-status`, `data-revision`). */
  get lane(): Locator {
    return this.byId('section-lane');
  }

  /** The status text. */
  get status(): Locator {
    return this.byId('section-status');
  }

  /** "Try again" (only when detection failed). */
  get retry(): Locator {
    return this.byId('section-retry');
  }

  /** Every section button in the lane, in order. */
  get buttons(): Locator {
    return this.lane.locator('[data-testid^="section-"][data-kind]');
  }

  /** The button of one section. */
  section(id: string): Locator {
    return this.byId(`section-${id}`);
  }

  /** Any element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Waits until the lane is `ready` with `count` sections. */
  async waitForReady(count: number, timeoutMs: number): Promise<void> {
    await expect(this.lane).toHaveAttribute('data-status', 'ready', { timeout: timeoutMs });
    await expect(this.buttons).toHaveCount(count);
  }

  /**
   * Asserts that the button's text starts with `label` (ui-test-ids: text = label). The button also shows the kind and a
   * screen-reader range ("bars 1 to 5, playing now") after the label.
   */
  async expectLabel(sectionId: string, label: string): Promise<void> {
    const escaped = label.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&');
    await expect(this.section(sectionId)).toHaveText(new RegExp(`^\\s*${escaped}(\\s|,|$)`, 'u'));
  }

  /** Asserts that a button shows the section's label, kind and bounds (1 decimal). */
  async expectSection(section: SectionDto): Promise<void> {
    const button = this.section(section.id);
    await this.expectLabel(section.id, section.label);
    await expect(button).toHaveAttribute('data-kind', section.kind);
    await expect(button).toHaveAttribute('data-start', laneSeconds(section.startSeconds));
    await expect(button).toHaveAttribute('data-end', laneSeconds(section.endSeconds));
  }

  /** The id of the section marked current (`data-current="true"`), or null. */
  async currentId(): Promise<string | null> {
    const current = this.lane.locator('[data-current="true"]');
    return (await current.count()) === 0 ? null : ((await current.first().getAttribute('data-testid'))?.replace(/^section-/u, '') ?? null);
  }
}

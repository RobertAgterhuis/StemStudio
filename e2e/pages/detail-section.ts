import { expect, type Locator, type Page } from '@playwright/test';
import type { DetailPhaseStatus, DetailStemName, ParentStemName } from '../support/api';
import { oneOf } from '../support/text';

/** Detail-phase section of the track detail page `/tracks/{id}` (ui-test-ids.md 1.1.0). */
export class DetailSection {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Container of the detail phase. */
  get section(): Locator {
    return this.page.getByTestId('detail-section');
  }

  /** Status text: `none`, `queued`, `running`, `completed`, `failed` or `cancelled`. */
  get status(): Locator {
    return this.page.getByTestId('detail-status');
  }

  /** Current step, e.g. "Splitting drums (1 of 3)". */
  get stage(): Locator {
    return this.page.getByTestId('detail-stage');
  }

  /** Progress bar exposing `aria-valuenow` 0-100. */
  get progress(): Locator {
    return this.page.getByTestId('detail-progress');
  }

  /** "This can take a long time" notice (queued/running, and songs > 10 min). */
  get longNotice(): Locator {
    return this.page.getByTestId('detail-long-notice');
  }

  /** Failure reason (only while failed). */
  get error(): Locator {
    return this.page.getByTestId('detail-error');
  }

  /** Retry of a failed or cancelled phase. */
  get retry(): Locator {
    return this.page.getByTestId('detail-retry');
  }

  /** "Split further" of a track without a detail phase (status `none`). */
  get splitFurther(): Locator {
    return this.page.getByTestId('detail-split-further');
  }

  /** Cancel of a queued or running phase. */
  get cancel(): Locator {
    return this.page.getByTestId('detail-cancel');
  }

  /** Limitations of detail separation (FR-017). */
  get limitationHelp(): Locator {
    return this.page.getByTestId('detail-limitation-help');
  }

  /** Reason shown when a delete is refused because the detail phase is active (FR-016). */
  get deleteBlocked(): Locator {
    return this.page.getByTestId('track-delete-blocked');
  }

  /** Outcome of one parent (`data-outcome` = kind, text = user text). */
  outcome(parent: ParentStemName): Locator {
    return this.page.getByTestId(`detail-outcome-${parent}`);
  }

  /** One detail stem listed under its parent. */
  stem(parent: ParentStemName, detail: DetailStemName): Locator {
    return this.page.getByTestId(`detail-stem-${parent}-${detail}`);
  }

  /** Waits until the status text is exactly one of `statuses` (case-insensitive). */
  async waitForStatus(statuses: DetailPhaseStatus | readonly DetailPhaseStatus[], timeoutMs: number): Promise<void> {
    const list = typeof statuses === 'string' ? [statuses] : statuses;
    await expect(this.status).toHaveText(oneOf(list), { timeout: timeoutMs });
  }

  /** Current progress (`aria-valuenow`). */
  async progressValue(): Promise<number> {
    return Number((await this.progress.getAttribute('aria-valuenow')) ?? Number.NaN);
  }
}

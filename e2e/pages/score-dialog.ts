import { expect, type Locator, type Page } from '@playwright/test';
import type { ScoreProfile } from '../support/score-api';

/** What the spec picks in the dialog (unset fields keep the dialog's defaults). */
export interface ScoreChoices {
  readonly profile?: ScoreProfile;
  readonly simplified?: boolean;
  readonly keyOverride?: string;
}

/** The sheet music dialog (ui-test-ids 1.7.0, feature 008). */
export class ScoreDialog {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any id of the page. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** The confirmation after submit. */
  get saved(): Locator {
    return this.byId('score-saved');
  }

  /** Link to the new item on `/library`. */
  get savedOpen(): Locator {
    return this.byId('score-saved-open');
  }

  /** The dialog (`data-source` = `channel` or `asset`). */
  get dialog(): Locator {
    return this.byId('score-dialog');
  }

  /** Readable error (validation, 409, 422, 503). */
  get error(): Locator {
    return this.byId('score-error');
  }

  /** Opens the dialog from the track page for a standard stem. */
  async openForStem(stem: string): Promise<void> {
    await this.byId(`score-stem-${stem}`).click();
    await expect(this.dialog).toHaveAttribute('data-source', 'channel');
  }

  /** Opens the dialog from a library row. */
  async openForAsset(assetId: string): Promise<void> {
    await this.byId(`asset-score-${assetId}`).click();
    await expect(this.dialog).toHaveAttribute('data-source', 'asset');
  }

  /** Applies `choices` and submits; waits for the confirmation. */
  async generate(choices: ScoreChoices = {}): Promise<void> {
    if (choices.profile !== undefined) {
      await this.byId('score-profile').selectOption(choices.profile);
    }
    if (choices.simplified !== undefined) {
      await this.byId('score-resolution').setChecked(choices.simplified);
    }
    if (choices.keyOverride !== undefined) {
      await this.byId('score-key-override').selectOption({ label: choices.keyOverride });
    }
    await this.byId('score-submit').click();
    await expect(this.saved).toBeVisible();
  }

  /** Id of the new item, read from the `score-saved-open` link (`/library#<assetId>` or `?asset=`). */
  async savedAssetId(): Promise<string> {
    const href = (await this.savedOpen.getAttribute('href')) ?? '';
    const id = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/iu.exec(href)?.[0];
    expect(id, `asset id in ${href}`).toBeDefined();
    return id ?? '';
  }
}

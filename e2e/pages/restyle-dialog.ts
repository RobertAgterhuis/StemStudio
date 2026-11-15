import { expect, type Locator } from '@playwright/test';
import type { StemName } from '../support/api';
import { setNumericValue } from '../support/controls';
import { applySteps, TestIdPage } from './test-id-page';

/** Choices of the restyle dialog; unset fields keep the dialog's defaults. */
export interface RestyleChoices {
  readonly style: string;
  readonly mode?: 'range' | 'inPlace';
  readonly range?: { readonly start: number; readonly end: number };
  readonly excludeStems?: readonly StemName[];
  /** Slider position 0-4 (subtle ... free). */
  readonly strength?: number;
  readonly takes?: 1 | 2;
}

/** The restyle dialog opened from the track page (ui-test-ids 1.8.0 `restyle-*`). */
export class RestyleDialog extends TestIdPage {
  /** The "Restyle" button on the track page. */
  get openButton(): Locator {
    return this.byId('track-restyle');
  }

  /** The dialog (`data-track-id`). */
  get dialog(): Locator {
    return this.byId('restyle-dialog');
  }

  /** Description of the selected style. */
  get description(): Locator {
    return this.byId('restyle-style-description');
  }

  /** The 5-step strength slider (`aria-valuenow` 0-4, `aria-valuetext` the step name). */
  get strength(): Locator {
    return this.byId('restyle-strength');
  }

  /** Personal-use / AI / no-imitation notice. */
  get notice(): Locator {
    return this.byId('restyle-personal-use-notice');
  }

  /** Readable error. */
  get error(): Locator {
    return this.byId('restyle-error');
  }

  /** Opens the dialog from the track page. */
  async open(): Promise<void> {
    await this.openButton.click();
    await expect(this.dialog).toBeVisible();
  }

  /** Selects a style by its visible name. */
  async chooseStyle(name: string): Promise<void> {
    await this.byId('restyle-style-select').selectOption({ label: name });
  }

  /** Applies `choices` (Advanced/Expert: every option is rendered) without starting. */
  async fill(choices: RestyleChoices): Promise<void> {
    await this.chooseStyle(choices.style);
    const { mode, range, strength, takes } = choices;
    await applySteps([
      [mode, () => this.byId(mode === 'inPlace' ? 'restyle-mode-inplace' : 'restyle-mode-range').check()],
      [range, () => this.setRange(range?.start ?? 0, range?.end ?? 0)],
      [strength, () => setNumericValue(this.strength, strength ?? 0)],
      [takes, () => setNumericValue(this.byId('restyle-takes'), takes ?? 1)],
    ]);
    for (const stem of choices.excludeStems ?? []) await this.byId(`restyle-stem-${stem}`).uncheck();
  }

  /** Sets the range inputs (seconds). */
  async setRange(start: number, end: number): Promise<void> {
    await setNumericValue(this.byId('restyle-range-start'), start);
    await setNumericValue(this.byId('restyle-range-end'), end);
  }

  /** Clicks "Start" and waits for the dialog to close. */
  async start(): Promise<void> {
    await this.byId('restyle-start').click();
    await expect(this.dialog).toBeHidden();
  }
}

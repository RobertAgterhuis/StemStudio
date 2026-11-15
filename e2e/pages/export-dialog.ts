import { expect, type Locator, type Page } from '@playwright/test';
import type { AudioFileFormat } from '../support/assets-api';

/** `data-mode` of the export dialog. */
export type ExportDialogMode = 'stem' | 'mix' | 'stem-pack';

/** Choices of one export; unset fields keep the dialog's defaults. */
export interface ExportChoices {
  readonly format?: AudioFileFormat;
  readonly bitDepth?: 16 | 24;
  readonly mp3Bitrate?: 192 | 256 | 320;
  readonly name?: string;
  /** Tick "Only the loop region" (mixer with a loop region). */
  readonly loopOnly?: boolean;
  /** "Selected channels" with exactly these channel keys (`drums`, `drums-kick`). */
  readonly selectedChannels?: readonly string[];
  /** Stem pack only. */
  readonly includeDetail?: boolean;
}

/**
 * Export actions on the track page and in the mixer, and the export dialog (ui-test-ids 1.5.0,
 * feature 007).
 */
export class ExportDialog {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any id of the dialog or its triggers. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** The dialog (`data-mode`). */
  get dialog(): Locator {
    return this.byId('export-dialog');
  }

  /** Readable reason of a refused export. */
  get error(): Locator {
    return this.byId('export-error');
  }

  /** Confirmation after submit. */
  get saved(): Locator {
    return this.byId('export-saved');
  }

  /** Link from the confirmation to the item on `/library`. */
  get savedOpen(): Locator {
    return this.byId('export-saved-open');
  }

  /** Opens the dialog for one standard stem (track page). */
  async openForStem(stem: string): Promise<void> {
    await this.openWith(`export-stem-${stem}`, 'stem');
  }

  /** Opens the dialog for one kept detail stem (track page). */
  async openForDetail(parent: string, detail: string): Promise<void> {
    await this.openWith(`export-detail-${parent}-${detail}`, 'stem');
  }

  /** Opens the dialog from the mixer. */
  async openForMix(): Promise<void> {
    await this.openWith('mixer-export', 'mix');
  }

  /** Opens the stem-pack dialog (track page, Advanced). */
  async openForStemPack(): Promise<void> {
    await this.openWith('export-all-stems', 'stem-pack');
  }

  /** Applies `choices` and submits; waits for the confirmation. */
  async save(choices: ExportChoices = {}): Promise<void> {
    await this.choose(choices);
    await this.byId('export-submit').click();
    await expect(this.saved).toBeVisible({ timeout: 30_000 });
  }

  /** The asset id the confirmation links to (from the `href` of `export-saved-open`). */
  async savedAssetId(): Promise<string> {
    const href = (await this.savedOpen.getAttribute('href')) ?? '';
    const id = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/.exec(href)?.[0];
    expect(id, `export-saved-open href "${href}"`).toBeDefined();
    return id ?? '';
  }

  private async openWith(trigger: string, mode: ExportDialogMode): Promise<void> {
    await this.byId(trigger).click();
    await expect(this.dialog).toHaveAttribute('data-mode', mode);
  }

  private async choose(choices: ExportChoices): Promise<void> {
    if (choices.selectedChannels !== undefined) await this.selectChannels(choices.selectedChannels);
    if (choices.format !== undefined) await this.byId('export-format').selectOption(choices.format);
    if (choices.bitDepth !== undefined) await this.byId('export-bit-depth').selectOption(String(choices.bitDepth));
    if (choices.mp3Bitrate !== undefined) await this.byId('export-mp3-bitrate').selectOption(String(choices.mp3Bitrate));
    await this.chooseFlags(choices);
  }

  private async chooseFlags(choices: ExportChoices): Promise<void> {
    if (choices.loopOnly !== undefined) await this.byId('export-range-loop').setChecked(choices.loopOnly);
    if (choices.includeDetail !== undefined) await this.byId('export-include-detail').setChecked(choices.includeDetail);
    if (choices.name !== undefined) await this.byId('export-name').fill(choices.name);
  }

  private async selectChannels(keys: readonly string[]): Promise<void> {
    await this.byId('export-source-selected').check();
    const boxes = this.page.locator('[data-testid^="export-channel-"]');
    for (const box of await boxes.all()) {
      const key = ((await box.getAttribute('data-testid')) ?? '').replace('export-channel-', '');
      await box.setChecked(keys.includes(key));
    }
  }
}

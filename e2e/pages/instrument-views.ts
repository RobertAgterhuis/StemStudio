import { expect, type Locator, type Page } from '@playwright/test';
import type { InstrumentFamily } from '../support/instrument-views';

/** Drum pieces of the hit lanes (ui-test-ids placeholder `<piece>`). */
export type DrumPiece = 'kick' | 'snare' | 'toms' | 'hihat' | 'cymbals';

/**
 * The instrument views of the mixer and the track page (ui-test-ids 1.15.0, feature 016): "Practise as", chord lane,
 * chord sheet, key, capo mode, drum trainer and singing feedback. Locators use `getByTestId` only.
 */
export class InstrumentViews {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any id of the page. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** "Practise as ..." menu button (`data-family`). */
  get practiseAs(): Locator {
    return this.byId('practise-as');
  }

  /** The region hosting the open family view (`data-view`). */
  get view(): Locator {
    return this.byId('instrument-view');
  }

  /** Chooses a family in the "Practise as" menu and waits for its view. */
  async practiseAsFamily(family: InstrumentFamily): Promise<void> {
    await this.practiseAs.click();
    await this.byId(`practise-as-${family}`).click();
    await expect(this.practiseAs).toHaveAttribute('data-family', family);
    await expect(this.view).toBeVisible();
  }

  /** Engine-owned chord lane host (`data-current-symbol`, `data-next-symbol`, `data-current-key`, `data-capo`). */
  get chordLane(): Locator {
    return this.byId('chord-lane');
  }

  /** Chords status (`data-status`). */
  get chordsStatus(): Locator {
    return this.byId('chords-status');
  }

  /** The chord sheet of the lyrics card. */
  get chordSheet(): Locator {
    return this.byId('chord-sheet');
  }

  /** One row of the chord sheet. */
  sheetRow(n: number): Locator {
    return this.byId(`chord-sheet-row-${String(n)}`);
  }

  /** One chord of a chord sheet row. */
  sheetChord(row: number, chord: number): Locator {
    return this.byId(`chord-sheet-chord-${String(row)}-${String(chord)}`);
  }

  /** Every edit button of the chord sheet (Advanced). */
  get chordEdits(): Locator {
    return this.page.locator('[data-testid^="chord-edit-"][data-help-id="chord-edit"]');
  }

  /** Key next to the mixer tempo (`data-tonic`, `data-mode`, `data-uncertain`). */
  get mixerKey(): Locator {
    return this.byId('mixer-key');
  }

  /** Key of the current region during playback. */
  get mixerKeyCurrent(): Locator {
    return this.byId('mixer-key-current');
  }

  /** Capo mode toggle (`aria-pressed`, guitar family only). */
  get capoMode(): Locator {
    return this.byId('capo-mode');
  }

  /** Drum hit lanes host (`data-next-hit-piece`, `data-next-hit-ms`, `data-visible-hits`). */
  get drumLanes(): Locator {
    return this.byId('drum-lanes');
  }

  /** "Drumless" toggle (`aria-pressed`). */
  get drumless(): Locator {
    return this.byId('drumless');
  }

  /** Pitch view host of singing feedback (`data-state`, `data-singer-cents`, `data-last-score`). */
  get pitchView(): Locator {
    return this.byId('pitch-view');
  }

  /** Singing start/stop (`aria-pressed`). */
  get singingToggle(): Locator {
    return this.byId('singing-toggle');
  }

  /** Summary card after stop (`data-average`, `data-units`). */
  get singingSummary(): Locator {
    return this.byId('singing-summary');
  }

  /** Waits until the lane host shows a symbol, then returns it. */
  async currentSymbol(): Promise<string> {
    await expect(this.chordLane).toHaveAttribute('data-current-symbol', /.+/);
    return (await this.chordLane.getAttribute('data-current-symbol')) ?? '';
  }
}

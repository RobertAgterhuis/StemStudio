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

  /** Drum trainer region. */
  get drumTrainer(): Locator {
    return this.byId('drum-trainer');
  }

  /** Lane label button of a piece (`aria-pressed` = guide piece at Advanced+). */
  drumLaneLabel(piece: DrumPiece): Locator {
    return this.byId(`drum-lane-label-${piece}`);
  }

  /** The 014 score view host of the track's drum score (`data-bar-number`). */
  get drumScore(): Locator {
    return this.byId('drum-score');
  }

  /** Waits until the lane host names its next hit and returns `{piece, ms}`. */
  async nextHit(): Promise<{ readonly piece: string; readonly ms: number }> {
    await expect(this.drumLanes).toHaveAttribute('data-next-hit-ms', /\d/);
    const piece = (await this.drumLanes.getAttribute('data-next-hit-piece')) ?? '';
    return { piece, ms: Number(await this.drumLanes.getAttribute('data-next-hit-ms')) };
  }

  /** Singing feedback region. */
  get singing(): Locator {
    return this.byId('singing');
  }

  /** Reference melody status (`data-status`). */
  get singingReference(): Locator {
    return this.byId('singing-reference-status');
  }

  /** Every unit score element next to the karaoke banner (`data-score`, `data-unit-index`). */
  get karaokeScores(): Locator {
    return this.byId('karaoke-score');
  }

  /** Starts singing: analyses the reference when needed, then presses start. */
  async startSinging(timeoutMs: number): Promise<void> {
    await expect(this.singing).toBeVisible();
    await this.analyseReferenceIfNeeded();
    await expect(this.singingReference).toHaveAttribute('data-status', 'ready', { timeout: timeoutMs });
    await expect(this.singingToggle).toBeEnabled();
    await this.singingToggle.click();
    await expect(this.singingToggle).toHaveAttribute('aria-pressed', 'true');
  }

  private async analyseReferenceIfNeeded(): Promise<void> {
    const analyse = this.byId('singing-reference-analyse');
    if (await analyse.isVisible()) {
      await analyse.click();
    }
  }

  /** Stops singing and returns the summary average (`data-average`). */
  async stopSinging(): Promise<number> {
    await this.singingToggle.click();
    await expect(this.singingSummary).toBeVisible();
    return Number(await this.singingSummary.getAttribute('data-average'));
  }

  /** The written key of a transposing instrument ("Your part: A major"). */
  get mixerKeyWritten(): Locator {
    return this.byId('mixer-key-written');
  }

  /** Key next to `track-bpm` on the track page. */
  get trackKey(): Locator {
    return this.byId('track-key');
  }

  /** "Capo 3" badge while capo mode is on with capo > 0. */
  get capoBadge(): Locator {
    return this.byId('capo-badge');
  }

  /** "Capo 2: play the original shapes" shortcut. */
  get capoShortcut(): Locator {
    return this.byId('capo-shortcut');
  }

  /** One capo suggestion button (`data-capo`). */
  capoSuggestion(n: number): Locator {
    return this.byId(`capo-suggestion-${String(n)}`);
  }

  /** Every capo suggestion button, in list order. */
  get capoSuggestions(): Locator {
    return this.byId('capo-suggestions').locator('[data-testid^="capo-suggestion-"]');
  }

  /** Turns capo mode on (guitar family). */
  async enableCapoMode(): Promise<void> {
    await expect(this.capoMode).toBeVisible();
    if ((await this.capoMode.getAttribute('aria-pressed')) !== 'true') {
      await this.capoMode.click();
    }
    await expect(this.capoMode).toHaveAttribute('aria-pressed', 'true');
  }

  /** Waits until the lane host shows a symbol, then returns it. */
  async currentSymbol(): Promise<string> {
    await expect(this.chordLane).toHaveAttribute('data-current-symbol', /.+/);
    return (await this.chordLane.getAttribute('data-current-symbol')) ?? '';
  }
}

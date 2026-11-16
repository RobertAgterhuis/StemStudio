import { expect, type Locator, type Page } from '@playwright/test';
import type { StemName, TrackStatus } from '../support/api';

const trackUrl = /\/tracks\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/?$/;

/** Track detail / progress view `/tracks/{id}` (ui-test-ids.md, "Track detail"). */
export class TrackDetailPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Status text (queued ... completed, failed). */
  get status(): Locator {
    return this.page.getByTestId('track-status');
  }

  /** Human-readable stage, e.g. "Separating stems". */
  get stage(): Locator {
    return this.page.getByTestId('track-stage');
  }

  /** Progress bar exposing `aria-valuenow` 0-100. */
  get progress(): Locator {
    return this.page.getByTestId('track-progress');
  }

  /** Failure reason (only while failed). */
  get error(): Locator {
    return this.page.getByTestId('track-error');
  }

  /** Retry button of a failed track. */
  get retry(): Locator {
    return this.page.getByTestId('track-retry');
  }

  /** Extra message shown when a retry started on this page ended in failed again. */
  get retryFailedAgain(): Locator {
    return this.page.getByTestId('track-retry-failed-again');
  }

  /** Delete button of a failed or completed track (opens the inline confirmation). */
  get deleteButton(): Locator {
    return this.page.getByTestId('track-delete');
  }

  /** Confirms the delete in the inline confirmation. */
  get deleteConfirm(): Locator {
    return this.page.getByTestId('track-delete-confirm');
  }

  /** Cancels the inline delete confirmation. */
  get deleteCancel(): Locator {
    return this.page.getByTestId('track-delete-cancel');
  }

  /** Detected BPM. */
  get bpm(): Locator {
    return this.page.getByTestId('track-bpm');
  }

  /** Model used (`htdemucs_6s`, `htdemucs` or `fake`). */
  get model(): Locator {
    return this.page.getByTestId('track-model');
  }

  /**
   * Device of the standard separation (ui-test-ids 1.2.0): exists once `track.device` is set;
   * `data-device` = `gpu`/`cpu`, `data-fallback` = `true`/`false`.
   */
  get device(): Locator {
    return this.page.getByTestId('track-device');
  }

  /** Fallback notice with the reason; exists only when `track.device.cpuFallback`. */
  get deviceFallback(): Locator {
    return this.page.getByTestId('track-device-fallback');
  }

  /** "Open mixer" action. */
  get openMixer(): Locator {
    return this.page.getByTestId('open-mixer');
  }

  /** The entry of one stem. */
  stem(name: StemName): Locator {
    return this.page.getByTestId(`track-stem-${name}`);
  }

  /** The lyrics card (feature 006; its controls are in `pages/lyrics-section.ts`). */
  get lyricsCard(): Locator {
    return this.page.getByTestId('lyrics-section');
  }

  /** "Sheet music" of one standard stem (feature 008; opens `pages/score-dialog.ts`). */
  scoreStem(name: StemName): Locator {
    return this.page.getByTestId(`score-stem-${name}`);
  }

  /** Opens the inline delete confirmation, cancels it once, then deletes for real. */
  async deleteAfterCancel(): Promise<void> {
    await this.deleteButton.click();
    await this.deleteCancel.click();
    await expect(this.deleteConfirm).toHaveCount(0);
    await this.deleteButton.click();
    await this.deleteConfirm.click();
  }

  /** "AI-generated" text badge of a generated track (ui-test-ids 1.8.0). */
  get originBadge(): Locator {
    return this.page.getByTestId('track-origin-badge');
  }

  /** Link to the source track of a restyled take (1.8.0). */
  get originSourceLink(): Locator {
    return this.page.getByTestId('track-origin-source-link');
  }

  /** Link to the lyrics sheet of a generated song (1.9.0). */
  get lyricsLink(): Locator {
    return this.page.getByTestId('track-lyrics-link');
  }

  /** "AI voice" / "AI voice: <name>" of a generated track with a vocal label (1.9.0). */
  get vocalBadge(): Locator {
    return this.page.getByTestId('track-vocal-badge');
  }

  /** Opens `/tracks/{id}`. */
  async goto(trackId: string): Promise<void> {
    await this.page.goto(`/tracks/${trackId}`);
  }

  /** Opens `/tracks/{id}` and waits for the title (the generation section renders with it). */
  async gotoLoaded(trackId: string): Promise<void> {
    await this.goto(trackId);
    await expect(this.page.getByTestId('track-title')).toBeVisible();
  }

  /** Waits until the browser shows a track detail URL and returns its track id. */
  async waitForTrackId(timeoutMs = 30_000): Promise<string> {
    await this.page.waitForURL(trackUrl, { timeout: timeoutMs });
    const match = trackUrl.exec(new URL(this.page.url()).pathname);
    if (match?.[1] === undefined) {
      throw new Error(`not a track URL: ${this.page.url()}`);
    }
    return match[1];
  }

  /** Waits until the status text shows `status` (case-insensitive). */
  async waitForStatus(status: TrackStatus, timeoutMs: number): Promise<void> {
    await expect(this.status).toHaveText(new RegExp(status, 'i'), { timeout: timeoutMs });
  }

  /** The BPM shown, parsed as a number (e.g. "100.2 BPM" -> 100.2). */
  async bpmValue(): Promise<number> {
    const text = (await this.bpm.textContent()) ?? '';
    return Number(/\d+(?:[.,]\d+)?/.exec(text)?.[0].replace(',', '.') ?? Number.NaN);
  }
}

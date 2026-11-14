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

  /** Detected BPM. */
  get bpm(): Locator {
    return this.page.getByTestId('track-bpm');
  }

  /** Model used (`htdemucs_6s`, `htdemucs` or `fake`). */
  get model(): Locator {
    return this.page.getByTestId('track-model');
  }

  /** "Open mixer" action. */
  get openMixer(): Locator {
    return this.page.getByTestId('open-mixer');
  }

  /** The entry of one stem. */
  stem(name: StemName): Locator {
    return this.page.getByTestId(`track-stem-${name}`);
  }

  /** Opens `/tracks/{id}`. */
  async goto(trackId: string): Promise<void> {
    await this.page.goto(`/tracks/${trackId}`);
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

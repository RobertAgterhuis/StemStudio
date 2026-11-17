import { expect, type Locator, type Page } from '@playwright/test';
import type { RecordingPart, TakeStatus } from '../support/recording-api';

/** `data-state` of `rec-panel` (engine `RecordingState`). */
export type RecordingStateName = 'closed' | 'open' | 'countIn' | 'recording' | 'finishing';

/** `data-reason` of `rec-unavailable`. */
export type UnavailableReason = 'insecure' | 'unsupported' | 'denied' | 'not-found' | 'busy' | 'lost';

/**
 * Recording panel, take strip and Takes panel of the mixer (ui-test-ids 1.14.0, feature 015). Waits are on contract
 * attributes (`data-state`, `data-permission`, `data-status` of a row by take id), never on texts alone.
 */
export class RecordingPanel {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Element by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** The panel (`data-state`, `data-permission`). */
  get panel(): Locator {
    return this.byId('rec-panel');
  }

  /** The Record/Stop button. */
  get record(): Locator {
    return this.byId('rec-record');
  }

  /** The take strip (`data-take-id`). */
  get strip(): Locator {
    return this.byId('strip-take');
  }

  /** Opens the panel from the mixer toolbar. */
  async open(): Promise<void> {
    if ((await this.byId('mixer-record-panel-toggle').getAttribute('aria-expanded')) !== 'true') {
      await this.byId('mixer-record-panel-toggle').click();
    }
    await expect(this.panel).toBeVisible();
  }

  /** Opens the panel and allows the microphone; waits until the input is open. */
  async openWithInput(): Promise<void> {
    await this.open();
    const allow = this.byId('rec-allow-mic');
    if (await allow.isVisible()) {
      await allow.click();
    }
    await expect(this.panel).toHaveAttribute('data-permission', 'granted', { timeout: 15_000 });
    await expect(this.byId('meter-input')).toBeVisible();
  }

  /** Chooses the recorded part. */
  async choosePart(part: RecordingPart): Promise<void> {
    await this.byId('rec-part-select').selectOption(part);
  }

  /** Chooses the count-in bars (0-2). */
  async chooseCountIn(bars: 0 | 1 | 2): Promise<void> {
    await this.byId('rec-count-in').selectOption(String(bars));
  }

  /** Waits for an engine recording state. */
  async waitForState(state: RecordingStateName, timeoutMs = 15_000): Promise<void> {
    await expect(this.panel).toHaveAttribute('data-state', state, { timeout: timeoutMs });
  }

  /** Records for `seconds` of capture (after the count-in) and stops; waits until the input is open again. */
  async recordFor(seconds: number): Promise<void> {
    await expect(this.record).toBeEnabled();
    await this.record.click();
    await this.waitForState('recording');
    await this.page.waitForTimeout(seconds * 1000);
    await this.record.click();
    await this.waitForState('open', 30_000);
  }

  /** The id of the take in the strip (`data-take-id`). */
  async stripTakeId(): Promise<string> {
    await expect(this.strip).toBeVisible();
    const id = await this.strip.getAttribute('data-take-id');
    expect(id, 'data-take-id').not.toBeNull();
    return id ?? '';
  }

  /** Every row of the Takes panel. */
  get rows(): Locator {
    return this.page.locator('[data-testid^="rec-take-row-"]');
  }

  /** A row of the Takes panel. */
  row(takeId: string): Locator {
    return this.byId(`rec-take-row-${takeId}`);
  }

  /** Waits until a row has `status` (`local` before the upload finished). */
  async waitForRow(takeId: string, status: TakeStatus | 'local', timeoutMs = 60_000): Promise<void> {
    await expect(this.row(takeId)).toHaveAttribute('data-status', status, { timeout: timeoutMs });
  }

  /** The id of the newest row (`rec-take-row-<id>`), once the uploaded take has a server id. */
  async newestSavedTakeId(): Promise<string> {
    const rows = this.page.locator('[data-testid^="rec-take-row-"]:not([data-status="local"])');
    await expect(rows.first()).toBeVisible({ timeout: 30_000 });
    const testId = (await rows.first().getAttribute('data-testid')) ?? '';
    return testId.replace('rec-take-row-', '');
  }

  /** Runs the click test from the panel; resolves with the time to the result (ms) and whether it succeeded. */
  async calibrate(timeoutMs = 15_000): Promise<{ readonly ok: boolean; readonly elapsedMs: number }> {
    await this.byId('rec-calibrate').click();
    await expect(this.byId('cal-dialog')).toBeVisible();
    const started = Date.now();
    await this.byId('cal-start').click();
    await expect(this.byId('cal-result')).toBeVisible({ timeout: timeoutMs });
    const elapsedMs = Date.now() - started;
    return { ok: (await this.byId('cal-result').getAttribute('data-ok')) === 'true', elapsedMs };
  }

  /** Enters a manual latency (Advanced) and saves it; the dialog stays open. */
  async enterManualLatency(milliseconds: number): Promise<void> {
    if (!(await this.byId('cal-dialog').isVisible())) {
      await this.byId('rec-calibrate').click();
    }
    await this.byId('cal-manual').click();
    await this.byId('cal-manual-ms').fill(String(milliseconds));
    await this.byId('cal-manual-ms').press('Tab');
    await this.byId('cal-save').click();
  }

  /** Closes the calibration dialog. */
  async closeCalibration(): Promise<void> {
    await this.byId('cal-cancel').click();
    await expect(this.byId('cal-dialog')).toHaveCount(0);
  }

  /** Records until the engine reports loop pass `pass` (Advanced: `rec-pass`), then `extraSeconds` more, and stops. */
  async recordLoopUntilPass(pass: number, extraSeconds: number, timeoutMs = 60_000): Promise<void> {
    await this.record.click();
    await this.waitForState('recording');
    await expect(this.byId('rec-pass')).toHaveAttribute('data-pass', String(pass), { timeout: timeoutMs });
    await this.page.waitForTimeout(extraSeconds * 1000);
    await this.record.click();
    await this.waitForState('open', 30_000);
  }

  /** Stars a take. */
  async star(takeId: string): Promise<void> {
    await this.byId(`rec-take-star-${takeId}`).click();
    await expect(this.byId(`rec-take-star-${takeId}`)).toHaveAttribute('aria-pressed', 'true');
  }

  /** "Delete the other passes" of a session, confirmed. */
  async keepStarred(sessionId: string): Promise<void> {
    await this.byId(`takes-keep-starred-${sessionId}`).click();
    await this.byId('confirm-dialog-ok').click();
  }

  /** Why Record is disabled, or null. */
  async disabledReason(): Promise<string | null> {
    const reason = this.byId('rec-record-disabled-reason');
    return (await reason.count()) === 0 ? null : reason.getAttribute('data-reason');
  }

  /** Waits for `rec-unavailable` with a reason. */
  async expectUnavailable(reason: UnavailableReason): Promise<void> {
    await expect(this.byId('rec-unavailable')).toHaveAttribute('data-reason', reason, { timeout: 15_000 });
  }
}

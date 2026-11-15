import { expect, type Locator } from '@playwright/test';
import type { GenerationAvailability } from '../support/generation-api';
import { TestIdPage } from './test-id-page';

/** The generation status banner or chip (ui-test-ids 1.8.0 `generation-status*`, 1.9.0 `data-voice-state`). */
export class GenerationStatusBanner extends TestIdPage {
  /** The banner (`data-state`, `data-voice-state`). */
  get banner(): Locator {
    return this.byId('generation-status');
  }

  /** The readable reason (exists when not ready). */
  get reason(): Locator {
    return this.byId('generation-status-reason');
  }

  /** Link to help topic `generation-gpu-required`. */
  get helpLink(): Locator {
    return this.byId('generation-help-link');
  }

  /** "First use downloads about X GB" (ready, models missing). */
  get downloadNotice(): Locator {
    return this.byId('generation-download-notice');
  }

  /** Waits until the banner reports `state`. */
  async expectState(state: GenerationAvailability, timeoutMs = 15_000): Promise<void> {
    await expect(this.banner).toHaveAttribute('data-state', state, { timeout: timeoutMs });
  }

  /** Waits until the banner reports the singer's `state`. */
  async expectVoiceState(state: GenerationAvailability, timeoutMs = 15_000): Promise<void> {
    await expect(this.banner).toHaveAttribute('data-voice-state', state, { timeout: timeoutMs });
  }

  /**
   * Asserts the 1.8.0 rule "disabled, not hidden": `control` is visible, disabled and described
   * (`aria-describedby`) by an element with the readable `reasonText`.
   */
  async expectDisabledWithReason(control: Locator, reasonText: string | RegExp): Promise<void> {
    await expect(control).toBeVisible();
    await expect(control).toBeDisabled();
    const describedBy = (await control.getAttribute('aria-describedby')) ?? '';
    expect(describedBy, 'aria-describedby').not.toBe('');
    const first = describedBy.split(/\s+/u)[0] ?? '';
    await expect(this.page.locator(`[id="${first}"]`)).toContainText(reasonText);
  }
}

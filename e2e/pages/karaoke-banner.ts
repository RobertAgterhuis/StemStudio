import { expect, type Locator, type Page } from '@playwright/test';
import type { LyricsStatus } from '../support/lyrics';

/** Banner text sizes (`karaoke-size`). */
export type KaraokeSize = 's' | 'm' | 'l';

/** Engine states written on the host (`data-state`, audio-engine.md "Karaoke"). */
export type KaraokeState = 'hidden' | 'countdown' | 'singing' | 'ended';

/** The karaoke banner and its controls in the mixer (ui-test-ids 1.6.0, feature 006). */
export class KaraokeBanner {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Any id of the page. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** The engine-owned host (`data-state`, `data-line-index`, `data-word-index`, `data-position-ms`, `data-sync`). */
  get host(): Locator {
    return this.byId('karaoke-banner');
  }

  /** Credit text (`Lyrics.credit`). */
  get credit(): Locator {
    return this.byId('karaoke-credit');
  }

  /** Show/hide toggle (`aria-pressed`). */
  get toggle(): Locator {
    return this.byId('karaoke-toggle');
  }

  /** Text size select (Advanced and Expert). */
  get size(): Locator {
    return this.byId('karaoke-size');
  }

  /** Offset slider -5000..5000 ms (`aria-valuenow`; Advanced and Expert). */
  get offset(): Locator {
    return this.byId('karaoke-offset');
  }

  /** Offset reset button. */
  get offsetReset(): Locator {
    return this.byId('karaoke-offset-reset');
  }

  /** Shown instead of the banner when the status is not `found` (`data-status`). */
  get unavailable(): Locator {
    return this.byId('karaoke-unavailable');
  }

  /** The engine's ball (present but hidden with reduced motion, audio-engine.md "Reduced motion"). */
  get ball(): Locator {
    return this.host.locator('.karaoke-ball');
  }

  /** The line in `slot` (`previous`, `current`, `next`). */
  line(slot: 'previous' | 'current' | 'next'): Locator {
    return this.host.locator(`.karaoke-line[data-slot="${slot}"]`);
  }

  /** The word currently sung (`data-state="current"`). */
  get currentWord(): Locator {
    return this.host.locator('.karaoke-word[data-state="current"]');
  }

  /** Waits until the engine writes `state` on the host. */
  async waitForState(state: KaraokeState, timeoutMs = 30_000): Promise<void> {
    await expect(this.host).toHaveAttribute('data-state', state, { timeout: timeoutMs });
  }

  /** Waits until the banner is replaced by the unavailable notice with `status`. */
  async expectUnavailable(status: LyricsStatus): Promise<void> {
    await expect(this.unavailable).toHaveAttribute('data-status', status);
    await expect(this.host).toHaveCount(0);
  }

  /** Is the banner shown (`aria-pressed` of the toggle)? */
  async isShown(): Promise<boolean> {
    return (await this.toggle.getAttribute('aria-pressed')) === 'true';
  }

  /** Selects a text size. */
  async setSize(size: KaraokeSize): Promise<void> {
    await this.size.selectOption(size);
  }
}

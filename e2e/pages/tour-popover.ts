import { expect, type Locator, type Page } from '@playwright/test';
import type { TourId } from '../support/preferences';

/** Upper bound of steps per tour (help-content.md 3: 1-8 steps). */
const maxSteps = 8;

/** The driver.js tour popover and the tour offer chips (ui-test-ids 1.3.0 "Tours"). */
export class TourPopover {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** The popover; attributes `data-tour-id`, `data-step` (1-based), `data-steps`. */
  get popover(): Locator {
    return this.page.getByTestId('tour-popover');
  }

  /** Offer chip of a tour. */
  offer(tourId: TourId): Locator {
    return this.page.getByTestId(`tour-offer-${tourId}`);
  }

  /** Starts a tour from its offer chip with the keyboard only (focus + Enter). */
  async startFromOfferWithKeyboard(tourId: TourId): Promise<void> {
    await this.offer(tourId).focus();
    await this.page.keyboard.press('Enter');
    await expect(this.popover).toHaveAttribute('data-tour-id', tourId);
  }

  /** Current 1-based step. */
  async step(): Promise<number> {
    return Number(await this.popover.getAttribute('data-step'));
  }

  /** Number of steps the running tour shows. */
  async steps(): Promise<number> {
    return Number(await this.popover.getAttribute('data-steps'));
  }

  /**
   * Presses Enter until the tour ends and returns the step numbers seen, in order. Each press must
   * advance the step by exactly one; the popover must be gone after the last step.
   */
  async finishWithEnter(): Promise<number[]> {
    const seen: number[] = [];
    for (let i = 0; i < maxSteps && (await this.popover.count()) > 0; i++) {
      const current = await this.step();
      seen.push(current);
      await this.page.keyboard.press('Enter');
      await expect.poll(() => this.stepOrZero()).not.toBe(current);
    }
    await expect(this.popover).toBeHidden();
    return seen;
  }

  /** Current step, or 0 once the popover is gone. */
  async stepOrZero(): Promise<number> {
    return (await this.popover.count()) === 0 ? 0 : this.step();
  }
}

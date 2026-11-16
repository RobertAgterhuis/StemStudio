import type { Locator, Page } from '@playwright/test';

/** One optional form step: run `action` only when `value` is set. */
export type FormStep = readonly [value: unknown, action: () => Promise<unknown>];

/** Runs the actions of the steps whose value is not `undefined`, in order. */
export async function applySteps(steps: readonly FormStep[]): Promise<void> {
  for (const [value, action] of steps) {
    if (value !== undefined) {
      await action();
    }
  }
}

/** Base of the feature 009/010 page objects: every element is found by its `data-testid`. */
export abstract class TestIdPage {
  /** @param page The Playwright page. */
  constructor(protected readonly page: Page) {}

  /** Any id of the page (ui-test-ids 1.9.0). */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }
}

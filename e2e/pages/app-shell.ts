import { expect, type Locator, type Page } from '@playwright/test';
import type { Level, ThemePreference } from '../support/preferences';

/** Resolved theme on `app-shell[data-theme]`. */
export type ResolvedTheme = 'light' | 'dark';

/** App shell of feature 004 (ui-test-ids 1.3.0 "Shell", "Preferences", "Welcome", "Journey"). */
export class AppShell {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Element with the given contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Root element; carries `data-level`, `data-theme`, `data-density`, `data-motion`. */
  get root(): Locator {
    return this.byId('app-shell');
  }

  /** Waits until the shell renders at `level`. */
  async expectLevel(level: Level): Promise<void> {
    await expect(this.root).toHaveAttribute('data-level', level);
  }

  /** Waits until the shell shows the resolved `theme`. */
  async expectTheme(theme: ResolvedTheme): Promise<void> {
    await expect(this.root).toHaveAttribute('data-theme', theme);
  }

  /** Waits until the shell has rendered with any level (preferences loaded). */
  async waitForShell(): Promise<void> {
    await expect(this.root).toHaveAttribute('data-level', /^(beginner|advanced|expert)$/);
  }

  /** Switches the level with the top-bar radio group (>= 768 px) and waits for the change. */
  async setLevel(level: Level): Promise<void> {
    await this.byId(`level-${level}`).click();
    await expect(this.byId(`level-${level}`)).toHaveAttribute('aria-checked', 'true');
    await this.expectLevel(level);
  }

  /** Opens the preferences panel. */
  async openPreferences(): Promise<void> {
    await this.byId('preferences-open').click();
    await expect(this.byId('preferences-panel')).toBeVisible();
  }

  /** Chooses a theme in the preferences panel. */
  async setTheme(theme: ThemePreference): Promise<void> {
    await this.openPreferences();
    await this.byId(`pref-theme-${theme}`).click();
  }

  /** One step of the Beginner journey rail. */
  journeyStep(step: 'import' | 'separate' | 'mix' | 'save'): Locator {
    return this.byId(`journey-step-${step}`);
  }

  /** Waits until a journey step has `state`; the current step also has `aria-current="step"`. */
  async expectJourneyStep(step: 'import' | 'separate' | 'mix' | 'save', state: 'done' | 'current' | 'todo'): Promise<void> {
    await expect(this.journeyStep(step)).toHaveAttribute('data-state', state, { timeout: 30_000 });
    if (state === 'current') {
      await expect(this.journeyStep(step)).toHaveAttribute('aria-current', 'step');
    }
  }

  /** Whether the focused element is inside `container` (focus trap and panel focus checks). */
  async focusIsInside(container: Locator): Promise<boolean> {
    return container.evaluate((el) => el.contains(document.activeElement));
  }

  /** Whether some element other than `body` has focus (focus was returned somewhere). */
  async focusIsOnAnElement(): Promise<boolean> {
    return this.page.evaluate(() => document.activeElement !== null && document.activeElement !== document.body);
  }
}

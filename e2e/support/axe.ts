/**
 * axe-core scan of the current page with the WCAG 2.0/2.1/2.2 A and AA rule tags (SC-001,
 * TEST-STRATEGY.md 10.3). No element is excluded; `axeViolationsIn` skips only the rules that
 * crash a browser (`crashingRules`).
 *
 * @packageDocumentation
 */
import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';

/** Rule tags of SC-001. */
export const wcagTags: readonly string[] = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/** One violation, reduced to what a failure message needs. */
export interface ViolationSummary {
  readonly rule: string;
  readonly impact: string;
  readonly help: string;
  readonly targets: readonly string[];
}

/** Scans the page and returns its violations (empty when compliant). */
export async function axeViolations(page: Page): Promise<ViolationSummary[]> {
  await transitionsDone(page);
  return summarise(await new AxeBuilder({ page }).withTags([...wcagTags]).analyze());
}

/**
 * Waits until no CSS transition or animation is running (at most 2 s), so colour contrast is measured on the final colours:
 * Firefox scanned the level switch of the setlists page mid-transition (014+015 integration, `level-beginner`).
 */
async function transitionsDone(page: Page): Promise<void> {
  await page
    .waitForFunction(() => document.getAnimations().every((animation) => animation.playState !== 'running'), undefined, { timeout: 2_000 })
    .catch(() => undefined);
}

/**
 * Rules that crash a browser's content process instead of reporting: axe-core's `target-size`
 * crashes Firefox (Windows and the Linux Playwright image, Playwright 1.65.1) on the Settings page
 * with a stored Claude key ("Page crashed"; found at the claude-key integration). The rule still
 * runs in Chromium, where the a11y project and the same scoped scans check it.
 */
const crashingRules: Readonly<Record<string, readonly string[]>> = { firefox: ['target-size'] };

/**
 * Scans only the part of the page under `selector` (for example one settings group in a given
 * state, 011 follow-up claude-key CK-SC-006) with the same rule tags; nothing is excluded, and
 * only the rules listed in `crashingRules` for this browser are skipped.
 */
export async function axeViolationsIn(page: Page, selector: string): Promise<ViolationSummary[]> {
  const browser = page.context().browser()?.browserType().name() ?? '';
  const skipped = crashingRules[browser] ?? [];
  await transitionsDone(page);
  const builder = new AxeBuilder({ page }).include(selector).withTags([...wcagTags]);
  return summarise(await (skipped.length > 0 ? builder.disableRules([...skipped]) : builder).analyze());
}

function summarise(results: Awaited<ReturnType<AxeBuilder['analyze']>>): ViolationSummary[] {
  return results.violations.map((violation) => ({
    rule: violation.id,
    impact: violation.impact ?? 'unknown',
    help: violation.helpUrl,
    targets: violation.nodes.map((node) => node.target.join(' ')),
  }));
}

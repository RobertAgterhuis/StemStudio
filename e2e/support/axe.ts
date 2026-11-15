/**
 * axe-core scan of the current page with the WCAG 2.0/2.1/2.2 A and AA rule tags (SC-001,
 * TEST-STRATEGY.md 10.3). No rule is disabled and no element is excluded.
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
  const results = await new AxeBuilder({ page }).withTags([...wcagTags]).analyze();
  return results.violations.map((violation) => ({
    rule: violation.id,
    impact: violation.impact ?? 'unknown',
    help: violation.helpUrl,
    targets: violation.nodes.map((node) => node.target.join(' ')),
  }));
}

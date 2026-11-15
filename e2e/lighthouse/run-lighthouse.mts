/**
 * Runs Lighthouse CI (`@lhci/cli` 0.15.1) with the budgets of `lighthouserc.json` against the URLs
 * written by `urls.setup.ts` (SC-003, TEST-STRATEGY.md 10.4). Started by `npm run test:lighthouse`
 * after the `lighthouse-setup` project; exits with lhci's status.
 *
 * @packageDocumentation
 */
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';

/** Hand-over file of `urls.setup.ts`. */
interface LighthouseTargets {
  readonly urls: readonly string[];
  readonly chromePath: string;
}

const e2eRoot = path.resolve(import.meta.dirname, '..');

/** Reads the hand-over file and checks its shape. */
function readTargets(): LighthouseTargets {
  const file = path.join(e2eRoot, '.state', 'lighthouse-urls.json');
  const targets = JSON.parse(readFileSync(file, 'utf-8')) as LighthouseTargets;
  if (targets.urls.length === 0 || targets.chromePath === '') {
    throw new Error(`${file} has no URLs or no Chromium path; run the lighthouse-setup project first`);
  }
  return targets;
}

/** Argument list for `lhci autorun` (no shell; URLs and Chromium override the config). */
function lhciArgs(targets: LighthouseTargets): string[] {
  const cli = createRequire(import.meta.url).resolve('@lhci/cli/src/cli.js');
  return [
    cli,
    'autorun',
    '--config=lighthouserc.json',
    `--collect.chromePath=${targets.chromePath}`,
    ...targets.urls.map((url) => `--collect.url=${url}`),
  ];
}

try {
  execFileSync(process.execPath, lhciArgs(readTargets()), { cwd: e2eRoot, stdio: 'inherit' });
} catch (error) {
  process.exitCode = 1;
  console.error(error instanceof Error ? error.message : error);
}

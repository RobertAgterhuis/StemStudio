/**
 * Validation of {@link PracticeSettings} (data-model 2.2, same rules as the backend domain).
 *
 * @packageDocumentation
 */
import type { PracticeSettings } from '../types';

/** Practice settings before the first `setPractice` (the defaults of `PracticeSettingsDto`). */
export const DEFAULT_PRACTICE: PracticeSettings = {
  speedPercent: 100,
  transposeSemitones: 0,
  trainer: { enabled: false, startPercent: 70, stepPercent: 5, targetPercent: 100, passesPerStep: 1 },
  countIn: { bars: 0, everyPass: false },
};

type Read = (settings: PracticeSettings) => unknown;

/** Integer fields: path, inclusive range, accessor. */
const INTEGERS: readonly (readonly [string, number, number, Read])[] = [
  ['speedPercent', 50, 120, (s) => s.speedPercent],
  ['transposeSemitones', -12, 12, (s) => s.transposeSemitones],
  ['trainer.startPercent', 50, 119, (s) => s.trainer.startPercent],
  ['trainer.stepPercent', 1, 20, (s) => s.trainer.stepPercent],
  ['trainer.targetPercent', 51, 120, (s) => s.trainer.targetPercent],
  ['trainer.passesPerStep', 1, 8, (s) => s.trainer.passesPerStep],
  ['countIn.bars', 0, 2, (s) => s.countIn.bars],
];

/** Boolean fields: path, accessor. */
const BOOLEANS: readonly (readonly [string, Read])[] = [
  ['trainer.enabled', (s) => s.trainer.enabled],
  ['countIn.everyPass', (s) => s.countIn.everyPass],
];

/**
 * @param settings - Requested settings (JSON from Blazor).
 * @returns A plain, validated copy.
 * @throws Error `Invalid practice settings: <field> <reason>` for the first problem found.
 */
export function validatePractice(settings: PracticeSettings): PracticeSettings {
  const problem = shapeProblem(settings) ?? integerProblem(settings) ?? booleanProblem(settings) ?? orderProblem(settings);
  if (problem !== null) {
    throw new Error(`Invalid practice settings: ${problem}`);
  }
  return {
    speedPercent: settings.speedPercent,
    transposeSemitones: settings.transposeSemitones,
    trainer: { ...settings.trainer },
    countIn: { bars: settings.countIn.bars, everyPass: settings.countIn.everyPass },
  };
}

function shapeProblem(settings: PracticeSettings): string | null {
  const missing = (['trainer', 'countIn'] as const).find((key) => {
    const value: unknown = settings[key];
    return typeof value !== 'object' || value === null;
  });
  return missing === undefined ? null : `${missing} must be an object`;
}

function integerProblem(settings: PracticeSettings): string | null {
  for (const [path, min, max, read] of INTEGERS) {
    const value = read(settings);
    if (!Number.isInteger(value) || Number(value) < min || Number(value) > max) {
      return `${path} must be an integer ${String(min)}..${String(max)} (got ${String(value)})`;
    }
  }
  return null;
}

function booleanProblem(settings: PracticeSettings): string | null {
  const bad = BOOLEANS.find(([, read]) => typeof read(settings) !== 'boolean');
  return bad === undefined ? null : `${bad[0]} must be a boolean (got ${String(bad[1](settings))})`;
}

function orderProblem({ trainer }: PracticeSettings): string | null {
  return trainer.startPercent < trainer.targetPercent
    ? null
    : `trainer.targetPercent must be above the start speed (start ${String(trainer.startPercent)}, target ${String(trainer.targetPercent)})`;
}

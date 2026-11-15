/**
 * Knowledge of the five tours (design-spec.md 7, help-content.md 3) for `tours.spec.ts`: where
 * and at which level each one is offered, and its definition file as shipped by the front end.
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import type { Level, TourId } from './preferences';
import type { ScreenId } from './screens';

/** Where a tour is exercised: its level and a screen on its route. */
export interface TourCase {
  readonly id: TourId;
  readonly level: Level;
  readonly screen: ScreenId;
}

/** One case per tour. */
export const tourCases: readonly TourCase[] = [
  { id: 'import-basics', level: 'beginner', screen: 'import' },
  { id: 'track-basics', level: 'beginner', screen: 'track' },
  { id: 'mixer-basics', level: 'beginner', screen: 'mixer' },
  { id: 'mixer-advanced', level: 'advanced', screen: 'mixer' },
  { id: 'expert-keyboard', level: 'expert', screen: 'mixer' },
];

/** The fields of a tour file the suite reads. */
export interface TourDefinition {
  readonly id: string;
  readonly version: number;
  readonly steps: readonly { readonly target: string }[];
}

const toursDir = path.resolve(__dirname, '..', '..', 'src', 'StemStudio.Web', 'Help', 'tours');

/** Reads `src/StemStudio.Web/Help/tours/<id>.json` (the front end's tour definition). */
export function readTourDefinition(id: TourId): TourDefinition {
  return JSON.parse(readFileSync(path.join(toursDir, `${id}.json`), 'utf-8')) as TourDefinition;
}

/** `[1, 2, ..., n]`. */
export function stepRange(n: number): number[] {
  return Array.from({ length: n }, (_, i) => i + 1);
}

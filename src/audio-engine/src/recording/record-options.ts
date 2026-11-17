/**
 * Validation of {@link RecordOptions} and the refusals of `startRecording` (contract 2.5.0, FR-009).
 *
 * @packageDocumentation
 */
import type { LatencyMethod, PracticeStatus, RecordOptions, RecordingPart } from '../types';

/** Default and largest number of loop passes per session (FR-010). */
export const MAX_PASSES = 50;
/** Default and longest capture per session (s, FR-010). */
export const MAX_SESSION_SECONDS = 1800;
/** Largest latency `L` (samples, 1 s). */
export const MAX_LATENCY_SAMPLES = 44100;

const METHODS: readonly LatencyMethod[] = ['loopback', 'manual', 'estimated'];
const PARTS: readonly RecordingPart[] = ['none', 'vocals', 'guitar', 'bass', 'piano', 'drums', 'other'];

/** {@link RecordOptions} with the defaults filled in. */
export type ValidRecordOptions = Required<RecordOptions>;

function integerIn(value: number, min: number, max: number): boolean {
  return Number.isInteger(value) && value >= min && value <= max;
}

/**
 * @param options - Requested options.
 * @returns The options with `maxPasses` (50) and `maxSeconds` (1800) defaulted.
 * @throws Error `Invalid record options: <field> ...` for the first invalid field.
 */
export function validateRecordOptions(options: RecordOptions): ValidRecordOptions {
  const valid: ValidRecordOptions = { maxPasses: MAX_PASSES, maxSeconds: MAX_SESSION_SECONDS, ...options };
  const checks: readonly [keyof RecordOptions, boolean][] = [
    ['latencySamples', integerIn(valid.latencySamples, 0, MAX_LATENCY_SAMPLES)],
    ['latencyMethod', METHODS.includes(valid.latencyMethod)],
    ['part', PARTS.includes(valid.part)],
    ['countInBars', integerIn(valid.countInBars, 0, 2)],
    ['maxPasses', integerIn(valid.maxPasses, 1, MAX_PASSES)],
    ['maxSeconds', valid.maxSeconds >= 1 && valid.maxSeconds <= MAX_SESSION_SECONDS],
  ];
  const failed = checks.find(([, ok]) => !ok);
  if (failed !== undefined) {
    throw new Error(`Invalid record options: ${failed[0]} (got ${String(valid[failed[0]])})`);
  }
  return valid;
}

/** What `startRecording` checks before anything starts. */
export interface StartConditions {
  readonly running: boolean;
  readonly inputOpen: boolean;
  readonly trackLoaded: boolean;
  readonly practice: PracticeStatus;
}

/**
 * @param conditions - Engine state.
 * @throws Error for the first unmet condition: a running recording, no input, no track, `recordingSpeed:` (speed not
 *   100 % or transpose not 0), `speedTrainer:` (trainer not off).
 */
export function assertCanRecord(conditions: StartConditions): void {
  const { practice } = conditions;
  const problems: readonly [boolean, string][] = [
    [conditions.running, 'A recording is already running'],
    [!conditions.inputOpen, 'Open an input first'],
    [!conditions.trackLoaded, 'Load a track first'],
    [practice.speedPercent !== 100 || practice.transposeSemitones !== 0, 'recordingSpeed: Recording works at 100 % speed without transpose.'],
    [practice.trainer.state !== 'off', 'speedTrainer: Switch the speed trainer off to record.'],
  ];
  const problem = problems.find(([failed]) => failed);
  if (problem !== undefined) {
    throw new Error(problem[1]);
  }
}

/**
 * Validation of the singing inputs (contract 2.6.0 errors): `Invalid pitch contour: <reason>` and
 * `Invalid singing request: <reason>`. Shared by `startSingingFeedback` and `scoreTake`.
 *
 * @packageDocumentation
 */
import type { PitchContour, SingingUnit } from '../types';

/** Most reference frames (15 minutes at 10 ms). */
export const MAX_CONTOUR_FRAMES = 90_000;
/** Highest reference value (MIDI 127 x 100). */
export const MAX_CENTS = 12_700;
/** Tolerance range (cents). */
export const TOLERANCE_RANGE = { min: 10, max: 200 } as const;
/** Largest accepted round trip (ms). */
export const MAX_ROUND_TRIP_MS = 1000;

function request(reason: string): never {
  throw new Error(`Invalid singing request: ${reason}`);
}

function contour(reason: string): never {
  throw new Error(`Invalid pitch contour: ${reason}`);
}

function isList(frames: unknown): boolean {
  return typeof (frames as { length?: unknown } | null)?.length === 'number';
}

/**
 * @param reference - Reference melody from Blazor (untrusted shape).
 * @returns The frames as an `Int16Array`.
 * @throws Error `Invalid pitch contour: <reason>`.
 */
export function validateContour(reference: PitchContour): Int16Array {
  if ((reference.hopMs as number) !== 10) {
    contour('hopMs not 10');
  }
  if (!isList(reference.frames)) {
    contour('frames missing');
  }
  if (reference.frames.length > MAX_CONTOUR_FRAMES) {
    contour(`more than ${String(MAX_CONTOUR_FRAMES)} frames`);
  }
  const values = Int16Array.from(reference.frames);
  if (!Array.from(reference.frames).every((v) => v >= 0 && v <= MAX_CENTS)) {
    contour(`values outside 0..${String(MAX_CENTS)}`);
  }
  return values;
}

/**
 * @param cents - Tolerance.
 * @returns The tolerance when in 10..200.
 * @throws Error `Invalid singing request: tolerance outside 10..200`.
 */
export function validateTolerance(cents: number): number {
  if (!(cents >= TOLERANCE_RANGE.min && cents <= TOLERANCE_RANGE.max)) {
    request(`tolerance outside ${String(TOLERANCE_RANGE.min)}..${String(TOLERANCE_RANGE.max)}`);
  }
  return cents;
}

/**
 * @param units - Units from Blazor (untrusted shape).
 * @returns The units.
 * @throws Error `Invalid singing request: units missing | units not ordered`.
 */
export function validateUnits(units: readonly SingingUnit[]): readonly SingingUnit[] {
  const list: unknown = units;
  if (!Array.isArray(list)) {
    request('units missing');
  }
  units.forEach((unit, i) => {
    const previousEnd = units[i - 1]?.endMs ?? Number.NEGATIVE_INFINITY;
    if (!(unit.startMs >= previousEnd && unit.endMs > unit.startMs)) {
      request('units not ordered');
    }
  });
  return units;
}

/**
 * @param ms - Round trip from the request, or null/undefined.
 * @returns The value when absent or in 0..1000.
 * @throws Error `Invalid singing request: roundTripMs outside 0..1000`.
 */
export function validateRoundTrip(ms: number | null | undefined): number | null {
  if (ms === null || ms === undefined) {
    return null;
  }
  if (!(ms >= 0 && ms <= MAX_ROUND_TRIP_MS)) {
    request(`roundTripMs outside 0..${String(MAX_ROUND_TRIP_MS)}`);
  }
  return ms;
}

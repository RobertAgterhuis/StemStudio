/**
 * Small validation and lifetime helpers shared by the web-interop modules.
 *
 * @packageDocumentation
 */
import type { Disposable } from './types';

/** Kebab-case id pattern of tour and topic ids (help-content.md 2). */
export const KEBAB_ID = /^[a-z0-9]+(-[a-z0-9]+)*$/;

/**
 * True for a plain object (not null, not an array).
 *
 * @param value - Any value.
 * @returns Whether the value can be read as a record.
 */
export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Returns the value when it is one of the allowed strings, else the fallback.
 *
 * @param value - Any value.
 * @param allowed - Allowed values.
 * @param fallback - Value used otherwise.
 * @returns The validated value.
 */
export function oneOf<T extends string, F>(value: unknown, allowed: readonly T[], fallback: F): T | F {
  return allowed.find((item) => item === value) ?? fallback;
}

/**
 * Wraps a clean-up action so it runs at most once.
 *
 * @param action - The clean-up.
 * @returns A disposable that is safe to dispose twice.
 */
export function once(action: () => void): Disposable {
  let done = false;
  return {
    dispose(): void {
      if (!done) {
        done = true;
        action();
      }
    },
  };
}

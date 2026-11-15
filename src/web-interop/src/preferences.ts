/**
 * Preferences record in browser local storage (data-model.md 1, web-interop.md 2): read, validate
 * field by field, write and cross-tab change notification.
 *
 * @packageDocumentation
 */
import type {
  Disposable,
  DotNetRef,
  PreferencesRead,
  StoredHelpProgress,
  StoredPreferences,
  StoredTourRecord,
} from './types';
import { KEBAB_ID, isRecord, once, oneOf } from './validation';

/** Local storage key of the record. */
export const PREFERENCES_KEY = 'stemstudio.preferences';

/** Largest record (UTF-8 bytes) that is read or written. */
export const MAX_PREFERENCES_BYTES = 16 * 1024;

const MAX_TOURS = 50;
const MAX_HINTS = 200;
const LEVELS = ['beginner', 'advanced', 'expert'] as const;
const THEMES = ['system', 'light', 'dark'] as const;
const DENSITIES = ['auto', 'comfortable', 'compact'] as const;
const MOTIONS = ['system', 'reduced'] as const;
const TOUR_STATUSES = ['completed', 'dismissed'] as const;

/**
 * The defaults of data-model.md 1.1 (a new object on every call).
 *
 * @returns Default preferences.
 */
export function defaultPreferences(): StoredPreferences {
  return {
    schemaVersion: 1,
    level: null,
    theme: 'system',
    density: 'auto',
    motion: 'system',
    help: { welcomeDismissed: false, tours: {}, hintsDismissed: [] },
  };
}

function isVersion(value: unknown): boolean {
  return typeof value === 'number' && Number.isInteger(value) && value >= 1;
}

function validTour(value: unknown): value is StoredTourRecord {
  if (!isRecord(value)) {
    return false;
  }

  const { version, status, at } = value;
  return isVersion(version) && oneOf(status, TOUR_STATUSES, null) !== null && typeof at === 'string';
}

function validateTours(raw: unknown): Record<string, StoredTourRecord> {
  const tours: Record<string, StoredTourRecord> = {};
  const entries = isRecord(raw) ? Object.entries(raw) : [];
  for (const [id, tour] of entries.filter(([key, value]) => KEBAB_ID.test(key) && validTour(value)).slice(0, MAX_TOURS)) {
    const { version, status, at } = tour as StoredTourRecord;
    tours[id] = { version, status, at };
  }

  return tours;
}

function validateHints(raw: unknown): string[] {
  const hints = Array.isArray(raw) ? (raw as unknown[]) : [];
  return hints.filter((hint): hint is string => typeof hint === 'string' && KEBAB_ID.test(hint)).slice(0, MAX_HINTS);
}

function validateHelp(raw: unknown): StoredHelpProgress {
  const help = isRecord(raw) ? raw : {};
  return {
    welcomeDismissed: help.welcomeDismissed === true,
    tours: validateTours(help.tours),
    hintsDismissed: validateHints(help.hintsDismissed),
  };
}

/**
 * Validates a parsed record: a non-object or another schema version gives all defaults; otherwise
 * every invalid or missing field takes its default and unknown fields are dropped.
 *
 * @param raw - Parsed JSON.
 * @returns A valid record.
 */
export function validatePreferences(raw: unknown): StoredPreferences {
  if (!isRecord(raw) || raw.schemaVersion !== 1) {
    return defaultPreferences();
  }

  const record: StoredPreferences = {
    schemaVersion: 1,
    level: oneOf(raw.level, LEVELS, null),
    theme: oneOf(raw.theme, THEMES, 'system'),
    density: oneOf(raw.density, DENSITIES, 'auto'),
    motion: oneOf(raw.motion, MOTIONS, 'system'),
    help: validateHelp(raw.help),
  };
  return typeof raw.updatedAt === 'string' ? { ...record, updatedAt: raw.updatedAt } : record;
}

function byteLength(text: string): number {
  return new TextEncoder().encode(text).length;
}

/**
 * Parses stored JSON; missing, oversized or unparseable text gives the defaults.
 *
 * @param json - The stored text, or null.
 * @returns A valid record.
 */
export function parsePreferences(json: string | null): StoredPreferences {
  if (json === null || byteLength(json) > MAX_PREFERENCES_BYTES) {
    return defaultPreferences();
  }

  try {
    return validatePreferences(JSON.parse(json));
  } catch {
    return defaultPreferences();
  }
}

function storage(): Storage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

/**
 * Reads and validates the stored record (never throws).
 *
 * @returns The record and whether storage works.
 */
export function readPreferences(): PreferencesRead {
  try {
    const json = (storage() ?? failStorage()).getItem(PREFERENCES_KEY);
    return { preferences: parsePreferences(json), storageAvailable: true };
  } catch {
    return { preferences: defaultPreferences(), storageAvailable: false };
  }
}

function failStorage(): never {
  throw new Error('local storage is unavailable');
}

/**
 * Writes the validated record with a fresh `updatedAt`.
 *
 * @param preferences - The whole record.
 * @returns False when storage is unavailable or full, or the record exceeds 16 KB.
 */
export function writePreferences(preferences: StoredPreferences): boolean {
  const json = JSON.stringify({ ...validatePreferences(preferences), updatedAt: new Date().toISOString() });
  if (byteLength(json) > MAX_PREFERENCES_BYTES) {
    return false;
  }

  try {
    (storage() ?? failStorage()).setItem(PREFERENCES_KEY, json);
    return true;
  } catch {
    return false;
  }
}

/**
 * Calls `OnPreferencesChanged(json)` with the validated record when another tab changes it.
 *
 * @param dotnet - Receiver of the callback.
 * @returns Handle that removes the `storage` listener.
 */
export function watchPreferences(dotnet: DotNetRef): Disposable {
  const onStorage = (event: StorageEvent): void => {
    if (event.key === PREFERENCES_KEY) {
      void dotnet.invokeMethodAsync('OnPreferencesChanged', JSON.stringify(parsePreferences(event.newValue)));
    }
  };
  window.addEventListener('storage', onStorage);
  return once(() => {
    window.removeEventListener('storage', onStorage);
  });
}

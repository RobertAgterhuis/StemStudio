import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  MAX_PREFERENCES_BYTES,
  PREFERENCES_KEY,
  defaultPreferences,
  parsePreferences,
  readPreferences,
  validatePreferences,
  watchPreferences,
  writePreferences,
} from '../src/preferences';
import type { DotNetRef, StoredPreferences } from '../src/types';

const full: StoredPreferences = {
  schemaVersion: 1,
  level: 'expert',
  theme: 'dark',
  density: 'compact',
  motion: 'reduced',
  help: {
    welcomeDismissed: true,
    tours: { 'import-basics': { version: 2, status: 'completed', at: '2026-11-15T10:12:00Z' } },
    hintsDismissed: ['mixer-pan'],
  },
  updatedAt: '2026-11-15T10:12:00Z',
};

function dotnet(): DotNetRef & { invokeMethodAsync: ReturnType<typeof vi.fn> } {
  return { invokeMethodAsync: vi.fn(() => Promise.resolve(undefined)) };
}

function breakStorage(): void {
  vi.spyOn(window, 'localStorage', 'get').mockImplementation(() => {
    throw new Error('SecurityError');
  });
}

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('defaults and validation (data-model.md 1.1)', () => {
  it('has the documented defaults', () => {
    expect(defaultPreferences()).toEqual({
      schemaVersion: 1,
      level: null,
      theme: 'system',
      density: 'auto',
      motion: 'system',
      help: { welcomeDismissed: false, tours: {}, hintsDismissed: [] },
    });
  });

  it('returns a fresh object each time', () => {
    const a = defaultPreferences();
    a.help.hintsDismissed.push('x');
    expect(defaultPreferences().help.hintsDismissed).toEqual([]);
  });

  it('keeps a fully valid record', () => {
    expect(validatePreferences(full)).toEqual(full);
  });

  it('replaces each invalid field by its default and keeps the others', () => {
    const result = validatePreferences({ ...full, level: 'guru', theme: 3, density: 'tiny', motion: null });
    expect(result.level).toBeNull();
    expect(result.theme).toBe('system');
    expect(result.density).toBe('auto');
    expect(result.motion).toBe('system');
    expect(result.help).toEqual(full.help);
  });

  it('validates help progress field by field', () => {
    const result = validatePreferences({
      ...full,
      help: {
        welcomeDismissed: 'yes',
        tours: {
          'import-basics': { version: 1, status: 'dismissed', at: '2026-11-15T10:00:00Z' },
          'Bad Id': { version: 1, status: 'completed', at: '2026-11-15T10:00:00Z' },
          'mixer-basics': { version: 0, status: 'completed', at: '2026-11-15T10:00:00Z' },
          'track-basics': { version: 1, status: 'started', at: '2026-11-15T10:00:00Z' },
          'expert-keyboard': { version: 1, status: 'completed', at: 7 },
          'mixer-advanced': 'nope',
        },
        hintsDismissed: ['mixer-pan', 4, 'Not Valid'],
      },
    });
    expect(result.help.welcomeDismissed).toBe(false);
    expect(Object.keys(result.help.tours)).toEqual(['import-basics']);
    expect(result.help.hintsDismissed).toEqual(['mixer-pan']);
  });

  it('drops help that is not an object and lists that are not arrays', () => {
    expect(validatePreferences({ ...full, help: 'x' }).help).toEqual(defaultPreferences().help);
    expect(validatePreferences({ ...full, help: { tours: [], hintsDismissed: 'a' } }).help).toEqual(defaultPreferences().help);
  });

  it('caps tours at 50 and hints at 200 entries', () => {
    const tours: Record<string, unknown> = {};
    for (let i = 0; i < 60; i++) tours[`tour-${String(i)}`] = { version: 1, status: 'completed', at: '2026-11-15T10:00:00Z' };
    const hints = Array.from({ length: 250 }, (_, i) => `hint-${String(i)}`);
    const result = validatePreferences({ ...full, help: { welcomeDismissed: true, tours, hintsDismissed: hints } });
    expect(Object.keys(result.help.tours)).toHaveLength(50);
    expect(result.help.hintsDismissed).toHaveLength(200);
  });

  it.each([[null], ['text'], [42], [[]], [{ ...full, schemaVersion: 2 }], [{ level: 'expert' }]])(
    'returns all defaults for %j (non-object or unknown schema version)',
    (raw) => {
      expect(validatePreferences(raw)).toEqual(defaultPreferences());
    },
  );

  it('drops unknown extra fields', () => {
    const result = validatePreferences({ ...full, extra: true }) as unknown as Record<string, unknown>;
    expect(result.extra).toBeUndefined();
  });

  it('keeps updatedAt only when it is a string', () => {
    expect(validatePreferences({ ...full, updatedAt: 5 }).updatedAt).toBeUndefined();
  });
});

describe('parsePreferences', () => {
  it('parses a stored record', () => {
    expect(parsePreferences(JSON.stringify(full))).toEqual(full);
  });

  it.each([[null], ['{not json'], ['x'.repeat(MAX_PREFERENCES_BYTES + 1)]])('falls back to defaults for %#', (json) => {
    expect(parsePreferences(json)).toEqual(defaultPreferences());
  });
});

describe('readPreferences', () => {
  it('reads defaults when nothing is stored', () => {
    expect(readPreferences()).toEqual({ preferences: defaultPreferences(), storageAvailable: true });
  });

  it('reads and validates the stored record', () => {
    localStorage.setItem(PREFERENCES_KEY, JSON.stringify({ ...full, theme: 'neon' }));
    const read = readPreferences();
    expect(read.storageAvailable).toBe(true);
    expect(read.preferences).toEqual({ ...full, theme: 'system' });
  });

  it('reports unavailable storage and returns defaults when storage throws', () => {
    breakStorage();
    expect(readPreferences()).toEqual({ preferences: defaultPreferences(), storageAvailable: false });
  });

  it('reports unavailable storage when getItem throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(readPreferences().storageAvailable).toBe(false);
  });
});

describe('writePreferences', () => {
  it('writes the validated record and stamps updatedAt', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-15T12:00:00Z'));
    expect(writePreferences({ ...full, updatedAt: undefined } as unknown as StoredPreferences)).toBe(true);
    const stored = JSON.parse(localStorage.getItem(PREFERENCES_KEY) ?? 'null') as StoredPreferences;
    expect(stored).toEqual({ ...full, updatedAt: '2026-11-15T12:00:00.000Z' });
  });

  it('validates before writing', () => {
    writePreferences({ ...full, level: 'guru' } as unknown as StoredPreferences);
    const stored = JSON.parse(localStorage.getItem(PREFERENCES_KEY) ?? 'null') as StoredPreferences;
    expect(stored.level).toBeNull();
  });

  it('refuses a record larger than 16 KB', () => {
    const hints = Array.from({ length: 200 }, (_, i) => `hint-${'a'.repeat(90)}-${String(i)}`);
    const big = { ...full, help: { ...full.help, hintsDismissed: hints } };
    expect(writePreferences(big)).toBe(false);
    expect(localStorage.getItem(PREFERENCES_KEY)).toBeNull();
  });

  it('returns false when storage is unavailable or full', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });
    expect(writePreferences(full)).toBe(false);
  });

  it('returns false when storage cannot be reached', () => {
    breakStorage();
    expect(writePreferences(full)).toBe(false);
  });
});

describe('watchPreferences', () => {
  function storageEvent(key: string | null, newValue: string | null): StorageEvent {
    return new StorageEvent('storage', { key, newValue });
  }

  it('forwards a change of the record as validated JSON', () => {
    const ref = dotnet();
    const watch = watchPreferences(ref);
    window.dispatchEvent(storageEvent(PREFERENCES_KEY, JSON.stringify({ ...full, theme: 'neon' })));
    expect(ref.invokeMethodAsync).toHaveBeenCalledWith('OnPreferencesChanged', JSON.stringify({ ...full, theme: 'system' }));
    watch.dispose();
  });

  it('forwards defaults when the record was removed', () => {
    const ref = dotnet();
    const watch = watchPreferences(ref);
    window.dispatchEvent(storageEvent(PREFERENCES_KEY, null));
    expect(ref.invokeMethodAsync).toHaveBeenCalledWith('OnPreferencesChanged', JSON.stringify(defaultPreferences()));
    watch.dispose();
  });

  it('ignores other keys', () => {
    const ref = dotnet();
    const watch = watchPreferences(ref);
    window.dispatchEvent(storageEvent('other', '{}'));
    expect(ref.invokeMethodAsync).not.toHaveBeenCalled();
    watch.dispose();
  });

  it('stops listening after dispose (twice is safe)', () => {
    const ref = dotnet();
    const watch = watchPreferences(ref);
    watch.dispose();
    watch.dispose();
    window.dispatchEvent(storageEvent(PREFERENCES_KEY, '{}'));
    expect(ref.invokeMethodAsync).not.toHaveBeenCalled();
  });
});

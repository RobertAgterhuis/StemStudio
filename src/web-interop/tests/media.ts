import { vi } from 'vitest';

/** A controllable `window.matchMedia` fake for jsdom (which has none). */
export interface FakeMedia {
  /** Sets whether a query matches and notifies its listeners. */
  set(query: string, matches: boolean): void;
  /** Number of registered change listeners. */
  listeners(): number;
}

/**
 * Installs a fake `matchMedia` on `window`; queries not set yet do not match.
 *
 * @param initial - Initial matches per query.
 * @returns Controls for the fake.
 */
export function installMedia(initial: Record<string, boolean> = {}): FakeMedia {
  const state = new Map(Object.entries(initial));
  const handlers = new Map<string, Set<(event: MediaQueryListEvent) => void>>();
  const of = (query: string): Set<(event: MediaQueryListEvent) => void> => {
    let set = handlers.get(query);
    if (!set) {
      set = new Set();
      handlers.set(query, set);
    }
    return set;
  };
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      media: query,
      get matches() {
        return state.get(query) ?? false;
      },
      addEventListener: (_: string, fn: (event: MediaQueryListEvent) => void) => of(query).add(fn),
      removeEventListener: (_: string, fn: (event: MediaQueryListEvent) => void) => of(query).delete(fn),
    })),
  );
  return {
    set(query, matches) {
      state.set(query, matches);
      for (const fn of of(query)) fn({ matches, media: query } as MediaQueryListEvent);
    },
    listeners: () => [...handlers.values()].reduce((sum, set) => sum + set.size, 0),
  };
}

/** The colour-scheme query used by the module. */
export const DARK = '(prefers-color-scheme: dark)';

/** The reduced-motion query used by the module. */
export const REDUCED = '(prefers-reduced-motion: reduce)';

/**
 * Returns a value that the test knows exists, or fails the test.
 *
 * @param value - The value.
 * @returns The value.
 */
export function must<T>(value: T | null | undefined): T {
  if (value === null || value === undefined) {
    throw new Error('expected a value');
  }

  return value;
}

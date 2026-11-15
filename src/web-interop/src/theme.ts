/**
 * Appearance rules shared by first-paint.js and the module (web-interop.md 1-2): resolve the stored
 * theme, density, motion and level against the OS settings and write them as data attributes.
 *
 * @packageDocumentation
 */
import type { Density, Disposable, DotNetRef, Level, Motion, StoredPreferences, Theme } from './types';
import { isRecord, once, oneOf } from './validation';

/** Media query of a dark OS colour scheme. */
export const DARK_QUERY = '(prefers-color-scheme: dark)';

/** Media query of the OS reduced-motion setting. */
export const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

/** The resolved appearance written to the root element. */
export interface Appearance {
  /** `data-theme`. */
  theme: Theme;
  /** `data-density`. */
  density: Density;
  /** `data-motion`. */
  motion: Motion;
  /** `data-level`. */
  level: Level;
}

/** Answers whether a media query matches. */
export type MediaMatcher = (query: string) => boolean;

const LEVELS = ['beginner', 'advanced', 'expert'] as const;

function resolveTheme(value: unknown, matches: MediaMatcher): Theme {
  const theme = oneOf(value, ['light', 'dark'] as const, 'system');
  if (theme !== 'system') {
    return theme;
  }

  return matches(DARK_QUERY) ? 'dark' : 'light';
}

function resolveDensity(value: unknown, level: Level): Density {
  const density = oneOf(value, ['comfortable', 'compact'] as const, 'auto');
  if (density !== 'auto') {
    return density;
  }

  return level === 'expert' ? 'compact' : 'comfortable';
}

function resolveMotion(value: unknown, matches: MediaMatcher): Motion {
  return value === 'reduced' || matches(REDUCED_MOTION_QUERY) ? 'reduced' : 'full';
}

/**
 * Resolves raw (possibly invalid) stored data; anything invalid takes its default.
 *
 * @param raw - Parsed stored record or anything else.
 * @param matches - Media query matcher (the OS settings).
 * @returns The appearance to apply.
 */
export function resolveAppearance(raw: unknown, matches: MediaMatcher): Appearance {
  const record = isRecord(raw) && raw.schemaVersion === 1 ? raw : {};
  const level = oneOf(record.level, LEVELS, 'beginner');
  return {
    theme: resolveTheme(record.theme, matches),
    density: resolveDensity(record.density, level),
    motion: resolveMotion(record.motion, matches),
    level,
  };
}

/**
 * Writes `data-theme`, `data-density`, `data-motion` and `data-level`.
 *
 * @param root - Element receiving the attributes.
 * @param appearance - Resolved appearance.
 */
export function applyAttributes(root: HTMLElement, appearance: Appearance): void {
  Object.assign(root.dataset, appearance);
}

function mediaFunction(): Window['matchMedia'] | undefined {
  return (window as { matchMedia?: Window['matchMedia'] }).matchMedia;
}

/**
 * The OS settings as a matcher; without `matchMedia` nothing matches.
 *
 * @returns The matcher.
 */
export function systemMatcher(): MediaMatcher {
  const media = mediaFunction();
  return (query) => (media ? media.call(window, query).matches : false);
}

const STORAGE_KEY = 'stemstudio.preferences';

/** Largest record read before the first paint, in UTF-16 code units (a cheap stand-in for 16 KB). */
const MAX_STORED_LENGTH = 16 * 1024;

function readStored(): unknown {
  try {
    const json = window.localStorage.getItem(STORAGE_KEY);
    return json !== null && json.length <= MAX_STORED_LENGTH ? JSON.parse(json) : null;
  } catch {
    return null;
  }
}

/**
 * Applies the stored appearance before the first paint (first-paint.js); any problem (no storage,
 * bad JSON) gives the defaults. Never throws.
 *
 * @param root - The element receiving the data attributes (normally `document.documentElement`).
 */
export function applyFirstPaint(root: HTMLElement): void {
  applyAttributes(root, resolveAppearance(readStored(), systemMatcher()));
}

/**
 * Applies a record to the document element (same rules as first-paint.js).
 *
 * @param preferences - The record to apply.
 */
export function applyAppearance(preferences: StoredPreferences): void {
  applyAttributes(document.documentElement, resolveAppearance(preferences, systemMatcher()));
}

/**
 * Calls `OnSystemAppearanceChanged()` when the OS colour scheme or reduced-motion setting changes.
 *
 * @param dotnet - Receiver of the callback.
 * @returns Handle that removes the media-query listeners.
 */
export function watchSystemAppearance(dotnet: DotNetRef): Disposable {
  const media = mediaFunction();
  const lists = media ? [DARK_QUERY, REDUCED_MOTION_QUERY].map((query) => media.call(window, query)) : [];
  const onChange = (): void => {
    void dotnet.invokeMethodAsync('OnSystemAppearanceChanged');
  };
  lists.forEach((list) => {
    list.addEventListener('change', onChange);
  });
  return once(() => {
    lists.forEach((list) => {
      list.removeEventListener('change', onChange);
    });
  });
}

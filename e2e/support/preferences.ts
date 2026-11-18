/**
 * Browser preferences of feature 004 (data-model.md 1, key `stemstudio.preferences`) as
 * Playwright storage states, one per experience level (T011, ui-test-ids 1.3.0 R7-R9).
 *
 * The 1.2.0 suite runs at **Advanced** with the welcome dismissed and every known tour recorded as
 * completed, so no welcome panel or tour offer appears and every 1.2.0 id renders as before (R7).
 * New specs pick another state with `test.use({ storageState: ... })`.
 *
 * Level states also store the 012 practice panel as open (`stemstudio.practice-panel`), like the
 * completed tours: the panel starts closed at neutral settings (CTO decision 2026-11-17), and the
 * practice specs drive its controls directly. The design matrix (`cellState`) leaves it unset, so
 * screenshots and scans show the real default.
 *
 * @packageDocumentation
 */
import type { BrowserContextOptions, Page } from '@playwright/test';
// The stored choice types are the web-interop module's own (one definition, no copy).
import type { DensityPreference, Level, MotionPreference, ThemePreference } from '../../src/web-interop/src/types';
import { webBaseUrl } from './env';

export type { DensityPreference, Level, MotionPreference, ThemePreference };

/** Ids of the five tours of design-spec.md 7. */
export type TourId =
  | 'import-basics'
  | 'track-basics'
  | 'mixer-basics'
  | 'mixer-advanced'
  | 'expert-keyboard'
  | 'practice-section'
  | 'hands-free';

/** One stored tour result (data-model.md 1.1 `help.tours`). */
export interface StoredTourRecord {
  readonly version: number;
  readonly status: 'completed' | 'dismissed';
  readonly at: string;
}

/** The stored record, schema version 1 (data-model.md 1.1). */
export interface StoredPreferences {
  readonly schemaVersion: 1;
  readonly level: Level | null;
  readonly theme: ThemePreference;
  readonly density: DensityPreference;
  readonly motion: MotionPreference;
  readonly help: {
    readonly welcomeDismissed: boolean;
    readonly tours: Readonly<Record<string, StoredTourRecord>>;
    readonly hintsDismissed: readonly string[];
  };
  readonly updatedAt: string;
}

/** Choices for {@link preferencesRecord}; every field is optional and defaults as noted. */
export interface PreferenceOptions {
  /** Default `null` (not chosen: the app shows Beginner and the welcome panel). */
  readonly level?: Level | null;
  /** Default `system`. */
  readonly theme?: ThemePreference;
  /** Default `auto`. */
  readonly density?: DensityPreference;
  /** Default `system`. */
  readonly motion?: MotionPreference;
  /** Default: true when a level is set. */
  readonly welcomeDismissed?: boolean;
  /** Record every known tour as completed so no offer is shown (default true when a level is set). */
  readonly toursDone?: boolean;
}

/** Playwright storage state object (cookies + local storage per origin). */
export type StorageState = Exclude<NonNullable<BrowserContextOptions['storageState']>, string>;

/** Local-storage key of the record (data-model.md 1). */
export const preferencesKey = 'stemstudio.preferences';

/** Local-storage key of the practice panel's open/closed choice (012, ui-test-ids 1.11.0 `practice-panel-toggle`). */
export const practicePanelKey = 'stemstudio.practice-panel';

/** Stored practice panel choice; `unset` stores nothing (closed at neutral settings, open otherwise). */
export type PracticePanelState = 'expanded' | 'collapsed' | 'unset';

/** Further local-storage entries of a storage state (`name -> value`). */
export type StorageEntries = Readonly<Record<string, string>>;

/** The local-storage entries of a practice panel choice. */
export function practicePanelEntries(state: PracticePanelState): StorageEntries {
  return state === 'unset' ? {} : { [practicePanelKey]: JSON.stringify({ expanded: state === 'expanded' }) };
}

/**
 * Content versions of the shipped tours (004; 012 added a practice step to both mixer tours);
 * bump here when a tour file's version changes (`tests/preferences-support.spec.ts` checks it).
 */
export const tourVersions: Readonly<Record<TourId, number>> = {
  'import-basics': 1,
  'track-basics': 1,
  'mixer-basics': 2,
  'mixer-advanced': 2,
  'expert-keyboard': 1,
  // 014: the section-loop and hands-free tours.
  'practice-section': 1,
  'hands-free': 1,
};

/** Fixed timestamp, so stored records (and screenshots of anything derived) are deterministic. */
const fixedAt = '2026-11-15T00:00:00Z';

/** Builds a valid schema-1 record from `options`. */
export function preferencesRecord(options: PreferenceOptions = {}): StoredPreferences {
  const o = { level: null, theme: 'system', density: 'auto', motion: 'system', ...withoutUndefined(options) } as const;
  return { schemaVersion: 1, level: o.level, theme: o.theme, density: o.density, motion: o.motion, help: helpFor(options), updatedAt: fixedAt };
}

function helpFor(options: PreferenceOptions): StoredPreferences['help'] {
  const chosen = (options.level ?? null) !== null;
  return {
    welcomeDismissed: options.welcomeDismissed ?? chosen,
    tours: toursFor(options.toursDone ?? chosen),
    hintsDismissed: [],
  };
}

function withoutUndefined(options: PreferenceOptions): PreferenceOptions {
  return Object.fromEntries(Object.entries(options).filter(([, value]) => value !== undefined));
}

function toursFor(done: boolean): Record<string, StoredTourRecord> {
  return done ? completedTours() : {};
}

function completedTours(): Record<string, StoredTourRecord> {
  return Object.fromEntries(
    Object.entries(tourVersions).map(([id, version]) => [id, { version, status: 'completed', at: fixedAt }]),
  );
}

/**
 * Storage state holding `record` for the web origin; `null` = empty storage (first run).
 * @param origin The origin the record belongs to (default: the origin of `E2E_BASE_URL`); the
 *   claude-key local-only spec opens the app under a second, non-loopback origin.
 * @param extra Further local-storage entries of that origin (for example {@link practicePanelEntries}).
 */
export function storageStateFor(
  record: StoredPreferences | null,
  origin: string = new URL(webBaseUrl).origin,
  extra: StorageEntries = {},
): StorageState {
  const entries = Object.entries(extra).map(([name, value]) => ({ name, value }));
  const localStorage = record === null ? entries : [{ name: preferencesKey, value: JSON.stringify(record) }, ...entries];
  return { cookies: [], origins: localStorage.length === 0 ? [] : [{ origin, localStorage }] };
}

/**
 * Storage state for a chosen level (welcome dismissed, tours done unless overridden).
 * @param practicePanel The stored practice panel choice (default `expanded`, see the file comment).
 */
export function levelState(level: Level, options: Omit<PreferenceOptions, 'level'> = {}, practicePanel: PracticePanelState = 'expanded'): StorageState {
  return storageStateFor(preferencesRecord({ ...options, level }), undefined, practicePanelEntries(practicePanel));
}

/** Default state of the whole suite (playwright.config.ts): Advanced, ui-test-ids R7. */
export const advancedState: StorageState = levelState('advanced');

/** A first visit: nothing stored (effective level Beginner, welcome shown). */
export const freshState: StorageState = storageStateFor(null);

/** Expert with the welcome dismissed and no tour offers. */
export const expertState: StorageState = levelState('expert');

/** Reads and parses the stored record of the page's origin (null when absent or unparseable). */
export async function readStoredPreferences(page: Page): Promise<StoredPreferences | null> {
  const text = await page.evaluate((key) => window.localStorage.getItem(key), preferencesKey);
  return parseRecord(text);
}

function parseRecord(text: string | null): StoredPreferences | null {
  try {
    return text === null ? null : (JSON.parse(text) as StoredPreferences);
  } catch {
    return null;
  }
}

/** Writes `record` into the page's local storage (takes effect on the next load). */
export async function writeStoredPreferences(page: Page, record: StoredPreferences): Promise<void> {
  await page.evaluate(
    ([key, value]) => {
      window.localStorage.setItem(key, value);
    },
    [preferencesKey, JSON.stringify(record)] as const,
  );
}

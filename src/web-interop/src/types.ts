/**
 * Public types of the web-interop module (contracts/web-interop.md 1.0.0, data-model.md 1).
 *
 * @packageDocumentation
 */

/** Version of the web-interop contract implemented by this module. */
export type WebInteropContractVersion = '1.0.0';

/** Experience level. */
export type Level = 'beginner' | 'advanced' | 'expert';

/** Stored theme choice; `system` follows `prefers-color-scheme`. */
export type ThemePreference = 'system' | 'light' | 'dark';

/** Stored density choice; `auto` is compact at Expert and comfortable otherwise. */
export type DensityPreference = 'auto' | 'comfortable' | 'compact';

/** Stored motion choice; `system` follows `prefers-reduced-motion`. */
export type MotionPreference = 'system' | 'reduced';

/** Resolved theme written to `data-theme`. */
export type Theme = 'light' | 'dark';

/** Resolved density written to `data-density`. */
export type Density = 'comfortable' | 'compact';

/** Resolved motion written to `data-motion`. */
export type Motion = 'full' | 'reduced';

/** How a tour ended. */
export type TourStatus = 'completed' | 'dismissed';

/** Scope of a key map registered by {@link registerShortcuts}. */
export type ShortcutScope = 'global' | 'mixer';

/** Progress of one tour (data-model.md 1.1 `help.tours`). */
export interface StoredTourRecord {
  /** Tour content version the record applies to (>= 1). */
  version: number;
  /** How the tour ended. */
  status: TourStatus;
  /** ISO 8601 UTC time the tour ended. */
  at: string;
}

/** Help progress (data-model.md 1.1 `help`). */
export interface StoredHelpProgress {
  /** True after a level choice or "Not now" in the welcome panel. */
  welcomeDismissed: boolean;
  /** Tour id (kebab-case) to record; at most 50 entries. */
  tours: Record<string, StoredTourRecord>;
  /** Dismissed inline-hint topic ids; at most 200 entries. */
  hintsDismissed: string[];
}

/** Raw stored record, schema version 1 (data-model.md 1.1). Invalid fields are replaced by defaults. */
export interface StoredPreferences {
  /** Always 1 for this contract version. */
  schemaVersion: 1;
  /** Chosen level, or null when not chosen yet (effective level Beginner). */
  level: Level | null;
  /** Theme choice. */
  theme: ThemePreference;
  /** Density choice. */
  density: DensityPreference;
  /** Motion choice. */
  motion: MotionPreference;
  /** Help progress. */
  help: StoredHelpProgress;
  /** ISO 8601 UTC time of the last write (set by {@link writePreferences}); informational. */
  updatedAt?: string;
}

/** Result of a read: the validated record plus whether storage works. */
export interface PreferencesRead {
  /** The validated record (defaults where the stored value was missing or invalid). */
  preferences: StoredPreferences;
  /** False when local storage threw on access. */
  storageAvailable: boolean;
}

/** A .NET object reference (DotNetObjectReference) as seen from JS. */
export interface DotNetRef {
  /** Invokes a `[JSInvokable]` method on the .NET object. */
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

/** A registration that can be undone. */
export interface Disposable {
  /** Removes listeners and releases resources; safe to call twice. */
  dispose(): void;
}

/** One tour step with its texts already resolved by .NET. */
export interface TourRuntimeStep {
  /** `data-help-id` of the target element. */
  target: string;
  /** Popover title. */
  title: string;
  /** Popover text. */
  text: string;
}

/** Localised button and progress labels of the tour popover. */
export interface TourLabels {
  /** "Next" button. */
  next: string;
  /** "Previous" button. */
  previous: string;
  /** "Done" button on the last step. */
  done: string;
  /** Accessible name of the close button. */
  close: string;
  /** Progress text template, e.g. "{{current}} of {{total}}". */
  progress: string;
}

/** A tour with texts already resolved by .NET (topic summaries filled in). */
export interface TourRuntime {
  /** Tour id (help-content.md 3). */
  id: string;
  /** Tour content version. */
  version: number;
  /** True disables popover animation. */
  reducedMotion: boolean;
  /** Steps in order; missing or hidden targets are skipped at run time. */
  steps: TourRuntimeStep[];
  /** Button and progress labels. */
  labels: TourLabels;
}

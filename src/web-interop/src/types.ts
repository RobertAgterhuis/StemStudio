/**
 * Public types of the web-interop module (contracts/web-interop.md 1.2.0, data-model.md 1).
 *
 * @packageDocumentation
 */

/**
 * Version of the web-interop contract implemented by this module: the newest full document (1.1.0 = 1.0.0 + 022's 1.2.0 +
 * 014's key map as data; labels identify merged deltas, not an order).
 */
export type WebInteropContractVersion = '1.1.0';

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

/** Scope of a key map registered by {@link registerShortcuts} (`library` 1.2.0, `setlist` 1.1.0). */
export type ShortcutScope = 'global' | 'mixer' | 'library' | 'setlist';

/** One key chord (web-interop 1.1.0 section 5.1); `key` is `KeyboardEvent.key` lower-cased (" " for Space). */
export interface KeyChord {
  /** The key, 1..20 characters. */
  readonly key: string;
  /** Ctrl (Cmd on macOS) required; default false. */
  readonly mod?: boolean;
  /** Shift required (true) or forbidden (false); omitted: forbidden for letters, not checked otherwise. */
  readonly shift?: boolean;
}

/** Layer of a binding: normal keys, or the foot-pedal layer (pedal mode). */
export type KeyLayer = 'keys' | 'pedal';

/** A command with its chords in one layer. */
export interface KeyBinding {
  /** Command id (014 data-model 5.2), passed to `OnCommand`. */
  readonly command: string;
  /** 0..2 chords. */
  readonly chords: readonly KeyChord[];
  /** Layer of the chords. */
  readonly layer: KeyLayer;
  /** Fires even in a typing context (Mod chords only). */
  readonly anywhere?: boolean;
  /** Feature that provides the handler. */
  readonly owner: string;
  /** Held for a feature not shipped yet: never dispatched. */
  readonly reserved?: boolean;
}

/** A key map of one scope. */
export interface KeyMap {
  /** The bindings. */
  readonly bindings: readonly KeyBinding[];
  /** False: every chord without `mod` is ignored (WCAG 2.2 SC 2.1.4). */
  readonly singleKey: boolean;
  /** True: pedal-layer chords also fire while focus is on a non-text control. */
  readonly pedalMode: boolean;
}

/** A user override chord in the preferences (data-model 5.1). */
export interface StoredChord extends KeyChord {
  /** Layer; default `keys`. */
  readonly layer?: KeyLayer;
}

/** Shortcut preferences (schemaVersion 2, data-model 5.1). */
export interface StoredShortcuts {
  /** Single-key shortcuts on (default true). */
  singleKey: boolean;
  /** Pedal mode on (default false). */
  pedalMode: boolean;
  /** Pedal preset id (default `play-section-start`). */
  pedalPreset: string;
  /** Command id -> 0..2 chords; an empty list unbinds the command. At most 60 commands. */
  overrides: Record<string, StoredChord[]>;
}

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

/** Raw stored record, schema version 2 (data-model.md 1.1, 014 data-model 5.1). Invalid fields are replaced by defaults. */
export interface StoredPreferences {
  /** Always 2 when written by this contract version (version 1 records are read and upgraded). */
  schemaVersion: 2;
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
  /** Shortcut preferences (014). */
  shortcuts: StoredShortcuts;
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

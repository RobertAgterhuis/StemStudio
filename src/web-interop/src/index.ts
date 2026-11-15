/**
 * StemStudio web-interop entry point (ES module loaded by Blazor via JS interop), contract
 * web-interop.md 1.0.0. Phase 0 skeleton: every function throws until the frontend agent
 * implements it (tasks T013-T059); the signatures are binding.
 *
 * @packageDocumentation
 */
import type {
  Disposable,
  DotNetRef,
  PreferencesRead,
  ShortcutScope,
  StoredPreferences,
  TourRuntime,
  WebInteropContractVersion,
} from './types';

export type * from './types';

/** Version of the web-interop contract implemented by this module. */
export const WEB_INTEROP_CONTRACT_VERSION: WebInteropContractVersion = '1.0.0';

function notImplemented(name: string, args: readonly unknown[]): never {
  throw new Error(`not implemented: ${name} (${String(args.length)} argument(s))`);
}

/**
 * Reads and validates the stored record; never throws once implemented.
 *
 * @returns The validated record and whether storage works.
 */
export function readPreferences(): PreferencesRead {
  return notImplemented('readPreferences', []);
}

/**
 * Writes the record (validated, at most 16 KB) and sets `updatedAt`.
 *
 * @param preferences - The whole record.
 * @returns False when storage is unavailable, full or the record is too large.
 */
export function writePreferences(preferences: StoredPreferences): boolean {
  return notImplemented('writePreferences', [preferences]);
}

/**
 * Calls `dotnet.invokeMethodAsync("OnPreferencesChanged", json)` when another tab changes the record.
 *
 * @param dotnet - Receiver of the callback.
 * @returns Handle that removes the `storage` listener.
 */
export function watchPreferences(dotnet: DotNetRef): Disposable {
  return notImplemented('watchPreferences', [dotnet]);
}

/**
 * Applies the theme, density, motion and level attributes (same rules as first-paint.js).
 *
 * @param preferences - The record to apply.
 */
export function applyAppearance(preferences: StoredPreferences): void {
  notImplemented('applyAppearance', [preferences]);
}

/**
 * Calls `dotnet.invokeMethodAsync("OnSystemAppearanceChanged")` when the OS colour scheme or the
 * reduced-motion setting changes.
 *
 * @param dotnet - Receiver of the callback.
 * @returns Handle that removes the media-query listeners.
 */
export function watchSystemAppearance(dotnet: DotNetRef): Disposable {
  return notImplemented('watchSystemAppearance', [dotnet]);
}

/**
 * Registers the key map of design-spec.md 6 and calls `dotnet.invokeMethodAsync("OnCommand", id)`
 * with a command id. Keys are ignored in inputs, textareas, selects, contenteditable, dialogs and
 * the tour popover; Space is never handled here.
 *
 * @param dotnet - Receiver of the command ids.
 * @param scope - `global` or `mixer` key map.
 * @returns Handle that removes the key listeners.
 */
export function registerShortcuts(dotnet: DotNetRef, scope: ShortcutScope): Disposable {
  return notImplemented('registerShortcuts', [dotnet, scope]);
}

/**
 * Starts a tour (driver.js); steps whose target is missing or hidden are skipped. Calls
 * `dotnet.invokeMethodAsync("OnTourEnded", tourId, "completed" | "dismissed", lastStep)`.
 *
 * @param dotnet - Receiver of the end callback.
 * @param tour - Tour with resolved texts.
 * @returns Handle that destroys the running tour.
 */
export function startTour(dotnet: DotNetRef, tour: TourRuntime): Disposable {
  return notImplemented('startTour', [dotnet, tour]);
}

/**
 * Returns the `data-help-id` of the focused element or of its closest ancestor.
 *
 * @returns The help id, or null when none applies.
 */
export function focusedHelpId(): string | null {
  return notImplemented('focusedHelpId', []);
}

/**
 * Remembers the focused element.
 *
 * @returns Handle that restores focus to that element on dispose.
 */
export function captureFocus(): Disposable {
  return notImplemented('captureFocus', []);
}

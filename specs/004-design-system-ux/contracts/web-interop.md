# Contract: web-interop module (v1.2.0)

Typed JS interop surface between `StemStudio.Web` (Blazor, C#) and the new TypeScript package
`src/web-interop/` (`@stemstudio/web-interop`), built by Vite to
`src/StemStudio.Web/wwwroot/js/web-interop.js` (ES module) plus
`src/StemStudio.Web/wwwroot/js/first-paint.js` (classic script). Owner: frontend. The skeleton
(`src/web-interop/src/types.ts`) is created by the architect in Phase 0 (T001). Rationale:
research.md R-09, R-10. The audio-engine contract (2.0.0) is not affected.

## 1. `first-paint.js` (classic script, first element in `<head>`)

- Synchronous, no imports, <= 1 KB minified, no network.
- Reads `localStorage["stemstudio.preferences"]`, validates the minimum it needs and sets on
  `document.documentElement`: `data-theme` (`light`|`dark`, `system` resolved by
  `matchMedia("(prefers-color-scheme: dark)")`), `data-density` (`comfortable`|`compact`, `auto`
  resolved from the level: Expert -> compact), `data-motion` (`full`|`reduced`, `system` resolved by
  `prefers-reduced-motion`), `data-level`.
- Any exception (no storage, bad JSON) -> defaults: theme from the OS, density comfortable, motion
  from the OS, level beginner.

## 2. ES module exports (`web-interop.js`)

```ts
/** Experience level. */
export type Level = "beginner" | "advanced" | "expert";

/** Raw stored record; see data-model.md 1. Unknown or invalid fields are replaced by defaults. */
export interface StoredPreferences { /* data-model.md 1.1 */ }

/** Result of a read: the validated record plus whether storage works. */
export interface PreferencesRead { preferences: StoredPreferences; storageAvailable: boolean; }

/** Reads and validates the record (never throws). */
export function readPreferences(): PreferencesRead;

/** Writes the record (validated, <= 16 KB); returns false when storage is unavailable or full. */
export function writePreferences(preferences: StoredPreferences): boolean;

/** Calls dotnet.invokeMethodAsync("OnPreferencesChanged", json) when another tab changes the record. */
export function watchPreferences(dotnet: DotNetRef): Disposable;

/** Applies theme, density, motion and level attributes (same rules as first-paint.js). */
export function applyAppearance(preferences: StoredPreferences): void;

/** Calls dotnet.invokeMethodAsync("OnSystemAppearanceChanged") when the OS colour scheme or
 *  reduced-motion setting changes. */
export function watchSystemAppearance(dotnet: DotNetRef): Disposable;

/** Registers the global key map (design-spec.md 6). Calls dotnet.invokeMethodAsync("OnCommand", id)
 *  with a command id (e.g. "help.open", "palette.open", "mixer.loop.toggle", "mixer.channel.select:3").
 *  Keys are ignored while focus is in an input, textarea, select, contenteditable, dialog
 *  or tour popover. Space is NOT handled here (mixer-keys.js keeps it). */
export function registerShortcuts(dotnet: DotNetRef, scope: "global" | "mixer" | "library"): Disposable;  // "library" since 1.2.0

/** Starts a tour. Steps whose target `[data-help-id=...]` is missing or hidden are skipped.
 *  Calls dotnet.invokeMethodAsync("OnTourEnded", tourId, "completed" | "dismissed", lastStep). */
export function startTour(dotnet: DotNetRef, tour: TourRuntime): Disposable;

/** Returns the data-help-id of the focused element or its closest ancestor, or null. */
export function focusedHelpId(): string | null;

/** Remembers the focused element and returns a handle that restores focus on dispose. */
export function captureFocus(): Disposable;

/** A .NET object reference (DotNetObjectReference) as seen from JS. */
export interface DotNetRef { invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>; }
export interface Disposable { dispose(): void; }

/** A tour with texts already resolved by .NET (topic summaries filled in). */
export interface TourRuntime {
  id: string;
  version: number;
  reducedMotion: boolean;
  steps: { target: string; title: string; text: string }[];
  labels: { next: string; previous: string; done: string; close: string; progress: string };
}
```

## 3. Rules

- Every exported function is documented with TSDoc and covered by Vitest (jsdom), >= 90 % line
  and branch; driver.js is replaced by a fake in unit tests.
- The tour popover gets `data-testid="tour-popover"` with `data-tour-id`, `data-step`,
  `data-steps`, and buttons `tour-next`, `tour-prev`, `tour-close` (ui-test-ids 1.3.0); focus is
  trapped in the popover and restored to the element focused before the tour.
- No module touches the audio engine or audio timing (constitution V).
- C# side: `IWebInterop` (Phase 0 stub) wraps these calls; components never call
  `IJSRuntime` for these features directly.
- The full TypeScript types (incl. `StoredPreferences`, `StoredHelpProgress`, `StoredTourRecord`,
  `TourRuntimeStep`, `TourLabels`, `ShortcutScope`) are in `src/web-interop/src/types.ts`; the
  module also exports `WEB_INTEROP_CONTRACT_VERSION` (`"1.2.0"`). `first-paint.ts` is a separate
  IIFE entry (`vite build --mode first-paint`).

## 4. C# mapping (`src/StemStudio.Web/Interop/`, Phase 0 T002)

| JS | `IWebInterop` | JS -> .NET callback |
|---|---|---|
| `readPreferences()` | `ReadPreferencesAsync()` -> `StoredPreferencesRead(JsonElement, bool)` | - |
| `writePreferences(p)` | `WritePreferencesAsync(JsonElement)` -> `bool` | - |
| `applyAppearance(p)` | `ApplyAppearanceAsync(JsonElement)` | - |
| `watchPreferences` | `WatchPreferencesAsync(Func<string, Task>)` -> `IAsyncDisposable` | `OnPreferencesChanged(json)` |
| `watchSystemAppearance` | `WatchSystemAppearanceAsync(Func<Task>)` | `OnSystemAppearanceChanged()` |
| `registerShortcuts` | `RegisterShortcutsAsync(ShortcutScope, Func<string, Task>)` | `OnCommand(commandId)` |
| `startTour` | `StartTourAsync(TourRuntime, Func<TourEnded, Task>)` | `OnTourEnded(tourId, status, lastStep)` -> `TourEnded(TourId, Completed, LastStep)` |
| `focusedHelpId()` | `FocusedHelpIdAsync()` -> `string?` | - |
| `captureFocus()` | `CaptureFocusAsync()` -> `IAsyncDisposable` (dispose restores focus) | - |

The implementation (`WebInterop`, frontend T016) owns the `DotNetObjectReference` and the
`[JSInvokable]` methods; the record mapping UserPreferences <-> JSON lives in
`LocalPreferencesStore`, not in the interop layer (Interop does not depend on Preferences).

## 5. Changelog

| Version | Feature | Change |
|---|---|---|
| 1.0.0 | 004 | First version. |
| 1.1.0 | 014 | Allocated to 014 (scope `setlist`, `specs/014-practice-flow/contracts/web-interop.delta.md`); arrives with the 014 round (not on this branch). |
| 1.2.0 | 022 | `ShortcutScope` adds `library` (`ShortcutScope.Library = 2` in C#). Key map of the scope: `/` -> `library.search.focus` (library pages `/library` and `/library/{folderId}`; ignored while typing in a field; `Mod+/` stays `shortcuts.open`). Additive. |

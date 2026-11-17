# Contract: web-interop module (v1.1.0)

Typed JS interop surface between `StemStudio.Web` (Blazor, C#) and the new TypeScript package
`src/web-interop/` (`@stemstudio/web-interop`), built by Vite to
`src/StemStudio.Web/wwwroot/js/web-interop.js` (ES module) plus
`src/StemStudio.Web/wwwroot/js/first-paint.js` (classic script). Owner: frontend. The skeleton
(`src/web-interop/src/types.ts`) is created by the architect in Phase 0 (T001). Rationale:
research.md R-09, R-10. The audio-engine contract (2.0.0) is not affected.

**Changelog**: 1.0.0 (feature 004). 1.1.0 (additive, feature 014, ADR-023 D5; merged unchanged from
`specs/014-practice-flow/contracts/web-interop.delta.md` in 014 Phase 0 part 2, T005) - the key map is data:
`KeyChord`, `KeyBinding`, `KeyMap`, scope `setlist`, `registerShortcuts(dotnet, scope, keyMap?)`,
`updateShortcuts`, `defaultKeyMap`, `captureChord`, `isReservedChord`, the extended guard (sliders, list
boxes, grids, menus; Space/Enter on buttons and links), pedal mode, the single-key switch (WCAG 2.2 SC
2.1.4), preferences schemaVersion 2 (`shortcuts`), and `mixer-keys.js` retired (Space is the binding
`mixer.play-pause`). The defaults are the catalogue `src/StemStudio.Web/wwwroot/js/keymap-defaults.json`
(section 5.4). Calling `registerShortcuts(dotnet, scope)` without a key map keeps the 1.0.0 behaviour.
1.2.0 (additive, feature 022; built in code by the 022 frontend lane, CTO decision row "web-interop 1.2.0") -
`ShortcutScope` adds `library`; its key map is `/` -> `library.search.focus` (Mod+/ stays the global
overview); module constant `1.2.0`.

**Contains**: 1.0.0, 1.2.0, 1.1.0 (merge order). Re-merged at the 014+015 round start (014 DELTA.md 8): this
document is 1.0.0 + 022's 1.2.0 + the 014 delta and carries 014's allocated label 1.1.0 (ROADMAP "Full-document
labels": labels identify the merged deltas, never an order; consumers compare by containment). The module
constant `WEB_INTEROP_CONTRACT_VERSION` names the newest full document: it stays `"1.2.0"` until the 014
frontend lane ships the key map as data, then becomes `"1.1.0"` (this document). In the data-driven key map
the scope `library` is a catalogue scope like the others: 022's hard-coded `LIBRARY` binding becomes the
catalogue row `library.search.focus` (owner 022, same key, same command).

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
export function registerShortcuts(dotnet: DotNetRef, scope: "global" | "mixer"): Disposable;

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
  module also exports `WEB_INTEROP_CONTRACT_VERSION` (`"1.2.0"` from feature 022, `"1.1.0"` once 014 ships; see the changelog). `first-paint.ts` is a separate
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

## 5. Key map as data (v1.1.0, feature 014)

### 5.1 Types and exports

```ts
/** One key chord; `key` is `KeyboardEvent.key` lower-cased ("l", "pagedown", " " for Space, "arrowleft"). */
export interface KeyChord {
  readonly key: string;          // 1..20 characters
  readonly mod?: boolean;        // Ctrl (Cmd on macOS); default false
  readonly shift?: boolean;      // default: Shift not required and not forbidden for named keys, false for printable keys
}

/** A command with its chords in one layer. */
export interface KeyBinding {
  readonly command: string;      // data-model 5.2 ids, e.g. "mixer.section.next", "mixer.seek:-5"
  readonly chords: readonly KeyChord[]; // 0..2
  readonly layer: 'keys' | 'pedal';
  /** Fires even in a typing context (only Mod chords, e.g. palette). */
  readonly anywhere?: boolean;
  /** Feature that provides the handler ("004", "012", "014", "015", ...); shown in the overview. */
  readonly owner: string;
  /** True: chords are held for a feature not shipped yet (015 R/C); never dispatched, not overridable. */
  readonly reserved?: boolean;
}

export interface KeyMap {
  readonly bindings: readonly KeyBinding[];
  /** False: every chord without `mod` is ignored (WCAG 2.1.4). Pedal-layer chords follow the same rule. */
  readonly singleKey: boolean;
  /** True: pedal-layer chords also fire while focus is on a non-text control. */
  readonly pedalMode: boolean;
}

export type ShortcutScope = 'global' | 'mixer' | 'library' | 'setlist';   // 'library' added in 1.2.0 (022), 'setlist' in 1.1.0
```

Exports (added):

| Export | Signature | Notes |
|---|---|---|
| `registerShortcuts` | `(dotnet, scope, keyMap?: KeyMap) => Disposable` | without `keyMap`: the 1.0.0 behaviour; with it: only the given bindings of that scope |
| `updateShortcuts` | `(handle: Disposable, keyMap: KeyMap) => void` | swaps the map without re-registering |
| `defaultKeyMap` | `(scope) => KeyMap` | the defaults of spec FR-020, read from the catalogue `src/StemStudio.Web/wwwroot/js/keymap-defaults.json` (architect, T006; imported at build time; the same file `KeyMapDefaults` reads in C#) |
| `captureChord` | `(dotnet, options: {timeoutMs: 1000..30000}) => Disposable` | "Learn": the next keydown anywhere (except Escape, which cancels) is reported once by `dotnet.invokeMethodAsync("OnChordCaptured", chord \| null)` and prevented |
| `isReservedChord` | `(chord) => string \| null` | the reason text when reserved (ADR-023 D5 list), else null |

### 5.2 Rules (in addition to section 3)

1. **Typing and native contexts**: `input`, `textarea`, `select`, `[contenteditable]`,
   `[role="dialog"]`, the tour popover (1.0.0) **plus** `input[type=range]`, `[role="slider"]`,
   `[role="listbox"]`, `[role="grid"]`, `[role="menu"]`, `[role="menubar"]`, `[role="tablist"]`
   (arrow keys stay native) and, for Space and Enter only, `button`, `a[href]`, `summary`,
   `[role="button"]`, `[role="checkbox"]`, `[role="switch"]`. Pedal-layer chords in pedal mode
   ignore the second list (non-text controls) but never the first.
2. Repeats (`event.repeat`) are ignored; Alt/Option and the OS key (meta on Windows/Linux, Ctrl on
   macOS) never match (1.0.0).
3. Conflicts: a chord matches at most one binding over all registered scopes (validation in C#
   before the map is passed; the TS side drops later duplicates and logs once to the console).
4. Reserved chords (never bound, `isReservedChord`): Tab, Shift+Tab, Enter, Escape, F5, F6, F7,
   F11, F12, Insert, CapsLock, ContextMenu, Mod+W/T/N/R/L/Q/P/F/0/=/+/-, Mod+Shift+T/N/W.
5. The `mixer-keys.js` Space handler is removed in 014; Space is the binding `mixer.play-pause`
   with the same guard (rule 1). Command ids are passed to `OnCommand(id)` as in 1.0.0.

### 5.3 Preferences record (schemaVersion 2)

`preferences.ts` reads schemaVersion 1 and 2 and writes 2; new field `shortcuts` (data-model
5.1): `{singleKey: boolean (true), pedalMode: boolean (false), pedalPreset: string
("play-section-start"), overrides: Record<commandId, KeyChord & {layer?}[]>}`, max 60 commands,
validated field by field (invalid entries dropped). The record limit stays 16 KB.

### 5.4 Default key map catalogue

The catalogue `src/StemStudio.Web/wwwroot/js/keymap-defaults.json` (architect, 014 T006) is the single source
of the default key map for TypeScript (`defaultKeyMap` imports it at build time) and C# (`KeyMapDefaults`).
Members: `commands[]` (`command`, `scope`, `owner`, `group`, `label`, `defaults` (0..2 `KeyChord`),
`reserved`, optional `anywhere`), `sequences[]` (the fixed 004 "G then X" sequences; a plain `g` never
binds), `pedal` (`defaultPreset`, `leftKey` PageUp, `rightKey` PageDown, `presets[]`), `reservedChords[]`
(rule 4, with the reason shown to the user) and `externalChords[]` (chords another feature handles outside the
key map, e.g. 023's lyrics editor: never assignable). `shift` omitted = not checked (named keys and printable
non-letters, whose `key` already differs with Shift). Command ids are those of 014 data-model 5.2 with 012's
shipped ids for the practice tempo (`mixer.speed:-5`, `mixer.speed:5`, `mixer.transpose:-1`,
`mixer.transpose:1`, `mixer.practice.reset`, `mixer.countin.toggle`, `mixer.trainer.toggle`). Default keys
that change in 1.1.0 (CTO decision 2026-11-16): `mixer.practice.reset` `r` -> `0`, `mixer.countin.toggle`
`c` -> `Shift+K`; `r` and `c` are held for feature 015 (`mixer.record.toggle`, `mixer.compare.cycle`,
`reserved: true`). Later features add rows (016 next) and bump this contract's minor.

### 5.5 C# mapping (`src/StemStudio.Web/Interop/`, `Practice/`)

`KeyChord`, `KeyBinding`, `KeyMap` records; `ShortcutsInterop.RegisterAsync(scope, keyMap)`,
`UpdateAsync`, `CaptureChordAsync(timeout)`; `KeyMapValidator` (domain rules, unit-tested) and
`KeyMapDefaults` (loaded from `keymap-defaults.json`, the same file the TS tests read).

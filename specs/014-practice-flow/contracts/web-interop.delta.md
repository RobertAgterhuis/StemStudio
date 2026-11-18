# Contract delta: web-interop 1.1.0 (feature 014)

Status: **binding delta** (014 planning, 2026-11-16). Base: `specs/004-design-system-ux/contracts/web-interop.md`
**1.0.0**. An "other" contract; 1.1.0 is allocated to 014 in `docs/ROADMAP.md` (CTO,
2026-11-16). Default key map: metronome `K`, count-in `Shift+K` (CTO decision, DELTA R-2). Additive: the 1.0.0 call `registerShortcuts(dotnet, scope)` keeps the
004 key map. ADR-023 D5. Owner: frontend (`src/web-interop/**`).

## 1. Key map as data

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

export type ShortcutScope = 'global' | 'mixer' | 'setlist';   // 'setlist' added in 1.1.0
```

Exports (added):

| Export | Signature | Notes |
|---|---|---|
| `registerShortcuts` | `(dotnet, scope, keyMap?: KeyMap) => Disposable` | without `keyMap`: the 1.0.0 behaviour; with it: only the given bindings of that scope |
| `updateShortcuts` | `(handle: Disposable, keyMap: KeyMap) => void` | swaps the map without re-registering |
| `defaultKeyMap` | `(scope) => KeyMap` | the defaults of spec FR-020, read from the catalogue `src/StemStudio.Web/wwwroot/js/keymap-defaults.json` (architect, T006; imported at build time; the same file `KeyMapDefaults` reads in C#) |
| `captureChord` | `(dotnet, options: {timeoutMs: 1000..30000}) => Disposable` | "Learn": the next keydown anywhere (except Escape, which cancels) is reported once by `dotnet.invokeMethodAsync("OnChordCaptured", chord \| null)` and prevented |
| `isReservedChord` | `(chord) => string \| null` | the reason text when reserved (ADR-023 D5 list), else null |

## 2. Rules (in addition to 1.0.0 section 3)

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

## 3. Preferences record (schemaVersion 2)

`preferences.ts` reads schemaVersion 1 and 2 and writes 2; new field `shortcuts` (data-model
5.1): `{singleKey: boolean (true), pedalMode: boolean (false), pedalPreset: string
("play-section-start"), overrides: Record<commandId, KeyChord & {layer?}[]>}`, max 60 commands,
validated field by field (invalid entries dropped). The record limit stays 16 KB.

## 4. C# mapping (`src/StemStudio.Web/Interop/`)

`KeyChord`, `KeyBinding`, `KeyMap` records; `ShortcutsInterop.RegisterAsync(scope, keyMap)`,
`UpdateAsync`, `CaptureChordAsync(timeout)`; `KeyMapValidator` (domain rules, unit-tested) and
`KeyMapDefaults` (loaded from `keymap-defaults.json`, the same file the TS tests read).

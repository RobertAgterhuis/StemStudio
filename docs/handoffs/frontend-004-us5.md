# Hand-off: frontend / 004 US5 (T058-T061, expert speed)

## What was built

- **Key map** (`src/web-interop/src/shortcuts.ts`, design-spec.md 6) as data tables. Global: `?`/F1
  `help.open`, Mod+K `palette.open` (also from text fields), Mod+/ `shortcuts.open`, G then I/T/M
  `go.import|tracks|mixes` (1 s timeout, any other key cancels). Mixer: Home `mixer.stop`, Left/Right
  `mixer.seek:-5|5` (Shift: 1 s), `[` `]` `mixer.loop.a|b`, L `mixer.loop.toggle`, K
  `mixer.metronome.toggle`, 1-9 `mixer.channel.select:n`, M/S `mixer.channel.mute|solo`, Mod+S
  `mixer.save`, Mod+Shift+S `mixer.save-as`. Ignored in inputs (incl. range), textareas, selects,
  contenteditable, dialogs and the tour popover; single keys never fire with Ctrl/Meta/Alt; Space never
  handled; Mod = Meta on macOS user agents; a key already handled by another registration (the global
  G sequence) is skipped by the mixer map.
- **`meters.ts`**: Expert dB readouts. The module mirrors the engine's `data-peak-db` into
  `meter-db-<key>` (one decimal, `-inf` at or below -60 dBFS) with a MutationObserver started when the
  module loads; no .NET round trip, no engine change.
- **Command palette** (`Components/Shell/CommandPalette.razor`): modal dialog `command-palette`, combobox
  `command-palette-input` (`aria-activedescendant`), options `command-<id>` (`aria-selected`) grouped: Go to
  (`go-import`, `go-tracks`, `go-mixes`), Actions (page actions from `Services/PageCommands`: the track
  page offers `open-mixer`, the mixer `mixer-save`, `mixer-save-as`, `mixer-play`), Tracks
  (`track-<id>`), Mixes (`mix-<id>`), Help (`help-<topicId>`). Filter by label, arrows + Enter, Escape or
  the backdrop close it and focus returns.
- **Shortcut overlay** (`ShortcutOverlay.razor`, rows from `Services/ShortcutCatalog`): `shortcut-overlay`
  and `shortcut-overlay-close`; the mixer group appears on the mixer route. Mod+/ opens it at Expert
  only (`shell.shortcut-overlay-key`); `help-shortcuts` in the help index opens it at every level; an
  Expert "Shortcuts" button sits in the context bar (`data-help-id="shortcut-overlay"`).
  `Services/ShellOverlays` holds which overlay is open.
- **Mixer** (`Mixer.Shortcuts.cs`): every command calls the method of the matching button; seek is
  clamped to the song; channel selection by number (`data-selected="true"` on `strip-<key>`); M/S act on
  the selected strip; Mod+S opens the name dialog for an unsaved mix (the field gets focus).
  Expert: `fader-value-<key>` and `pan-value-<key>` number fields (two-way with the sliders),
  `meter-db-<key>` / `meter-db-master`, `kbd` hints in the transport dock and on the Commands button.
- **Job details** (`Components/Pages/JobDetails.razor`, Expert): disclosure `job-details` with
  `job-details-standard` (model, status, stage, progress, device, CPU fallback reason, created/updated in
  `<time>`) and `job-details-detail` (status, stage, current stem, progress, device, fallback).
- **Tour** `expert-keyboard` v1 (route `/tracks/{id}/mixer`, Expert): palette, shortcut overlay, numeric
  entry. Topic anchors `shortcut-overlay`, `mixer-numeric-entry`, `track-job-details` added.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T058 -> T059 | `813240b` (23 failing) | `1f86302` |
| T060 -> T061 | `713ad51` (44 failing .NET + `meters.test.ts`) | `2682423` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 794, Skipped: 0, Total: 794
  | StemStudio.Web | 99.27% | 92.54% | 100% |
src/web-interop: npx vitest run --coverage
  Tests 229 passed; Statements 100 % (253/253), Branches 99.37 % (160/161), Functions 100 % (97/97), Lines 100 % (243/243)
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0; web-interop lint/typecheck clean;
`run_all.py`: 688 files, 5 329 units, 38 clones, **0 errors**, 181 warnings.

## Dependencies added

None.

## Deviations

- `expert-keyboard` runs on the mixer route instead of "any screen": numeric entry exists only there, and
  the tour-target coverage test requires every target to render on the tour's route.
- `meter-db-*` text is filled by web-interop (MutationObserver on `data-peak-db`) instead of an engine
  callback; the web-interop surface of web-interop.md is unchanged (one extra export, `startMeterReadouts`,
  also started on module load).

## Open issues / requests

- **qa**: palette ids `command-open-mixer` (track page, completed track), `command-mixer-save`,
  `command-mixer-save-as`, `command-mixer-play`; the selected option carries `aria-selected="true"`.
  `G` then `M` goes to Mixes even on the mixer page (the mixer's M is not triggered).

## Next owner

frontend (polish, T063).

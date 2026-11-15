# Hand-off: frontend / 007 US2 (what I hear and selected channels)

Tasks 007:T044 (red) and T045 (green).

## What was built

- `Mixer/MixerState.Export.cs`: `ToExportTarget(track)` - the current, possibly unsaved mixer state as `ExportMixDto`
  (master, channel settings, expanded parents, detail channel settings), every visible channel in mixer order
  (standard stems, then detail stems of expanded parents), the audible ones by the gate rule (`IsAudible`), the loop
  region and the duration.
- Mixer header: `mixer-export` "Save to library" (marker `export-dialog`) opens `ExportDialog` in `data-mode="mix"`.
- `Components/Export/SourcePicker.razor`: fieldset "What to save" (marker `export-dialog`): radio `export-source-audible`
  "What I hear" (preselected, every level -> request `mode: mix`, `channels: null`, `mix` = snapshot) and, behind the
  new catalogue feature `export.selected-channels` (Advanced+), radio `export-source-selected`.
- `Components/Export/ChannelPicker.razor`: `export-channel-<key>` checkboxes (key = channel id with `-`), prefilled with
  the audible channels; the request lists exactly the ticked channels in mixer order; an empty selection disables
  `export-submit` and shows "Select at least one channel." (`export-error-hint`, extra id).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T044 / T045 | dc8f58a (7 of 7 fail) | 81ee4eb |

## Tests and coverage

```text
Passed! - Failed: 0, Passed: 957, Total: 957
| StemStudio.Web | 98.34% line | 90.7% branch | 98.76% method |
```

## Quality checks

Build 0 warnings, format clean, `run_all.py` 0 errors.

## Notes for qa

- The metronome is never part of the snapshot (FR-003). The fader % -> volume mapping is the existing linear one
  (80 % = 0.8), as qa-007-pre004 assumed.
- Known flaky (pre-existing 004 test, thread-pool starvation under parallel load, passes on rerun):
  `TrackDetailActionsTests.Retry_EndingFailedAgain_ShowsMessage_ClearedByNextRetry`.

## Next owner

frontend (007 US3), qa (007:T047).

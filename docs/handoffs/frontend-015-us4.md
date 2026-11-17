# Hand-off: frontend / 015 US4 (lane L6, T063-T064) + qa T065

## What was built
- `Recording/TakeLibrary.Compare.cs`: compare mode (Both default, kept on load, reset on unload as engine D8),
  `SetCompareAsync`, `CycleCompareAsync` (C: Both -> My take -> Original; part `none`: take on/off), `RestoreAsync` (saved
  mix: load the take with its strip settings; stale ids ignored).
- `Components/Recording/TakeCompare.razor(.cs)`: WAI-ARIA radio group `take-compare` with `take-compare-take/-original/-both`
  (`aria-checked`, roving tabindex, arrow keys move, select and focus), `take-compare-toggle` switch for part `none`;
  rendered in the take strip.
- Mixer (`Mixer.Recording.cs`, L5 hooks a4aec34e): every strip change updates `MixerState.Take` (dirty only when it really
  changed) and `ExternalSolo` (a soloed take silences the stems like a stem solo); `OnMixLoaded` loads the takes and restores
  the strip; the take strip is dimmed when another channel is soloed. The take list merges by id so the strip keeps its row.
- E2E `e2e/tests/recording-compare.spec.ts` (T065): bass take, My take -> `meter-bass` silent and `meter-take` moving, user
  mute untouched; Original the reverse; "Save as" and reopening the mix restores the take strip (`data-take-id`).

## Tests and coverage
- Web: **3293 passed, 0 failed**; 015 frontend code **line 1167/1181 = 98.8 %, branch 504/542 = 93.0 %**.
- E2E: `tsc --noEmit`, `eslint --max-warnings 0` clean; not run (lane rule).

## Quality checks
`dotnet format StemStudio.slnx --verify-no-changes` clean, 0 warnings.

## Docs impact
User manual: compare My take / Original / Both (C), take on/off for "none", take strip saved with the mix.

## Next owner
L6 continues with US5 (T068-T070).

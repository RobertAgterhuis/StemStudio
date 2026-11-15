# Hand-off: frontend / 002-us5

## What was built (T079, T080)

- **Save / save as / update** send `MixSettingsDto.Expanded` (expanded parents in display order) and
  `DetailChannels` (every detail channel of every group, at most 6 + 3 + 3 x 9 = 36 < `MaxDetailChannels` 50).
  The state for this was already in `MixerState.ToSettings` (T037). T079 verifies it end to end through the page.
- **Loading a mix** (`?mixId=` at open, or switching mixes while the mixer is open):
  - `MixerState.ApplyMix` restores expansion and detail settings. Entries for unknown parents or stems are
    ignored. Then `PushStateAsync` sends `setExpanded(parent, x)` for every parent whose engine state differs
    from the mix, **before** any `setChannel`. After that it re-applies every standard and detail channel,
    master, metronome and loop.
  - The page tracks which parents the engine has expanded (`_engineExpanded`; all collapsed after
    `loadTrack`). So opening a track without a mix sends no redundant `setExpanded(false)`, and switching from
    an expanded mix to an old mix sends `setExpanded(drums, false)`.
- **Old mixes** (no `expanded` / `detailChannels`): every group collapses, detail channels reset to defaults,
  and the 001 settings are restored exactly (SC-005).
- **Failed expand during a mix load** (`Failed to load stem "drums.kick": HTTP 404`): only that group stays
  collapsed. The error goes to `mixer-engine-error`. Transport and every other control stay usable, and the
  expand button can be retried.
- **Dirty flag**: every detail control change and every expand/collapse marks the mixer unsaved. Loading and
  saving clear it.

## Files changed

- `src/StemStudio.Web/Components/Mixer/Mixer.Detail.cs` (`PushExpansionAsync`, `TryExpandAsync`, engine expansion tracking)
- `src/StemStudio.Web/Components/Mixer/Mixer.razor.cs` (`PushStateAsync` expands first)
- `tests/StemStudio.Web.Tests/Mixer/DetailMixTests.cs` (new, 5 tests)
- `specs/002-detail-separation/tasks.md`: T079, T080 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T079 -> T080 | `f5b4674` (3 failing: load order, old-mix collapse, rejected expand on load; save and dirty already passed through T037) | `a0bb45a` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 280, Skipped: 0, Total: 280
  | StemStudio.Web | 99.87% | 94.81% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
```

`Mixer.Detail.cs` 100 % line / 100 % branch.

## Quality checks

- `dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0.
- `run_all.py`: `386 files, 2755 units, 17 clones, 1 exemptions, 0 errors, 107 warnings -> exit 0` (no new warnings).

## Dependencies added

None.

## Open issues / requests

- backend: duplicate/rename must copy `expanded` and `detailChannels` (US5 AS3). The UI only calls the
  existing endpoints.

## Next owner

frontend (polish: T084); qa (US5 E2E, D10).

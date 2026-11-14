# Hand-off: frontend / us3

## What was built

- `MetronomePanel` (`Components/Mixer/MetronomePanel.razor` + `.razor.cs`) on the mixer page has these controls:
  - an on/off toggle (`metronome-toggle`, with `aria-pressed`);
  - a click volume control (`metronome-volume`, native range 0-100, `aria-valuenow`);
  - a manual BPM field (`metronome-bpm`). It is empty while following the detected beats, shows the detected BPM
    as placeholder, and clearing it removes the override;
  - an offset field in ms (`metronome-offset`);
  - a beats per bar field (`metronome-beats-per-bar`);
  - a reset button (`metronome-reset`).
- Each accepted change updates `MixerState`, marks the mixer dirty and calls `setMetronome` at once.
- Validation lives in `MetronomeRules`. BPM must be 30-300, offset an integer from -2000 to 2000, beats per bar an
  integer from 1 to 12. Input outside these ranges is refused, shows a message in `metronome-error`, does not call
  the engine and does not mark the mixer dirty.
- Reset (`MixerState.ResetMetronome`) restores: no BPM override, offset 0 and the detected beats per bar. On/off and
  volume stay as they are (FR-014, ADR-008 #4).
- The metronome starts disabled when the grid is missing or unreliable. A hint (`metronome-hint`) explains why.
- No metronome meter is rendered (CTO note).

## Red -> green evidence

| Task pair | Red | Green |
|---|---|---|
| T100 -> T101 | c9afbc6 (does not compile: `MetronomeRules` missing) | 34ff755 |

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`:
`Passed! - Failed: 0, Passed: 155, Skipped: 0, Total: 155`; `StemStudio.Web` line 98.7 %, branch 90.54 %,
method 99.46 %. The gate passes.

## Quality checks

`dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0.

## Dependencies added

None.

## Open issues / requests

- Extra id `metronome-hint` (additive). All `e2e/pages/mixer-page.ts` metronome selectors exist. `metronome-bpm`
  and `metronome-offset` are number inputs committed on `change`, which is what `fillAndCommit` does (fill + Tab).

## Next owner

CTO review; frontend continues with US4.

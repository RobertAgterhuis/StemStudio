# Hand-off: frontend / 012 US2 (T052, T053)

Branch `012-lane-frontend`. Contracts: ui-test-ids 1.11.0 `trainer-*`, audio-engine 2.3.0 "Speed trainer" /
`PracticeStatus.trainer`, data-model 2.2 (start < target) and 3.2.

## What was built

| Pair | Red | Green |
|---|---|---|
| T052/T053 | 02440bca | 8c1bcc7c |

- `Components/Mixer/Practice/SpeedTrainerPanel.razor(.cs)` inside the practice panel, gated by the new catalogue entry
  `practice.trainer` (Advanced, topic `practice-speed-trainer`): `trainer-panel` (`role="group"`, help marker),
  `trainer-toggle` (`role="switch"`, `aria-checked`, `aria-describedby` -> the needs-loop text), `trainer-needs-loop`
  ("Set a loop (A-B) to use the speed trainer." or, for an enabled loop < 0.5 s, "The loop must be at least 0.5 s
  long."), `trainer-start/step/target/passes` (number inputs with the contract ranges), `trainer-error`
  (`role="alert"`), `trainer-status` (`aria-live="polite"`, `aria-atomic="true"`, `data-state/-pass/-speed`).
- Status texts: running "Pass 3 - 80 % (target 100 %)", target "Pass 9 - 100 % (target reached)", waiting
  "Ready - starts at 70 % (target 100 %)", off "Off". The "Pass n - " part is `aria-hidden`, so the live region
  announces speed steps only (FR-017), while the visible text and `textContent` stay as in the contract.
- `Mixer/TrainerForm.cs`: the four fields are validated together (whole numbers in range, then
  "The target must be above the start speed."); an invalid entry stays visible with its error and nothing is sent until
  it is fixed in either field. The speed control is read-only while the engine reports `running`/`target` (US1).
- `Mixer.Practice.cs`: `SetTrainerAsync`, `ToggleTrainerAsync`, `mixer.trainer.toggle` (ignored at Beginner and when
  turning on without a usable loop or with the stage unavailable).
- Help topic `practice-speed-trainer` (feature `practice.trainer`), tour `mixer-advanced` v2 with a step on
  `practice-speed-trainer`; `practice-speed` relates to it.
- Quality-gate request folded in (same commit): `Status_ShowsEveryState` uses `TheoryData<TrainerStatus,string,string>`
  (3 parameters), `InputValue.Whole` shared by `MetronomeRules` and `TrainerForm` (removed a 7-line clone),
  `ShortcutCatalog.Groups` (overlay `@code` size), `HelpRenderer.OpensInNewTab` (Rewrite CCN 7 -> 6, as before 012),
  `StartEngineAsync` 15 LOC.

## Decisions (owner unavailable)

1. **An enabled trainer can always be switched off**, also after its loop was removed or with the stage unavailable;
   the switch is disabled only for turning it on (otherwise a saved-but-inactive trainer could not be cleared).
2. `trainer-needs-loop` also shows the **0.5 s rule** for an enabled but too short loop (audio-engine.md text).
3. Pass numbers are visible but not announced (only speed changes are spoken).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2027, Skipped: 0, Total: 2027
| StemStudio.Web | 99.6% line | 93.68% branch | 99.7% method |   gate 90/90 met
```

web-interop unchanged since HO:us1 (293 passed, 99.71 / 98.19 / 100 / 99.71).

## Quality checks

`dotnet build` 0 warnings, `dotnet format --verify-no-changes` clean. `python tools/quality/run_all.py`:
`2249 files, 17691 units, 258 clones, 3 exemptions, 0 errors, 823 warnings -> exit 0`.

## Dependencies added

None.

## Docs impact

User manual (mixer): speed trainer - start, step, target, passes per step, needs an enabled loop of at least 0.5 s,
status line, pause keeps the pass, stop/new loop/changed setting restart at pass 1, shortcut T (Advanced).

## Open issues / requests

- audio-engine: the UI shows the trainer as running/at target purely from `PracticeStatus.trainer.state`; a status
  after every pass start (contract) keeps the pass number current.

## Next owner

frontend (US3: T059/T060).

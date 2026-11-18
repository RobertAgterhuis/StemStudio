# Hand-off: frontend r14 fix round 2 (UI/E2E, 014 + 015)

Branch `r14-fix-ui2` from `014-015-round` (5e5d683f). Test-first for every product bug.

## Findings and fixes

| # | Finding | Cause | Fix |
|---|---|---|---|
| 1 | recording.a11y: axe `listitem`; keyboard case found no `rec-allow-mic` | take rows (`li`) inside `ul role=radiogroup`; permission granted for the whole file | Takes panel: `fieldset` + sr-only `legend` groups the native radios, rows stay a plain `ul` (inner session list no `role=none`). Keyboard case starts without permission and grants it while "Allow microphone" is shown; reduced-motion case grants it itself |
| 2 | remap to `j` saved but not applied | `SetShortcutsAsync` used the 250 ms debounce; the page was left before the write | shortcut changes are stored at once, and before the change is rendered (rendered = stored) |
| 3 | recording-compare: unsaved indicator stayed | rerun on the same volume hit "name taken" (dialog stayed open); behind it a real bug: a mix opened by `?mixId=` restored its take before the engine had the track, so the strip stayed empty | take restore waits for the engine (`OnEngineStarted` hook); spec uses a run-unique mix name |
| 4 | practice-log scenario 2: tempo empty | spec waited on text "100", already in "Ready - starts at 80 % (target 100 %)", and read the summary after the first write. API mapping is fine | spec waits for `data-state=target` and polls until the 90 % pass is in; recorder: hold/close wait for an in-flight write and write again (a late pass was skipped) |
| 5 | setlist play: wrong track id after Ctrl+Left | assertion on stem requests (browser cache) | new `mixer-console` with `data-track-id` (ui-test-ids 1.13.0 row added in 014 delta, 014 and 015 full docs); also fixed: setlist editor saves are serialized and built from the latest setlist (note + Move down wiped the note), a live key map applies a preference change made while it registered, spec sets an entry note (the bar shows the entry note, FR-017) and waits for the stored pedal key |
| 6 | recording-adjust / denied flaky | level switch clicked before the circuit was interactive; the E2E stack's loopback input never calls getUserMedia, so "Allow" always opened it | switch level after `waitForReady` (adjust, loop); denied case blocks the microphone in the page (permission query `denied`, getUserMedia `NotAllowedError`) |

## Verification

- bUnit `StemStudio.Web.Tests` (Release, coverage): 3475 passed; line 99.43 %, branch 93.25 %.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean. e2e `tsc --noEmit` and `eslint --max-warnings 0`: clean.
- E2E own stack `e2efx2` (18980/18981, Chromium, retries 0): recording project 16 passed, 1 skipped, 2 failed
  (drums, keys: "The take is digital silence" = stereo-take silence, engine lane r14-fix-eng);
  recording.a11y 4/4; shortcuts-pedals, practice-log, setlists, main-flow, mixer, practice-levels, help 66/66;
  setlists scenario 3 8/8 with `--repeat-each`. Stack torn down (`down -v`, images removed).

No new dependencies.

# Hand-off: frontend (L5) / 016 HO:polish (T135)

Branch `016-round-lane-l5`. Not pushed, not merged. Commits of this session: `0570854b..HEAD` (T081/T082 completion
through T135). E2E authoring (T083, T092, T107, T112, T115, T122, T129, T136) moved to lane L2.

## T135

| Item | Commit | Result |
|---|---|---|
| SIG list of the orchestrator | the `refactor(web): 016 units within the SIG limits` commit | ChordPanel (DetectAsync, TryDetectAsync, SyncLaneAsync), ChordSheet.LoadSheetAsync, KaraokeBanner.SyncChordsAsync, MidiAction, Mixer.OnChordsLoaded, ScoreForm.Request, TopicParser.Parse, ChordLabels / InstrumentFamilies tables (tuple tables), InstrumentCatalogueCache.GetAsync, ChordSheetLayout.Parts, ScoreTabFields code-behind (US2), ChannelScoreView, SingingPanel: **no SIG warning left in the 016 frontend files**; behaviour unchanged (all tests green) |
| accessibility | 07b934b3 | every view in `instrument-view` (drum trainer, singing, chords, tab, part) is outside the dock, has no positive tabindex and names every control with its visible text; keyboard: Practise-as menu (APG), lane labels and look-ahead, singing toggle and slider, capo controls are native buttons/inputs; text alternatives: chord sheet (lane), `drum-bar-text` (lanes), `singing-announcer` and summary (pitch view); reduced motion reaches the chord lane, the hit lanes and the pitch view (component tests) |
| help | 07b934b3 | all 11 `instruments` topics name their limits (chords-advanced got its limits paragraph); BTC credit (Park et al. 2019, MIT) in `chords` and `chords-advanced` |

## Tests and coverage (final)

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3811/3811 | 99.06 % line, 92.43 % branch, 99.61 % method |

`dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format StemStudio.slnx --verify-no-changes`:
clean. `python tools/quality/run_all.py` (main checkout venv): PASS, 0 errors, jscpd ran (457 clones repo-wide), CSS
token check clean.

## Security self-review (whole lane diff)

Untrusted inputs and their handling: browser storage (Drumless snapshot, capo map, family, instrument, look-ahead,
tolerance) - parsed into typed records, unknown enum values ignored, numbers clamped, null entries skipped (hostile
tests); API documents (vocal pitch frames clamped to 0..12700 before the engine; chord labels built from fixed lists);
user text (chord edit uses fixed lists only). No microphone audio or score leaves the browser (FR-047). No new
dependencies. One new browser script (`js/singing-raw.js`, same origin, reads only a data attribute).

## Requests for the orchestrator (open)

1. Drumless metronome (mid-wave item 2): the restore is correct; the fake track's reliable grid starts the metronome on.
   L2's `instrument-drums.spec.ts:84` should compare with the state before Drumless (see HO:us7).
2. A close control for `instrument-view` is not in ui-test-ids 1.15.0 (views are switched with "Practise as"); add an
   id if a close button is wanted.

## Deviations

None from the contracts.

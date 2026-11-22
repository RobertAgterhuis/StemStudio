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

## Review fixes (2026-11-22)

Independent code review of lane L5; orchestrator decision: fix all, test-first (red `test(web)` then green commit per
finding). Commits `b57c9f2a..73b4fd33` plus this hand-off on `016-round-lane-l5`.

| Finding | Red | Green | Fix |
|---|---|---|---|
| H1 (FR-044) lead stays audible on collapsed vocals | b57c9f2a, 750ea77e | cbbf80a4 | While singing runs the mixer expands the vocals group (engine `setExpanded` + state) and mutes `vocals.lead`; when the engine refuses the expansion `SingingMix.MuteLead` mutes the audible parent `vocals` instead. Flags restored and the group collapsed again (only if singing expanded it) when the feedback stops. |
| H2 (FR-033) guide piece only ever unmuted; silent on collapsed drums | d8e6e3d7 | 756f3593 | `FamilyMixService.Guide` starts from `MinusOne(Drums)` and unmutes the one piece, so a second piece replaces the first. The drums group is expanded while a guide is active and collapsed again when the guide is cleared or Drumless ends. |
| M1 (FR-014, FR-075) capo display survives leaving guitar | 67ff92a9 | f87950a3 | "Practise as" another family sends `setChordDisplay(capo 0, showSounding false)` when a capo display was set. |
| M2 stale analyses hidden | 0cdfdc84 | 400425d5 | `AnalysisText.Shown` returns the document for `ready` and `stale`; chord lane, karaoke banner, chord sheet (with its editor) and drum lanes use it. The stale status line appends the backend's reason; the sheet shows the stale line as plain text (no new test id). "Detect chords" on a stale edited chart still asks before replacing the edits. |
| L1 tab seeded from the suggestion; guitar default for bass | 2d88d5ff | ab1e44d4 | `NewTab` uses `Initial(options)`; `TabChoice.HasTuning` is false when no catalogue tuning fits the profile, and then no tab options are shown or sent. |
| L2 rapid Drumless clicks | 159e7422 | 274b74f1, 91f9e991 | One Drumless or guide change at a time (`_drumBusy`); a click while a change is being sent is ignored. Tested with a delayed engine fake. |
| L5 polling after 404 | 1a9b7937 | b347ba2a | Chord panel, singing panel and drum trainer stop polling after a 404 (`AnalysisText.KeepPolling`) and keep the not-found error in their existing alert. |
| Take transpose (orchestrator item, engine 2.6.0) | a5625d53, then c2ca16dd | 36b2d116, then 73b4fd33 | `TakeScoreRequest.TransposeSemitones` (camelCase `transposeSemitones`, default 0). Orchestrator decision: "Score this take" sends the take's recorded transpose, which is 0 under the 015 upload rule (`RecordingTakeUploadDto.TransposeSemitones` must be 0); the mixer's current transpose is not used. The field stays plumbed for a future `RecordingTakeDto` transpose (code comment in `TakeScoreAction`). |

Test corrections inside the package: the two H2 mixer tests open the trainer through Drumless first (corrected in
756f3593); `SingingUnitsTests.LeadMute_MutesTheLeadVocal_AndRestoresIt` now expands the vocals group first (the old
expectation encoded the H1 bug).

### Checks after the fixes

| Check | Result |
|---|---|
| `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3839/3839; 99.1 % line, 92.37 % branch, 99.64 % method |
| `dotnet build StemStudio.slnx -c Release` | 0 warnings, 0 errors |
| `dotnet format StemStudio.slnx --verify-no-changes` | clean |
| `tools/quality/run_all.py` | PASS, 0 errors, jscpd ran (459 clones repo-wide); no finding in a changed `src/` file |

Security: no new untrusted input; the stale reason is backend text rendered as text (Blazor-encoded). No new dependencies.

### Take transpose decision

Settled by the orchestrator: takes are scored at their recorded transpose (0), see the table row above.

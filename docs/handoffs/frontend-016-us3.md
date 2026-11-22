# Hand-off: frontend (L5) / 016 HO:us3 (T090, T091)

Branch `016-round-lane-l5`. Not pushed, not merged. E2E T092 is lane L2's.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| prep | - | f90051a1 | `Scores/ChannelScoreMaker` makes a channel's sheet music item (no tablature) and waits for it; shared by `MidiAction` and the drum trainer |
| T090/T091 Drumless | 79f1dbb6 | afe6f9d7 | `drumless` toggle (`aria-pressed`, `data-help-id="drumless"`) in the mixer's section nav: minus-one drums incl. hidden detail channels (data-model 5.3), metronome on, one-bar count-in when a tempo is known (012), opens `instrument-view` (`data-view="drum-trainer"`); second click restores flags, metronome and count-in. The snapshot is stored at `stemstudio.drumless.previous.<trackId>`; on load it is kept only while every drum channel is still muted, else removed |
| T090/T091 drum trainer | 79f1dbb6 | afe6f9d7 | `DrumTrainer`: `drum-hits-status`, POST detection on the first open of status `none`, polling, `drum-hits-split-required` + `drum-hits-split-start` (StartDetail, polls until the hits come), `drum-lane-label-<piece>` in notation order (Beginner disabled; Advanced `aria-pressed` guide via `drums.guide`, disabled without a `drums.<piece>` channel), `drum-lanes` host (empty, `@key`, `role="img"`), `drum-look-ahead` 2000..8000 (Advanced, stored at `stemstudio.drums.lookAheadMs`, `setHitLaneOptions`), `drum-bar-text` (sr-only list "Bar 1: Kick on 1 (loud), Hi-hat on 1 and ..."; no grid: 2 s windows), limits, `drum-score` (014 `ScorePanel` with new `Profile` parameter preferring the drum item) or `drum-score-generate` |
| T090/T091 guide | 79f1dbb6 | afe6f9d7 | `Drumless.Guide`: re-mutes the drum family, then unmutes `drums.<piece>`; a guide click without Drumless turns Drumless on first |
| help | 79f1dbb6 | afe6f9d7 | topic `drum-trainer` (anchors `drum-trainer`, `drum-lanes`, `drumless`; split first, guide, look-ahead, notation, limits); catalogue `drums.trainer` (B), `drums.guide` (A); help tests now also cover `tablature`, `midi-download` |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3690/3690 | 99.25 % line, 93 % branch, 99.62 % method |

Build 0 warnings, `dotnet format --verify-no-changes` clean, `run_all.py` PASS (0 errors, jscpd ran: 450 clones; one
duplication error ChordPanel/DrumTrainer fixed before commit). Remaining warning in new code:
`DrumTrainer.TryGenerateAsync` 19 lines (limit 15) - accepted (three catch arms), will be reviewed at HO:polish.
WCAG: every trainer control is named with its visible text (`InstrumentViewsAccessibility`), the look-ahead input sits in
its label, the toggle and the trainer are outside `.ss-mixer-dock`, the lanes have a text alternative.

Security: the stored Drumless snapshot is browser data: null entries and null stems are ignored (`FamilyMixService.Restore`
hardened, hostile test), count-in bars are clamped by `SetCountInBars`, a snapshot without flags is ignored; the
look-ahead from storage or the slider is clamped to 2000..8000.

## Decisions

1. Drumless follows data-model 5.3 (others unmuted and unsoloed) rather than AS3's "other channels keep their settings";
   the restore brings back every captured flag, so a second click undoes both.
2. Until US6 builds the view host, Drumless opens the `instrument-view` region; it stays open after the restore (no close
   control in ui-test-ids 1.15.0; US6's "Practise as" switches views).
3. One engine score follower: while the drum trainer is open the mixer's own score panel is not rendered (its toggle
   shows `aria-expanded="false"`); the trainer's `drum-score` hosts the `ScorePanel` with the drum item.
4. Lane labels exist at every level; at Beginner they are disabled buttons without `aria-pressed`.

## Deviations

None.

## Requests for the orchestrator

- A close control for `instrument-view` is not in ui-test-ids 1.15.0; please confirm US6 "Practise as" (or none) is the
  way out, or add an id (e.g. `instrument-view-close`).

## Open items

Next: T105/T106 (US4 singing feedback). L1 note recorded for T120/T127: `data-current-key` is '' without a key; the
`.chord-sounding` child is styled by us with tokens.

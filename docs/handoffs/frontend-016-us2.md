# Hand-off: frontend (L5) / 016 HO:us2 (T081, T082)

Branch `016-round-lane-l5`. Not pushed, not merged. E2E T083 moved to lane L2 (orchestrator decision 2026-11-22).

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T081/T082 tablature options | 7fbd54da | 6595d6b6, 79491fa8, 0570854b | `ScoreTabFields` in the sheet music dialog: `score-tab-layout` (`notation-and-tab`/`tab-only`/`none`, `none` sends `tablature: null`), `score-tab-tuning` (Beginner: standard + drop D of the profile, all catalogue tunings from Advanced via `tab.tunings`; labels from the instrument catalogue), `score-tab-capo` (guitar only, Advanced, 0..12, prefilled from capo mode); `TabChoice` follows the chosen profile |
| T081/T082 MIDI action | 7fbd54da | 0570854b, fda1eb8a | `MidiAction` on every track-page stem/detail row and in every mixer strip menu (`midi-<key>`): reuses a ready score item of the channel and downloads its MIDI file; otherwise creates one (no tablature), shows `midi-progress-<key>` (`aria-valuenow`) and `aria-busy`, then downloads; `midi-error-<key>` with the readable reason |
| T081/T082 library tab line | 9f04410f | 751a6225 | `asset-score-tab-<id>`: "Tab: Drop D, capo 2 (3 notes moved by an octave)"; every non-zero limit (octave moves, chord notes left out, wide stretches) spelled out with singular/plural; no line without tablature facts |
| T081/T082 help | 7fbd54da | 0570854b | topics `tablature` (anchors `score-tablature`, `score-tab-tuning`) and `midi-download` |
| SIG | - | fda1eb8a | LF line endings of four files written with CRLF; `MidiAction.RunAsync`/`WaitAsync` split below 15 lines / complexity 5; `ScoreTabFields` `@code` moved to a code-behind |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3642/3642 | 99.26 % line, 93.1 % branch, 99.55 % method |

Build `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format StemStudio.slnx --verify-no-changes`
clean. `run_all.py` PASS (0 errors; jscpd ran: 446 clones; no new warnings in the files of this checkpoint's
production code). WCAG in bUnit: tab fields and MIDI action names contain their visible text, targets >= 24 px, the
MIDI action lives in the strip menu outside `.ss-mixer-dock`.

Security: no new untrusted input. The tab line is built from enums and ints; the MIDI download URL from the asset id
and a fixed route.

## Decisions

1. The library tab line uses the tuning labels of data-model 4.1 (static table `TabFactsText`), so the library needs no
   catalogue call; the dialog keeps the catalogue labels.
2. The MIDI action never asks for tablature (a MIDI file has none); for guitar/bass sources it sends `tablature: null`.

## Deviations

None.

## Open items

Next: T090/T091 (US3 drum trainer, Drumless).

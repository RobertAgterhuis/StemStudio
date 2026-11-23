# Hand-off: frontend (L5) / 016 HO:us5 (T110, T111)

Branch `016-round-lane-l5`. Not pushed, not merged. E2E T112 is lane L2's.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T110/T111 dialog | 3695f7e5 | 4560718c | `ScorePartFields`: `score-part-instrument` (all levels, catalogue instruments with `concert` first as "Concert pitch (no transposition)", values = catalogue ids) and `score-part-clef` (Advanced via `parts.clef`; only when the backend allows a clef and not for keys/piano); no part fields for drums or when `options.parts` is absent/unavailable. Concert pitch + automatic clef sends `part: null` (008 output unchanged) |
| T110/T111 library | 3695f7e5 | same | `asset-score-part-<id>`: "Alto Saxophone in E-flat, treble clef (2 notes out of range)" |
| T110/T111 profiles | 3695f7e5 | same | `strings`, `brass`, `woodwinds` named in the profile select and given library quality notes |
| help | 3695f7e5 | same | topic `transposed-parts` (anchors `score-part`, `score-part-clef`); catalogue `parts.instrument` (B), `parts.clef` (A) |
| SIG | - | same | `ScoreForm.Request` split (key/part helpers), part construction moved to `NewPart` |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3748/3748 | 99.13 % line, 92.74 % branch, 99.63 % method |

Build 0 warnings, format clean, `run_all.py` PASS (0 errors, jscpd ran: 453 clones; no warnings left in this checkpoint's
production code). WCAG: both selects have visible labels; the dialog's existing focus handling is unchanged.
Security: option values are mapped back through the catalogue list only (no free text reaches the request).

## Decisions

1. The clef select is hidden (not disabled) for keys/piano, as the backend refuses a clef there (422 `clef-not-allowed`).

## Deviations

None.

## Open items

Next: T113/T114 (US6 "Practise as"), including the orchestral view's `score-part-instrument` (orchestrator decision).

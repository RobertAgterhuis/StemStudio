# Hand-off: frontend (L5) / 016 HO:us4 (T105, T106)

Branch `016-round-lane-l5`. Not pushed, not merged. E2E T107 is lane L2's.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T105/T106 singing panel | 2f03ef6e | 4950e6bf | `SingingPanel` in `instrument-view` (`data-view="singing"`, opened with the internal `Mixer.OpenView`, used by US6): `singing-reference-status` (+ `singing-reference-analyse`, `singing-reference-estimate`; analysed automatically on the first open when the estimate is <= 60 s, data-model 7.3), `singing-toggle` (`aria-pressed`, disabled with `aria-describedby` while the reference is not ready), `singing-state` (`data-state`), `pitch-view` host (empty, `role="img"`), `singing-headphones-hint`, `singing-error` (engine reasons; "No input device is open" explained) with a button opening the 015 recording panel, `singing-tolerance` 10..200 step 5 (Advanced; defaults 100/50/25 by level; stored and clamped), `singing-latency` (from the 015 `LatencyInUse`, `data-latency-source` = method), `singing-raw` (Expert), `singing-announcer` (`aria-live="polite"`, "Line 3: 87 points"), `singing-summary` (`data-average`, `data-units`) |
| units | 2f03ef6e | 4950e6bf | `SingingUnits.From`: synced lyric lines (+ offset; untimed/empty lines left out, sorted) -> 014 sections -> 4-bar blocks of the grid -> 10 s blocks; the reference contour clamps values outside 0..12700 to unvoiced |
| mixer | 2f03ef6e | 4950e6bf | lead vocal (`vocals.lead`, else `vocals`) muted while running and restored (`SingingMix`); `karaoke-score` next to the banner in the dock (`data-unit-index`, `data-score`, text "Line 1: 92") |
| take scoring (P2) | 2f03ef6e | 4950e6bf | `TakeScoreAction` in every ready 015 take row: `take-score-<takeId>` -> `scoreTake` with the take URL, start sample, units and tolerance -> `take-score-result-<takeId>` (`data-average`); the mixer cascades a `SingingSource` |
| raw readout | - | 4950e6bf | `wwwroot/js/singing-raw.js` mirrors the host's `data-singer-cents` into `[data-singing-raw-for]` ("220.0 Hz, A3 +3 cents") in the browser - no per-frame interop |
| help | 2f03ef6e | 4950e6bf | topics `singing-feedback` (anchors `singing`, `pitch-view`; privacy, limits), `singing-tolerance-latency` (A/E); catalogue `singing.feedback` (B), `singing.tolerance` (A), `singing.raw` (E) |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3736/3736 | 99.16 % line, 92.77 % branch, 99.62 % method |

Build 0 warnings, format clean, `run_all.py` PASS (0 errors, jscpd ran: 451 clones; a DrumTrainer/SingingPanel duplicate
fixed). Open warning: lizard reports `SingingPanel::Create` CC 6 (a parser artefact of the `string.Create` property
expressions; reviewed at HO:polish). The JS mirror has no unit test (no JS test runner in `StemStudio.Web`); the E2E raw
check is L2's if wanted.

WCAG: every control named with its visible text (`InstrumentViewsAccessibility`), the slider inside its label, scores
announced politely, the panel outside the dock, the banner score in the dock does not take focus.

Security / privacy: no microphone audio or score leaves the browser (FR-047): the panel only calls `GET/POST vocal-pitch`
and `GET lyrics`; the reference frames from the API are clamped before they reach the engine (a hostile or broken
document cannot make `startSingingFeedback` throw); the stored tolerance is clamped.

## Decisions

1. `RoundTripMs`/`LatencyMethod` are sent only for a calibrated latency (`loopback`/`manual`); with an estimate they are
   left out so the engine uses the open input's estimate (data-model 3.3).
2. The "link to the 015 input settings" is a button that opens the recording panel (the input selector lives there).
3. Unit names in texts: "Line" (lyrics), "Section", "Block" (4 bars), "Part" (10 s).

## Deviations

None.

## Open items

Next: T110/T111 (US5 parts). Orchestrator decision noted: `score-part-instrument` also in the orchestral instrument view
with `mixer-key-written` (T120/T121).

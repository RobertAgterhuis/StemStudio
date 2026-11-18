# CTO hand-off: round 014 + 015 integration (2026-11-18)

Branch `014-015-round`, from `93dd2a13` (all six lanes, both checkpoint fix rounds and current `main` merged). Not pushed,
not merged to `main` (the orchestrator does that). Lane plan: `docs/handoffs/cto-r14-lane-plan.md`.

## What was integrated

014 practice flow (song sections, score follows playback, setlists with PDF, keyboard and foot pedals, practice log) and
015 play-along recording (input, calibration, loop takes, takes panel, compare, timing adjustment, export with the take).
Contracts: backend-api 1.14.0 (contains 014's 1.13.0), worker-api 1.8.0, audio-engine 2.5.0 (contains 014's 2.4.0),
web-interop 1.1.0, ui-test-ids 1.14.0, render-vectors 1.1.0. Migrations of the round are consistent with the model
snapshot (`dotnet ef migrations has-pending-model-changes`: no changes).

## Integration items (checkpoint notes, all worked through)

| Item | Result |
|---|---|
| audio-engine.md clarifications | 2.4.0/2.5.0 (full + delta): `setLoop` with an identical region is not a loop change; `captureWav(<takeId of the loaded take>)` re-encodes from `max(0, startSample + adjustmentSamples)`; `setTakeAdjustment` is absolute (relative to the loaded `startSample`), `-8820..8820`, no-op without a take. No version bump (text follows approved behaviour) |
| `document-failed` | `PracticeFlowProblemTypes.DocumentFailed` in `StemStudio.Contracts`; the Api's temporary constant is gone |
| 008 data-model 2.4 | bar one = first downbeat >= 0 of the (shifted) grid (`BeatGridTempo.bar_one_downbeat`); 014 data-model 2.5 step 3 follows it; 014 research R-11 + data-model 2.5 step 4: Leipzig `@font-face` only on pages with Leipzig text |
| THIRD-PARTY-LICENSES | ReportLab 5.1.0 (BSD), DejaVu fonts (`fonts-dejavu-core`, Bitstream Vera licence + public domain) |
| Shortcut overview ids | Fully applied: only the generic `shortcut-row-mixer-record-toggle` / `shortcut-row-mixer-compare-cycle` (code, bUnit, ui-test-ids 1.14.0); no `shortcut-record` / `shortcut-compare` left outside `bin/` |
| `takes-error`, `mixer-console` | Already in ui-test-ids 1.14.0 / 1.13.0 (fix rounds) |
| Size cap, `document-failed` contract response | Done in `r14-fix-be` (8 MiB per score SVG page, 16 MiB per setlist PDF) |
| Decisions | 47 rows added to `docs/decisions/CTO-DECISIONS-2026-11-15.md` (one per hand-off decision group, existing rows kept) |
| Api branch coverage | 90.84 % (gate 90 %), no extra tests needed |
| score-follow at 50 % | Spec bug, not engine: the helper "paused" by clicking `transport-play` (not a toggle), so playback ran on; and `transport-position` floors to 0.1 s. Probed at 50 % / 120 %: `data-position-ms` equals the transport before and after the pause |

## Fixed in this integration (test first)

| Commit | What |
|---|---|
| `326d4f41` | **Product (also on `main`)**: the library folder view stopped polling at its first 404. "Open in library" after an export links to the song folder of the preview placement, which exists only once the item is filed, so `export.spec.ts:67` (SC-004) waited 60 s for a row (main CI run 44212916383, and here). bUnit test first; the view now asks again every 2 s after a 404 and shows the folder when it appears |
| `96a01a97` | E2E: the 014 tours (`practice-section`, `hands-free`) in the stored tour versions (self-test red; tours were offered on every page); section starts within half of the worker's 0.1 s rounding step of a downbeat; range score before its first bar = `data-bar-index="-1"` (the old `/\d/` matched it) |
| `0707013d` | E2E: score follow pauses with `transport-pause` and allows the floored position text; Ctrl+/ at Advanced opens the overlay (014 FR-024 changed 004's Expert-only rule) |
| `30d7e326` | E2E: axe scans wait for running CSS transitions (Firefox measured the level switch of the setlists page mid-transition; 2 of 2 runs red, 3 of 3 green after) |
| (see log) | E2E no-detect pass: re-imports the click track when the default pass already detected its sections; `sections-edit` disposes its context also when skipped |
| `1280e4bd` | 014 T103 (was never assigned to a lane): `e2e/visual/practice-flow.spec.ts`, `e2e/a11y/practice-flow.a11y.spec.ts` (axe both themes, keyboard walks of the new pages, FR-031 "not colour alone") |

## Verification

Per-suite table: `docs/STATUS.md`, section "Feature 014 + 015". Stack: own compose project `e2er14`
(`docker-compose.yml` + `docker-compose.e2e.yml` + an untracked override with images `e2er14/*:local`, ports
19080/19081), fake separator, retries 0, heavy suites one at a time; the owner's `stemstudio` stack untouched.

## Open items

See STATUS "Open" row and the final report to the orchestrator.

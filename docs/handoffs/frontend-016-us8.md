# Hand-off: frontend (L5) / 016 HO:us8 (T127, T128)

Branch `016-round-lane-l5`. Not pushed, not merged. E2E T129 is lane L2's.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T127/T128 capo mode | 9c1193e4 (+ literal fix) | 6bfbdd22 | `CapoMode`: `capo-mode` (`aria-pressed`), `capo-select` 0..12 (`aria-valuenow`, clamped), `capo-badge` "Capo 3" when > 0, `capo-suggestions` (plain `<ul>`) with `capo-suggestion-<n>` buttons "Capo 3 - C shapes, 100 % open" (`data-capo`, one click applies), `capo-shortcut` "Capo 2: play the original shapes" when the backend returns one, `capo-show-sounding` (Advanced, `capo.sounding`); suggestions reload when the transpose changes; 409 chords-not-ready shown as status text |
| storage | 9c1193e4 | 6bfbdd22 | per-track map at `stemstudio.chords.capo` (`{on, capo, showSounding}`), restored and clamped on load |
| mixer | 9c1193e4 | 6bfbdd22 | only for the guitar family ("Practise as Guitar"): `setChordDisplay({capo, showSounding})` re-spells lane and banner; no capo controls for bass and keys |
| track page | 9c1193e4 | 6bfbdd22 | offered when the remembered family is guitar; the chord sheet (and its ChordPro link) uses the capo; the sheet music dialog prefills `score-tab-capo` with it (FR-078) |
| CSS | - | 6bfbdd22 | `.chord-sounding` (engine child, sounding symbol only, per L1) gets brackets via `::before/::after`, muted token colour; 24 px minimum targets for the new small buttons (tokens only) |
| help | 9c1193e4 | 6bfbdd22 | topic `capo-mode` (anchor `capo-mode`); catalogue `capo.mode` (B), `capo.sounding` (A) |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3805/3805 | 99.04 % line, 92.4 % branch, 99.61 % method |

Build 0 warnings, format clean, `run_all.py` PASS (0 errors, jscpd ran: 456 clones, CSS token check clean).
Note: the red commit 9c1193e4 did not compile (raw JSON literals); fixed in the next commit - process slip, the branch builds
from then on.

Security: the stored capo map is browser data; the capo is clamped to 0..12 and only the current track's entry is used.

## Decisions

1. Capo mode lives next to the chord panel in the mixer (guitar family of this visit) and above the lyrics card on the
   track page (remembered guitar family); "the chord view's menu" entry of AS1 is the Practise-as menu.

## Deviations

None.

## Open items

Next: T135 polish (SIG list, accessibility pass, help texts final).

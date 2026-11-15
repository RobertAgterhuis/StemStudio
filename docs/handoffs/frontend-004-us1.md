# Hand-off: frontend / 004 US1 (T025-T036, design system on every screen)

## What was built

- **Design components** (`Components/Design`): `StemColor` (+ `Known`, `Parent`, `Pattern`), `SplitState`,
  `SplitStates` (track and detail status -> idle/running/done), `StemTab` (coloured tape with the name in
  condensed type, pattern per detail index), `StemRibbon` (one grey strand -> six stem lines, missing stems
  grey, one strand while processing), `SplitProgress` (signature visual, `--split` custom property drawn
  by a `scaleX` clip transform, reveal once on done, static under reduced motion), `EmptyState`,
  `ErrorState` (existing error ids stay on the text element with `role=alert`), `Skeleton`.
- **Import**: title + lede, URL field with the primary Import button, a drop zone where the native file
  input covers the zone (click and drop work without script), device line, legal notice (always), the
  full limitation text in its own card, designed error state.
- **Tracks**: `TrackTable` with ribbons, pill status chips, "Open mixer" (Beginner: "Practise", same id),
  Expert compact rows with BPM and model columns, `tracks-search` / `tracks-sort` behind
  `LevelGate tracks.search` (client-side, `Services/TrackListQuery`), empty/error/loading states,
  stacked rows under 768 px.
- **Track page**: header (display title, artist, ribbon), `TrackProcessing` card (status chip, stage,
  `split-progress` next to `track-progress`, device, actions), `TrackSummary` (facts row, stem tabs on the
  `track-stem-*` items, primary Open mixer), `DetailSection` with `detail-split-progress`, detail stems as
  patterned tabs in the parent colour, limitation note at every level; `DeviceBadge` gets its
  memory/developer_board icon through CSS so its text stays exact.
- **Mixer console**: header (title, mix name chip, unsaved chip, Save as / Save, `mix-error`), panel row
  (Loop, Click), desk with the strips region (scrolls inside itself) and the pinned Master (ink tab),
  sticky transport dock with the tabular counter. Strips: stem tab, meter + vertical fader, pan, M/S
  (`ss-strip-toggle`, >= 24 px), expand. Detail groups under a bracket in the parent colour with a
  pattern per detail strip. Meters: three static level bands (-6 / -1 dBFS on the engine's -60..0 scale)
  revealed by a cover scaled with `scaleY(calc(1 - var(--meter-level, 0)))`; no inline style, no
  transition. `html:has(.ss-transport-dock)` sets `scroll-padding-bottom` (WCAG 2.4.11).
- **Mixes and dialogs**: ribbons from the track list (joined client-side), Load primary, actions inline
  at every level with `aria-label` "<action> <mix name>", Expert icon-only buttons with a visible CSS
  tooltip, `mixes-search` at Advanced+, designed states. `SaveMixDialog`: focus moves to the name field,
  Escape cancels, focus returns to the opener (`IWebInterop.CaptureFocusAsync`). `ConfirmDialog`
  unchanged structurally (Cancel first, destructive last).
- **Bug found while rendering the app**: `ToastService` was not registered (the shell crashed the
  circuit). Fixed with a host test that resolves every feature 004 service (`e718e87` red, `b049287` green).

## Design review screenshots (T038 input)

`docs/design-review/004/{import,track,mixer,tracks,mixes}-{dark,light}.png` (1440 x 1000, full page,
level Advanced, reduced motion, real data from the running stack's API on :8081 read-only; the app ran
from a scratch publish on :5180, the `stemstudio` compose project was not touched). The mixer shots show
the drums group expanded. Per the CTO's instruction the owner approval (T038) is pre-approved under the
owner's mandate; work continued without waiting. Change requests, if any, go into polish (T063).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T025 -> T026 | `387757e` (32 failing) | `83d40d7` |
| T027 -> T028 | `7b16248` (4 failing; the others guard existing behaviour) | `65c37f8` |
| T029 -> T030 | `3729f21` (14 failing) | `3301ce0` |
| T031 -> T032 | `30b0ded` (17 failing) | `c5a2ac8`, styles `071babd` |
| T033 -> T034 | `4893136` (7 failing) | `559a161` |
| T035 -> T036 | `83925ee` (10 failing) | `e757db9` |
| host services | `e718e87` (1 failing) | `b049287` |

Screenshot review fixes: `8f72e34`. Note: in T034 the meter assertion was changed from
`scaleY(var(--meter-level, 0))` to the cover form `scaleY(calc(1 - var(--meter-level, 0)))` so the level
bands stay at fixed dB positions (a scaled fill would squash them).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 606, Skipped: 0, Total: 606
  | StemStudio.Web | 99.13% | 91.83% | 99.61% |   (line | branch | method; gate 90 %: pass)
src/web-interop: 146 Vitest tests, 100 % statements/branches/functions/lines
```

## Quality checks

- `dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0.
- web-interop lint/typecheck clean.
- `run_all.py`: 637 files, 4 908 units, 29 clones, **0 errors**, 164 warnings.
- No literal colour outside `tokens.css` (the meter bands and every component use tokens; inline styles
  only set custom properties to `var(--ss-stem-*)`).

## Dependencies added

None.

## Open issues / requests

- **qa**: `tracks-search`/`tracks-sort`/`mixes-search` render only when the list is not empty. Mix
  library buttons now carry `aria-label` "<Action> <mix name>" (visible text kept at Beginner/Advanced).
  Default track order is newest first (client-side), the API order before.
- Branch coverage is 91.8 %; polish adds tests for the remaining branches.

## Next owner

CTO (T038, pre-approved) / frontend (US2, T039 onwards).

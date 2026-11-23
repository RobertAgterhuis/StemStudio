# Hand-off: frontend / 020 HO:us1 (T046, T047)

Branch `020-lane-frontend`. Not pushed, not merged.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T046/T047 | see `git log` "failing tests for the stem quality card" | "stem quality card on the track page" (+ SIG/test split commit) | `Api/IStemStudioApiClient.Insights.cs` + implementation: all six 1.19.0 operations (`getQualityReport`, `requestQualityReport`, `getHandoutOptions`, `createHandout`, `getPracticeNotes`, `setPracticeNote` with 204 -> null); `Insights/QualityText` (words, icons, labels, Advanced/Expert lines), `QualityLayout` (detail stems grouped under the parent, orphans last, status rules); `Components/Insights/QualityPanel` (+ `QualityRow`, `QualityRating`, `TipList`) on the track page after the detail card; `QualityBadge` on standard and detail mixer strips (`Mixer.Quality.cs`, `StemGroup` code-behind) |

Consumer-driven fakes: `tests/StemStudio.Web.Tests/Fakes/InsightsData.cs` holds the `ReadyQualityReport` and
`BassHandoutOptions` examples of backend-api 1.19.0 verbatim and derives the other states from them; `PageTestContext`
answers `getQualityReport` with status `none` and empty practice notes by default.

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3934/3934 | 99.02 % line, 92.35 % branch (gate 90/90) |

New files 100 % line; branch below 100 only where US2 adds the actions (`TipList`, `TipText`). `dotnet build` 0 warnings;
`dotnet format StemStudio.slnx --verify-no-changes` clean; `tools/quality/run_all.py` PASS (0 errors; jscpd: one 14-line
clone warning, the shared client-test fixture header, same as the 016 client tests).

## Accessibility (WCAG 2.2 AA, design-time ARIA check)

- `quality-panel` is a `section` labelled by its `h2` "Stem quality"; a polite `role=status` line announces queued,
  running, ready and failed.
- Ratings: `role="img"` with the full label ("Other: fair, some sound of guitar is in this stem"), which contains the
  visible word (2.5.3); icon `aria-hidden`; shape differs per rating, colour only repeats it (1.4.1, R20-1).
- Rows and tips are plain `ul`/`li` with buttons, no `listbox`/`option` (LESSONS 023 nested-interactive).
- The badge is a link named "Quality of Piano: Probably not in this song. Open the stem quality report"; at least
  `--ss-target-min` (24 px) high, visible focus outline (2.5.8, 2.4.7). The card lives in the scrolling track page.
- `insights.css` uses tokens only (tested: no hex/rgb/hsl).

## Decisions

1. The outdated notice reads "Made with an older version, or the stems changed since. Check again to update the
   ratings." The DTO has one `outdated` flag, so the two causes of ui-test-ids are named together.
2. The check button stays enabled after a failed load (the user can retry); disabled only while queued, running or
   while the request is on its way.
3. The panel reloads and polls when the page's track poll shows `Track.quality` queued or running (automatic report
   after a separation or a detail split).
4. The mixer reads the report once, only when `Track.quality` exists; a failed read shows no badges.
5. `LibraryCareAccessibilityTests` asserted that `care` is the last style sheet; it now asserts the sheet is listed,
   since `insights` follows it.

## Deviations

None.

## Help / docs

Topic anchors added: `stem-quality` (`quality-panel`, `quality-badge-*`), `stem-quality-tips` (`quality-tip-*`),
`stem-quality-measures` (`quality-bleed-*`, `quality-artefacts-*`, `quality-index-*`), `stem-quality-reliability`
(`quality-analyzer-version`). For the CTO (user manual, `docs/public/src/**`): a "Stem quality" section for the track
page and the mixer badge, from the `stem-quality*` topics.

## Open items

None for this checkpoint. Next: US2 (T053/T054).

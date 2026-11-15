# Hand-off: frontend / 004 US3 (T048-T051, in-app help)

## What was built

- **HelpContext** (`Help/HelpContext.cs`, scoped): open/close, topic or index, Back history, focus
  captured on open and restored on close; resolution focused element -> `data-help-id` -> topic, else the
  screen topic (route templates such as `/tracks/{id}/mixer` matched against the current path); while open
  it follows navigation to the new screen's topic. It subscribes to navigation on first open (inside a
  circuit).
- **Help panel** (`Components/Help/{HelpPanel,HelpSearchBox,HelpTopicView,HelpIndex}.razor`): `aside
  help-panel` (`data-topic-id`), `help-close`, `help-back`, search with `help-result-<id>`, the topic
  (`help-topic`, `-title`, `-body`), level note + `help-level-switch` for features above or below the
  current level, `help-related-<id>`, the index per area and the tours offered at the level
  (`help-tour-start-<id>`). Docked at >= 1280 px (the shell reflows via `ss-shell-help-open`), overlay
  from 768 px, full sheet below. Escape closes; focus moves to the panel heading on every topic change.
- **Opening**: the help button and the rail Help item open the **screen** topic (qa alignment: the button's
  own marker is ignored); `?` and F1 (`web-interop/src/shortcuts.ts`, global scope, ignored in text
  fields, selects, editable elements, dialogs and the tour popover) open the topic of the focused control.
  Help links in topics render as `/help/<id>`; the shell's `NavigationLock` cancels that navigation and
  shows the topic (the mixer's unsaved-changes lock ignores help links).
- **Hints**: `HelpHint` (`help-hint-<topicId>`, next to labels), `HelpTip` (tooltip `help-tooltip-<topicId>`
  with the summary and `help-learn-more` on hover and focus, Escape hides it; wraps pan and M/S in every
  strip), `InlineHint` (`hint-<topicId>`, Beginner only, dismissible, stored in preferences).
- **Content** (T051): 56 topics (design-spec.md 7 inventory plus `help`, "Getting help"), all areas, the
  glossary with 15 terms; markers on every screen (import, tracks, track, detail, mixer, mixes, shell).
  `track-job-details` and `mixer-numeric-entry` have no anchors yet (their controls come in US5), the
  `shortcut-overlay` anchor follows in US5 as well.
- **Coverage tests** (`Help/HelpCoverageTests.cs`, help-content.md 4): marker -> topic, literal anchors
  present in markup, one screen topic per `@page`, `route:` links, catalogue <-> topic features, safe
  rendering of every topic, the design-spec inventory, the 20 reference queries of quickstart V-H3, and
  tour targets rendered on their route per level (a fact over all tours; tours arrive in US4).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T048 -> T049 | `40041f4` (30 failing .NET + the Vitest help-key file) | `9787b1a` |
| T050 -> T051 | `c4fa46d` (25 of 28 failing) | `ba35d14` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 733, Skipped: 0, Total: 733
  | StemStudio.Web | 98.82% | 91.97% | 99.66% |
src/web-interop: npx vitest run --coverage
  Tests 157 passed; Statements 100 % (130/130), Branches 100 % (79/79), Functions 100 % (49/49), Lines 100 % (125/125)
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0; web-interop lint/typecheck clean;
`run_all.py`: 665 files, 5 088 units, 31 clones, **0 errors**, 169 warnings.

## Dependencies added

None.

## Deviations

- Extra topic `help` ("Getting help") carries the `help-button` anchor (the import tour's last step).
- Search tie-breaking: a prefix match scores half of a whole-word match (research.md R-06 says "prefix
  matches count"); with it all 20 reference queries return the expected topic first.

## Open issues / requests

- **qa**: tooltips render on hover and on keyboard focus of the wrapper around the control and contain
  `help-learn-more`; markers `mute-*`/`solo-*` -> `mixer-mute-solo`, `pan-*` -> `mixer-pan`,
  `fader-*` -> `mixer-volume`, `meter-*` -> `mixer-meters`, `expand-*` -> `mixer-detail-groups` (only when a
  group exists). The help panel heading receives focus on open.

## Next owner

frontend (US4, T053 onwards).

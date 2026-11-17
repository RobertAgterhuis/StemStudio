# Hand-off: frontend-023 / a11y list (QA r22 finding on `lyric-assist.a11y.spec.ts`)

Branch `r22-qa023` (from e5da0ed3). Fixes the 12 of 16 a11y failures (axe `nested-interactive` and
`label-content-name-mismatch` on every `lyric-suggestion-<id>`).

## Decision

| Date | Decision | Reason | Taken by |
|---|---|---|---|
| 2026-11-17 | `lyric-assist-list` is a plain list (`<ul role="list">`), not a listbox; each `lyric-suggestion-<id>` is `<li role="listitem">` without `aria-label`/`aria-labelledby`; the entry buttons stay inside the entry and are reached with Tab | ARIA forbids interactive content inside `role="option"` (WCAG 4.1.2, axe `nested-interactive`); the `aria-labelledby` name of the option did not contain the visible text of the entry (WCAG 2.5.3, axe `label-content-name-mismatch`) | orchestrator |

## What changed
- `Components/Lyrics/Assist/SuggestionList.razor(.cs)`: `<ul role="list" aria-label="Suggestions">`, no `tabindex`,
  no `aria-activedescendant`, no focus handler; the listbox id `ListId` is gone. Keys stay on the `<ul>` (they bubble
  from the focused entry or button): F8 / Shift+F8, Alt+Enter, Alt+Backspace, Alt+I, Escape, unchanged in meaning; they
  act on the entry that holds the focus. New: `FocusEntry` (focusin on an entry or its buttons makes it the focused
  suggestion, without moving the text cursor or announcing) and `FollowAsync` (after a decision by key or button the
  focus moves to the entry that takes the decided one's place, so it is not lost to `<body>`; none when the list is
  empty or the decision failed). It only focuses (injected `ILyricEditorInterop.FocusAsync`) and leaves the text
  selection alone: a reveal there made the next Check check only the selected lines (found by the `strict` E2E).
- `SuggestionEntry.razor`: `<li role="listitem" tabindex="-1">` (F8 focuses it), `aria-current="true"` on the focused
  entry, id `SuggestionEntry.EntryId(id)` = `lyric-entry-<id>` (was `OptionId` / `lyric-opt-<id>`). All `data-*`
  attributes and every child test id unchanged. `OnFocus` and `OnDecided` parameters.
- `SuggestionActions.razor`: buttons and the scope select no longer `tabindex="-1"`; `OnDecided` after each decision.
- `Songs/LyricsEditorPage.razor.cs`: reveal focuses the entry (`EntryId`) instead of the listbox.
- `wwwroot/css/components/lyrics.css`: focus ring on `.ss-assist-entry:focus-visible` (was the list).
- JS interop: no change (`lyric-editor.js` never read `aria-activedescendant`; `focusById` now gets the entry id).
- Highlight linking (`lyric-highlight-<id>` `is-active`) follows `Board.Active` as before.
- Contract: `specs/023-lyric-writing-assistant/contracts/ui-test-ids.delta.md` and the merged
  `ui-test-ids.md`: rows `lyric-assist-list` and `lyric-suggestion-<suggestionId>`, F8 row and a note under the
  keyboard table. No other `specs/` or `docs/` file carried the listbox rows (history in `tasks.md` T035 and
  `frontend-023-final.md` left as written).
- E2E: `e2e/a11y/lyric-assist.a11y.spec.ts` expects `role="list"`; `e2e/tests/lyric-assist.spec.ts` keyboard test finds
  the focused entry by `[role="listitem"][aria-current="true"]` (was `[role="option"]:focus, [aria-selected="true"]`);
  doc comment in `e2e/pages/lyric-assist-pane.ts`.
  The keyboard test now waits until the entry holds the browser focus (`toBeFocused`), after the accept for the focus
  follow, and after F8 until `aria-current` moved (it read the old entry before).

## Tests
- bUnit (`LyricAssistKeyboardTests`, `LyricAssistCheckTests`, `LyricAssistAccessibilityTests`): list/listitem roles,
  no listbox/option/activedescendant, no widget role around any button, buttons and select in the tab order, no
  `aria-label`/`aria-labelledby` inside the list except the scope select, `aria-current` on exactly the focused entry,
  focusin selects without moving the cursor, Alt+Enter on a button accepts its entry, focus follows after accept and
  reject, a failed decision keeps the entry, decision keys without a focused entry do nothing.
- `StemStudio.Web.Tests`: see the lane report for the counts and coverage.

## Dependencies
None added.

## E2E (own stack `e2e23`, ports 18380/18381, fake lyric backend, retries 0)
- `a11y/lyric-assist.a11y.spec.ts`: 16 passed (was 4 of 16).
- `scripts/lyric-e2e.sh fake`: 28 passed.

## Open
- `strict shows the licences as ideas ...` (`e2e/tests/lyric-assist.spec.ts`) failed once in 6 runs with
  `--repeat-each 3` at its first assertion (licences after the strict check), before any list interaction; QA saw it
  fail before this change too (round 22 run 4). Flaky, not caused by the list change; qa to look at the
  sensitivity -> check ordering.
- NVDA pass (SC-008, manual, `docs/testing/023-lyric-assist.md` section 6) should be repeated once on the new list.

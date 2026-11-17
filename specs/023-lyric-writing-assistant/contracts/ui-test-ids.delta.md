# Contract delta: UI test ids 1.23.0 (feature 023, additive)

Adds the lyric assistant to the 010 lyrics editor (route `/lyrics/{id}`, a new page; the `/lyrics` list keeps its edit dialog and links each sheet title to the page; ids `lyrics-title-input`,
`lyrics-language-select`, `lyrics-text-input`, `lyrics-save`, `lyrics-error` unchanged) and one
row to the 010 take details. Merged into the full `ui-test-ids.md` 1.23.0 at Phase 0 (T003) on top
of the newest full document (1.22.0 of 022 when merged first, else 1.21.0). No existing id changes.

Placeholders: `<suggestionId>`, `<markId>`, `<wordId>` are GUIDs in canonical lower-case `D`
format; `<n>` is a 1-based line number; `<category>` is a `SuggestionCategory` value; `<level>` is
the 004 journey level at which the element is rendered (`B` Beginner, `A` Advanced, `E` Expert;
"all" = every level).

| Screen | `data-testid` | Meaning / attributes | Level |
|---|---|---|---|
| Lyrics editor | `lyric-assist-pane` | the assistant pane (region, `aria-label="Lyric assistant"`); attribute `data-state` = `idle` \| `running` \| `done` \| `unavailable` | all |
| Lyrics editor | `lyric-assist-toggle` | shows/hides the pane (does not start anything) | all |
| Lyrics editor | `lyric-assist-check` | "Check" (spelling, grammar, flow); saves unsaved changes first | all |
| Lyrics editor | `lyric-assist-devices` | "Suggest devices" (devices and poetic licence) | all |
| Lyrics editor | `lyric-assist-alternatives` | "Alternatives" for the line at the cursor / selection; count select `lyric-assist-count` (A, E) | all |
| Lyrics editor | `lyric-assist-continue` | "Continue" after the block at the cursor | all |
| Lyrics editor | `lyric-assist-titles` | "Title ideas" | A, E |
| Lyrics editor | `lyric-assist-instruction` | optional instruction input (<= 200 chars) for devices and writing help | all |
| Lyrics editor | `lyric-assist-cancel` | cancel a running assist (only while running) | all |
| Lyrics editor | `lyric-assist-progress` | progress bar, `aria-valuenow` 0-100 (blocks done / total) | all |
| Lyrics editor | `lyric-assist-layers` | the layer/model line ("Spelling: OpenTaal dictionary - grammar and flow: qwen3.5:9b, local"); attributes `data-backend`, `data-model`, `data-fallback` `true`/`false` | A, E |
| Lyrics editor | `lyric-assist-unavailable` | reason and fix of an unavailable layer or language (contains the 011 fix text) | all |
| Lyrics editor | `lyric-assist-quality-note` | standing quality note (SC-001), when the status reports one | all |
| Lyrics editor | `lyric-assist-language-guess` | "Looks like Dutch - set language" (language `unknown`); button `lyric-assist-language-apply` | all |
| Lyrics editor | `lyric-assist-not-own` | refusal text when the sheet matches library lyrics (track title) | all |
| Lyrics editor | `lyric-assist-own-work-open` | "This is my own text" on the refusal (only when the problem says `overridable`) | all |
| Lyrics editor | `lyric-assist-imitation-refusal` | refusal of an instruction that asks to imitate an artist (`artist-imitation`; text names the rule, no assist is started) | all |
| Lyrics editor | `lyric-assist-refusal` | any other refusal of a create request (revision conflict, language, model unavailable, notice, assist active); attribute `data-code` = the problem `code` | all |
| Lyrics editor | `lyric-assist-error` | an unexpected error of the pane (network, server); `role="alert"` | all |
| Lyrics editor | `lyric-own-work-dialog`, `lyric-own-work-statement`, `lyric-own-work-track`, `lyric-own-work-name`, `lyric-own-work-check`, `lyric-own-work-confirm`, `lyric-own-work-cancel` | confirmation dialog (statement `own-work.v1`, matched track, typed name, tick box; confirm disabled until both set) | all |
| Lyrics editor | `lyric-own-work-badge` | "Own work confirmed by <name> on <date> (matches <track>)"; attributes `data-track-id`, `data-confirmed-at` | all |
| Lyrics editor | `lyric-own-work-revoke` | revoke (opens the existing confirm dialog `confirm-dialog-ok` / `confirm-dialog-cancel`) | all |
| Lyrics editor | `lyric-assist-withheld` | "n suggestions withheld (too close to existing lyrics)" | all |
| Lyrics editor | `lyric-assist-claude-notice`, `lyric-assist-claude-notice-ack`, `lyric-assist-claude-notice-cancel` | the one-time Claude notice dialog | all |
| Lyrics editor | `lyric-assist-sensitivity` | radio group: `lyric-assist-sensitivity-loose`, `-balanced`, `-strict` | A, E |
| Lyrics editor | `lyric-assist-filter-<category>` | category filter toggle (attribute `aria-pressed`); `lyric-assist-filter-all` | A, E |
| Lyrics editor | `lyric-assist-accept-all` | "Accept all" for the current filter (count in the label) | all |
| Lyrics editor | `lyric-assist-undo` | undo the newest accept (enabled when `assist.canUndo`) | all |
| Lyrics editor | `lyric-assist-list` | the suggestion list: a plain list (`<ul role="list">`, no `aria-activedescendant`, not a tab stop itself); rendered only while there are open suggestions. Not a listbox: entries hold buttons (WCAG 4.1.2, axe `nested-interactive`; orchestrator decision 2026-11-17) | all |
| Lyrics editor | `lyric-suggestion-<suggestionId>` | one entry (`<li role="listitem">`, `tabindex="-1"` so F8 can focus it, `aria-current="true"` on the focused entry, no `aria-label` other than its visible text); its buttons are in the tab order (Tab); attributes `data-category`, `data-type`, `data-severity`, `data-status`, `data-source`, `data-line` | all |
| Lyrics editor | `lyric-suggestion-original-<suggestionId>`, `lyric-suggestion-proposal-<suggestionId>`, `lyric-suggestion-explanation-<suggestionId>` | texts of the entry | all |
| Lyrics editor | `lyric-suggestion-syllables-<suggestionId>` | "11 -> 9 syllables" (when present) | all |
| Lyrics editor | `lyric-suggestion-accept-<suggestionId>`, `lyric-suggestion-reject-<suggestionId>` | decide one | all |
| Lyrics editor | `lyric-suggestion-intentional-<suggestionId>` | "Mark as intentional" (corrections only); scope menu `lyric-suggestion-intentional-scope-<suggestionId>` (word / phrase / line; A, E) | all |
| Lyrics editor | `lyric-suggestion-add-word-<suggestionId>` | "Add to my words" (spelling only) | A, E |
| Lyrics editor | `lyric-suggestion-source-<suggestionId>` | source layer and model of the entry | E |
| Lyrics editor | `lyric-highlight-<suggestionId>` | inline highlight over the text (underline style per category, not colour only); attribute `data-category` | all |
| Lyrics editor | `lyric-line-provenance-<n>` | marker on a line produced by an accepted suggestion (`data-provenance="accepted-suggestion"`) | all |
| Lyrics editor | `lyric-assist-live` | polite live region (`aria-live="polite"`) for announcements | all |
| Lyrics editor | `lyric-syllable-toggle` | shows/hides the syllable gutter | A, E |
| Lyrics editor | `lyric-syllables-<n>` | syllable count of line `n`; attribute `data-deviates` `true`/`false`, `data-target` | A, E |
| Lyrics editor | `lyric-rhythm-target` | rhythm target form: `lyric-rhythm-bpm`, `lyric-rhythm-beats`, `lyric-rhythm-bars`, `lyric-rhythm-spb`, `lyric-rhythm-save`, `lyric-rhythm-clear` | A, E |
| Lyrics editor | `lyric-density` | words per second at the target duration (warning style above 4.0) | A, E |
| Lyrics editor | `lyric-rhymes-open` | opens the rhyme finder for the word at the cursor | all |
| Lyrics editor | `lyric-rhymes-panel`, `lyric-rhymes-input`, `lyric-rhymes-result-<word>`, `lyric-rhymes-insert-<word>` | the rhyme finder (results with `data-kind` `perfect`/`near`, `data-syllables`) | all |
| Lyrics editor | `lyric-marks-open`, `lyric-marks-list`, `lyric-mark-<markId>`, `lyric-mark-delete-<markId>`, `lyric-mark-selection` | intentional marks list; "Mark selection as intentional" | A, E |
| Lyrics editor | `lyric-words-open`, `lyric-words-list`, `lyric-word-<wordId>`, `lyric-word-delete-<wordId>` | personal word list of the sheet's language | A, E |
| Lyrics editor | `lyric-assist-log` | assist log (blocks, timings, discarded counts, token usage) | E |
| Lyrics editor | `lyric-revisions-open`, `lyric-revision-<revision>`, `lyric-revision-restore-<revision>` | revision list | E |
| Take details (010) | `take-lyrics-assisted` | "Lyrics: n lines AI-assisted" (only when n > 0) | all |
| Agent panel (011) | `agent-approval-lyrics-diff` | inside `agent-approval-card` for `apply_lyric_suggestions`: one row per change, `agent-approval-lyrics-diff-<suggestionId>` with before/after | all |

## Keyboard (binding for qa and frontend, FR-028)

| Key | Where | Action |
|---|---|---|
| F8 / Shift+F8 | editor or list | next / previous open suggestion (moves the text cursor to its highlight and focuses the list entry; the decision keys act on the entry that holds the focus, also when a button of it is focused) |
| Alt+Enter | focused suggestion | accept |
| Alt+Backspace | focused suggestion | reject |
| Alt+I | focused correction | mark as intentional (default scope: word for spelling, phrase otherwise) |
| Escape | list | back to the text at the suggestion's position |
| Ctrl+Alt+C | editor | Check |

The list is not a listbox (orchestrator decision 2026-11-17, WCAG 4.1.2 / axe `nested-interactive`):
Tab reaches every button of every entry; after a decision (key or button) the focus moves to the
entry that takes the decided one's place.

Announcements in `lyric-assist-live`: check started ("Checking 40 lines"), finished ("Check
finished: 7 suggestions - 2 spelling, 1 grammar, 3 flow, 1 device"), focused suggestion ("Line 4,
spelling error: wiet, proposed weet. <explanation>"), accept/reject/undo results.

## Help topics (ids = `data-help-id`, 004 framework)

`lyric-assist` (what it does, on demand, own lyrics only), `lyric-assist-licence` (poetic licence,
sensitivity, intentional marks, my words), `lyric-assist-devices` (device types), `lyric-assist-rhythm`
(syllables, rhythm target, density), `lyric-assist-privacy` (local model, Claude notice, what is
sent), `lyric-assist-limits` (model quality per language, verbatim check limits).

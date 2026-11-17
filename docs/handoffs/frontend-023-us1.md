# Hand-off: frontend-023 / checkpoint US1 (+ shell, US2/US3 UI, 022 T081/T082)

Interim checkpoint; the final hand-off is `frontend-023-final.md` (coverage, quality checks, all decisions).

## What was built
- 022 T081/T082: layout UI tools `open_library_folder` (folder id pattern and query 1..100 re-checked, escaped) and
  `open_lyrics_editor` (`?assist=`) in `NavigationUiTools`; `AgentPageContexts` maps `/lyrics/{id}` to `lyrics` + `lyricsId`
  (`/library/{folderId}` was already `library`).
- 023 T022/T023: typed client for all 1.23.0 operations incl. `GET /api/lyric-assist/settings` (CTO decision, afa126df) and the
  SSE stream; new page `/lyrics/{id}` (`Components/Songs/LyricsEditorPage`) with the 010 field ids and the assistant pane.
- T033-T036 (US1), T042/T043 (US2), T051/T052 (US3): check, streaming, highlights, decisions, filters, sensitivity, marks, words,
  undo, keyboard (F8, Shift+F8, Alt+Enter, Alt+Backspace, Alt+I, Escape, Ctrl+Alt+C), live region, devices, rhythm target,
  syllable gutter, density, alternatives, continue, titles, rhyme finder, withheld counter, refusals.

## Tests and coverage
`dotnet test tests/StemStudio.Web.Tests` (filter Songs, Help, FeatureCatalog): 534 passed, 0 failed. Full suite and coverage
numbers follow in the final hand-off.

## Decisions (interim, confirmed by the CTO)
- Help topics live in area `mixes` (`Help/topics/mixes/lyric-assist*.md`): the help-content contract allows no `lyric-assist` area.
- New page `/lyrics/{id}`; the `/lyrics` list keeps its Edit dialog; the title links to the page.
- Imitation refusal gets `lyric-assist-imitation-refusal`.

## Open issues / requests
- ui-test-ids 1.23.0 edits requested (final list in the final hand-off): route `/lyrics/{id}`, `lyric-assist-imitation-refusal`,
  `lyric-assist-refusal` (other refusals, `data-code`), `lyric-assist-error`.

## Next owner
frontend-023 (US4-US6, polish), then CTO.

# Hand-off: frontend-023 / final (023 T022-T077 frontend rows, 022 T081/T082)

Branch `r22-lane-fe023` (from 4486ddb7, merged `022-023-round` at afa126df for `GET /api/lyric-assist/settings`).
Interim checkpoint: `frontend-023-us1.md`.

## What was built
- **Agent (022 T081/T082, T021 page-context part; 023 T058/T059)**: layout UI tools `open_library_folder`
  (`/library/{folderId}` or `/library?q=`; folder id `^(ar|al|so)[0-9a-f]{16}$` and query 1..100 re-checked, exactly one,
  escaped; otherwise `{opened:false, reason}`) and `open_lyrics_editor` (`/lyrics/{id}`, `?assist=` opens the pane) in
  `NavigationUiTools` (NavigationManager, so every NavigationLock guard applies). `AgentPageContexts`: `/lyrics/{id}` ->
  `lyrics` + `lyricsId`; `/library/{folderId}` stays `library`. Approval card: `agent-approval-lyrics-diff` with rows
  `agent-approval-lyrics-diff-<id>` built from `GET /api/lyrics/{id}/suggestions?status=open`, never from the model's
  arguments (ids only; unknown ids counted); reason text for `lyrics-edit`.
- **Client** `Api/{IStemStudioApiClient,StemStudioApiClient}.LyricAssist.cs`: all 1.23.0 operations used by the UI incl.
  `getLyricAssistSettings` and the SSE stream (`LyricAssistEvent`, stops after the terminal event). `rhythmTarget` is always
  sent (sensitivity changes resend the current target).
- **Page** `/lyrics/{id}` (`Components/Songs/LyricsEditorPage`): 010 ids `lyrics-title-input`, `lyrics-language-select`,
  `lyrics-text-input`, `lyrics-save` (with `expectedRevision`), `lyrics-error`; unsaved-changes guard; highlight layer behind
  the text area (`lyric-highlight-*`, underline style per category, drawn only while the original text is still at the
  anchor); provenance markers; syllable gutter. List page: sheet title links to the editor; Edit dialog unchanged.
- **Pane** `Components/Lyrics/Assist/**`: session (`LyricAssistSession`: explicit start only, save first, Claude notice
  before anything is sent, refusals, stream + snapshot reconcile, decisions, settings, own work, language guess, lists),
  toolbar, decisions (sensitivity, filters, accept all, undo), listbox with keys, tools (rhymes, rhythm/density, marks,
  words, usage, log, revisions), Claude notice and own-work dialogs, `wwwroot/js/lyric-editor.js` (selection; only stops
  the browser defaults of the assistant keys).
- **Take details** (`take-lyrics-assisted`), **feature catalogue** (11 entries `lyrics.assist-*`), **help** (T072, 6 topics).

## Files changed
src: `Agent/{AgentPageContexts,NavigationUiTools,EditorRoutes,LyricsDiffArguments,ApprovalText}.cs`,
`Api/{IStemStudioApiClient,StemStudioApiClient}.LyricAssist.cs`, `Components/Agent/{ApprovalCard,LyricsApprovalDiff}.razor`,
`Components/Generation/TakeDetails.razor`, `Components/Lyrics/Assist/**` (incl. `LyricAssistUiRegistration.cs`),
`Components/Songs/{LyricsEditorPage.razor(.cs),LyricsListPage.razor}`, `Experience/FeatureCatalog.cs`,
`Help/topics/mixes/lyric-assist*.md` (6), `wwwroot/css/components/lyrics.css`, `wwwroot/js/lyric-editor.js`.
tests: `Agent/{EditorUiToolsTests,AgentPageContextEditorTests}.cs`, `Components/Agent/LyricsApprovalDiffTests.cs`,
`Experience/FeatureCatalogTests.cs`, `Fakes/LyricAssistData.cs`, `Generation/TakeDetailsLyricsTests.cs`,
`Help/LyricAssistHelpTests.cs`, `Songs/LyricAssist*Tests.cs`, `Songs/LyricEditor*.cs`, `Songs/LyricOwnWorkTests.cs`.
Task rows ticked: 023 T022, T023, T033-T036, T042, T043, T051, T052, T058, T059, T063, T064, T072, T076, T077; 022 T081, T082.

## Tests and coverage
- `dotnet test tests/StemStudio.Web.Tests -v q`: **2629 passed, 0 failed** (Web suite before the lane: 2504).
- Coverage (coverlet cobertura, lane files: `Components/Lyrics/Assist/**`, editor page, lyric client, agent tools/diff,
  take details, approval card/text, list page, feature catalogue): **lines 98.60 % (1548/1570), branches 93.59 % (891/952)**.
  Whole `StemStudio.Web`: lines 99.33 %, branches 94.23 %.
- Fakes are consumer-driven: `Fakes/LyricAssistData.cs` reads `tests/StemStudio.Api.Tests/Contract/LyricAssistSamples.json`
  (the client deserialises all three samples) and builds the other DTOs with the contract ids and shapes.

## Quality checks
- `dotnet build src/StemStudio.Web`: 0 warnings, 0 errors (TreatWarningsAsErrors). `dotnet format --verify-no-changes`: clean.
- `sig_check.py` on the lane files: **0 errors**, 21 warnings: 1 production (`LyricAssistSession.SaveIfDirtyAsync` 17 NLOC,
  soft limit 15, hard 30), 20 test units 16-27 NLOC (test limit 30, one at 34 NLOC: `MarksWordsAndOverride_UseTheirRoutes`).
- `duplication.py` (jscpd) on the lane's production folders: 0 clones. `css_tokens_check.py`: 0 errors.
- WCAG 2.2 AA in bUnit (`LyricAssistAccessibilityTests`, keyboard tests): names contain the visible text, labelled fields,
  `role=listbox/option` + `aria-activedescendant`, polite live region with the contract texts, modal dialogs labelled and
  described, target size (`--ss-target-min` on pane buttons, chips, radios), focus not obscured (`scroll-padding-bottom` with
  `--ss-dock-inset`), highlights by underline style (wavy/double/dotted/dashed/solid), not colour only.
- Security self-review (model output and agent arguments are untrusted): every sheet/model text is rendered as text (test
  with `<img onerror>` payload); no `MarkupString`; agent UI-tool arguments re-validated before navigating; the approval diff
  takes texts only from stored suggestions; own-work override has no agent control; SSE/JSON errors fall back to the snapshot.

## Dependencies added
None.

## Docs impact
User manual chapter "Lyric assistant": the editor page (`Lyrics` -> sheet title -> **Assistant**), Check / devices /
alternatives / continue / title ideas, suggestions and highlights, accept / reject / mark as intentional / my words / undo,
sensitivity and filters (Advanced), syllables, rhythm target and density (Advanced), rhyme finder, keys (Ctrl+Alt+C, F8,
Shift+F8, Alt+Enter, Alt+Backspace, Alt+I, Escape), Claude notice and usage, unavailable model, own lyrics only and the
own-work confirmation (US6), Expert assist log and revisions; take details "Lyrics: n lines AI-assisted"; agent approval
diff. Source texts: the six help topics.

## Decisions (CTO-confirmed during the wave)
1. Help topics in area `mixes` (`Help/topics/mixes/lyric-assist*.md`): the help-content contract has no `lyric-assist` area.
   Screen topic of `/lyrics/{id}` is `lyric-assist` (route front matter); help search finds all six (tests).
2. New page `/lyrics/{id}` (route parameter `Id` so the help route reads `/lyrics/{id}`); the list keeps its Edit dialog.
3. `GET /api/lyric-assist/settings` used for the notice and statement texts (contract fix afa126df); the PUT is never used to read.
4. Imitation refusal: `lyric-assist-imitation-refusal`.
Lane decisions: the pane is closed by default (toggle or `?assist=` opens it); per-entry buttons are `tabindex=-1` inside the
option (reached by mouse and by the contract keys) to keep one tab stop for the listbox; "Add to my words" also rejects the
suggestion (closes it); after every decision the client refreshes `GET .../suggestions` (server statuses incl. superseded);
a save conflict while text is unsaved does not reload (no data loss), a conflict on accept reloads sheet and suggestions;
feature ids are `lyrics.assist-*` (catalogue id pattern forbids a hyphen in the first segment).

## Open issues / requests
- **ui-test-ids 1.23.0 edits (CTO)**: add route `/lyrics/{id}` (Lyrics editor page, 010 ids reused) and the ids
  `lyric-assist-imitation-refusal`, `lyric-assist-refusal` (other refusals, attribute `data-code`) and `lyric-assist-error`.
  Note that `lyric-assist-list` exists only while there are open suggestions.
- **App.razor** (not lane-owned) needs no change: lane styles are appended to the existing `lyrics.css`.
- **qa**: check `/lyrics/{id}` at 320 px width, 720 px height and with the help panel open (focus not obscured); axe on the
  pane (listbox with buttons inside options: verify `nested-interactive`; buttons are `tabindex=-1`); NVDA pass (SC-008);
  Ctrl+Alt+C on AltGr layouts (the JS stops the default only inside the editor).
- Text-area/overlay alignment relies on identical font metrics; check visually on Chromium and Firefox.

## Next owner
CTO (integration, ui-test-ids edit, CTO-DECISIONS rows), qa (E2E of US1-US6 against these ids).

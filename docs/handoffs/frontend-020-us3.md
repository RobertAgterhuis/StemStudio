# Hand-off: frontend / 020 HO:us3 (T076-T079)

Branch `020-lane-frontend`. Not pushed, not merged.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T076/T077 | e569f280 | 5b918a20 | `Components/Handouts/HandoutMenu` (menu button `handout-menu`, items `handout-family-<family>` in data-model 6.1 order, `handout-customise` at Advanced+; one click posts `{source, family}` only; `handout-saved` status with `handout-saved-open` = `LibraryLinks.Item`; `handout-missing` lists the default parts that are missing or partial, from `getHandoutOptions`; `handout-error`), `PracticeNotesEditor` (`practice-notes` region, `practice-notes-family`, `practice-notes-text` max 4,000 with counter, `practice-notes-save`, `practice-notes-saved` polite status; one draft per family; empty text removes) on the track page ("Lesson handouts" card after the quality card) |
| T078/T079 | e569f280 | 5b918a20 | `HandoutDialog` + `HandoutParts` (`handout-dialog` modal with focus trap; `handout-family`, `handout-transposition` only when the options offer transpositions (melodic), `handout-paper`, `handout-ingredient-<code>` with `data-status`, disabled unless available, `handout-ingredient-action-<code>`, `handout-notation-<assetId>`, `handout-estimate`, `handout-notice`, `handout-submit`/`-cancel`/`-error`); library `HandoutAssetPanel` under the item row (`asset-handout-<id>` with `data-family`, `asset-preview-<id>` "Handout preview", `asset-download-pdf-<id>`, `asset-handout-omitted-<id>`, `asset-handout-warnings-<id>` at Advanced+, `asset-handout-again-<id>` posts the recipe and reloads); chip `library-filter-handout` (every level) |

Helpers: `Handouts/HandoutText` (labels, estimate, "Not included", summary), `HandoutChoices` (dialog state -> request),
`IngredientLinks` (Q3 targets), `HandoutRecipes` ("Make again"), `Services/MenuButtonState` + `MenuKeys` (APG menu
button), `FocusOnce`, `AssetFilter.ToggleKind`. `PdfPreview` got an optional `Label` (default unchanged).

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 4039/4039 | 99.1 % line, 92.52 % branch (gate 90/90) |

`dotnet build` 0 warnings; format clean; `run_all.py` PASS (0 errors; 020 warnings: one 7-line `using` header clone
between two test files).

## Accessibility (WCAG 2.2 AA, design-time ARIA check)

- Menu button pattern (WAI-ARIA APG): `button[aria-haspopup=menu][aria-expanded][aria-controls]`, `ul[role=menu]`
  labelled "Lesson handout"/"Setlist handout", `li[role=none] > button[role=menuitem]` with roving tabindex; ArrowDown
  / ArrowUp on the button open at the first / last item; arrows wrap, Home, End; Escape closes and returns the focus;
  Tab closes. No interactive content inside a non-interactive role (no nested-interactive).
- Saved status `role=status` (inline, not a 4 s toast, so the "Open in library" link stays reachable and is never under
  the fixed bars, 2.2.1 / 2.4.11); errors `role=alert`.
- Dialog: `role=dialog`, `aria-modal`, labelled, focus moves in and back (ModalDialog), focus trap, Escape closes;
  fieldsets "Parts" / "Sheet music" with legends; every select/textarea has a `label for`.
- Practice notes: `@bind:get/@bind:set` with `oninput` (LESSONS 022), counter linked with `aria-describedby`, polite
  live status.
- The test helper `InstrumentViewsAccessibility` now also accepts `label[for]` labels.

## Decisions

1. The "toast with open action" is the inline status `handout-saved` with the `handout-saved-open` link (same as 022's
   `export-saved`); the app's 4-second toast has no action and would hide the link.
2. `handout-missing` is shown at every level after a one-click handout (ui-test-ids says levels "all"); it lists the
   default parts with status `missing` or with a create action (partial, e.g. "Sheet music: Bass score; tablature not
   generated yet"). A failing options read hides the list, not the saved status.
3. The dialog sends explicit `include` and `notationAssetIds` (the ticked ones in options order); one click sends
   neither (null = defaults). The paper follows the default only for the first options; the transposition starts at C.
4. Create links (Q3): score/tab/drum notation/transposed part -> the sheet music dialog of the family's stem (keys ->
   piano, melodic and overview -> other); lyrics/chords -> `#lyrics-heading`; split drums -> `#detail-heading`;
   sections -> the mixer; write notes -> the notes editor with the family selected. Setlist dialogs show no links.
5. Warnings of a handout item are gated with `handout.customise` (Advanced+, the contract levels of
   `asset-handout-warnings-*`); no new catalogue entry.
6. The quality card also reloads when the track's detail phase status changes, so tips follow a detail split without a
   new analysis (qa note, TEST-STRATEGY 23.4).

## Security self-review

Untrusted inputs: note text and server texts are rendered through Razor (encoded); the note length is capped by
`maxlength` and the backend; mixer query values are only compared with existing channel names; link URLs are built
from GUIDs and `Uri.EscapeDataString`; the PDF preview URL comes from `PublicApiBaseUrl` + the asset id. No HTML is
injected (`MarkupString` not used).

## Help / docs

Topic anchors: `handouts` (`handout-menu`, `asset-handout-*`, `library-filter-handout`), `handout-families`
(`handout-dialog`, `handout-family-*`, `handout-ingredient-*`), `handout-transposition`, `handout-licence`
(`handout-notice`), `practice-notes`. For the CTO (user manual): "Lesson handouts" and "Practice notes" on the track
page, handout items in the library.

## Deviations

None.

## Open items

None for this checkpoint. Next: US4 (T085/T086).

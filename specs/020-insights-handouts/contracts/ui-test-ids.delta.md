# Contract delta: UI test ids for feature 020 (draft, additive)

Status: **draft delta** (020 planning, 2026-11-16). Base: the full `ui-test-ids.md` **1.18.0** of
feature 019. In 020 Phase 0 (T003) the architect merges this delta unchanged into
`specs/020-insights-handouts/contracts/ui-test-ids.md` **1.19.0**. Nothing below renames, removes
or changes an existing id; every base rule applies (`getByTestId` only, level visibility,
placement inside 004's layout). Ids marked **(G2)** exist only if gate G2 passes.

Binding for the **frontend** agent once merged; **qa** authors the 020 specs against these names.

## Changelog line (for the merged document)

> 1.19.0 (additive, feature 020) - stem quality panel and badges with tips and actions; lesson
> handout menu, dialog and library items; setlist handout; practice notes; high-quality vocal
> re-separation (gated). All 1.18.0 ids are unchanged.

## Ids

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Track page | `quality-panel` | region "Stem quality" (`aria-labelledby` its heading); attribute `data-status` = `none`/`queued`/`running`/`ready`/`failed` | all |
| Track page | `quality-disclaimer` | the estimate disclaimer text (always visible when ready) | all |
| Track page | `quality-check` | button "Check stem quality" / "Check again"; disabled while queued/running | all |
| Track page | `quality-progress` | progress bar while queued/running (`aria-valuenow` 0-100) | all |
| Track page | `quality-outdated` | notice "Made with an older version" / "The stems changed since" | all |
| Track page | `quality-error` | failure text | all |
| Track page | `quality-row-<channel>` | one row per stem, `<channel>` = ChannelId with `.` replaced by `-` (`drums-kick`); attribute `data-rating` = `good`/`fair`/`poor`/`not-present`/`unknown` | all |
| Track page | `quality-rating-<channel>` | rating word + icon (never colour alone), `aria-label` e.g. "Vocals: fair, some sound of drums is in this stem" | all |
| Track page | `quality-previous-<channel>` | "was Fair" (exists only when `previousRating` differs) | all |
| Track page | `quality-reason-<channel>` | reason text | all |
| Track page | `quality-bleed-<channel>` | bleed level and main source ("Bleed: medium, mostly drums") | Advanced, Expert |
| Track page | `quality-artefacts-<channel>` | artefact level and kinds | Advanced, Expert |
| Track page | `quality-index-<channel>` | numeric indices, reliability labels per dimension (`data-bleed-reliability`, `data-artefact-reliability`) | Expert |
| Track page | `quality-analyzer-version` | analyzer version and measured-at | Expert |
| Track page | `quality-tip-<channel>-<code>` | one tip (list item) | all |
| Track page | `quality-tip-action-<channel>-<code>` | the tip's button (exists only when the tip has an action): Split further / Open detail stem / Mute / Re-separate | all |
| Mixer | `quality-badge-<channel>` | small rating chip on the channel strip (icon + word); opens the track page quality row | all |
| Track page | `handout-menu` | "Lesson handout" menu button (`aria-haspopup="menu"`) | all |
| Track page | `handout-family-<family>` | menu item per family (`overview`, `vocals`, `guitar`, `bass`, `keys`, `drums`, `melodic`); one click creates the handout with defaults | all |
| Track page | `handout-customise` | menu item "Customise..." opening the dialog | Advanced, Expert |
| Handout dialog | `handout-dialog` | dialog (`role="dialog"`, labelled) | Advanced, Expert |
| Handout dialog | `handout-family` | family selector | Advanced, Expert |
| Handout dialog | `handout-transposition` | C / B-flat / E-flat / F (exists only for `melodic`) | Advanced, Expert |
| Handout dialog | `handout-paper` | A4 / Letter | Advanced, Expert |
| Handout dialog | `handout-ingredient-<code>` | checkbox per ingredient; attribute `data-status` = `available`/`missing`/`not-applicable`; disabled when not available | Advanced, Expert |
| Handout dialog | `handout-ingredient-action-<code>` | link to create a missing ingredient ("Make tablature") | Advanced, Expert |
| Handout dialog | `handout-notation-<assetId>` | checkbox per notation item | Advanced, Expert |
| Handout dialog | `handout-estimate` | estimated time text | Advanced, Expert |
| Handout dialog | `handout-notice` | the fixed notice line | Advanced, Expert |
| Handout dialog | `handout-submit`, `handout-cancel`, `handout-error` | actions and error text | Advanced, Expert |
| Track page / setlist | `handout-missing` | Beginner: inline list of missing ingredients under the menu after a one-click handout ("Not included: tablature") | all |
| Track page / setlist | `handout-saved`, `handout-saved-open` | toast after creation and its "Open in library" action | all |
| Track page | `practice-notes` | region "Practice notes" | all |
| Track page | `practice-notes-family` | family selector of the notes editor | all |
| Track page | `practice-notes-text` | multi-line input (max 4,000, counter) | all |
| Track page | `practice-notes-save`, `practice-notes-saved` | save button and saved status (live region) | all |
| Setlist page | `setlist-handout-menu` | "Setlist handout" menu button | all |
| Setlist page | `setlist-handout-family-<family>` | menu item per family | all |
| Setlist page | `setlist-handout-customise` | opens `handout-dialog` for the setlist | Advanced, Expert |
| Library | `asset-handout-<assetId>` | row of a handout item; attribute `data-family` | all |
| Library | `asset-preview-<assetId>` | existing preview id, PDF viewer for handout items | all |
| Library | `asset-download-pdf-<assetId>` | existing id, reused for handouts | all |
| Library | `asset-handout-omitted-<assetId>` | "Not included: ..." text | all |
| Library | `asset-handout-warnings-<assetId>` | worker warnings | Advanced, Expert |
| Library | `asset-handout-again-<assetId>` | "Make again" (posts the recipe) | all |
| Library | filter chip `library-filter-handout` | kind filter "Handouts" | all |
| Re-separation dialog **(G2)** | `reseparate-dialog` | confirmation dialog | all |
| Re-separation dialog **(G2)** | `reseparate-estimate`, `reseparate-consequences`, `reseparate-discard-detail` (checkbox, exists only with detail stems), `reseparate-confirm`, `reseparate-cancel` | | all |
| Track page **(G2)** | `reseparation-progress`, `reseparation-cancel`, `reseparation-error` | phase display | all |
| Track page **(G2)** | `stem-set-pipeline` | "Separated with the high-quality vocal model" | all |

## Rules added by 020

- **R20-1**: rating elements carry text and an icon; tests assert `data-rating` and the text, never
  a colour.
- **R20-2**: at Beginner, `quality-bleed-*`, `quality-artefacts-*`, `quality-index-*`,
  `quality-analyzer-version`, `handout-customise`, `setlist-handout-customise` and the dialog are
  not rendered (base level rule).
- **R20-3**: no id contains a raw `.`; ChannelIds use `-` in ids (`quality-row-drums-kick`).

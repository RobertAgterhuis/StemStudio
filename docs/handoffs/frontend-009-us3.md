# Hand-off: frontend / 009 US3 (own styles)

Tasks **009:T060 -> T061** (ticked). Commits: red 52e5c7c -> green 981951b.

## What was built

- `/styles/new` (`StyleNewPage`) and `/styles/{id}/edit` (`StyleEditPage`) share `StyleEditor`:
  `style-name-input` (maxlength 60), `style-genre-select` (14 families, option label e.g. "Jazz"),
  `style-subgenre-input` (40), `style-tempo-select`, `style-instrument-tags` / `style-mood-tags` (text input, one tag
  per **Enter** or the `*-add` button; lists `style-instrument-tag-list` / `style-mood-tag-list`, remove buttons
  "Remove <tag>"; max 8 tags of 30 characters, duplicates ignored), `style-vocal-select`, `style-strength-select`,
  `style-description-input` (textarea, 300), `style-save`, `style-cancel` (back to `/styles`).
- `style-error` (`role="alert"`) with `data-field`: client check of the name, tag problems (`moods` /
  `instrumentation`), API validation errors (first field of `errors`, e.g. `description` for the imitation guard), a
  409 without field errors is `name` (style-name-taken); other problems have no `data-field`.
- Library actions: `style-new` (link), `style-duplicate-<id>` on every style (opens the copy in the editor),
  `style-edit-<id>` and `style-delete-<id>` on own styles only; delete asks first (`style-delete-confirm`, Escape or
  `style-delete-cancel` cancel); `styles-action-error`.
- Editing a preset by URL shows `style-load-error` ("Presets cannot be changed. Duplicate ...").
- `FieldProblem` (Api/) maps API errors to field + message (reused by the 010 forms).
- Help topics `styles-editor` (screen topic of `/styles/{id}/edit`; the no-imitation rule) and `styles-new` (screen
  topic of `/styles/new`; an extra topic because every route needs exactly one screen topic).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1323, Total: 1323
| StemStudio.Web | 99.06% line | 93.44% branch |
```

## Quality checks

Build 0 warnings; format clean; `sig_check src/StemStudio.Web`: 0 errors.

## Dependencies added

None.

## Open issues / requests

- qa: after `style-duplicate-*` the browser moves to the editor of the copy (`/styles/<copy>/edit`); the spec then
  navigates to `/styles` itself, which is compatible.

## Next owner

frontend continues with 009 US4 (T065/T066).

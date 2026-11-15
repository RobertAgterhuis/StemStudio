# Hand-off: frontend / 009 US2 (style library)

Tasks **009:T048 -> T049** (ticked). Commits: red 0fdbebd -> green 16cfc83.

## What was built

- `/styles` (`Components/Styles/StylesPage`): every style in the library order of the API (presets first, then by
  name); `style-row-<id>` with `data-kind`, kind as text ("Preset" / "Own style"), genre (+ sub-genre), tempo feel,
  moods, instruments, vocals, default strength, description; `styles-error`, `style-empty`.
- `StyleFilters`: native `<select>` `style-filter-genre` (14 families), `style-filter-mood` and
  `style-filter-instrument` (distinct tags of the library, sorted, option label = tag), `style-filter-kind`
  (All / Presets / Mine); first option label **"All"**; `<input type=search>` `style-search`.
- `StyleFilter` (case-insensitive name, sub-genre, description and tag search) and `StyleText` (labels).
- Help topic `styles-library` (screen topic of `/styles`).

## Deviation

T048 says "list with genre-family grouping". The rows are **not regrouped** by family: qa's
`styles.spec.ts` asserts that the visible ids after a mood filter equal the API order (presets first, then by name).
The family is a column and a filter instead.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1295, Total: 1295
| StemStudio.Web | 98.95% line | 93.37% branch |
```

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; `sig_check` on `src/StemStudio.Web`: 0 errors.

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend continues with 009 US6 (T055/T056).

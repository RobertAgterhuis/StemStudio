# Hand-off: frontend / 009 US5 (restyle only a section)

Tasks **009:T072 -> T073** (ticked). Commits: red 4edcace -> green c30c040.

## What was built

- Restyle dialog (Advanced, `restyle.options`): radios `restyle-mode-range` (default, checked) and
  `restyle-mode-inplace` (same `name`, `aria-describedby="restyle-mode-inplace-help"` with the explanation "only the
  part between start and end changes, the rest of the song stays as it is ... at most 8 minutes"). Not rendered at
  Beginner (default mode `range`).
- `RestyleForm.Validate` refuses in place for tracks longer than 480 s; the request carries `mode: inPlace` with the
  range inputs.
- Take details show "<start> to <end>, in place".
- Topic `restyle-options` explains in place.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1336, Total: 1336
| StemStudio.Web | 99.06% line | 93.55% branch |
```

## Quality checks

Build 0 warnings; format clean; `sig_check`: 0 errors.

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend continues with 009 polish (T077).

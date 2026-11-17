# Hand-off: frontend / 013 US5 (T064)

Branch `013-lane-frontend`. Contracts as in `frontend-013-us1.md`.

## What was built

| Task | Red | Green | Content |
|---|---|---|---|
| T064 | 91e494f4 | f89e955f | `restore-mode-merge` in `RestoreDialog` behind `FeatureCatalog` `care.restore-merge` (Advanced, topic `backup-restore`); the merge report |

Behaviour:

- `restore-mode-merge` ("Add what is missing (keep everything I have)") at Advanced and Expert, only on a non-empty
  library (an empty library has no mode). Merge needs no typed word (`confirmation` omitted) and is not blocked by a
  kept previous library; switching back to replace asks for `REPLACE` again.
- Report: "The missing items of the backup were added to your library." and, in `library-job-warnings`, one line per
  skipped item (`same-id` "already in your library", `same-source` "the same song is already in your library",
  `dependency-skipped`, `consent-invalid`, `option`, `missing-file`) and per renamed item ("added as \"Name
  (restored)\" (the name was taken)").

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2330, Skipped: 0
| StemStudio.Web | 99.65% line | 93.94% branch | 99.74% method |   gate 90/90 met
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` clean; `sig_check.py src/StemStudio.Web`: 0 errors,
39 warnings, none in a 013 unit.

## Dependencies added

None.

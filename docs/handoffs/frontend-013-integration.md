# Hand-off: frontend / 013 integration (quality errors, merge report words)

Branch `013-cto-frontend` (from `013-library-care`). Contracts as in `frontend-013-us1.md`; merge restore readings from
`backend-013-us5.md` "Contract readings".

## What changed

| Commit | Content |
|---|---|
| cafa41ef | `refactor(web)`: the 8 frontend errors of `run_all.py` fixed without exemptions |
| 62dc3312 | `feat(web)`: item words for the merge restore report (US5) |

Quality errors:

- **Duplication** (`BackupsTab`/`StorageTab`, `CleanupDialog`/`RestoreDialog`): new
  `Components/Care/CareDialog.cs` (abstract, `: ModalDialog`; injected `ILibraryCareClient Client`, `Busy`,
  `Error`/`ErrorCode` with `ShowError(ApiError?)`) used by all four care dialogs (backup, restore, clean-up,
  discard), and `Components/Care/CareTabBase.cs` (abstract, `: ComponentBase`; `Client`, `[Parameter] Status`) used by
  both tabs (`CareTab` was already the tab enum). The `.razor` files `@inherits` the bases; no markup or test id changed.
- **Parameters** (test fakes, max 4): `LibraryCareData.Status` lost `work`, `Size` lost `optional`/`regenerability`,
  `Job` lost `report`/`error`/`cancellable`/`id`; `StorageData.Volume` lost `reason`, `Category` lost `regenerability`
  (now derived: a category with `how` is regenerable, otherwise original), `Track` lost `removed`. Call sites set the
  rest with `with { ... }`; new `LibraryCareData.Completed(kind, report)` (completed, progress 100, not cancellable).
  Test intent unchanged.

Merge report (`LibraryCare/JobView.SkippedText`): the plural collection names map to `Track`, `Mix`, `Style`,
`Lyrics sheet`, `Voice profile`, `Library item` (assets), `Generation`, `Agent conversation`. Lines read
`Track "Song": already in your library` and `Mix "X" added as "X (restored)" (the name was taken)` (the word
"skipped" is gone; the reason says it). Unknown names fall back to readable words in the singular
(`practiceLoops` -> `Practice loop`, `track` -> `Track`), an empty name to `Item`. Tests:
`RestoreMergeTests.MergeReport_ListsSkippedAndRenamedItems` (all eight categories plus a rename, exact list in order),
`LibraryCareTextTests.SkippedText_NamesTheCategory_AndFallsBackToReadableWords` (7 cases),
`PreviousLibraryTests` updated to `voiceProfiles`.

## Numbers

```text
dotnet build StemStudio.slnx -c Release                        0 warnings, 0 errors
dotnet format StemStudio.slnx --verify-no-changes              clean
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2356, Skipped: 0
| StemStudio.Web | 99.65% line | 93.98% branch | 99.74% method |   gate 90/90 met
uv run --project tools/quality --frozen python tools/quality/run_all.py
quality gate: 2586 files, 20952 units, 289 clones, 3 exemptions, 0 errors, 1068 warnings -> exit 0   (was 8 errors)
```

No new dependencies. Note: commit cafa41ef alone does not compile the tests (two call sites move with 62dc3312); the
branch head builds and tests green.

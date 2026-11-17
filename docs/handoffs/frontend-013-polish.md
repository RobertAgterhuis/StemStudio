# Hand-off: frontend / 013 polish (frontend part of T066)

Branch `013-lane-frontend` (from `013-library-care` 7253cf37). Earlier checkpoints: `frontend-013-us1.md` (T039,
T040), `-us2.md` (T052), `-us3.md` (T056), `-us4.md` (T060), `-us5.md` (T064). T066 is a shared row (every owner),
so it is not ticked here.

## What was done

| Item | Commit | Content |
|---|---|---|
| SIG limits | ca594bf2 and per story | every 013 unit within unit size / complexity / razor-code limits (`CareCall`, `BackupCategoryText`, code-behinds) |
| Accessibility review | 53158bf9 | keyboard and screen-reader review (markup level) of the backup, restore, discard and clean-up dialogs and the maintenance overlay; two fixes: the job indicator's accessible name now contains its visible text (WCAG 2.5.3 "Backup 42 % (running). Open Library care"), an unmeasured storage area has no ARIA name/value without a role; `Care/LibraryCareAccessibilityTests.cs` |
| XML docs | all commits | every public type and member of the 013 code documented (build with documentation warnings: 0) |
| Decisions | this commit | six 013 rows appended to `docs/decisions/CTO-DECISIONS-2026-11-15.md` |

Review result (what the tests guard):

- Every dialog: `role="dialog"` (overlay: `alertdialog`), `aria-modal`, labelled by its title, focus moved to the title
  on open and back to the opener on close (`ModalDialog`), Tab/Shift+Tab trapped (`FocusTrap`), Escape closes (not
  the overlay, which closes by itself), native radios in a `fieldset` with `legend`, every input labelled, typed
  confirmations (`REPLACE`, `DISCARD`) named in the label, errors `role="alert"`, estimate/space/result `role="status"`.
- Page: WAI-ARIA tabs with roving tabindex and Arrow/Home/End; job progress `progressbar` with value text; status and
  stage changes in one polite live region (not every poll); tables with `scope` headers and a caption, sortable headers
  are buttons inside the `th` with `aria-sort`; wide tables scroll inside a focusable, named region (WCAG 1.4.10);
  ids unique; every `aria-labelledby`/`-describedby`/`-controls` resolves; disabled buttons carry their reason as
  visible text with `aria-describedby`.
- Target size: tab buttons 44 px high, text buttons and sort buttons keep `--ss-target-min` (24 px); focus rings via
  `:focus-visible`; reduced motion turns the progress transition off.
- Focus not obscured (WCAG 2.4.11): the job indicator is inline in the context bar and the Library care page has no
  sticky bar, so no fixed element can cover a focused control (the 012 `--ss-dock-inset` mechanism is not needed here).
  Not verified in a real browser or with a screen reader in this lane (no API in the worktree): qa's axe run on
  `/care` (T041) and the CTO's integration wave cover that.

## Tests and coverage (final)

```text
dotnet build StemStudio.slnx -c Release                         0 Warning(s), 0 Error(s)
dotnet format (Web, Web.Tests) --verify-no-changes             clean
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2338, Skipped: 0   (012 baseline 2084 + 254 new)
| StemStudio.Web | 99.65% line | 93.98% branch | 99.74% method |   (baseline 99.6 / 93.71)   gate 90/90 met
tools/quality sig_check.py src/StemStudio.Web: 0 errors, 39 warnings (baseline 39; none in a 013 unit)
tools/quality css_tokens_check.py: 0 errors, 0 warnings
```

## Dependencies added

None (.NET, npm). Licences unchanged.

## Open items

1. **Contract gap** (architect): `storage-worker-entries` should list worker cache entries (sha prefix, size, in use,
   from library), but backend-api 1.12.0 has no per-entry data; the frontend lists the worker categories. Needs an
   additive `StorageOverview` field if per-entry rows are wanted.
2. **Integration** (CTO/backend): the frontend assumes `listLibraryJobs`, the status' `activeJob` and the report `type`
   discriminator as in the delta; the client accepts the discriminator out of order. `jobs-active` problems are read
   with their `activeWork` extension.
3. **qa**: the extra ids listed in the decisions file are additive; E2E specs can wait on `library-job[data-status]`
   (polled every 2 s).

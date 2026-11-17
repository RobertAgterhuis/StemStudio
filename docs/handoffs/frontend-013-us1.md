# Hand-off: frontend / 013 US1 (T039, T040)

Branch `013-lane-frontend` (from `013-library-care` 7253cf37). Contracts: backend-api 1.12.0 DTOs
(`src/StemStudio.Contracts/LibraryCare`), ui-test-ids 1.12.0 (`specs/013-library-care/contracts/ui-test-ids.md`),
help-content 1.0.0. The backend is replaced by a substituted `ILibraryCareClient` (bUnit, `PageTestContext`).

## What was built

| Task | Red | Green | Content |
|---|---|---|---|
| T039 | a5feda4f | f47509b1 | `Api/{ILibraryCareClient,LibraryCareClient}.cs` (all 1.12.0 library-care and storage calls, `AllowOutOfOrderMetadataProperties` for the report `type`), `Components/Care/{LibraryCarePage,BackupsTab,BackupDialog,LibraryJobPanel,BackupList,BackupDetailsPanel,FocusTrap,StorageTab (placeholder)}`, `LibraryCare/{CareText,ArchiveRules,BackupForm,JobView}.cs`, `wwwroot/css/components/care.css` (sheet `care` in `App.razor`), `FeatureCatalog` `care.backup-options`/`care.job-bytes`/`care.verify` (Advanced), `care.manifest` (Expert), help topics (all nine 013 topics; anchors of later screens are added with them) |
| T040 | d6ac97c1 | 7c8c93af | `nav-care` (rail, before Settings), `Components/Layout/{LibraryJobIndicator,MaintenanceOverlay}.razor`, `LibraryCare/LibraryCareStatusSource.cs` (scoped; started by the shell and by `/care`), `Program.cs` registrations |
| refactor | | ca594bf2 | SIG unit size / razor-code limits (`CareCall`, `BackupCategoryText`, code-behinds) |

Behaviour (ids and attributes exactly as ui-test-ids 1.12.0):

- `/care`: `care-page` with `data-gate` (`open`/`backupRunning`/`maintenance`) and `data-help-id="care-page"`
  (screen topic `library-care`). Tabs `care-tab-backups`/`care-tab-storage` follow the WAI-ARIA tabs pattern
  (`role=tab`, `aria-selected`, roving `tabindex`, Arrow/Home/End, automatic activation); `?tab=storage` selects the
  second; switching replaces the history entry (`care` / `care?tab=storage`).
- Backups tab: `backup-folder-state` (`data-available`, `data-writable`, hint + free space), `backup-folder-error`
  (reason + fix) while unavailable or read-only; `backup-new` disabled with a visible reason (`aria-describedby`)
  while a job runs or the folder is unavailable.
- `backup-dialog` (modal, focus moved to the title, focus trap, Escape): `backup-profile-complete`/`-compact`;
  at Advanced+ `backup-profile-custom` and the six `backup-option-*` (changing one selects the matching profile or
  Custom); `backup-estimate` (`data-total-bytes`, `data-free-bytes`, `data-enough`, `role=status`), at Advanced+ one
  `backup-estimate-<category>` row (`data-bytes`, `data-included`, class); the estimate is read again after every
  change and a stale answer is ignored; `backup-voice-notice` when voice profiles are included and the estimate counts
  at least one voice file; `backup-start` disabled unless `data-enough="true"`; `backup-error` with `data-code`.
- `library-job` (`data-kind`, `data-status`, `data-stage`): `library-job-progress` (`role=progressbar`,
  `aria-valuenow`, `aria-valuetext` "<n> % - <stage>"), `-stage` (server `stageText`, else built-in text), `-item`,
  `-eta`, `-bytes` (Advanced+), `-cancel` while cancellable, `-error` (`data-code`), `-report`, `-warnings`; a polite
  live region announces only title and stage. The panel shows the active job, else the last job
  (`listLibraryJobs?limit=1`); when the active job disappears from the status, the final job is read and the list
  reloaded.
- `backup-list` / `backup-empty` (folder hint); `backup-row-<fileName>` `data-state` = `restorable`/`migration`/
  `incompatible`/`unrecognised`; `backup-row-status-<fileName>` (`data-help-id` -> `backup-compatibility`);
  `backup-download-<fileName>` = `<PublicApiBaseUrl>/api/backups/{fileName}/file` with `download`;
  `backup-details-<fileName>`; `backup-verify-<fileName>` (Advanced+); `backup-restore-<fileName>` (dialog in T052;
  disabled for incompatible/unrecognised or while a job runs); `backup-delete-<fileName>` -> `ConfirmModal`
  (`backup-delete-confirm` / `-cancel`).
- `backup-details`: `-contents`, `-counts`, `backup-details-voice-<profileId>` (`data-valid`, name, person, statement,
  date), `backup-details-manifest` at Expert.
- Shell: `library-job-indicator` (link to `/care`, `data-kind`, `data-progress`) inline in the context bar (never
  covers a focused control, WCAG 2.4.11); `library-maintenance` (`role=alertdialog`, focus moved in, focus trap,
  disappears by itself). Polling: 2 s while a job is active or the gate is not open, 10 s otherwise (`TimeProvider`).

## Decisions (owner unavailable; also in `docs/decisions/CTO-DECISIONS-2026-11-15.md`)

1. Errors of the list actions (check, delete) are shown in `backup-error` (with `data-code`) above the list: the
   contract has no list-level error id and the dialog is never open at the same time.
2. Delete stays enabled while a job runs (the API answers `409 archive-in-use` only for the archive in use, and qa's
   spec expects the 409 text); Restore and Check are disabled with a note while a job runs.
3. Extra ids not in the contract (additive, for the bUnit tests and qa): `backup-details-close`,
   `backup-details-error`, `backup-list-error`; focus-trap sentinels carry `data-focus-sentinel`.
4. Focus trapping (FR-032) without new JS: `FocusTrap` sentinels (Tab past the end returns to the dialog title,
   Shift+Tab before the start goes to an anchor after the last control).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2208, Skipped: 0
| StemStudio.Web | 99.15% line | 93.22% branch | 99.46% method |   gate 90/90 met
```

Branch coverage is 0.5 points below the 012 baseline (93.71 %) because `JobView`'s restore/merge report texts are
only reached by the US2/US5 tests (T052, T064); recovered there.

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` (Web, Web.Tests) clean;
`tools/quality/sig_check.py src/StemStudio.Web`: 0 errors, 39 warnings, none in a 013 unit.

## Dependencies added

None.

## For qa

`e2e/pages/LibraryCarePage.ts` can use the ids above; the download link is absolute (`PublicApiBaseUrl`). The job
panel's text updates every 2 s (poll), so wait for `data-status` rather than a fixed time.

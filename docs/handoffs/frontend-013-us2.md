# Hand-off: frontend / 013 US2 (T052)

Branch `013-lane-frontend`. Contracts as in `frontend-013-us1.md` (backend-api 1.12.0, ui-test-ids 1.12.0).

## What was built

| Task | Red | Green | Content |
|---|---|---|---|
| T052 | a5892d03 | a1cc8f76 (+ SIG fix in the hand-off commit) | `Components/Care/{RestoreDialog,PreviousLibraryPanel,DiscardPreviousDialog}.razor(.cs)`, `LibraryCare/RestoreForm.cs`, `JobView` restore/undo report and skipped items, help anchors of `backup-restore` (`restore-dialog`, `previous-library`) and `backup-voice-data` (`restore-voice-profiles`); `backup-compatibility` was already done in US1 |

Behaviour (ids exactly as ui-test-ids 1.12.0):

- `backup-restore-<fileName>` opens `restore-dialog` (modal, focus trap, Escape). It reads `getBackup` (size, voice
  profiles) and `getStorageOverview` (library volume free space).
- Empty library (`status.libraryEmpty`): the text "Your library is empty; the backup will be restored" (extra id
  `restore-empty`), no mode, no confirmation; sends `mode: replace` without `confirmation`.
- Non-empty: `restore-mode-replace` (checked; `restore-mode-merge` arrives with T064 at Advanced+),
  `restore-confirm-input` (type `REPLACE`), sent as `confirmation`.
- `restore-voice-profiles` when the archive has profiles (consent proofs or a voice-profile count).
- `restore-space` (`data-enough`): needed = unpacked size + 256 MiB (`LibraryCareLimits.RestoreFreeMarginBytes`)
  against the library volume; unknown free space does not block (the API checks again).
- `restore-blockers` from `status.activeWork`, and from the `activeWork` extension of a `409 jobs-active` answer;
  rows `restore-blocker-<kind>-<id>` (kind kebab-case, e.g. `lyrics-alignment`, `agent-run`) with links:
  import/detail/lyricsAlignment/score -> `tracks/{id}` when the id is a GUID (else `tracks`), export -> `library`,
  generation -> `generations`, agent run -> no link ("stop it in the composer panel").
- A kept previous library blocks a replace with the text "Undo the last restore or discard the previous library
  first" (the API answers `409 previous-library-exists`).
- `restore-start` is off until details are loaded, no blockers, enough space and the confirmation is valid;
  `restore-error` with `data-code` for every refusal (409/422/503).
- `previous-library` (`data-help-id`): date, archive, size, counts; `previous-library-undo` starts the
  `undoRestore` job (followed in the job panel); `previous-library-discard` opens a dialog with
  `previous-library-discard-input` (type `DISCARD`), `previous-library-discard-confirm` (off until typed) and
  `previous-library-discard-cancel`; errors in `previous-library-error` / `previous-library-discard-error`
  (`data-code`). Both buttons wait while a library job runs.
- Report: "The backup was restored." / "The previous library is back." plus the migration and the kept previous
  library, a line per restored category; restore warnings (for example the 011 claude-key line "The stored Claude key
  must be entered again") and skipped items in `library-job-warnings`.

## Decisions (owner unavailable; also in `docs/decisions/CTO-DECISIONS-2026-11-15.md`)

1. Blocker link targets (the contract says "with links" but not where): as listed above.
2. Extra ids (additive): `restore-empty`, `previous-library-error`, `previous-library-discard-cancel`,
   `previous-library-discard-error`.
3. The free space of the restore check comes from the storage overview (the status has no library free space); when it
   cannot be read the dialog says so and does not block.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2246, Skipped: 0
| StemStudio.Web | 99.53% line | 93.57% branch | 99.73% method |   gate 90/90 met
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` clean; `sig_check.py src/StemStudio.Web`: 0 errors,
39 warnings, none in a 013 unit.

## Dependencies added

None.

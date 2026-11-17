# Hand-off: frontend / 013 US4 (T060)

Branch `013-lane-frontend`. Contracts as in `frontend-013-us1.md`.

## What was built

| Task | Red | Green | Content |
|---|---|---|---|
| T060 | 70310966 | d481a96c | `Components/Care/CleanupDialog.razor(.cs)`, `LibraryCare/CleanupText.cs`, clean-up controls in `StorageTab`, `DetailSection` (`detail-files-removed`), `Mixer.razor` (`mixer-detail-removed`), `MixerState` keeps removed detail settings, `DetailPhaseText.SplitParents` / `ExportTarget.HasDetail` ignore removed detail stems; help topic `storage-cleanup` (anchors `cleanup-dialog`, `mixer-detail-removed`, `detail-files-removed`) |

Behaviour (ids exactly as ui-test-ids 1.12.0):

- Storage tab: `cleanup-detail-stems`, `cleanup-worker-cache`, `cleanup-leftovers` (all tracks, `trackIds` omitted);
  `cleanup-track-detail-<trackId>` in the per-track table (Advanced+, only rows with detail stems that are not
  removed). All are disabled with a visible reason (`aria-describedby`) while the gate is not open or a restore /
  undo restore job is active; a verify job does not block.
- `cleanup-dialog` (modal, focus trap, Escape = close): `previewCleanup` on open; `cleanup-plan-items` with
  `cleanup-plan-item-<kind>-<trackId>` (or `-all`), files, size and how to regenerate; `cleanup-plan-skipped` with
  reasons; `cleanup-plan-total` "Frees <size>" (`data-bytes`); `cleanup-confirm` (off for an empty plan) and
  `cleanup-dialog-cancel` (becomes "Close" after execution); `cleanup-result` (`data-freed-bytes`, plus failed items);
  `cleanup-error` (`data-code` `plan-stale`/`plan-expired`/`backup-running`/...) with `cleanup-repreview`, which makes a
  new plan. Closing after a removal measures the storage again (`refresh=true`).
- Track page: `detail-files-removed` inside `detail-section` while `detail.filesRemovedAt` is set ("Detail stems
  removed - Split further to restore them."), `detail-split-further` next to it (hidden while the new split runs);
  the outcome list of the removed stems is not shown.
- Mixer: a track with `filesRemovedAt` has no detail groups (the engine gets no detail stems, the parents play);
  `mixer-detail-removed` (above the strips) when the loaded mix has detail settings; Save / Save as send the mix's
  `expanded` and `detailChannels` unchanged (FR-025 "settings kept"). Stem packs no longer offer the removed detail
  stems; lyrics alignment no longer sees split vocals of such a track.

## Decisions (owner unavailable; also in `docs/decisions/CTO-DECISIONS-2026-11-15.md`)

1. Clean-up is refused only during a backup or a restore (FR-024): the buttons are off while the gate is not open or a
   restore/undo job is active; a verify job leaves them on.
2. The per-row clean-up uses the table's level (Advanced+) without its own catalogue entry.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2322, Skipped: 0
| StemStudio.Web | 99.55% line | 93.71% branch | 99.74% method |   gate 90/90 met
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` clean; `sig_check.py src/StemStudio.Web`: 0 errors,
39 warnings, none new (`MixerState::MixerState` complexity 6 is pre-existing).

## Dependencies added

None.

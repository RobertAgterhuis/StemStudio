# Contract delta: UI test ids for feature 013 (library care)

Status: **delta, binding names** (architect, 2026-11-16). Base: the full `ui-test-ids.md` **1.11.0**
of feature 012 (on this branch the latest full document is
`specs/010-lyrics-to-song/contracts/ui-test-ids.md` 1.9.0 plus the 011 delta 1.10.0). In Phase 0
(tasks.md T005) the CTO/architect merges this delta unchanged into the full document as **1.12.0**.
Nothing below renames, removes or changes an existing id; every existing rule applies (R1-R9:
`getByTestId` only, level visibility, placement inside 004's layout, ids on the interactive element,
`*-error` elements exist only while an error is shown).

Binding for the **frontend** agent; the **qa** agent writes the 013 specs against these names.

## Changelog line (for the merged document)

> 1.12.0 (additive, feature 013) - Library care page (`/care`) with the Backups tab (folder state,
> new backup dialog with estimate and options, library job progress, archive list, details,
> verify, download, delete, restore dialog, previous library undo/discard) and the Storage tab
> (volumes, categories, per-track table, model caches, clean-up dialog), the shell's library-job
> indicator and maintenance overlay, and the "detail stems removed" notices. All 1.11.0 ids are
> unchanged.

## Template parts

- `<fileName>` = the archive file name verbatim (`^[A-Za-z0-9][A-Za-z0-9._-]{0,119}\.zip$`, so it is
  a valid attribute value; Playwright uses `getByTestId('backup-row-' + fileName)`).
- `<category>` = a backend `Category` or `StorageCategory` value in kebab-case
  (`detail-stems`, `pending-uploads`, `voice-profiles`, `worker-separation-cache`, ...).
- `<volumeId>` = `library`, `backups`, `worker-data`, `model-cache`.
- `<modelCacheId>` = the folder id from the overview (`torch`, `hf`, `whisper`, `acestep`, ...).
- `<kind>` = `detail-stems`, `worker-cache`, `leftovers`; `<trackId>`, `<profileId>` GUIDs in `D` format.
- `<column>` = `total`, `source`, `stems`, `detail-stems`, `artwork`, `exports`, `scores`, `generations`, `recordings`, `worker-cache`.

## Ids

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Shell | `nav-care` | rail link "Library care" to `/care` (bottom bar under 768 px) | all |
| Shell | `library-job-indicator` | exists while a library job is queued or running; link to `/care`; attributes `data-kind`, `data-progress` (0-100) | all |
| Shell | `library-maintenance` | modal overlay while `gate = maintenance` ("Switching to the restored library ..."), focus moved into it, `role="alertdialog"`; disappears by itself | all |
| Library care (`/care`) | `care-page` | page root; attribute `data-gate` = `open` / `backupRunning` / `maintenance` | all |
| Library care | `care-tab-backups`, `care-tab-storage` | tabs (`role="tab"`, `aria-selected`); `?tab=storage` selects the second | all |
| Backups tab | `backup-folder-state` | folder line: host path hint and free space; attributes `data-available`, `data-writable` (`true`/`false`) | all |
| Backups tab | `backup-folder-error` | exists while the folder is unavailable or read-only; reason and fix | all |
| Backups tab | `backup-new` | opens the backup dialog; disabled with the reason while a library job runs or the folder is unavailable | all |
| Backup dialog | `backup-dialog` | modal dialog | all |
| Backup dialog | `backup-profile-complete`, `backup-profile-compact` | radio buttons "Complete" (all categories) and "Smaller: without detail stems" | all |
| Backup dialog | `backup-option-detail-stems`, `backup-option-exports`, `backup-option-scores`, `backup-option-generations`, `backup-option-recordings` (015 play-along takes), `backup-option-voice-profiles` | checkboxes; changing one selects the profile "Custom" (`backup-profile-custom`, rendered only at Advanced/Expert) | Advanced, Expert |
| Backup dialog | `backup-estimate` | total estimate text; attributes `data-total-bytes`, `data-free-bytes`, `data-enough` | all |
| Backup dialog | `backup-estimate-<category>` | one row per category: size, files, "Regenerable"/"Original", included or not; attributes `data-bytes`, `data-included` | Advanced, Expert |
| Backup dialog | `backup-voice-notice` | exists when voice profiles are included and at least one exists: "contains voice samples; not encrypted; keep it on a protected drive" | all |
| Backup dialog | `backup-start`, `backup-dialog-cancel` | buttons; start disabled while `data-enough="false"` | all |
| Backup dialog | `backup-error` | readable reason (409 codes); attribute `data-code` | all |
| Library job panel (Backups tab) | `library-job` | panel of the active or last job; attributes `data-kind`, `data-status`, `data-stage`, `data-job-id` (added editorially at the 013 integration, so E2E can tell a new job from the last one) | all |
| Library job panel | `library-job-progress` | `role="progressbar"`, `aria-valuenow` 0-100, `aria-valuetext` "<n> % - <stage text>" | all |
| Library job panel | `library-job-stage`, `library-job-item`, `library-job-eta`, `library-job-bytes` | stage text, current item, time left, "<done> of <total>" | all (`library-job-bytes`: Advanced, Expert) |
| Library job panel | `library-job-cancel` | exists while `cancellable` | all |
| Library job panel | `library-job-error` | readable reason; attribute `data-code` (JobError code) | all |
| Library job panel | `library-job-report` | report summary when completed (counts per category, archive size or restored counts) | all |
| Library job panel | `library-job-warnings` | list of warnings / skipped items; exists when there are any | all |
| Backups tab | `backup-list` | list of archives (newest first) | all |
| Backups tab | `backup-empty` | empty state with the folder hint ("Copy an archive into ./backups to restore it") | all |
| Backups tab | `backup-row-<fileName>` | one row; attribute `data-state` = `restorable` / `migration` / `incompatible` / `unrecognised` | all |
| Backups tab | `backup-row-status-<fileName>` | compatibility text (with the reason when not restorable) | all |
| Backups tab | `backup-download-<fileName>` | link to `GET /api/backups/{fileName}/file` (`download` attribute) | all |
| Backups tab | `backup-details-<fileName>` | opens the details panel | all |
| Backups tab | `backup-verify-<fileName>` | starts a verify job | Advanced, Expert |
| Backups tab | `backup-restore-<fileName>` | opens the restore dialog; disabled with the reason when not restorable or a job runs | all |
| Backups tab | `backup-delete-<fileName>` | opens the confirm dialog; confirm `backup-delete-confirm` | all |
| Backup details | `backup-details` | panel; `backup-details-contents` (included/excluded categories), `backup-details-counts` | all |
| Backup details | `backup-details-voice-<profileId>` | consent proof: profile name, person name, statement version, date; attribute `data-valid` | all |
| Backup details | `backup-details-manifest` | format, app and API versions, last migration, entry count, total bytes, warnings | Expert |
| Restore dialog | `restore-dialog` | modal dialog | all |
| Restore dialog | `restore-mode-replace`, `restore-mode-merge` | radio buttons; on an empty library neither is shown and the text says "Your library is empty; the backup will be restored" | replace: all; merge: Advanced, Expert |
| Restore dialog | `restore-voice-profiles` | checkbox "Restore voice profiles" (exists when the archive has profiles) | all |
| Restore dialog | `restore-space` | needed and free space; attribute `data-enough` | all |
| Restore dialog | `restore-blockers` | exists when jobs are active; lists them (`restore-blocker-<kind>-<id>`) with links | all |
| Restore dialog | `restore-confirm-input` | text input; replace on a non-empty library requires typing `REPLACE` | all |
| Restore dialog | `restore-start`, `restore-dialog-cancel` | buttons; start disabled until valid | all |
| Restore dialog | `restore-error` | readable reason; attribute `data-code` | all |
| Backups tab | `previous-library` | panel when a previous library exists: date, size, counts | all |
| Backups tab | `previous-library-undo` | "Undo restore" | all |
| Backups tab | `previous-library-discard` | opens the confirm dialog with `previous-library-discard-input` (type `DISCARD`) and `previous-library-discard-confirm` | all |
| Storage tab | `storage-refresh`, `storage-measured-at` | refresh button (bypasses the cache), measured time | all |
| Storage tab | `storage-volume-<volumeId>` | free/total bar (`role="meter"`, `aria-valuenow` = used %); attributes `data-free-bytes`, `data-total-bytes`; shared disks show one line with `data-shared-with` | all |
| Storage tab | `storage-worker-unavailable` | exists when the worker part is not available; reason | all |
| Storage tab | `storage-category-<category>` | row: size, files, class badge; attributes `data-bytes`, `data-class` (`original`/`regenerable`); regenerable rows show how to regenerate | all |
| Storage tab | `storage-manage-<category>` | link for original categories to the screen where items are deleted one by one (`/tracks`, `/library`, `/generations`, `/voices`; `storage-manage-recordings` opens the track page's takes list of 015, or `/tracks` before 015) | all |
| Storage tab | `storage-tracks-table` | per-track table (`<table>`, sortable headers `storage-sort-<column>` with `aria-sort`) | Advanced, Expert |
| Storage tab | `storage-track-row-<trackId>` | row; attributes `data-total-bytes`, `data-detail-removed`; title links to the track page | Advanced, Expert |
| Storage tab | `storage-top-tracks` | the five largest tracks as a list (Beginner replacement of the table) | Beginner |
| Storage tab | `storage-unattached` | items without a track | all |
| Storage tab | `storage-model-cache-<modelCacheId>` | size, models, "Downloaded again at first use"; no delete control | all |
| Storage tab | `storage-worker-entries` | worker cache entries table (sha prefix, size, in use, from library) | Expert |
| Storage tab | `storage-backups`, `storage-previous-library` | backups folder total; previous library size (exists when present) with link to the Backups tab | all |
| Storage tab | `cleanup-detail-stems`, `cleanup-worker-cache`, `cleanup-leftovers` | buttons that open the clean-up preview for all tracks; disabled with the reason during a backup or restore | all |
| Storage tab | `cleanup-track-detail-<trackId>` | per-row action "Remove detail stems" | Advanced, Expert |
| Clean-up dialog | `cleanup-dialog` | modal dialog with the preview | all |
| Clean-up dialog | `cleanup-plan-items` | list; rows `cleanup-plan-item-<kind>-<trackId>` (or `-all` for worker-wide items) with files, size and how to regenerate | all |
| Clean-up dialog | `cleanup-plan-skipped` | exists when items are skipped; reasons | all |
| Clean-up dialog | `cleanup-plan-total` | "Frees <size>"; attribute `data-bytes` | all |
| Clean-up dialog | `cleanup-confirm`, `cleanup-dialog-cancel` | buttons | all |
| Clean-up dialog | `cleanup-result` | after execution: freed size; attribute `data-freed-bytes` | all |
| Clean-up dialog | `cleanup-error` | readable reason; attribute `data-code` (`plan-stale`, `plan-expired`, `backup-running`) with a "Preview again" button `cleanup-repreview` | all |
| Track detail | `detail-files-removed` | inside `detail-section`, exists when `detail.filesRemovedAt` is set: "Detail stems removed - Split further to restore"; `detail-split-further` is rendered next to it | all |
| Mixer | `mixer-detail-removed` | notice above the strips when the mix has detail settings but the track's detail stems are removed | all |

## Help topics (ids = `data-help-id`, area `mixes`, help-content 1.0.0 format)

| Topic id | Anchored on | Content (summary) |
|---|---|---|
| `library-care` | `care-page` (screen topic, route `/care`) | what Backups and Storage do; one job at a time |
| `backup-contents` | `backup-dialog`, `backup-estimate` | categories, always vs optional, regenerable vs original, what is never included (models, settings, secrets, browser preferences) |
| `backup-folder` | `backup-folder-state`, `backup-folder-error` | the `./backups` bind mount, `STEMSTUDIO_BACKUP_HOST_DIR`, Linux owner fix, copying archives to an external drive, download |
| `backup-restore` | `restore-dialog`, `previous-library` | empty vs replace vs merge, verification first, previous library, undo, discard, active jobs |
| `backup-compatibility` | `backup-row-status-*` | versions, older archives are updated, newer ones need a newer StemStudio |
| `backup-voice-data` | `backup-voice-notice`, `restore-voice-profiles`, `backup-details-voice-*` | voice samples and consent proof travel together; not encrypted; protected drive; leave them out |
| `storage-overview` | `care-tab-storage`, `storage-volume-*` | areas, shared Docker disk, model caches, source copies |
| `storage-regenerable` | `storage-category-*` | the regenerable/original table and costs |
| `storage-cleanup` | `cleanup-dialog` | the three clean-ups, what never gets removed, how to get things back, model caches (manual route) |

# Hand-off: frontend 014, HO:us3 (lane L5, round 014+015)

US3 "Edit the sections".

## Tasks (red -> green)

| Task | Commit |
|---|---|
| T072 failing tests: `SectionDraftTests`, `SectionEditorTests` | 8fa32329 |
| T073 `Practice/SectionDraft.cs`, `Components/Practice/SectionEditor.razor(.cs)`, lane actions and Expert confidence | 89561143 |
| T075 help `mixer-sections-edit`, `mixer-sections-detection` | a706b078 |

Hooks for L6 in this stretch: a4aec34e (C/D: `MixerState.ExternalSolo`, `MixerState.Take`/`SetTake`, `partial void
OnMixLoaded(MixDto)`), 206106aa (`TrackDeleteTakesNote` in the track delete confirmation), 7d522861 (export:
`ExportForm`/`ExportTarget` partial, `private partial Guid? TakeIdForRequest()` with the stub `Export/ExportForm.Take.cs`,
slot `TakeExportPresets` with a stub; both stubs owned by L6).

## Behaviour

- `section-edit` (Advanced+, FeatureCatalog `sections.edit`) toggles the editor below the lane (`aria-expanded`); only
  enabled for a ready set. Rows `section-editor-row-<n>` with `-kind-`, `-label-`, `-start-` (seconds, step 0.1: arrow
  keys move the boundary), `-instrument-` (solos only; options = the track's channels), `-delete-`, `-merge-` (not on
  the last row); `section-editor-split` (playhead, snapped to downbeats/beats/0.1 s by the set's `snap` and the track
  grid), `section-editor-add` (rest of the song), `section-editor-done` (one `replaceSections` with the draft's
  revision), `section-editor-cancel`.
- Rules: 1 s minimum on both sides of a boundary; merge keeps the first id, kind and label; delete gives the time to the
  previous adjacent section (or the next); auto labels ("Verse 2", "Guitar solo") are renumbered unless the user typed
  the name (verse, pre-chorus, chorus, bridge always numbered; other kinds only when repeated); an empty name restores
  the auto label.
- Errors in `section-editor-error`: 409 `sections-changed` -> "Another window saved the sections first. Cancel and open
  the editor again to see them." (no extra reload button: no test id in the contract); 422 shows the server message;
  local checks (1..64 sections, >= 1 s) before sending.
- `section-detect-again` (Expert): detects at once for detected sets; for edited sets the `section-overwrite-confirm` /
  `-cancel` dialog first, then `detectSections(overwrite: true)`; the lane restarts polling when the set turns queued.
- `section-confidence-<id>` (Expert): "82 %".

## Quality

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Web (Release, coverage) | 3198 passed | 99.55 % | 93.64 % |
| web-interop | 412 passed | 99.8 % | 98.29 % |

- Release build 0 warnings; format clean except L6's `RecordingApiClientTests.cs:103`; `run_all.py` PASS (0 errors, jscpd
  ran). SIG warnings (no errors): `SectionDraft` ctor 17 NLOC, `Delete` 18 NLOC / CCN 7, `MoveStart` CCN 7,
  `SectionEditor.SaveAsync` 21 NLOC.
- a11y: every editor control has a visible label (`<label for>`), buttons name the section ("Delete Verse 1"), errors
  are `role="alert"`, rows are an ordered list with a per-row name.

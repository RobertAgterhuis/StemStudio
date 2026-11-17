# Hand-off: frontend 014, HO:polish (lane L5, round 014+015)

All 28 tasks of lane L5 are done and ticked in `specs/014-practice-flow/tasks.md`: T044-T049, T058-T059, T061-T067,
T069, T072-T073, T075, T082-T083, T085, T092-T093, T095, T098-T099, T101. Story hand-offs:
`frontend-014-{foundational,us1,us2,us3,us4,us5,us6}.md`.

## Since HO:us6

| Commit | Change |
|---|---|
| d752e8c9 | Hook 5 (rest): `shortcut-record` / `shortcut-compare` on the label cell of the two 015 rows of the overview, rendered at Expert only (015 R10); the practice pages share `ApiCall.RunAsync` (jscpd clone) |
| 92479a69 | `first-paint.js` back within 1 KB (1017 bytes; it grew to 1042 with the schema version 2 check) |

## Hooks delivered to L6 (015), complete list

`b632e788` AudioEngineInterop partial; `bb77f30f` AddRecordingUi in Program.cs (stub `RecordingUiRegistration`);
`f85ce7ee` optional `IEngineOptionsSource`; `9f507c5b` ChannelStrip `AdditionalAttributes`; `99ff1ed1` 015
FeatureCatalog rows; `d9f8e1a2` Mixer slots (`RecordingToolbarToggle`, `TakeStripSlot`, `RecordingArea`, base
`MixerSlot`), `partial void RegisterRecordingCommands(MixerCommandRegistry)`, `reserved: false` for R/C; `a4aec34e`
`MixerState.ExternalSolo`, `MixerState.Take`/`SetTake`, `partial void OnMixLoaded(MixDto)`; `206106aa`
`TrackDeleteTakesNote` in the track delete confirmation; `7d522861` export partials (`ExportForm`, `ExportTarget`,
`TakeIdForRequest` - L6 replaces it with `WithTake`), `TakeExportPresets` slot; `d752e8c9` overview rows. The help area
`recording` was added and reverted (`85454c43`, `c9e0343d`): 015 topics use area `mixer`.

## Final quality

| Component | Tests | Line | Branch | Functions |
|---|---|---|---|---|
| StemStudio.Web (Release, `-p:CollectCoverage=true`, incl. merged L6 suites) | 3418 passed | 99.44 % | 93.33 % | 99.68 % (method) |
| web-interop (`vitest --coverage`) | 412 passed | 99.8 % | 98.29 % | 100 % |

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `tools/quality/run_all.py`: PASS, 0 errors (jscpd ran, 384 clones, none an error). SIG warnings of my code are listed
  per story hand-off (largest: `PracticeRecorder.FlushAsync` 24 NLOC, `AddToSetlist.AddAsync` 20 NLOC, CCN 6-7 in
  `SectionDraft.Delete/MoveStart`, `ScorePanel.BarClickAsync`).
- web-interop: `eslint --max-warnings 0`, `tsc --noEmit`, `npm run build` clean.
- Security self-review: untrusted input is the browser preferences record (validated field by field in TS and C# with the
  shared cases; 16 KB limit) and API responses. No new dependencies.
- a11y (bUnit): role/name checks per component (lists of buttons, switches with `aria-checked`, `aria-pressed` toggles,
  `aria-expanded` disclosures, alerts and status regions, chart table views, names containing the visible text).

## Decisions recorded in the story hand-offs (for CTO-DECISIONS at integration)

- Override rules and layer semantics of the key map (foundational); Ctrl+/ at every level (us2); tour targets of
  `hands-free` (us2); setlist play route and the extra help topic `setlist-edit` (us5); `PracticeLogPanel` name and the
  host time zone for `utcOffsetMinutes` (us6); `first-paint` version check `< 3` (polish).

## Open items

- None blocking. E2E specs for 014 are L3's (`practice-levels.spec.ts` may switch to `0` / `Shift+K`).
- The second keys chord of a command is not editable in the settings page (data model and validator support it).

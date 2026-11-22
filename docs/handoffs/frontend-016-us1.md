# Hand-off: frontend (L5) / 016 HO:us1 (T070, T071, T074, T075, T076)

Branch `016-round-lane-l5`. Not pushed, not merged.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| infra | - | 0988f5d5 | `IInstrumentViewEngine` (all 12 engine 2.6.0 methods, both singing callbacks) on `AudioEngineInterop`, registered on the same engine object; C# engine types `Audio/InstrumentViewTypes.cs`; drift test checks `InstrumentViewMethods` against `types.ts`; `PendingCallbacks` emptied (T105/T106 amendment) |
| T070/T071 | 512bcca1 | 49a1cce4 (+ 1d1d5024 SIG) | `ChordPanel` (status, detect/re-detect with overwrite question and the 409 `edits-exist` retry, vocabulary at Advanced, engine at Expert, lane toggle stored in `stemstudio.chords.lane`, empty `chord-lane` host attached after `loadTrack`, limits); banner chords (`KaraokeBanner.Chords.cs`: `setKaraokeChords`, `data-chords="true"`, a refused chart is not resent); `ChordSheet` in the lyrics card (rows, chord spans, transpose -12..12, ChordPro link at Advanced, capo parameter for US8) with `ChordEditDialog` (root/quality/bass -> Harte label); help area `instruments` (AreaOrder after `mixes`, index title), topics `chords`, `chords-advanced`; feature catalogue `chords.view/full/edit/export/engine`, `scores.chords`; `ViewPreferences` (all data-model 8 keys); `instruments.css` |
| T074/T075 | 512bcca1 | 49a1cce4 | `score-include-chords` (Advanced, vocals/guitar/keys/piano only, disabled with `aria-describedby` reason when `options.chords.available` is false or absent) |
| T076 | - | cc218ba4 | `e2e/tests/instrument-chords.spec.ts` (`@instrument`): lane follows C, G, Am, F (expected symbols computed from the API chart), banner `.karaoke-chord` before its word, sheet rows, Advanced edit persists (restored in `finally`), ChordPro starts with `{title:`, lead sheet with chords has `<harmony>`. New `pages/instrument-views.ts`, `support/instrument-views.ts` (waits on the track's own analysis), `support/instrument-scores.ts` |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3617/3617 | 99.29 % line, 93.12 % branch |
| e2e | `npx tsc --noEmit`, `npx eslint --max-warnings 0 .` | clean | - |

Build 0 warnings (whole solution), `dotnet format StemStudio.slnx --verify-no-changes` clean, `run_all.py` PASS (0 errors,
jscpd ran: 445 clones). WCAG in bUnit: every control of the chord panel, sheet and editor has a name containing its
visible text (`InstrumentViewsAccessibility`), the lane and its toggle are outside `.ss-mixer-dock`, edit buttons >= 24 px.
Security: the chord edit label is built from fixed lists only (Harte pattern); ChordPro URL from the track id and ints.

## Decisions

1. Chord-sheet edit buttons map the n-th sheet chord to the n-th segment with a chord (or to the n-th segment when the
   sheet lists N.C. too); when the counts differ no edit button is shown (the sheet DTO carries no segment index).
   `chord-edit-<n>` uses the **segment index**.
2. `chords-overwrite-confirm` is a hidden marker inside the Radzen confirm dialog (`IConfirmService` overload with a test
   id), so the dialog keeps `confirm-dialog-ok`/`-cancel` as the contract says.
3. Re-detection at Advanced is gated by `chords.full` (no own catalogue id in data-model 6).
4. `chords-engine` sends `engine: null` for BTC (backend default), `chroma-hmm` explicitly.
5. The E2E chord spec does not use "Practise as" (US6 not yet built; the keys/guitar picker is checked in
   `instrument-practise-as.spec.ts`), so it can run at the mid-wave checkpoint.

## Deviations

None from the contracts. Catalogue id `practise-as` (data-model 6) fails the catalogue id pattern
`^[a-z]+(\.[a-z0-9-]+)+$` of the 004 tests; US6 will use `views.practise-as` unless the orchestrator decides otherwise.

## Open items

Next: T081-T083 (US2). `ChordSheet.Capo` is wired to capo mode in US8.

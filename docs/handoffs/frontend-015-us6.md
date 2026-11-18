# Hand-off: frontend / 015 US6 (lane L6, T074-T075) + qa T076

## What was built
- `Export/ExportTarget.Take.cs` (`TakeExport`, `TakeExportPreset`, `ExportTarget.ForTake`): a take export target adds the
  `take` channel (`export-channel-take` under "Selected channels"), adds the take to "What I hear" and translates the compare
  mode into explicit mutes (My take: the part's stem and its detail channels muted; Original: the take muted), with
  `mix.take` = the strip as heard.
- `Export/ExportForm.Take.cs`: `WithTake` (hook): "Take only" -> mode raw, channels `["take"]`, `takeId`, no mix (backend
  trims to the take); "Take + mix" (default) -> mix mode with `mix.take` and `takeId`; without a take the 007 request is
  unchanged (all 007 export tests green). `TakeSaving` for `export-take-saving`.
- `Export/ExportForm.cs`: only the two approved hook spots (`AudioRequest() => WithTake(new() {...})` and the partial
  declaration).
- `Components/Recording/TakeExportPresets.razor` (slot in the export dialog): `export-source-take-only`,
  `export-source-take-mix`, `export-take-saving`.
- `Mixer.Recording.cs`: `OpenTakeExport` from the take row's `rec-take-export-<id>` (disabled until the take is ready).
- Contract: cherry-picked L4's amendment bf224362 (`ExportMix.take`, `ExportMixDto.Take`).
- E2E `e2e/tests/recording-export.spec.ts` (T076): take only as WAV 24-bit has the take's frame count; take + mix as MP3 is
  ready, listed for the track, provenance contents start with the take name.

## Tests and coverage
`TakeExportTests` (7, real mixer page): take + mix request, take only request, My take / Original mutes, take in selected
channels, ordinary mixer export unchanged, saving note. Web 3383 passed; 015 code line 99.0 %, branch 91.9 %.

## Decisions / deviations
- The submit button cannot be disabled from L6's files (`CanSubmit` is L5's); `export-take-saving` explains the state and
  the backend's `409 take-not-ready` is shown as `export-error`. The row's export button is disabled while saving, so the
  dialog normally never opens for a saving take.
- The mixer's own "Save to library" (L5) does not add the take; take exports start from the take row (US6 scenarios).

## Docs impact
User manual: export a take (take only / take + mix, compare is included, formats).

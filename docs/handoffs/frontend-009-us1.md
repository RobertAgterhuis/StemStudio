# Hand-off: frontend / 009 US1 (restyle a track)

Branch `009-010-generative` worktree, from 2150022. Tasks **009:T043 -> T044** (ticked).

## What was built

- Typed client (`IStemStudioApiClient` is now `partial`): `GetGenerationStatusAsync`, styles (list, get, create,
  update, duplicate, delete), `CreateRestyleAsync`, generations (list with `GenerationQuery(kind, sourceTrackId)`,
  get, cancel, retry, delete), `ImportTakeAsync`. Query values use the contract spelling (`WireName.Of`).
- `RestyleForm` (Generation/): defaults = first style, whole track up to 480 s, every stem, the style's default
  strength, 1 take; an instrumental style removes and locks `vocals`; client-side checks 10-480 s, inside the track,
  >= 1 stem, 1-2 takes (the API checks again).
- `RestyleDialog` (+ `RestyleOptions`, Advanced via `LevelGate Feature="restyle.options"`): native `<select>`
  `restyle-style-select` (option label = style name), `restyle-style-description`, number inputs
  `restyle-range-start/end`, checkboxes `restyle-stem-<stem>`, `<input type=range>` `restyle-strength` with
  `aria-valuenow` 0-4 and `aria-valuetext` (Subtle..Free), number `restyle-takes`, `restyle-personal-use-notice`,
  `restyle-error`, `restyle-start`/`restyle-cancel`, Escape closes; focus handled by `ModalDialog`.
- `GenerationList` (polling `PollingComponent`) + `GenerationRow` + `TakeItem` + `TakeDetails` + `AiBadge`:
  `generation-row-*` (`data-status`, `data-kind`), `generation-progress-*` (Radzen progress bar, `aria-valuenow`),
  `generation-stage-*` (stage or the readable state), cancel/retry/delete by state, `generation-error-*`,
  `generation-delete-confirm` (new reusable `Shared/ConfirmModal`), `take-play-*` (plain `<audio controls>` on the
  public API URL), `take-ai-badge-*` ("AI-generated" text), `take-details-*` (native `<details>`: style + kind,
  strength, source, range, stems, seed, model + revision, device).
- Track page: section `track-generations` with `track-restyle` (enabled when the status is `ready`); started
  restyles appear at the top. Page `/generations`. Nav `nav-generations`, `nav-styles` (after a divider; the bottom
  bar scrolls sideways under 768 px).
- `GenerationStatusSource` (scoped, 10 s cache; API failure -> `notInstalled` with the reason), registered in
  `Program.cs` (fix commit after a failing host test).
- Help topics (area `tracks`): `restyle-options`, `restyle-strength` (feature `restyle.options`),
  `generation-ai-label`, `generation-personal-use`, `generation-limits` (screen topic of `/generations`).
- CSS `components/generation.css` (tokens only), rail divider in `shell.css`.

## Files changed

`src/StemStudio.Web/{Api,Generation,Styles,Services/WireName.cs,Components/Generation,Components/Pages/Generations.razor,
Components/Shared/ConfirmModal.razor,Components/Layout/NavMenu.razor,Components/Pages/TrackDetail.razor,Experience/FeatureCatalog.cs,
Help/topics/tracks/*,wwwroot/css/components/{generation,shell}.css,Program.cs}`, tests `tests/StemStudio.Web.Tests/{Api/GenerationApiClientTests.cs,
Generation/*,Fakes/{GenerationData,PageTestContext}.cs,Experience/FeatureCatalogTests.cs,WebHostTests.cs}`.

Commits: red 1a6b5e5 -> green 8904d8c; red 875be74 -> green 5bc92bf.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1284, Total: 1284
| StemStudio.Web | 98.91% line | 93.24% branch |
```

## Quality checks

`dotnet build` 0 warnings (TreatWarningsAsErrors); `dotnet format --verify-no-changes` clean;
`uv run --project tools/quality python tools/quality/sig_check.py src/StemStudio.Web tests/StemStudio.Web.Tests`:
0 errors (warnings only in test data builders).

## Dependencies added

None.

## Open issues / requests

- One pre-existing timing-flaky test (`BandInfoHostTests.Mixer_KeepsItsTrackWhenTheRefreshFails`) failed once in the
  baseline run before any change; it passed in every later run.
- qa: importing a take (US4), the status banner and disabled reasons (US6) and in-place (US5) follow in later
  checkpoints.

## Next owner

frontend continues with 009 US2 (T048/T049).

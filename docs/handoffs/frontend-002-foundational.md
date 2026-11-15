# Hand-off: frontend / 002-foundational

## What was built

- **T035 API client** (`backend-api.openapi.yaml` 1.2.0): `IStemStudioApiClient.StartDetailAsync(Guid, CancellationToken)`
  -> `POST /api/tracks/{id}/detail` (202 -> `TrackDto`), `CancelDetailAsync(Guid, CancellationToken)` ->
  `POST /api/tracks/{id}/detail/cancel` (200 -> `TrackDto`). 404/409/503 problems throw `ApiException` with the
  problem `detail` as `UserMessage` through the existing `ProblemReader`. `TrackDto.Detail` is read from the
  wire (`none` and completed phases with outcomes and detail stems); a body without `detail` falls back to
  `DetailPhaseDto.None`.
- **T037 mixer state** (`src/StemStudio.Web/Mixer/`):
  - `DetailChannelState` (parent, name, engine id `<parent>.<detail>`, a `ChannelState` for the strip,
    `ToDto()` -> `DetailChannelSettingsDto`, `Apply()`), `StemGroupState` (parent, stems in display order,
    `Expanded`, `From(ParentOutcomeDto)`, `Apply(MixSettingsDto)`), and `MixerState.Detail.cs`.
  - Groups exist only for parents with outcome `split` whose stem exists on the track, in standard display
    order. Stems are ordered by `DetailNames.AllowedFor(parent)` (`rest` last). Names not allowed for the parent
    are dropped, and a group without stems is dropped.
  - `ChannelState` gained `Key` (`drums-snare`, for `data-testid`/element ids) and `Label` (`snare`).
    `MixerState.Channel(id)` and the setters also accept detail ids, so `SetVolume("drums.snare", ...)` returns
    `ChannelSettingsDto("drums.snare", ...)`, which goes straight to `setChannel` (engine 2.0.0).
  - Gate mirror (FR-013, engine 2.0.0): `IsHidden` covers the detail channels of a collapsed parent and the
    own channel of an expanded parent. `AnySolo` counts only non-hidden channels. `IsAudible = !hidden && !mute
    && (!anySolo || solo)`. Flags of hidden channels are kept but ignored.
  - `SetExpanded` marks dirty. `RejectExpanded` collapses without touching the dirty flag (used when the engine
    rejects `setExpanded`). Detail settings survive collapse.
  - `ToSettings` writes `expanded` (expanded parents in display order) and every detail channel. `ApplyMix`
    restores both; a mix without them (saved before 1.2.0) collapses every group and resets detail channels to
    defaults. Unknown parents/stems in a mix are ignored.

## Files changed

- `src/StemStudio.Web/Api/IStemStudioApiClient.cs`, `src/StemStudio.Web/Api/StemStudioApiClient.cs`
- `src/StemStudio.Web/Mixer/ChannelState.cs`, `MixerState.cs`, `MixerState.Mix.cs`
- `src/StemStudio.Web/Mixer/MixerState.Detail.cs`, `DetailChannelState.cs`, `StemGroupState.cs` (new)
- `tests/StemStudio.Web.Tests/Api/DetailApiClientTests.cs` (new; `partial` of `StemStudioApiClientTests` to
  share the fixture), `tests/StemStudio.Web.Tests/Api/StemStudioApiClientTests.cs` (`partial`)
- `tests/StemStudio.Web.Tests/Mixer/MixerStateDetailTests.cs` (new)
- `tests/StemStudio.Web.Tests/Fakes/TestData.cs`: contract example JSON (`DetailNoneTrackJson`,
  `DetailQueuedTrackJson`, `DetailCompletedTrackJson`) and builders (`DetailStem`, `Split`, `Outcome`, `Phase`,
  `CompletedDetail`, `DetailTrack`, `WithDetail`)
- `specs/002-detail-separation/tasks.md`: T034-T037 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T034 -> T035 | `2802cc2` (does not compile: `StartDetailAsync`/`CancelDetailAsync` missing) | `a77f224` |
| T036 -> T037 | `5074f55` (does not compile: `Groups`, `SetExpanded`, `IsHidden`, `ChannelState.Key` missing) | `0bc6ad3` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true   (coverlet gate 90 % line+branch)
  Passed!  - Failed: 0, Passed: 222, Skipped: 0, Total: 222
  | StemStudio.Web | 99.85% | 94.03% | 100% |   (line | branch | method)
```

New files: `MixerState.Detail.cs`, `DetailChannelState.cs`, `StemGroupState.cs`, `StemStudioApiClient.cs`,
`ChannelState.cs`: 100 % line, 100 % branch each.

## Quality checks

- `dotnet build` (TreatWarningsAsErrors): 0 warnings, 0 errors. `dotnet format StemStudio.slnx --verify-no-changes`: exit 0.
- `uv run --project tools/quality python tools/quality/run_all.py`: `370 files, 2687 units, 17 clones,
  1 exemptions, 0 errors, 107 warnings -> exit 0`. No warning in the new production files. The
  `MixerState` constructor warning (complexity 6) is from 001; I added no branch to it. I extracted
  `ApplyChannels` so `ApplyMix` stays within 15 LOC.

## Dependencies added

None.

## Open issues / requests

- None for other owners. Note for the backend: the client relies on `detail` always being present (contract),
  but it tolerates its absence.

## Next owner

frontend (US1: T055/T056).

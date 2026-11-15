# Hand-off: frontend / 002-polish

## What was built (T084)

- **Accessibility of detail groups**:
  - Every input and button in an expanded group has an accessible name. The spoken names are "drums snare
    volume/pan", "Mute/Solo drums snare", "drums snare level meter" and "drums snare channel".
  - `group-<parent>` is `role=group` with the label "<parent> detail stems". The expand toggle is a native
    `button` (keyboard Enter/Space, in the tab order) with `aria-expanded`, `aria-controls="group-<parent>"`
    and "Show/Hide detail stems of <parent>".
  - An expanded parent strip announces "<parent> channel, replaced by its detail stems". The space play/pause
    shortcut from 001 already ignores focused buttons, so space on the toggle expands instead of playing.
- **FR-017 on the import page**: `stem-limitation-help` now also says that detail stems are split in the
  background, that their quality varies per song and instrument, that some instruments cannot be separated
  and that it takes considerably longer. The existing 001 sentence is kept.
- The shared accessible-name assertion moved to `MixerTestContext.AssertEveryControlIsNamed`.

## Whole feature 002 summary (frontend)

- **API client**: `StartDetailAsync`, `CancelDetailAsync`. `TrackDto.Detail` is read from the wire.
- **Track page** (`DetailSection`, `TrackDetail.Detail.cs`):
  - status, stage, progress, long-duration notice, outcome texts and detail stem list;
  - limitations help;
  - Split further, Retry, Cancel and the failure reason;
  - `track-delete-blocked`;
  - polling while the detail phase is active.
- **Mixer**:
  - `MixerState` groups follow the gate mirror of engine 2.0.0;
  - `ExpandToggle` / `StemGroup` with full detail strips;
  - `loadTrack` groups, then `setExpanded` (engine first, the state follows), then `setChannel("<parent>.<detail>")`;
  - meters for detail channels;
  - mix save/load of `expanded` and `detailChannels`, and old mixes collapse everything.
- **ui-test-ids 1.1.0**: every id in "Additions in 1.1.0" is rendered. Nothing was renamed or removed; all
  1.0.0 ids still pass their tests. Extra ids rendered: `detail-outcome-bass` (the backend returns an outcome
  for every stem).
- **CTO note on the audio-engine hand-off** (expand after `loadTrack`, then re-apply detail `setChannel`; the
  error text; the parent stays audible and expand can be retried): implemented that way and covered by
  `StemGroupTests` and `DetailMixTests`.

## Red -> green (all pairs of feature 002)

| Task pair | Red | Green |
|---|---|---|
| T034 -> T035 | `2802cc2` | `a77f224` |
| T036 -> T037 | `5074f55` | `0bc6ad3` |
| T055 -> T056 | `64d6dec` | `e366846` |
| T062 -> T063 | `07d15e8` | `fd201e6` |
| T069 -> T070 | `e42ea87` | `59a4720` |
| T074 -> T075 | `4668a2c` | `5ad33b6` |
| T079 -> T080 | `f5b4674` | `a0bb45a` |
| T084 | `f4de175` (2 failing: replaced-strip label, import text) | `dd99c9f` |

Checkpoint notes: `frontend-002-foundational.md`, `-us1.md` .. `-us5.md`, this file.

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release --no-incremental     0 Warning(s), 0 Error(s)
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true   (coverlet gate 90 % line+branch)
  Passed!  - Failed: 0, Passed: 284, Skipped: 0, Total: 284      (198 at the start of feature 002)
  | StemStudio.Web | 99.87% | 94.84% | 100% |   (line | branch | method)
dotnet format StemStudio.slnx --verify-no-changes              exit 0
```

New or changed 002 files, line / branch: `DetailPhaseText.cs` 100/100, `MixerState.Detail.cs` 100/100,
`StemGroupState.cs` 100/100, `DetailChannelState.cs` 100/100, `Mixer.Detail.cs` 100/100, `StemGroup.razor` 100/100,
`ChannelStrip.razor.cs` 100/100, `TrackDetail.Detail.cs` 100/100, `DetailSection.razor.cs` 100/100,
`DetailStemList.razor` 100/100, `AudioEngineInterop.cs` 100/94.7, `TrackDetail.razor.cs` 100/91.7.

## Quality checks

`uv run --project tools/quality python tools/quality/run_all.py`:
`387 files, 2761 units, 17 clones, 1 exemptions, 0 errors, 107 warnings -> exit 0`. No new warnings came from
002 frontend code. The remaining Web warnings are 001 units I did not change (`StartEngineAsync`,
`EngineAsync`, `MixerState` constructor, `ChannelState.Apply`, two `@code` blocks, `Import`/`Mixes`
methods) plus test-only warnings.

## Dependencies added

None (feature 002, frontend).

## Open issues / requests

1. backend: DELETE must answer 409 with a readable `detail` while the phase is active. Cancel and start must
   return the track (contract 1.2.0). Duplicate/rename must copy `expanded` and `detailChannels`.
2. qa: the E2E selectors for feature 002 are all in ui-test-ids 1.1.0. `expand-<parent>` is disabled with
   "Loading..." while the group decodes. Wait for `aria-expanded="true"` before using the detail strips.
3. Deviation (documented in us1): the component file is `DetailSection.razor` instead of
   `TrackDetail.Detail.razor`, because a `.razor` file name with a dot cannot be a component class.
4. CTO: the hand-off of the audio-engine was not merged into this worktree. The Web side is tested against
   `types.ts` 2.0.0 (drift test) and a faked `IAudioEngine` / bUnit JSInterop only. Run E2E after merging.

## Next owner

CTO (integration wave: merge with backend and audio-engine, then run the Playwright `@detail` suite); qa (T091).

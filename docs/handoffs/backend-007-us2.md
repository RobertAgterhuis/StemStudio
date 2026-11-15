# Hand-off: backend / 007-us2 (T042-T043)

## What was built

`MixExportTests` covers mode `mix` of `POST /api/assets/audio-exports`:

- **What I hear** (`channels = null`): the audible level from the engine's gate rule v2, with each channel's fader and
  pan and the master as `masterGain`. Defaults: 24-bit with dither; contents "bass, mixed"; the recipe keeps the mix
  snapshot with `channels: null`.
- **Selected channels**: mute and solo are ignored; inputs come in mixer order.
- **Saved mix** (`mixId`): the mix is loaded and copied into the recipe (`masterVolume` 0.5 reaches the worker). An
  unknown mix or a mix of another track -> 404. Neither or both of `mix`/`mixId` -> 422 `mix`.
- Mix rule violations keep their paths, e.g. `mix.detailChannels[0].stem`.
- Every channel muted -> 422 `nothing-to-export` with the contract detail.
- A ready MP3 shows `audio.appliedGainDb` (-1.8), `peakDbfs`, the bit rate and `audio/mpeg`.

T043 needed no new code. The mix branch of `ExportPlanner` (`MixAsync`, `SavedMixAsync`, `ValidateMix`) was written in
T036 as part of the shared request planning, and every T042 case passed on its first run.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T042 / T043 mixed exports | 43da4cf: green on its first run (no red step) | d43c344 (code from T036) |

This is a TDD deviation: the T042 tests came after the code they cover, because ExportPlanner plans raw and mixed
exports in one flow.

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true
  640 passed   StemStudio.Api line 99.43 %  branch 95.33 %   Contracts 100 / 100
```

## Quality checks

`dotnet format --verify-no-changes` is clean. SIG is unchanged since US1 (0 errors, 0 warnings).

## Dependencies added

None.

## Next owner

backend (US3: T049/T050).

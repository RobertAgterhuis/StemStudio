# Hand-off: backend / 002-us5 (T077-T078)

## What was built

- `MixMapper`: `MixSettingsDto.Expanded` and `DetailChannels` are mapped in both directions. If they are absent
  or JSON `null` they are treated as empty, so a 1.1.0-shaped body is accepted and returned with `[]`.
- `MixValidator`: a `null` entry is reported as `expanded[i]` / `detailChannels[i]` ("This value is required.").
  The Domain rules (T027) give the value errors at `expanded[i]`, `detailChannels[i].parent|stem|volume|pan`.
  On create these carry the `settings.` prefix, on update they have none, as for the existing fields.
- Create, get, update (rename) and duplicate round-trip both fields exactly. Duplicate copies them in
  `Mix.Duplicate`.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T077 / T078 | 9920a95 (8 of 10 red) | fe6598d |

## Files changed

- `src/StemStudio.Api/Mixes/{MixMapper,MixValidator}.cs`
- `tests/StemStudio.Api.Tests/Mixes/MixDetailEndpointTests.cs` (new)

## Tests and coverage

```text
Api.Tests 369 passed   StemStudio.Api line 99.6 %  branch 97.92 %   StemStudio.Contracts 100 % / 100 %
```

## Quality checks

`dotnet format --verify-no-changes` exit 0; `run_all.py`: 397 files, 2908 units, 0 errors, 125 warnings
(none in backend `src`).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

backend (polish: T083).

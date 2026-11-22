# Hand-off: backend / 016 US1 (T064, T065, T072, T073)

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen (backend-api 1.15.0, worker 1.9.0); no deviations.

## Tasks (red -> green)

| Tasks | Red | Green |
|---|---|---|
| T064/T065 chords endpoints (get, detect, delete, edit segment, sheet, ChordPro), `Track.instrumentViews`/`key`, instrument catalogue, un-staged operations | `3be60e38` | `f6efb92b` |
| T072/T073 `includeChords` (range-shifted symbols, `preferFlats`), `409 chords-not-ready`, `ScoreOptions.chords`, `ScoreFacts.chordsIncluded` | `bdcd6909` | `1c019f3a` |
| Gate fixes: format, 3 SIG errors in T065 code (CCN 11, 5 parameters, 8 duplicated lines) | - | `style: dotnet format`, `refactor(api): ...` (see git log) |

## Tests and coverage (coverlet gate, Release, full run before the SIG refactor; affected 1,123 InstrumentViews/Contract tests re-run green after it)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4179 passed | 99.10 % | 94.16 % |
| StemStudio.Persistence | 178 passed | 99.48 % | 95.12 % |
| StemStudio.Api | 3325 passed, 4 skipped (existing platform skips) | 98.38 % | 90.61 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: `dotnet build StemStudio.slnx -c Release` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` clean;
`tools/quality/run_all.py` PASS (0 errors, 457 clones, jscpd ran).

## Decisions

1. `AnalysisEndpoints.GetAsync` takes an `AnalysisProjection<TDto>(Kind, Map)` (max 4 parameters); chord routes are mapped in three groups (read, run, view) to stay under CCN 10. Later endpoint groups (drum hits, vocal pitch, key, capo) reuse the projection.
2. `ChordService.FindReadyForDisplayAsync` merges the transpose/capo range check with the ready lookup (sheet and ChordPro).

## Security self-review (US1)

Chord edit label: Harte pattern only (`C:power`, `{title: x}` refused with 422). ChordPro: title and artist stripped of control characters and braces before the header; attachment name reduced to a safe set (`../../etc/passwd` -> `etc_passwd.cho`, CR/LF and quotes replaced), sheet and ChordPro served `no-store`. Transpose and capo ranges checked (422). Worker results bounded before storing (foundational hand-off). Hostile cases are in `ChordEndpointsTests`.

## Open items

- `AnalysisResultMapper` branch coverage 74 %: hostile-result cases planned for HO:polish.
- Notes for T079/T080 (not started): existing 008 tests assert that `drums.<piece>` cannot use the `drums` profile (`ProfileCatalogTests`/`ScoreRecipeTests` `Allowed("drums.kick")` empty, `DrumScoreTests.OtherChannels_CannotUseTheDrumsProfile`); 016 reverses this for the five kept pieces (`Allowed` = `[Drums]`, `Suggest` stays null per contract, `ScoreRecipeRules.Recipe` must also store `Tablature`, `Part`). 422 codes `tablature-not-allowed`, `tuning-mismatch`, `capo-not-allowed` follow the typed-problem pattern of `ScoreProblems.Typed` (type = code). `listAssets` `channel` filter: SQL `json_extract` on the provenance like `AssetOrigins`.

## Dependencies

None added.

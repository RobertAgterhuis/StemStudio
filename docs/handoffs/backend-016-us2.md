# Hand-off: backend / 016 US2 (T079, T080)

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen (backend-api 1.15.0, worker 1.9.0); no deviations. No EF model change, no migration.

## Tasks (red -> green)

| Task | Red | Green |
|---|---|---|
| T079/T080 tablature validation (`tablature-not-allowed`, `tuning-mismatch`, `capo-not-allowed`, field `tablature.capo`), `ScoreOptions.tablature`, tablature facts, `drums.<piece>` with profile `drums` (one-piece `drumInputs` + `singlePiece`), `listAssets` `channel` filter | `b8c6fd19` | `0d0e540d` |
| Gate fix: four parameters at most in `TablatureScoreTests` (SIG) | - | `2eb24fb8` |

## Tests and coverage (coverlet gate, Release, full run at `0d0e540d`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4198 passed | 99.13 % | 94.19 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3348 passed, 4 skipped (existing platform skips) | 98.37 % | 90.64 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: `dotnet build StemStudio.slnx -c Release` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` clean;
`tools/quality/run_all.py` PASS (0 errors, 460 clones, so jscpd ran).

## Decisions

1. New Domain `ScoreUnprocessable` enum (all five 1.15.0 codes, clef/part used from T109) with `ScoreUnprocessableException`; `ScoreTablatureRules.Check` runs only once the profile is allowed. Order: profile not guitar/bass, then tuning of the other instrument, then capo with a bass tuning. The capo range 0..12 is a field error (`tablature.capo`) checked after them. `ScoreProblems.For` maps the codes to typed 422s. Like the other score problems (008 `drums-not-split`, `tempo-required`), the `type` is the bare code.
2. `ProfileCatalog.Allowed("drums.<piece>")` = `[drums]` for kick, snare, toms, hihat, cymbals (`Suggest` stays null). `Suggest` now returns null for ids with more than one dot. For a single piece, the resolver gives `DrumPieces = { piece: path }` and the submitter sets `singlePiece`.
3. `ScoreOptions.tablature` follows the suggested profile: guitar gives 7 tunings, default `guitar-standard`, capo allowed; bass gives 5 tunings, default `bass-4-standard`, no capo; any other profile gives `{false, null, [], false}`.
4. `listAssets?channel=`: the pattern is `\A[a-z]+(\.[a-z-]+)?\z` (the contract pattern with `\z`, so a trailing newline is refused), at most 64 characters and only together with `trackId` (otherwise 422 on `channel`). The SQL `json_extract(Provenance, '$.ScoreRecipe.Source.Channel') = @p` is parameterised, like `AssetOrigins`.

## Security self-review (US2)

Untrusted inputs: the `channel` query and the tablature settings. Hostile cases are in the tests: `bass'--`, `x' OR '1'='1` (Persistence, parameterised, 0 hits), upper case, a trailing dot, three parts, capo -1/13/99. No other surface was added.

## Dependencies

None added.

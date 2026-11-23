# Hand-off: backend / 016 US5 (T108, T109)

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen; no deviations. No EF model change (the recipe JSON already holds `Part`, round trip tested in T047).

## Tasks (red -> green)

| Task | Red | Green |
|---|---|---|
| T108/T109 profiles `strings`/`brass`/`woodwinds` suggested for `<parent>.strings|brass|woodwinds`; `part` validation (`part-not-allowed`, `clef-not-allowed`); `ScoreOptions.parts`; `ScoreFacts.part`; `Part` kept in the recipe and sent to the worker | `d13d0bf0` | `8d8341ff` |

## Tests and coverage (coverlet gate, Release, full run at `8d8341ff`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4219 passed | 99.16 % | 94.21 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3375 passed, 4 skipped (existing platform skips) | 98.54 % | 90.79 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: build 0 warnings; `dotnet format --verify-no-changes` clean; `tools/quality/run_all.py` PASS (0 errors, 467 clones, so jscpd ran). `ProfileCatalog.Suggest` and `ScoreRecipeRules.Validate` are now within the soft limits.

## Decisions

1. **Allowed profiles.** A section stem (`<parent>.strings|brass|woodwinds`) offers the five 008 pitched profiles plus its own section profile. Any other pitched source keeps the five 008 profiles. A library item gets its section suggestion added the same way (`ProfileCatalog.Allowed(channel, suggestion)`). The reason is the contract: `ScoreOptions.allowedProfiles` has `maxItems: 6` and `limits` has `maxItems: 6`, so offering all eight pitched profiles everywhere would break it. The 008 option tests stay unchanged.
2. **Part rules** (data-model 4.2/4.3). Every pitched profile takes any written instrument. `drums` with a part gives `part-not-allowed`. `keys`/`piano` with a clef other than `auto` gives `clef-not-allowed`. The checks run after the tablature checks (profile, then tuning, then capo, then part, then clef). The typed 422 uses the bare code, as the other score problems do.
3. **`ScoreOptions.parts`** follows the suggested profile: `available` for every pitched profile, `defaultInstrument` = `concert`, `clefAllowed` = not keys/piano. For drums or no suggestion it is `{false, concert, false}`.
4. **Section profile texts.** Name "Strings"/"Brass"/"Woodwinds". Each limit text says the section is reduced to its loudest line (data-model 4.4 `section-reduced`).

## Security self-review (US5)

New inputs are the `part` enums, which the JSON binding restricts to the catalogue; an unknown value is refused by the binder. There is no free text.

## Dependencies

None added.

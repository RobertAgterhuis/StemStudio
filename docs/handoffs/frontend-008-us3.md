# Hand-off: frontend / 008 US3 (piano on two staves)

Tasks 008:T054 (red) and T055 (green).

## What was built

- `score-quality` (Advanced+, feature `score.quality`, marker `sheet-music-piano`), rendered only for the piano
  profile.
  - Options: `accurate` "Accurate: piano model (about 9 minutes)" and `fast` "Fast: general model (about 31 seconds)".
    The times come from the piano estimates in `ScoreOptions.estimates`.
  - It preselects `ScoreOptions.defaultQuality` (accurate on a GPU worker, fast on the CPU), and `score-estimate`
    follows the choice.
  - At Beginner the default quality is sent. The request carries `quality` for piano only.
- Library: `asset-attribution-<id>` shows `ScoreFacts.attribution` (the piano model credit, CC BY 4.0) when it is
  set.
- Help: `tracks/sheet-music-piano.md` (feature `score.quality`): accurate vs fast, CPU time, the default per device,
  and the credit.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T054 / T055 | ee483f2 (6 failing) | 3df1d0d |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1210, Total: 1210
| StemStudio.Web | 99.46% line | 92.78% branch | 99.7% method |
```

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.Web`: 0 errors. No new warning.

## Dependencies added

None.

## Next owner

frontend (008 US4: T063/T064); qa T056.

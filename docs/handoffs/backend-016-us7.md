# Hand-off: backend / 016 US7 (T116, T117)

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen. No EF model change.

## Tasks (red -> green)

| Task | Red | Green |
|---|---|---|
| T116/T117 `getKey`: key, display with transpose and original, `(uncertain)` below 0.5, written key per `instrument`, regions with start bars from a reliable grid (null otherwise), ready or stale chart only; `Track.key` absent without ready chords; un-staged | `c0e9c3c3` | `189278a9` |
| Gate fix: `KeyViewMapper.ToDto` CC 11 -> within limit | - | `10b8d4c6` |

## Tests and coverage (coverlet gate, Release, full run at `10b8d4c6`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4219 passed | 99.16 % | 94.21 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3389 passed, 4 skipped (existing platform skips) | 98.53 % | 90.78 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: build 0 warnings; `dotnet format --verify-no-changes` clean; `tools/quality/run_all.py` PASS (0 errors, 468 clones, so jscpd ran).

## Decisions

1. **Written key.** `writtenKey` is the written key of the *sounding* key, that is the concert key moved by the transpose. A player reading along with transposed playback needs that key. For example, C major at +2 with horn-f gives A major. It is null for `concert`.
2. **Region display** is the region key spelled at the transpose, and `tonic` stays at concert pitch (as the contract example shows). Only the global `display` carries "(uncertain)".
3. **Start bars.** `startBar` = `SectionSnapper.BarAt` (the downbeat count, the same rule as the 014 sections), only with a reliable grid.
4. **Query validation.** `instrument` is parsed as the catalogue wire id (case-sensitive), and `transpose` must be in -12..12. Anything else gives 422 with `errors.instrument`/`errors.transpose`.

## Requests for the orchestrator

- Contract gap (no deviation taken): `getKey` declares only 200 and 404, but an out-of-range `transpose` or an unknown `instrument` must be refused. The API answers 422 validation problem, as the chord sheet does. Please add `'422': ValidationProblem` to `getKey` (and to `getCapoSuggestions` for `transpose`) in the next contract revision. The 422 tests do not assert the contract for that status.

## Security self-review (US7)

Inputs `transpose` (int, range-checked) and `instrument` (enum wire id, allow-list via the catalogue enum, case-sensitive; `HORN-F`, `kazoo` refused). There is no free text in the output beyond the catalogue names.

## Dependencies

None added.

# Hand-off: backend / 009-us5 (T070-T071)

## What was built

No production change: the in-place rules were implemented with the planner in T038 (`RestylePlanner`: in place only
for tracks <= 480 s -> 400 `mode`; range 10-480 s inside the track; mode and range sent to the generator, which
repaints the range; the take keeps the duration the generator reports, i.e. the track length).
`InPlaceRestyleTests` (4 tests) document the behaviour.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T070/T071 | a7da1bb (tests green on arrival; deviation from strict red -> green, the behaviour existed since d00d1f6) | - |

## Tests and coverage

`InPlaceRestyleTests` 4 passed; cumulative numbers in backend-009-polish.

## Dependencies added

None.

## Next owner

backend (009 polish T076).

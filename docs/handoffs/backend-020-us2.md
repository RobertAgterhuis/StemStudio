# Hand-off: backend / 020 US2 tips (T049-T052)

| Task pair | Red | Green |
|---|---|---|
| T049/T050 + T051/T052 | 046982e1 | e8459459 |

- `Domain/Insights/TipRules.cs` (pure): rules 1, 2, 4, 5, 6, 7 of data-model 5.3 in order, at most two. Rule 3
  (`rerun-high-quality`) is never produced (G2 deferred, not in the 1.19.0 enums), so no device estimate is needed.
  There is no GPU tip (test over every channel x rating x detail state: no text contains "GPU" or "re-separate").
- `Api/Insights/QualityTips.cs`: tips built on every read from the current track state (detail phase status, outcomes,
  files removed), so a split changes them without a new analysis (`QualityTipsTests`).
- Actions: `mute(channel)`, `split-detail(channel, parent=channel)` (as the contract example), `open-detail(parent, child)`
  with the best measured kept child (rest excluded), `open-detail(parent, "rest")` for `rest-share` on a detail stem.

## Readings (please confirm)

1. "Detail phase none" for rule 4 also covers a completed phase whose files were removed.
2. Rule 6 applies when none of rules 2-4 applied; `{Instrument}` is the detail name in words ("Electric guitar").
3. The `rest` stem itself gets no `listen-to-rest` tip.

The 016 test `ChordEndpointsTests.TheAutoStarter_IsACompletionHook` now allows the quality trigger as a second detail hook
(one-time edit).

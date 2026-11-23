# Hand-off: frontend (L5) / 016 HO:foundational (T057-T060)

Branch `016-round-lane-l5` (from design head 7463ac54). Not pushed, not merged.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T057/T058 | bfe903c4 | be6cd3c9 (+ 7c65ccb5 SIG refactor) | `InstrumentViews/InstrumentFamilies.cs` (data-model 5.3 family rule, picker order, ids, labels, views), `InstrumentViews/FamilyMixService.cs` (`MinusOne`, `Capture`/`Restore` via `ChannelFlags`, `Guide` for FR-033); `PracticePresets.PlayAlong` now uses `MinusOne` (DELTA 6) |
| T059/T060 | 879fdbe8 | 308609e8 | `IStemStudioApiClient.InstrumentViews.cs` + implementation: chords get/detect/edit/delete, sheet, key (`instrument` as wire name), capo suggestions, drum hits, vocal pitch, catalogue, `ListChannelScoresAsync` (`kind=score&trackId&channel`); `InstrumentCatalogueCache` (scoped, failed load not cached); score extensions round-trip through the existing `GetScoreOptionsAsync`/`CreateScoreAsync` |

Consumer-driven fakes: `tests/StemStudio.Web.Tests/Fakes/InstrumentViewsData.cs` holds the backend 1.15.0 examples
(`getChords`, `getChordSheet`, `getKey`, `getCapoSuggestions`) verbatim plus builders for the other shapes.

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3536/3536 | 99.45 % line, 93.3 % branch (gate 90/90) |

`dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` clean; `tools/quality/run_all.py` PASS
(0 errors, jscpd ran: 443 clones; my new files only carry unit-size warnings of test data and two 16/18-line units).

## Decisions

1. Family rule literally as data-model 5.3: `other` is orchestral only while not split; `rest` follows the parent's
   standard family; the standard `guitar`/`piano` stems stay in their family even when split.
2. `Restore` restores the captured flags of **every** channel (the family's and the others'), so "Drumless" off undoes
   the unmute/unsolo of the other channels too (superset of "previous drum settings", FR-032).
3. Views per family (spec US6 AS1): vocals singing, guitar and bass tablature, keys chords, drums drum-trainer,
   orchestral part.
4. No client method for ChordPro: the download is a plain link (`PublicApiBaseUrl` + `ApiRoutes.ChordPro`), like the
   asset and backup downloads.

## Deviations

None.

## Open items

None for this checkpoint. Next: T070-T076 (US1).

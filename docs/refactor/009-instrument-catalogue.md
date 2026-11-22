# 009 One instrument family catalogue for Domain, Contracts and Api
- Status: proposed · Date: 2026-11-19 · Area: Domain (Tracks, Scores, Recording) / Contracts (StemNames, DetailNames, InstrumentLabels) / Api · Priority: P2

## Problem statement
The instrument vocabulary (the six stems, the detail stems of each family, and which family a channel belongs to) is written out
several times, in separate lists that nothing checks against each other. Families that are not vocals, drums, guitar or piano get
weaker treatment as a result. That goes against the owner rule that every instrument family counts equally.

Evidence of the duplication (no parity test found: no test references both `StemCatalog` and `StemNames`, or both `DetailCatalog`
and `DetailNames`):
- Six-stem list, twice: `src/StemStudio.Domain/Tracks/StemCatalog.cs:7,10` and `src/StemStudio.Contracts/StemNames.cs:25,28`.
- Detail parents and children, twice: `src/StemStudio.Domain/Tracks/Detail/DetailCatalog.cs:13-25` and
  `src/StemStudio.Contracts/DetailNames.cs:10-31`.
- Drum pieces, four times: `DetailCatalog.cs:21`, `DetailNames.cs:13`, `src/StemStudio.Api/Scores/ScoreSourceResolver.cs:51`
  (`DrumPieceNames`), and as labels in `src/StemStudio.Contracts/Library/InstrumentLabels.cs:27`.
- Family classification exists four more times, each with its own enum or switch: `ScoreProfile` plus `ProfileCatalog.Suggest`
  (`src/StemStudio.Domain/Scores/ProfileCatalog.cs:25-38`), `RecordingPart` (`src/StemStudio.Domain/Recording/RecordingValues.cs:4-26`),
  `TakeNaming` (`Domain/Recording/TakeNaming.cs:23`), and `InstrumentLabels.Families` (Contracts).
- Raw stem-name strings in services: `Api/Voices/ConversionStemWaiter.cs:52,84` (`"vocals"`), `Api/Scores/ScoreOptionsService.cs:36`
  and `ScoreSourceResolver.cs:87-88` (`"drums"`), `Domain/Generations/RestylePlanner.cs:71`.

Evidence of unequal treatment:
- `ProfileCatalog.Suggest` (`ProfileCatalog.cs:35`) maps every `other.*` and `piano.*` detail channel to `ScoreProfile.Keys`
  ("Two staves split at middle C"). That includes `other.brass`, `other.woodwinds` and `other.strings`, and
  `piano.acoustic-guitar` / `piano.electric-guitar`. A trumpet or violin part gets a piano score. There is no score profile for
  strings, brass or woodwinds at all (`ScoreValues.cs:6`).
- The test that pins the suggestions (`tests/StemStudio.Domain.Tests/Scores/ScoreRecipeTests.cs:15-24`) uses `guitar.rhythm`, which
  is not a channel in either detail catalogue. It covers no brass, string or woodwind channel. Drift between the lists already
  reaches the tests.

## Impact
- Adding or renaming a family or a detail stem means changing 5-8 files in three projects, with nothing that reports a missed one.
  That is how the brass/strings/woodwinds gap happened.
- Players of orchestral and wind instruments get a wrong score layout by default (user experience, and a breach of the owner
  rule).
- Sigrid and SonarQube report this as duplication in small, separate pieces that never reach the clone threshold, so the quality
  gate does not catch it (`docs/quality/SIG-REPORT.md` shows 0 production clones in Domain and Api).

## Risk
- Leaving it: every instrument-specific feature (chords, tabs, instrument views from ADR-033) adds another copy.
- The change: catalogue values are part of the API contract and the worker contract. Their strings must not change. ADR-002 keeps
  Contracts free of dependencies and keeps Domain referencing nothing. So the shared catalogue has to live in Domain, with Contracts
  either taking its strings as constants or being checked against it by a test. Risk is low if step 1 pins every current value.

## Effort
M (2-4 days). Assumes no change to wire values and no new score profiles. Adding profiles for strings, brass and woodwinds is a
product decision, and the worker and engraving owners may need to follow.

## Step-by-step plan
1. Safety net: one parity test class in `tests/StemStudio.Api.Tests` (it sees Domain and Contracts) that asserts
   `StemCatalog` = `StemNames`, `DetailCatalog.AllowedFor(p)` = `DetailNames.AllowedFor(p)` for every parent, that
   `ScoreSourceResolver.DrumPieceNames` equals the drums details minus `rest`, and that every catalogue channel has an
   `InstrumentLabels` entry and a `ProfileCatalog.Suggest` result (or an explicit "none" list). Fix `guitar.rhythm` in
   `ScoreRecipeTests`.
2. Make `DetailCatalog` and `StemCatalog` the single source in Domain, and have each enum-like mapping (`RecordingPart`, profile
   suggestion, take naming) look up a `InstrumentFamily` record there instead of matching raw strings. Check: step-1 tests green,
   no change in contract tests.
3. Replace the raw `"vocals"`/`"drums"` strings in Api and Domain services with the catalogue constants. Remove
   `ScoreSourceResolver.DrumPieceNames`. Check: `grep '"(vocals|drums|bass|guitar|piano|other)"'` in Api and Domain finds only the
   catalogue.
4. Product follow-up (owner decision): suggest a single-staff melodic profile for monophonic families (brass, woodwinds, strings
   detail stems) instead of `Keys`, with one test row per family. Record this in the MoSCoW "every instrument counts equally" entry.
5. Verify: the parity test is part of the gate, and adding a dummy detail stem in a scratch branch makes it fail in all the places
   that need an update. Roll back step by step. Each step is a separate commit with no behaviour change except step 4.

## Dependencies and related proposals
Related to ADR-033 (instrument views) and ADR-031/032. The frontend audit may find the same lists in Web. The Contracts catalogue
is the shared point between the two.

# 018 Per-feature registration modules instead of one composition hotspot
- Status: proposed · Date: 2026-11-19 · Area: Api / composition root (ApiComposition.cs) · Priority: P2

## Problem statement
`src/StemStudio.Api/ApiComposition.cs` (281 lines) is the file that changes most in the backend: 55 commits, 4 of them fixes
(`git log --format= --name-only` over the backend projects; the next files are at 17 and 13). It registers the services of
features 001-010 inline. These include `AddGenerationServices` with 29 registrations (lines 186-218) and the score, lyrics,
artwork and asset blocks. It also maps every endpoint group (lines 115-140) and runs start-up work (migrations, seeding, hash
index, agent recovery, lines 86-106). Later features already have their own module (`AddLibraryCare`, `AddAgentHost`,
`AddLibraryFolders`, `AddLyricAssist`, `AddPracticeFlow`, `AddRecording`, lines 63-69), so the house uses two styles.

## Impact
- Every round with parallel lanes edits the same lines, which leads to merge conflicts and missed registrations. Commit `004196f3`,
  for example, had to add `DetailPhaseLocks` here.
- A missed or duplicate registration only shows up at run time (DI resolve), usually in E2E.
- Start-up order is implicit: Library Care recovery before migration, migration before seeding and recovery. It is easy to break
  when a new start-up step is added.

## Risk
- Leaving it: steady cost per round, low defect risk.
- The change: behaviour-neutral moves. The one real risk is changing the order of registration where `TryAdd*` or decorators depend
  on it (`TimeProvider`, `ISeedSource`, `IDiskSpace`, and `DecorateGpuJobClients` in `AddAgentHost`). Rollback is a revert.

## Effort
S (1-2 days).

## Step-by-step plan
1. Safety net: add a DI validation test (`ValidateOnBuild` and `ValidateScopes` on the `ApiFactory` host), and a snapshot test of
   the registered service types and lifetimes (sorted list compared to a checked-in text file). Any accidental change then shows up
   as a test diff.
2. Move each inline block into a `<Feature>Registration.cs` next to its feature, as `AddDetail`, `AddArtwork`, `AddAssets`,
   `AddLyrics`, `AddScores`, `AddGenerations`, `AddTracks`, `AddSync`. Keep the call order the same. Check: the snapshot is
   unchanged.
3. Give each feature a `Map<Feature>Endpoints` that is already called from one list. Move the health endpoint into its own module.
4. Make start-up an ordered list of `IStartupStep` (recover library care, ensure database, migrate, seed, refresh hash index,
   recover agent runs), each with a name that is logged. Check: the start-up tests and the Library Care recovery tests pass.
5. Verify: `ApiComposition.cs` under 80 lines with no feature types in it, the snapshot unchanged, E2E green. Roll back with a
   revert.

## Dependencies and related proposals
Lowers the conflict surface for every other backend proposal. A good first step in a round, because it is a small, mechanical win.

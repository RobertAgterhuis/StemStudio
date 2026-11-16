# Frontend hand-off: 011 quickstart fixes (branch `011-fix-ui`)

Fixes from the 011 quickstart on the owner's stack (`docs/STATUS.md` section 011: B1, O1-O4), each red-green.

| Item | Fix |
|---|---|
| B1 | `AgentPanel` placeholder comes from a C# constant (an entity in a Razor attribute was emitted literally) |
| O1 | `AgentPanelState` badge: a run's backend/model/fallback/reason only while the run has not ended; otherwise the status. The status is reloaded when a run ends (`ReloadStatusAsync`, also used by the 10 s refresh) |
| O2 | `mixer_set_channel` / `mixer_set_master` results add `clamped`, `requestedGainDb` (0.1 dB, null without a gain argument) and `maxGainDb` (0); `current` is the applied level. `Gain.MaxDb`, `Gain.IsCapped`, `Gain.Requested` |
| O3 | `AgentSavedMix.From` recognises a succeeded `save_mix_as`/`update_mix` step with a `mixId` summary; `AgentPanelState` calls `AgentUiToolHost.MixSavedAsync` -> `MixerUiTools.MixSavedAsync` -> new `IMixerControls.MarkSavedAsync`; the mixer page reads the mix, and when it belongs to its track marks it saved and navigates to `?mixId=` (replace). Replayed steps of a resumed run are skipped. The user's own Save as with the panel open already cleared the indicator (test added, it was green before) |
| O4 | `Components/Pages/NotFound.razor` (`/not-found`, `MainLayout`, `data-testid` `not-found`, `not-found-home`), `Router NotFoundPage`, `UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true)` in `Program.cs` (https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0#not-found-responses). `HelpCoverageTests` excludes `/not-found` from the one-screen-topic rule |

## Evidence

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: **Passed 1739, Failed 0**; `StemStudio.Web` line
**99.42 %**, branch **94.17 %**, method 99.68 % (gate 90/90). Release build of `StemStudio.Web` 0 warnings;
`dotnet format --verify-no-changes` clean. Only the Web tests were run (no Docker, no Playwright).

## Follow-ups

- ai-agent: the tool descriptions may tell the model to use `clamped`/`current` when it reports a gain change.
- `AgentUsageTests` now expects three status calls (open, run end, switch to local).
- No new dependencies.

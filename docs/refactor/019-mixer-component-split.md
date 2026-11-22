# 019 Split the Mixer page component into feature components with their own state
- Status: proposed · Date: 2026-11-19 · Area: frontend / StemStudio.Web Components/Mixer · Priority: P2

## Problem statement
`Components/Mixer/Mixer` is one class spread over **14 partial files and the markup** (1 434 lines):
`Mixer.razor` (192), `Mixer.razor.cs` (194), `Mixer.Agent.cs`, `.BandInfo.cs`, `.Controls.cs`, `.Detail.cs`, `.Export.cs`,
`.Practice.cs`, `.PracticeLog.cs`, `.Recording.cs` (188), `.Save.cs`, `.Score.cs`, `.Sections.cs`, `.Shortcuts.cs`.
Measured (grep over `Components/Mixer/Mixer*.cs`):
- 123 methods, 15 private fields and 30 private properties on one instance; 15 `[Inject]`/`[Parameter]`/`[CascadingParameter]`/
  `[SupplyParameterFromQuery]` members; 23 distinct `using` namespaces.
- Features talk to each other through `partial void` hooks across files: `OnEngineStarted` (declared `Mixer.razor.cs:115`,
  implemented `Mixer.Recording.cs:172`), `OnMixLoaded` (`Mixer.Save.cs:47` / `Mixer.Recording.cs:163`),
  `RegisterRecordingCommands` (`Mixer.Shortcuts.cs:32` / `Mixer.Recording.cs:103`). The lifecycle methods (`OnAfterRenderAsync`,
  `DisposeAsync`, `Mixer.razor.cs:70-112`) call into six features in a fixed order.
- Its model `Mixer/MixerState` is split the same way (9 partial files, 549 lines, 46 methods).
- Hotspot: `Mixer.razor` changed in 24 commits and `Mixer.razor.cs` in 15 since 2026-10-01 (`git log --since=2026-10-01`), the most of
  any Web file; every feature from 007 to 015 added a partial file here.

Each unit is within the SIG unit thresholds (a crude scan of all 971 Web C# units found none over 30 lines or McCabe 10 — the
script is in `.claude/scratchpad/main/units.py`), so the problem is at **component level**: partial files hide one class that
SonarQube and Sigrid see whole (class coupling, number of methods, component independence), and every change to the page is a
change to everything.

## Impact
- Delivery speed: parallel lanes (practice, recording, agent, score) collide in the same class and the same `.razor` markup —
  the 014+015 round needed a merge of both into this file set (`git log`: `7b5cd39e merge: 014 practice flow and 015 recording
  takes`).
- Defects: lifecycle ordering bugs such as the one in `010-mixer-engine-lifetime.md` arise because one method owns
  six features' start-up and teardown.
- Testability: bUnit tests must render the whole page with every service substituted to test one feature.

## Risk
Risk rating: **medium**.
- Leaving it: cost grows with each feature; the next features (more practice, more agent tools) will add partials 15 and 16.
- The change: UI regressions in the most used page. Mitigations: purely structural steps, one feature per PR, the existing test
  ids (`ui-test-ids`) unchanged, the Playwright visual and a11y projects as the regression net. Rollback per PR.

## Effort
L (6–10 days), one feature per step of roughly 0.5–1.5 days. Assumes the existing bUnit and Playwright suites stay green
unchanged and that test ids and DOM structure stay as they are.

## Step-by-step plan
1. Safety net: run `tests/StemStudio.Web.Tests` (3 500 tests) and the Playwright `visual` and `a11y` projects on `main`; record
   the baselines. Add one bUnit characterisation test per feature that is only covered through e2e today (check the coverage
   report: `Mixer.Agent.cs` is at 85 %).
2. Introduce a cascading `MixerSession` (scoped to one Mixer instance) that holds `MixerState`, the engine handle and the
   lifecycle events (`EngineStarted`, `MixLoaded`, `Disposing`) instead of `partial void` hooks.
3. Move one feature at a time into a child component or a small service that subscribes to `MixerSession`, starting with the
   most isolated: Export (14 lines), BandInfo (30), Score (41), then Agent, Sections, Detail, PracticeLog, Recording, Practice,
   Save, Shortcuts. Each PR deletes one `Mixer.*.cs` file and moves its markup block out of `Mixer.razor`. Check: tests green,
   visual baselines unchanged.
4. Split `MixerState` along the same lines where a feature owns its own state (Take, Export, Practice); keep a thin aggregate for
   dirty tracking and save.
5. End state: `Mixer` keeps routing, loading and layout only (target: under 300 lines in total, under 10 injected/parameter
   members). Verify with the same suites and with Sonar/Sigrid on the touched files; roll back per PR.

## Dependencies and related proposals
Do `010-mixer-engine-lifetime.md` first (small, fixes a defect, and its `MixerSession` lifetime guard is the seed of
step 2).

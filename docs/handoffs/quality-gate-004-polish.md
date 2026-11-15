# Hand-off: quality-gate / 004-polish (T066) - PRE-MERGE RUN, not final

T066 needs the merged wave: frontend's components, `src/web-interop/src/**`, `tokens.css` and
the retired `app.css`. This note records the run on the 004 branch with the quality-gate
changes only (base 6258eae + T006/T007). T066 stays **unticked**. The CTO re-runs the command
below after the merge and ticks T066 if it exits 0, or calls quality-gate back for refactor
requests.

## What was built
Nothing new beyond the foundational hand-off. This note is the pre-merge measurement and the
refactor requests for the files the 004 wave touches anyway.

## Files changed
- `docs/handoffs/quality-gate-004-polish.md` (this note)

## Tests and coverage
Same as `quality-gate-004-foundational.md`: 253 passed, tools/quality 100% line and 100% branch
(903 statements, 176 branches).

## Quality checks
```
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 577 files, 4455 units, 26 clones, 1 exemptions, 0 errors, 155 warnings -> exit 0
```
- Dependency direction: PASS (.NET, TS packages, worker).
- Design tokens: PASS with 14 warnings, all in the legacy `src/StemStudio.Web/wwwroot/app.css`.
- `web-interop` component: 14 units / 38 LOC, all in the low band (5 / 5 / 5).
- Checklist for the post-merge run:
  1. exit 0
  2. `## Design tokens` reads `**PASS** - no raw colour values outside tokens.css.` (`app.css`
     deleted by T020; a remaining `app.css` is a frontend finding)
  3. `web-interop` row present, with no unit above 15 LOC / CCN 5 / 4 parameters
  4. no `web-interop -> audio-engine` edge

## Dependencies added
None.

## Open issues / requests
Per owner, for the files the 004 wave rebuilds anyway (top of the current report):
- **frontend**
  - `Mixer/ChannelState.cs:37` `Apply`: CCN 9. Use a lookup table per setting.
  - `Components/Mixer/LoopControls.razor:26` (40-line `@code`) and `TransportBar.razor:17`
    (38 lines): move the logic to `.razor.cs` while re-skinning.
  - Unit size 16-19 LOC in `MetronomePanel.razor.cs:29` `Format`, `Mixes.razor.cs:58`
    `RenameAsync`, `Import.razor.cs:78` `RunImportAsync` and `Mixer.razor.cs:94`
    `StartEngineAsync`.
  - `MixerState.cs:19` constructor: CCN 6.
  - `tests/StemStudio.Web.Tests/Pages/TrackDetailActionsTests.cs:1`: 9 lines duplicated with
    `TrackListPageTests.cs`. Extract a shared fixture.
  - `wwwroot/app.css`: 14 raw colours, retire it in T020.
- **beat-analysis, audio-ingest** (not touched by 004): `downbeats.py:39` and `spotify.py:177`
  are unchanged soft warnings, to refactor when next touched.

## Next owner
CTO: merge, then re-run `run_all.py`, check the four points above and tick T066 (or hand back to
quality-gate).

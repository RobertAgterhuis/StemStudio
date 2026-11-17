# Hand-off: frontend / 015 polish (lane L6, T084, T086)

## What was built (whole lane, frontend part)
Recording interop and callbacks (foundational), recording panel, permission gate, input selector with ARIA meter, options,
latency status, Record/Stop with block reasons, uploader with streaming and polling, take strip (reused ChannelStrip),
Takes panel with sessions, stars, keep-starred, rename, delete, retry, details per level, calibration dialog, compare
control, take channel saved with the mix and restored, external solo, take export presets, timing adjustment, track delete
note, and 14 help topics (`Help/topics/mixer/recording*.md`, area `mixer` by orchestrator decision) - see the per-story
hand-offs `frontend-015-{foundational,us1,us2,us3,us4,us5,us6,us7}.md`.

T084: help topics of data-model 5 with real texts (overview with the privacy rule, permissions per browser, one connection
topic per instrument family, latency incl. the clap test, monitoring, loop takes, compare, export, the 100 % speed rule, limits
(two channels, 200 takes, Chrome/Edge), Expert timing). `RecordingHelpTests` (17) plus the existing help coverage tests
(every marker has a topic, anchors in markup). FeatureCatalog rows and the shortcut overview rows were delivered by L5
(hooks A and 5); per the orchestrator the overview ids are `shortcut-row-mixer-record-toggle` / `-compare-cycle`.

## Tests and coverage (final)
- `dotnet test tests/StemStudio.Web.Tests --settings tests/coverlet.runsettings`: **3383 passed, 0 failed**.
- StemStudio.Web total: line 98.7 %, branch 93.6 %.
- 015 frontend code (`Web/Recording/**`, `Components/Recording/**`, `Mixer.Recording.cs`, `AudioEngineInterop.Recording.cs`,
  `StemStudioApiClient.Recording.cs`, `Export/*.Take.cs`): **line 1591/1607 = 99.0 %, branch 612/666 = 91.9 %**.

## Quality checks
- `dotnet format StemStudio.slnx --verify-no-changes`: clean. Build 0 warnings.
- `uv run --project tools/quality --frozen python tools/quality/run_all.py`: PASS, 0 errors (jscpd ran: 387 clones repo-wide,
  none in 015 code). 015 warnings left (below the hard limits): `Mixer.TakeExportFor`/`MutedForCompare` complexity 6/7,
  six units of 16-19 lines, `InputMeter.razor` @code 34 lines.

## Dependencies added
none

## Docs impact
User manual chapter "Play-along recording" (see the story hand-offs); technical manual: `StemStudio:Recording:TestLoopback`,
`StemStudio:Recording:MaxStreamBytes` (Web), recording browser support (localhost / 127.0.0.1 only).

## Decisions / deviations (summary, details in the story hand-offs)
- Preferences in `localStorage["stemstudio.recording"]`; help area `mixer`; extra id `takes-error`; take export from the take
  row; `R` opens the panel first; a successful calibration is saved at once; `LoadTakeRequest.adjustmentSamples` always 0.

## Open issues
- `tests/fixtures/pyproject.toml` coverage source for the recording generator (L2/integration).
- The mixer's own export does not include the take (by design, see US6).

## Next owner
CTO integration (T087-T091).

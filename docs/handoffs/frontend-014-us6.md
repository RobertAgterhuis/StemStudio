# Hand-off: frontend 014, HO:us6 (lane L5, round 014+015)

US6 "Practice log and progress".

## Tasks (red -> green)

| Task | Commit |
|---|---|
| T098 failing tests `PracticePanelTests` (panel + recorder wiring), `PracticePageTests` | 509a2c76 |
| T099 `Components/Practice/{PracticeLogPanel,PracticeCharts}.razor`, `Components/Pages/PracticePage.razor`, `Mixer.PracticeLog.cs`, rail `nav-practice` | 5c88db3f |
| T101 help `mixer-practice-log`, `practice-privacy`; tour `practice-section` gets the `practice-log-panel` step | 5a510470 |

The panel component is `PracticeLogPanel` (012 already has `Components/Mixer/Practice/PracticePanel`, the speed panel);
the task text names `PracticePanel.razor`.

## Behaviour

- Mixer: `practice-log-panel-toggle` (all levels, `aria-expanded`) shows `practice-log-panel` (`aria-label="Practice log"`):
  `practice-log-today` / `-week` / `-total` ("12 min", "2 h 30 min"), `practice-log-sections-table` (Advanced+,
  FeatureCatalog `practice.log-sections`), `practice-log-chart-days` and (Advanced+, `practice.log-tempo`)
  `practice-log-chart-tempo` as SVG bars with a text summary in the figure caption; `practice-log-chart-as-table`
  swaps both for `-days-table` / `-tempo-table`; `practice-log-off-note` + `practice-log-turn-on`; first-use
  `practice-log-notice` + `-ok` / `-turn-off`; `practice-log-delete-track` + `practice-log-delete-confirm`.
- `/practice` (rail `nav-practice` after Setlists, screen topic `practice-privacy`): `practice-overview-table` with
  `practice-overview-row-<trackId>` (link to the mixer), `practice-enabled` (`role="switch"`), `practice-delete-all`
  (confirmation `practice-delete-all-confirm`), `practice-sessions` with `practice-session-<id>` /
  `practice-session-delete-<id>` (Expert, `practice.sessions`).
- Recorder wiring: after the engine has loaded the track the mixer reads the log switch; when on it creates a
  `PracticeRecorder` (writer `upsertPracticeSession`, the app `TimeProvider`), feeds it playback state, positions with
  the heard speed (`PracticeStatus.SpeedPercent`), `LoopPassed` with the trainer state and the loop's `sectionId`, and
  keeps its sections and mix current; it closes (final flush) when the mixer goes away. Turning the log on in the panel
  starts a recorder; a 409 `practice-log-off` stops it on its own.
- `utcOffsetMinutes` for the summary and overview comes from the app host's time zone (`TimeProvider.LocalTimeZone`):
  StemStudio runs on the user's computer, so it matches the browser; there is no browser time-zone interop.

## Quality

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Web (Release, coverage) | 3417 passed | 99.44 % | 93.34 % |
| web-interop | 412 passed | 99.8 % | 98.29 % |

Release build 0 warnings; format clean except L6's `RecordingApiClientTests.cs:103`; `run_all.py` PASS after moving the
two pages' error handling onto the existing `ApiCall.RunAsync` (jscpd had found a 16-line clone).

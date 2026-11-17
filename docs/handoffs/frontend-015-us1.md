# Hand-off: frontend / 015 US1 (lane L6, T040-T043)

## What was built
- **Mixer integration** (`Components/Mixer/Mixer.Recording.cs`, L5 hooks d9f8e1a2/9f507c5b): the page is the
  `IRecordingHost` of one `RecordingSession`, created lazily by the slots from the DI services (null without them, so other
  features' mixer tests are unaffected) and disposed by `RecordingArea` (microphone released when the mixer goes away,
  FR-003). `mixer.record.toggle` (R: opens the panel the first time, then Record/Stop) and `mixer.compare.cycle` (C, US4
  stub) are registered through `RegisterRecordingCommands`.
- **Slots** (`Components/Recording/`): `RecordingToolbarToggle` -> `RecordingToggleButton` (`mixer-record-panel-toggle`,
  `aria-expanded`), `RecordingArea` -> `RecordingPanel` + `TakesPanel` while the panel is open, `TakeStripSlot` -> `TakeStrip`
  (reuses `ChannelStrip` with `ChannelState("take")`, so every `*-take` id and level matches the stems; `data-take-id`).
- **Panel**: `PermissionGate` (allow, unavailable with `data-reason` insecure/unsupported/denied/not-found/busy/lost, try
  again), `InputSelector` (device select by label, mode Advanced, `InputMeter` with `role="meter"` whose `aria-valuenow`
  follows the engine's `data-peak-db` at 2 Hz through the collocated `InputMeter.razor.js`), `RecordOptionsForm` (part,
  mute original, count-in, monitoring + volume Advanced + warning), `LatencyStatus` (estimate/calibration/manual,
  samples Expert, setup changed > 220 samples, calibrate button), `RecordButton` (Record/Stop `aria-pressed`, block reasons
  speed/trainer/limit/input/track, "Set to 100 %" = the mixer's practice reset, live region, elapsed, pass, saving
  progress, error, save again).
- **Logic** (`Recording/`): `RecordingSession` (+ `.Takes.cs`: callbacks, de-duplicated capture handling, uploads,
  1 s polling), `InputSetup`, `LatencyChoice`, `TakeLibrary` (list newest first, strip, local -> saved FLAC switch, capture
  release), `TakeItem`, `TakeUploader` (sequential, `IJSStreamReference.OpenReadStreamAsync(MaxStreamBytes)` straight into
  the multipart request), `RecordingText`.
- **API client** partials for every recording operation of backend 1.14.0 (streaming multipart `audio` + `metadata`).
- `StemStudio:Recording:MaxStreamBytes` (Web, default 256 MiB) in `RecordingUiOptions`.
- Help: 14 stub topics `Help/topics/mixer/recording*.md` (area `mixer`, orchestrator decision; full texts in T084).

## Files changed
`src/StemStudio.Web/{Recording/**, Components/Recording/**, Components/Mixer/Mixer.Recording.cs, Api/*.Recording.cs,
Help/topics/mixer/recording*.md}`, `tests/StemStudio.Web.Tests/Recording/**`.

## Tests and coverage
- `dotnet test tests/StemStudio.Web.Tests --settings tests/coverlet.runsettings`: **3194 passed, 0 failed** (after merging
  L5 a4004559 and L1 84192c66).
- Web total: line 98.5 %, branch 93.5 %. 015 frontend code (`Web/Recording/**`, `Components/Recording/**`,
  `Mixer.Recording.cs`, `AudioEngineInterop.Recording.cs`, `StemStudioApiClient.Recording.cs`):
  **line 858/869 = 98.7 %, branch 356/392 = 90.8 %**.
- S-02 .NET half (`TakeUploadTimingTests`, Release): a 5-minute stereo 24-bit take (79,380,044 bytes) goes through
  `TakeUploader` + typed client into a request read to the end in **252 ms** (SC-004 budget 15 s). Circuit transfer and the
  real API: CTO T089.

## Quality checks
- `dotnet format StemStudio.slnx --verify-no-changes`: clean; build 0 warnings.
- `run_all.py`: PASS, 0 errors (jscpd ran, 369 clones in the repo, none in 015 code). Remaining 015 warnings: 7 production
  units of 16-19 lines / complexity 7 (`TakeLibrary.Request`) below the hard limits, test units 16-28 lines.

## Dependencies added
none

## Docs impact
User manual: recording panel (privacy note, allow microphone, input, part, mute original, count-in, monitoring with
headphones, latency status, Record/Stop and R, speed rule with "Set to 100 %", saving status, Save again, Takes panel,
take strip). Technical manual: `StemStudio:Recording:MaxStreamBytes` and `TestLoopback` (Web).

## Decisions / deviations
- `RecordingSession` loads takes and calibrations when the panel opens; the Takes panel is shown with the recording panel
  (US1). US4 (saved mix restores the strip) will load takes on mixer open.
- `rec-take-row-<id>` uses the capture key until the server accepted the upload (a GUID afterwards); E2E waits on the
  saved id (`newestSavedTakeId`).
- `R` with the panel closed opens it first (the input must be chosen before recording).
- "Set to 100 %" calls the mixer's practice reset (speed 100 %, transpose 0, same as "Original speed and key").

## Open issues / requests
- L5 hook C (`MixerState.IsAudible` with a soloed take) still open (US4).
- `tests/fixtures/pyproject.toml` coverage source for `recording.generate_recording_fixtures` (to L2).

## Next owner
L6 continues with US2 (T049-T051).

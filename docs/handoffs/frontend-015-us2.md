# Hand-off: frontend / 015 US2 (lane L6, T049-T050)

## What was built
- `Recording/CalibrationRun.cs`: one calibration of the open input. `StartAsync` calls `calibrateLatency()`, stores a
  successful result at once (`PUT /api/recording/calibrations`: label, device id hint, samples, `loopback`, spread, clicks,
  output latency snapshot), keeps a failure with its reason; the engine's refusal while playing is shown as the error.
  Manual value 0-1000 ms, 0.1 ms steps, stored as `manual` with `RecordingLimits.ToSamples` rounding. `DeleteAsync` forgets
  the calibration (the estimate is used again). Tips per failure reason (noSignal, tooFewClicks, inconsistent, tooLong).
- `Components/Recording/CalibrationDialog.razor(.cs)` (ModalDialog: focus capture/return, Escape): `cal-dialog`,
  `cal-instructions` (cable or headphone cup, device name), `cal-running` (`aria-busy`), `cal-result` (`data-ok`),
  `cal-result-ms`, `cal-result-samples` (Expert), `cal-result-clicks`, `cal-result-spread`, `cal-reason` (`data-reason`),
  `cal-try-again`, `cal-manual` / `cal-manual-ms` / `cal-manual-slider` / `cal-save` (Advanced), `cal-delete`, `cal-cancel`.
- Opened from `rec-calibrate` in the panel; `rec-latency-status` follows the stored method (loopback/manual/estimated) and
  `rec-setup-changed` the output-latency drift (> 220 samples), both from US1.

## Tests and coverage
- `dotnet test tests/StemStudio.Web.Tests --settings tests/coverlet.runsettings`: **3212 passed, 0 failed**.
- 015 frontend code: **line 977/988 = 98.9 %, branch 396/434 = 91.2 %** (`CalibrationDialogTests`: 18 cases).

## Quality checks
`dotnet format StemStudio.slnx --verify-no-changes` clean, 0 warnings; `run_all.py` passed at HO:us1, no new duplication
(rerun at HO:us3).

## Dependencies added
none

## Docs impact
User manual: calibration (cable / headphone cup, result, saved per input, failure tips, manual entry from Advanced,
forget a calibration, "setup changed" notice).

## Decisions / deviations
- A successful measurement is saved immediately (quickstart 2.3 "It is saved for this input"); `cal-save` saves the manual
  value. No engine cancel exists: Close during a measurement closes the dialog and the result is ignored.

## Next owner
L6 continues with US3 (T056-T058).

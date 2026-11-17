# Hand-off: frontend / 015 US7 (lane L6, T081-T082) + qa T083

## What was built
- `Recording/TakeLibrary.Adjust.cs`: live preview `setTakeAdjustment(samples)` (absolute relative to the take start, the
  approved L1 semantics), clamp +-200 ms, 0.1 ms rounding; Apply re-encodes through `captureWav(<takeId>)` and sends
  `PUT /api/takes/{id}/audio` with `startSample = max(0, start + shift)`, `lengthSamples` reduced by the part cut at sample 0,
  `adjustmentSamples = shift`; the take is `saving`, polled, and the strip reloads the new FLAC when ready; `AdjustmentNote`.
- `LoadTakeRequest.adjustmentSamples` is now always 0: the stored audio already carries every applied shift.
- `Components/Recording/TakeAdjust.razor(.cs)` in the take strip: `take-adjust` (slider, `aria-valuenow` ms), `take-adjust-ms`,
  `take-adjust-apply`, `take-adjust-reset` at Expert (`recording.timing-adjust`), `take-adjust-note` from Advanced.
- E2E `e2e/tests/recording-adjust.spec.ts` (T083): Expert, -10.0 ms applied -> start 441 samples earlier, adjustment -441,
  alignment revision 1, note "Timing adjusted by -10.0 ms".

## Tests and coverage
`TakeAdjustTests` (14). Web 3383 passed; 015 code line 99.0 %, branch 91.9 %.

## Process note (honest)
For T081/T082 the implementation commit (feat, "tests follow") came before the test commit c710cb9e; the tests are complete
and green, but this story did not follow red-first commit order.

## Docs impact
User manual: timing adjustment (Expert), note from Advanced.

# Hand-off: generative / 009 US5 - in-place restyle (T068-T069)

## What was built

- **ACE-Step**: `mode: inPlace` maps to `task_type="repaint"` on the whole summed source, with
  `repainting_start`/`repainting_end` set to the range and `chunk_mask_mode="explicit"`.
  `restyle_values()` is a pure function.
- **Fake engine**: in `inPlace` mode the take is the source, resampled to 48 kHz stereo
  (linear), with only the range replaced by the fake signal. Samples outside the range are
  identical within 24-bit quantisation. The take is as long as the source.
- **Handler and API**: the handler already passes the whole sum for `inPlace` (no cut). The API
  test checks that a 30 s source with a 10-25 s range gives 30 s takes.

## Red -> green

`424c2d2` (red) -> `699d5ef` (green). The API take-length test and the 44.1 kHz resample test
already passed at red, because the fake already wrote takes of the source length.

## Next owner

CTO (listening check of repaint quality, SC-004, is the owner's).

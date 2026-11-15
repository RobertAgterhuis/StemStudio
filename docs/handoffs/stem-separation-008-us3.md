# Hand-off: stem-separation / 008 US3 (T051)

## What was built

`tests/transcription/test_accuracy_integration.py::test_the_piano_crnn_reaches_f1_085_on_the_polyphonic_texture`
(`@pytest.mark.integration`): qa's `score_piano_texture_20s.wav` through the real piano CRNN
(`PianoTranscriber` + `TorchRuntime(8)` + the worker's device runner, checkpoint from the model
store, SHA-256 verified) and the notation cleaning/quantisation; prints the time per audio second.

Commit: `c92dfaa` (with T040).

## Measurement (this Windows machine, CPU, torch 2.15.0+cpu, 8 threads)

```text
score_piano_texture_20s.wav: F1 0.933 (105 raw notes), 0.503 s per audio s     (target >= 0.85)
2 passed in 12.11s
```

The checkpoint loads with `torch.load(..., weights_only=True)` (verified on the real file).

## Tests and coverage

Integration test only (deselected by default); the unit suite stays at 100 % (see us1).

## Quality checks

ruff / mypy clean for the file.

## Dependencies added

None (piano checkpoint CC BY 4.0, downloaded at first use; `PIANO_ATTRIBUTION` on every score).

## Open issues / requests

CTO (T080): re-measure on the GPU image (expected about 0.075 s per audio s, research.md).

## Next owner

CTO (reference machine).

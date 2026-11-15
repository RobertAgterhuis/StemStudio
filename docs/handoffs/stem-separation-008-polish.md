# Hand-off: stem-separation / 008 polish (T073)

## What was built

- Refactors for SIG limits: `transcription_pipeline` (`_drums`, `_on_detail_stem`,
  `_piano_on_cpu`), `api/routes._runtime_health`, `transcription/audio._read`,
  `main.default_transcription`; shared `lane_store.LaneJobStore` and `lane_service.LaneJobService`
  for the render and transcription lanes; docstrings on every public unit of `transcription/`
  (vendor excluded), `jobs/`, `api/`.
- Vendored sources with their records (`vendor_check.py`: 0 errors):

| Vendored package | Version / source | Licence | Record |
|---|---|---|---|
| `transcription/vendor/basic_pitch` | basic-pitch 0.4.0 (tag v0.4.0), PyPI sdist SHA-256 `6f48ac4b909c990fd59460622137a03b857c829bb1f3e65c71708da576ab68e5` | Apache-2.0 | LICENSE (unchanged), NOTICE (upstream NOTICE + vendoring record) |
| `transcription/vendor/piano_transcription` | piano_transcription_inference 0.0.6, PyPI sdist SHA-256 `b6dd00f9b4bcacb6140725f03b4139a5e0f4acd35a69e492a5d1f734ecfbd231`; LICENSE from upstream commit `566057930a8423aa31b91296e7002c513269446d` | MIT | LICENSE, NOTICE |

- `worker/pyproject.toml` (CTO-approved, commit `c7107d3`): `transcription/vendor` excluded from
  coverage, ruff and mypy like `detail/vendor`.

## Files changed

See the 008 foundational, us1, us3 and us4 hand-offs; polish commit `3e71a09`.

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
2357 passed, 3 skipped, 10 deselected in 106.37s
TOTAL 9419 stmts, 0 missed, 1070 branches, 0 partial -> 100 % (transcription/ without vendor,
jobs/, api/, alignment/: 100 %)
uv run pytest -m integration tests/transcription/test_accuracy_integration.py tests/alignment/test_align_clip.py
-> bass F1 1.000, piano F1 0.933, SC-003 20/22 (CPU)
```

## Quality checks

ruff check / format --check clean; mypy --strict src clean (tests: 10 audio-ingest errors in
`tests/engraving`); `sig_check.py` (alignment, transcription, jobs, api, main.py): 0 errors,
warnings only (`_job_warnings` CC 6, `fake._triads` CC 6, two test units); `run_all.py`:
1018 files, **0 errors**, 256 warnings.

## Dependencies added

None in the lock (vendored subsets above; soxr used directly, see the 006 request).

## Open issues / requests

- **architect**: `soxr` as a direct dependency + mypy override.
- **audio-ingest**: the mypy errors in `tests/engraving`.

## Next owner

CTO (008 T079 merge and gates, T080 reference machine, T081 licences).

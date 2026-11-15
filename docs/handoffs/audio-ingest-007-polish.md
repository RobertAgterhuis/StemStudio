# Hand-off: audio-ingest (agent B) / 007 polish (007:T064)

Follows `docs/handoffs/audio-ingest-007-foundational.md` (007:T011-T020).

| Task | Red | Green |
|---|---|---|
| 007:T064 mid-render I/O failures | `6078632` | `e3215b2` |
| 007:T014 SIG clone fix (before HO:foundational) | - | `a1592e3` |

## What was built

- SIG review of `render/` and `tests/render/`: every unit <= 15 LOC, complexity <= 5,
  <= 4 parameters, no clone (the `resolve` docstring that duplicated `ports.LibraryReader` was
  shortened to a reference, `a1592e3`). Docstrings on every public module, class and function.
- Hardening (`e3215b2`, tests `worker/tests/render/test_failures.py`):
  - a read failure in the middle of a file (libsndfile `RuntimeError`) is
    `RenderError("Cannot read <path>")` instead of an unexpected exception
    (`OpenInputs` now holds `(name, SoundFile)` pairs);
  - an encoder failure on `write` or on the final `close`/flush (`OSError` such as a full disk,
    or libsndfile's `RuntimeError`) is `RenderError("The export file could not be written")`; the
    encoder is closed (its own I/O errors suppressed) and the partial file deleted;
  - any other exception in pass 2 (notably `RenderCancelledError`) keeps its type and also
    removes the partial file.
- Measured locally (not a test): six 4-minute stereo 16-bit FLAC stems, gain 0.8, pan -0.3,
  master 0.9, 10 s blocks -> FLAC-24 in 3.6 s, MP3-320 in 6.6 s (Windows dev box), in line with
  research.md E-01.

## Files changed

- `worker/src/stemstudio_worker/render/{reader,executor,paths}.py`
- `worker/tests/render/test_failures.py` (new)
- `specs/007-export-library/tasks.md` (T064 ticked)
- `docs/handoffs/audio-ingest-007-polish.md` (this note)

## Tests and coverage

```text
cd worker
uv run pytest tests/render --cov=stemstudio_worker.render --cov-branch
  125 passed;  render/  344 stmts 0 missed, 60 branches 0 partial -> 100 % line, 100 % branch
    __init__ 10/10, dither 23/23 (2 br), encoders 59/59 (10 br), executor 116/116 (10 br),
    mixing 23/23 (6 br), paths 37/37 (12 br), reader 76/76 (20 br)
uv run pytest --cov --cov-branch                       (whole worker)
  999 passed, 3 deselected (integration); TOTAL 4210 stmts 0 missed, 468 branches 1 partial (pre-existing)
```

## Quality checks

```text
uv run ruff check src tests            All checks passed!
uv run ruff format --check src tests   all files already formatted
uv run mypy src tests                  Success: no issues found in 165 source files (strict)
python tools/quality/sig_check.py worker/src/stemstudio_worker/render worker/tests/render
  13 files, 162 units, 0 errors, 0 warnings, 0 exemptions
python tools/quality/run_all.py
  653 files, 4970 units, 29 clones, 1 exemption, 1 error, 162 warnings
```

`render/**` and `tests/render/**` contribute 0 errors. The single `run_all` error is outside
this lane and untouched since Phase 0 (`git diff d2e06a7 -- e2e src tools` is empty):
`e2e/support/preferences.ts:12` duplicates `src/web-interop/src/types.ts:8` (16 lines). The
only `render` warning is "render is not in the dependency map" (quality-gate 007:T006/T007).

## Dependencies added

None.

## Open issues / requests

Unchanged from the foundational hand-off (quantisation scale `2^(b-1)` vs data-model's
`2^(b-1) - 1`, pan-0 row of the table, dither on 24-bit, MP3 decoded-length tolerance, extra
error texts, `peakDbfs` floor). Additionally:

- **quality-gate**: add `"render": _ADAPTER` to `PYTHON_ALLOWED` in
  `tools/quality/dependency_check.py`; look at the pre-existing e2e/web-interop clone.
- **stem-separation**: map `RenderError` to the job `error` with `str(error)`; every message from
  `render/` is readable and contains no secrets (only library-relative paths).

## Next owner

stem-separation (007:T021-T024, T033/T034), CTO for the integration wave.

# Hand-off: stem-separation / 006 polish (T057)

## What was built

- Refactors for SIG limits: `alignment/mapping` (`_span`, `_words_span`, `_short_share`,
  `_overlap_share`; every unit CC <= 5), `alignment/stable_ts_aligner._Run` (`attempt` /
  `_result`), `alignment/fake._spread`; `jobs/job_records.JobRecordStore` shared by the metadata
  and lyrics stores (removed a 8-line clone), de-duplicated lyrics route imports.
- Docstrings on every public module, class and function of `alignment/`, `jobs/lyrics_*`,
  `api/lyrics_routes.py`; lyrics text is never logged (unexpected errors log the exception type
  only; tested with the fixture text).

## Files changed

See `stem-separation-006-us2.md`; polish commit after `3f4371c`.

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
2237 passed, 8 deselected
TOTAL 8534 stmts, 0 missed, 998 branches, 0 partial -> 100 % (alignment, jobs, api: 100 %)
uv run pytest -m integration tests/alignment/test_align_clip.py -s  -> SC-003 20/22, 1 passed
```

## Quality checks

ruff, ruff format, mypy --strict clean; `sig_check.py` (alignment, jobs, api, main.py):
0 errors, 0 warnings; `run_all.py`: 0 errors, 240 warnings (none in an owned source file).

## Dependencies added

None (see the us2 hand-off for the direct use of `soxr`).

## Open issues / requests

As in `stem-separation-006-us2.md`.

## Next owner

CTO (006 T064 merge, T066 reference machine).

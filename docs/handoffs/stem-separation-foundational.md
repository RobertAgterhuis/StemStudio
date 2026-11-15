# Hand-off: stem-separation / foundational

## What was built
- T035/T036: `jobs/store.py` `JobStore`: one `jobs/<jobId>.json` per job, written to `<id>.json.tmp`
  and atomically replaced; thread-safe in-memory index; `new_job_id()` = `uuid4().hex`;
  `get()` rejects ids that are not 32 lower-hex (no path traversal); `load_all()` skips corrupt
  files with a warning; `find_active(source_key)` returns the newest non-failed job.
- `jobs/models.py` `StoredJob` (wire `Job` + internal `uploadFile`, `audioSha256`, `result`) and
  helpers `metadata_of(SourceInfo)` / `source_of(Job)` (titles clipped to 500, empty -> "Untitled").

## Red -> green evidence
| Task pair | Failing test commit | Implementation commit |
|---|---|---|
| T035 -> T036 | 5354406 | 8dd1d27 |

## Tests and coverage
`uv run pytest tests/jobs -q --cov=stemstudio_worker.jobs --cov-branch`: 12 passed;
`jobs` 66 statements, 6 branches, 100 % line, 100 % branch.

## Quality checks
`ruff check src tests`: all passed; `ruff format --check`: 18 files formatted; `mypy src tests`
(strict): no issues in 18 files. `tools/quality/sig_check.py` does not exist yet in this worktree;
all units are <= 15 LOC, CC <= 3, <= 2 parameters.

## Dependencies added
None.

## Open issues / requests
None at this checkpoint.

## Next owner
stem-separation continues with US1 (T056..T068).

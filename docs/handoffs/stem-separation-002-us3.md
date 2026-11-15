# Hand-off: stem-separation / 002-us3 (progress, failure, retry, cancel)

## What was built

- Tests of the whole job subsystem (`WorkerRuntime` with the real scheduler, runners and
  `DetailPipeline` over fake detail models) for SC-006 and US3:
  - a standard job submitted while a detail step runs starts right after that step (measured
    < 5 s after the step returns; in practice a few ms);
  - cancel during a step stops it at the next inference chunk (<= 2 chunks), status `cancelled`;
  - retry after cancel resumes from the finished parent checkpoints: the new job's first stage
    is "Splitting vocals (1 of 4)" (drums skipped); retry after a failure with all checkpoints
    completes without any model call.
- New behaviour (T066): while a standard job runs ahead of active detail jobs, those jobs report
  stage **"Waiting for standard separations"** (contract example text), keeping status and
  progress, `currentParent = null`. `PriorityScheduler(..., on_yield)` tells the active detail
  ids right before such a standard job (a failing hook is logged and never blocks the standard
  job); `DetailRunner.waiting(ids)`, `DetailTracker.waiting()`.
- Coverage gaps closed: unknown id in `DetailJobStore.update`; the step gate is now a
  `TypeGuard` (no unreachable branch).

## Red -> green commits

| Tasks | Red | Green |
|---|---|---|
| T065 / T066 | `950e96d` | `8a137b3` |

The priority/cancel/retry tests of T065 passed on their first run (the behaviour came with
T023/T045/T047); the waiting-stage tests were red and drove T066.

## Files changed

- `worker/src/stemstudio_worker/jobs/{scheduler,detail_runner,detail_progress,detail_texts,runtime}.py`,
  `worker/src/stemstudio_worker/detail/steps.py`
- `worker/tests/jobs/{test_detail_lifecycle (new),test_scheduler,test_detail_runner,test_detail_store}.py`

## Tests and coverage

```text
cd worker && uv run pytest --cov
643 passed, 3 deselected; total line 100.00 % (2999/2999), branch 99.71 % (345/346)
(the one partial branch is the scheduler loop exit `127->exit`, reached only by stop()).
```

## Quality checks

```text
ruff check, ruff format --check, mypy --strict: clean
tools/quality/run_all.py: non-vendor worker code 0 errors (vendored MSST errors pending
quality-gate T007, see the US1 hand-off).
```

## Dependencies added

None.

## Open issues / requests

- backend: the worker now reports "Waiting for standard separations" with `currentParent = null`
  while a standard job runs ahead; the detail section can show it as is.

## Next owner

stem-separation continues with T082 (polish).

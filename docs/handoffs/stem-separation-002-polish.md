# Hand-off: stem-separation / 002-polish (T082)

## What was built

- `worker/scripts/check_reconstruction.py <trackDir>` (quickstart D3) on top of
  `stemstudio_worker.detail.verify`: for every `detail/<parent>/` below a backend track directory
  (`<DataDir>/tracks/<id>`) or a worker cache entry (`cache/by-audio/<sha>`) it sums the detail
  stems, compares them with `stems/<parent>.flac` (<= -30 dB), checks that `rest` plus at least
  two sub-stems exist and that every sub-stem passes the audibility gate; prints one line per
  parent; exit 0 = all pass, 1 = a parent fails, 2 = usage error.
  Run: `uv run --project worker python worker/scripts/check_reconstruction.py <trackDir>`.
- SIG polish: `jobs/runner.process` split (complexity 7 -> under 5), `detail/demix.demix`
  (17 -> 15 LOC), verify CLI split; a convoluted runner test simplified.

## Red -> green commits

| Task | Red | Green |
|---|---|---|
| T082 | `518eea0` | `ab4fd7d` (+ test cleanup `535d295`) |

## Complete commit list of the wave (stem-separation, feature 002)

Foundational: `830b86f` `57c2357` `f62a13c` `0b96c28` `75ace8f` `c121f9d` `2b91903` `dcfa25d`
`a559d0f` `c53696e` `14d49d5` `76952b1` `18bc703` `89f4a04` (hand-off).
US1: `f8a9214` `ef42702` `0c95009` `5f564c5` `52fe51f` `e9a6e7f` `96d345a` `110f7ef` `bebd848`
`0dac1b8` `b9a74d9` `9111c73` `11df75f` `7399f54` `4798da7` (hand-off).
US3: `950e96d` `8a137b3` `2721868` (hand-off). Polish: `518eea0` `ab4fd7d` `535d295` + this note.

## Tests and coverage

```text
cd worker && uv run pytest --cov
650 passed, 3 deselected (2 Demucs/ingest integration + 1 detail integration)
worker total: line 100.00 % (3056/3056), branch 99.71 % (349/350)
stem-separation component (detail, jobs, api, separation, main):
  line 100.00 % (2167/2167), branch 99.59 % (245/246)
  detail/  line 100.00 % (868/868), branch 100.00 % (86/86)   (vendor/** omitted, ADR-010)
  jobs/    line 100.00 % (845/845), branch  99.00 % (99/100)  (scheduler loop exit on stop())
  api/     line 100.00 % (265/265), branch 100.00 % (42/42)
  main.py  line 100.00 % (35/35)

uv run pytest -m integration tests/detail/test_detail_integration.py -s
1 passed in 42.1 s; detail wall time 41.1 s (drums 8.6 s, vocals 2.4 s, guitar+other 30.0 s)
on the 10 s synthetic band, CPU only, models cached in worker/data/models (git-ignored).
First run downloaded and SHA-256 verified 1,859,359,312 B.
```

## Quality checks

```text
uv run ruff check src tests scripts       All checks passed!
uv run ruff format --check                 124 files already formatted
uv run mypy src tests scripts (strict)     Success: no issues found in 130 source files
uv run --project tools/quality python tools/quality/run_all.py
  quality gate: 419 files, 3161 units, 20 clones, 17 errors, 131 warnings -> exit 1
  17/17 errors are in worker/src/stemstudio_worker/detail/vendor/msst/** (vendored MIT code,
  pending the quality-gate exclusion T006/T007); 0 errors elsewhere. Remaining warnings in my
  production code: main.create_app 16 LOC (soft limit 15). Test helpers have a few soft
  unit-size warnings (16-22 LOC).
```

## Dependencies added

None in this task (see the US1 hand-off for the vendored MSST code and the MDX-Net wrapper
credit).

## Open issues / requests

1. quality-gate: exclude `detail/vendor/**` and add `detail` to the dependency map (T007) - the
   only reason `run_all.py` exits 1.
2. architect: confirm the 3-line change of `worker/tests/test_detail_contract.py` (stub test
   replaced, see the US1 hand-off).
3. devops: `worker/data/models` is the local default for `STEMSTUDIO_MODEL_DIR` in the
   integration test; CI should set `STEMSTUDIO_MODEL_DIR` to the cached model volume (T008).
4. CTO (T092): the real-song SC-001 measurement still has to be done on the reference machine;
   on the 10 s fixture the instrument step (Mega 53) dominates as measured in research.md.

## Next owner

CTO integration wave (T089-T093).

# Hand-off: stem-separation / 020 US1 (T040, T041)

Lane WORKER-IO, branch `020-lane-worker-io`.

## What was built

- `POST /v1/quality-jobs` (202 + `Location`), `GET /v1/quality-jobs/{id}`, `DELETE` (204,
  idempotent, also for unknown or malformed ids) in `api/insights_routes.py`.
- `jobs/quality_service.py`: `QualityService` over the insights lane. Create: every stem path must
  match `ports.is_quality_input_path` (stems and detail stems only; PDFs, artwork, mixes refused
  with 400 `invalid-input-path`), then the library path guard (400 for an escape, 404
  `input-not-found`). Run: paths resolved again, `StemAudio` in request order, `QualityInputs`
  with the 002 detail metrics; stages `loading` 0-0.2, `analysing` 0.2-0.9, `rating` 0.9-1.0;
  delete cancels at the next progress report; a finished job has `stage: null`;
  `QualityAnalysisError` / library errors fail with their text, anything else with the generic
  text. `UnavailableAnalyzer` (version `0.0.0`) fails every job readably when the configured
  analyzer cannot be imported.
- `jobs/insights_service.py` (base of the quality and handout services), `jobs/insights.py`
  (`InsightsParts`, `InsightsServices`, `start_insights`), `jobs/input_guard.py` (`check_input`
  with a per-kind predicate, now also used by the 014 practice services).
- Composition (`main.py`): `QUALITY_ANALYZERS` = `real` -> `insights.analyzer:SignalQualityAnalyzer`,
  `fake` -> `insights.fake:FakeQualityAnalyzer` (built with no arguments through the 016 module
  lookup); `create_quality_analyzer(settings)`, `default_insights(settings, overrides)`,
  `AppOverrides.quality_analyzer`. `Health.insights` = analyzer kind from
  `STEMSTUDIO_QUALITY_ANALYZER`, `analyzerVersion` = the analyzer's `version`, renderer and fonts
  from `handouts/health.py` (probed once); `Health.lanes.insights` from the lane.
- Health bodies in `tests/api/test_jobs_api.py` and `tests/test_smoke.py` now take the analyzer
  version and the renderer/fonts probe from the code (they differ between the Windows host, the
  image and CI).
- Refactors: `LaneJobService.process/run` shared (removed an 11-line production clone with the
  practice services); `Lanes.start` back within 15 lines.

## Commits

| Task | Red | Green |
|---|---|---|
| T040 / T041 | 542fc381 | 1864e72c (+ refactor commit after it) |

## Tests and coverage

Full worker default suite on the Windows host: **3903 passed**, 10 skipped (WeasyPrint/symlink),
12 deselected; package **99.9 % line, 100 % branch** (14,859 statements; the 18 missed lines are
`handouts/fetcher.py`, covered only where Pango exists: 100 % in the Linux image).

| Module | Line | Branch |
|---|---|---|
| `api/insights_routes.py` | 100 % | 100 % |
| `api/routes.py` | 100 % | 100 % |
| `jobs/quality_service.py` | 100 % | 100 % |
| `jobs/insights_service.py` | 100 % | 100 % |
| `jobs/insights.py` | 100 % | 100 % |
| `jobs/input_guard.py` | 100 % | 100 % |
| `jobs/practice_service.py` (changed) | 100 % | 100 % |
| `jobs/runtime.py` | 100 % | 100 % |
| `main.py` | 100 % | 100 % |
| `handouts/health.py` (audio-ingest) | 100 % | 100 % |

Tests: `tests/api/test_quality_routes.py`, `tests/api/test_insights_composition.py`,
`tests/handouts/test_health.py`; fake `tests/api/insights_fakes.py::FakeQualityAnalyzer` with the
data-model 2.6 ratings (vocals fair/bleed from drums, drums good, bass good, guitar poor with
flicker, piano not-present, other fair/bleed from guitar, detail good, rest fair with
`rest-share`, version `fake-1`); bodies conform to the 1.11.0 contract (`assert_conforms`).

## Quality checks

`ruff check`, `ruff format --check`, `mypy --strict src tests` (493 files): clean.
`run_all.py` on jobs/handouts/api/main + their tests: PASS, 0 errors; jscpd output read: no
production clones; test clones (route test helpers like the 014 ones) are warnings.

## Security self-review

Untrusted input = stem paths. Per-kind allow-list first (regex `fullmatch`: no `..`, no
absolute path, no other file type), then the library guard (`resolve(strict=True)` inside the
real root, symlink escapes refused: test skips on the Windows host, runs on Linux). Paths are
resolved again when the job runs (a file deleted meanwhile fails the job with its path).
Job ids are generated; delete of a malformed id is a no-op.

## Interface for beat-analysis (please confirm at integration)

The worker builds `stemstudio_worker.insights.analyzer.SignalQualityAnalyzer()` (real) and
`stemstudio_worker.insights.fake.FakeQualityAnalyzer()` (fake) with no arguments, each with a
`version` attribute/property and `analyze(inputs, progress)`. If the beat lane chose other names,
only `main.QUALITY_ANALYZERS` changes. The thread cap is applied by the lane
(`threadpoolctl`), not by the analyzer.

## Deviations

None. Decision: `DELETE` answers 204 for a malformed id too (the contract declares only 204).

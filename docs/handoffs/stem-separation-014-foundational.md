# Hand-off: stem-separation / 014 T030-T033 (L2, foundational worker part)

## What was built

- `jobs/practice_store.py`: three `LaneJobStore`s (`section-jobs/`, `score-view-jobs/`, `setlist-document-jobs/`,
  each `<id>.json` + `<id>/` for files; restart -> `failed` "interrupted by restart" with files dropped; delete
  idempotent; 24 h sweep). The 1.8.0 wire jobs have no `updatedAt`, so the stored record keeps a
  `Stamped*Job` (wire job + `updated_at`); `wire_job()` returns the contract model. `PracticeStores` bundles them.
- `jobs/practice_lane.py`: `PracticeLane` (one `LaneScheduler` "practice", FIFO across the three kinds via
  `submit(process, job_id)`, `health()` = `Health.lanes.practice`, `sweep()` of all three stores) and
  `PracticeParts` (the analyser; `structure_health()` = `Health.structure`).
- `jobs/paths.py`: `section_jobs`, `score_view_jobs`, `setlist_document_jobs`.
- `jobs/runtime.py`: `Lanes.practice` / `Lanes.practice_parts`, started after restart handling, swept hourly,
  stopped with the other lanes; `WorkerParts.practice`.
- `api/routes.py`: `/health` reports `lanes.practice` and `structure` (approved by the orchestrator: L2 owns the
  whole worker this wave except the contract shapes).
- `analysis/structure/fake.py` (`FakeStructureAnalyzer`, data-model 2.4), `analysis/structure/names.py` (kind and
  004 channel display names, numbering per kind), `analysis/structure/analyzer.py` (placeholder
  `StemsNoveltyAnalyzer`, fails with a `StructureError`; replaced by T051), `main.create_structure_analyzer`
  (fake/real by `WorkerSettings.structure_analyzer`) and `AppOverrides.structure_analyzer`.
- Pre-existing mypy errors in `tests/contract/test_practice_contract.py` (Phase 0) fixed (protocol attribute
  types, enum statuses) - `mypy src tests` was red at the design head.

Fake rules as implemented: bars start at the grid's downbeats and continue with the grid tempo (bar = beatsPerBar x
60 / bpm) after the last downbeat; without a grid (or bpm 0) 2 s bars from 0; intro = 0 .. bar 4, then 8 bars per
section through `verse, chorus, verse, chorus, solo (guitar), chorus, outro`; the last section created runs to the
end (outro for long songs, the 8th section takes the rest); `maxSections` stops early; snap `downbeat` (reliable),
`beat` (unreliable, warning `grid-unreliable`), `time` (no grid, no warning); clusters A..E by kind; confidence
0.9; progress 1.0 once; reads no audio.

Deviation (internal only, no contract): score-view and setlist jobs keep the record at `<dir>/<id>.json` beside
the `<id>/` folder (the shared `LaneJobStore` layout of 007/008) instead of `<id>/job.json` (data-model 2.8).

## Tests and coverage

`cd worker && uv run pytest --cov --cov-branch`: **2607 passed**, 3 skipped (10 integration deselected); total
line 99.98 %, branch 100 %. New files: `practice_lane.py` 100/100, `practice_store.py` 100/100, `fake.py` 100/100,
`names.py` 100/100, `analysis/structure/__init__.py` 100; `analyzer.py` 80 % (placeholder, replaced in T051).
New tests: `tests/jobs/test_practice_lane_store.py` (10), `tests/analysis/structure/test_fake.py` (14),
`tests/analysis/structure/test_names.py` (8), `tests/api/test_practice_health.py` (3).

## Quality checks

ruff check, ruff format --check, `mypy src tests` (strict config): clean. `run_all.py`: PASS, no finding in the
new production files.

## Dependencies added

none

## Docs impact

none yet (`/health` gains `structure` and `lanes.practice`, contract 1.8.0 already documented).

## Next owner

L2 itself (T020-T029, T050-T053); backend L3 can rely on `Health.structure` from the running worker.

# Hand-off: stem-separation / 014 T052-T053, T076-T077, T086-T087 + 015 T011 (L2, HO:polish)

## What was built

- `jobs/practice_service.py`: `SectionService`, `ScoreViewService`, `SetlistDocumentService` on the shared
  `practice` lane (`PracticeServices`, `Lanes.practice`), FIFO with one job at a time. Create checks: every stem
  path matches `STEM_PATH_PATTERN` and names its own field (`stems.vocals` -> `.../stems/vocals.flac`), every
  detail path `DETAIL_PATH_PATTERN` of its channel (`other.woodwinds` -> `.../detail/other/woodwinds.flac`), the
  score `SCORE_MUSICXML_PATTERN`; then the library path guard (400 `invalid-input-path`, 404 `input-not-found`).
  Without a readable library the job is queued and fails with the library text when it runs (as 007).
  Progress through the analyser; a progress report of a deleted job stops it. Pages `page-NNN.svg`,
  `setlist.pdf`. Stand-ins (`NoAnalyzer`, `NoScoreView`, `NoDocuments`) when a part is not composed.
- `jobs/lane_service.py`: a service may run on a shared lane (`lane: str | LaneScheduler`, `_submit`,
  `_run_job`); `RenderService` uses `_run_job` too (jscpd clone removed).
- `api/practice_routes.py`: the eight 1.8.0 paths; 202 + `Location`; 409 `job-not-completed` for pages/file before
  completion; 404 unknown job or page; `DELETE` 204 (idempotent), 400 for a malformed id. `api/problems.py`:
  `path_guarded()` shared with the render routes.
- `main.py`: `create_structure_analyzer` (fake/real), `default_practice` (analyser, `VerovioScoreViewRenderer`,
  `ReportLabSetlistRenderer`), `AppOverrides.{structure_analyzer, score_view_renderer, setlist_renderer}`.
- 015 T011 `tests/render/test_take_conversion.py`: green on the existing renderer (padded 24-bit mono/stereo WAV
  -> FLAC 24 identical integers, mono on both channels, leading zeros kept; all-zero -> "Nothing to export").

## Tests and coverage

`tests/api/test_section_routes.py` (19), `test_score_view_routes.py` (11), `test_setlist_document_routes.py` (9),
`test_practice_service.py` (6), `test_practice_health.py` (3), `tests/jobs/test_practice_lane_store.py` (10),
`tests/render/test_take_conversion.py` (3); responses checked against the 1.8.0 document (`assert_conforms`).
Worker: **2754 passed**, 3 skipped, 10 integration deselected; **line 100 %, branch 100 %** (11,811 statements,
1,350 branches).

## Quality checks

ruff, ruff format, `mypy src tests` clean; `run_all.py` PASS (0 errors; no warning in the new production files;
jscpd ran: 365 clones in the repo, none new in production code).

## Security self-review

Inputs are library-relative paths: per-field patterns, the allow-list, `resolve(strict)` and containment in the
real library root; hostile cases tested (`..`, another stem's file, a detail of another channel, a score path to
another asset file or a PDF). Page numbers are integers formatted into the file name (no traversal); job ids are
checked by the store (32 hex). Setlist content is bounded by the schema (422 for >100 songs, lines >500).

## Dependencies added

none

## Open issues

- The API (L3/L4) reads `ScoreViewResult.pages` as `{page, widthPx, heightPx}` (schema), not the data-model's
  `{index, file, ...}` sketch; pages are fetched at `/v1/score-view-jobs/{id}/pages/{page}`.

## Next owner

Integration; L3 for the backend consumer (worker client) against these routes.

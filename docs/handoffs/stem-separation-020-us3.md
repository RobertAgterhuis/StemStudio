# Hand-off: stem-separation / 020 US3 (T074, T075)

Lane WORKER-IO, branch `020-lane-worker-io`.

## What was built

- `api/handout_routes.py`: `POST /v1/handout-jobs` (202 + `Location`), `GET`, `DELETE` (204,
  idempotent), `GET /v1/handout-jobs/{id}/file` (PDF with `Content-Length`; 409
  `handout-not-completed` "Job is in state <state>." before completion; 404 when gone).
- `jobs/handout_service.py`: per-field allow-list (artwork must match `ARTWORK_PATTERN`,
  notation `HANDOUT_PDF_PATTERN`; stems, MusicXML, `..`, absolute, backslash paths -> 400),
  then `input_guard.resolve_contained` inside the real library root (escape by symlink -> 400,
  missing or folder -> 404). Run: paths resolved again, the renderer gets `{library path: real
  path}` as `files`; result `HandoutResult` (size, SHA-256, pages, outline, warnings); PDF at
  `handout-jobs/<id>/handout.pdf`. Stages `layout` 0-0.6, `notation` 0.6-0.9, `writing` 0.9-1;
  a render error without text gets "The handout could not be written".
- Composition: `AppOverrides.handout_renderer`, `default_handout_renderer` ->
  `handouts.renderer.WeasyHandoutRenderer` (imports WeasyPrint only when rendering, so the worker
  starts without Pango; `Health.insights.renderer` then reports nulls).

## Why a second path guard

The library reader's guard (`render/paths.py`) only accepts the general allow-list, which by
design never includes PDFs or JPEGs (data-model 2.2 "the general allow-list ... unchanged"). The
handout inputs are therefore resolved by `jobs/input_guard.resolve_contained` over
`STEMSTUDIO_LIBRARY_DIR` with the same rule (real path inside the real root, regular file). No
port or contract change.

## Commits

| Task | Red | Green |
|---|---|---|
| T074 / T075 | d07e41c2 | 84d3733b |

## Tests and coverage

`tests/api/test_handout_routes.py` (30 cases, bodies from the contract example, `assert_conforms`
on every response), `tests/jobs/test_input_guard.py`. `handout_service.py`, `handout_routes.py`,
`insights.py`, `insights_service.py`, `main.py`: 100 % line and branch; `input_guard.py` 100 %
over both platforms (the symlink escape runs in the Linux image, line 40 on Windows).

## Security self-review

Untrusted: the document (paths and texts). Paths: per-field regex `fullmatch` first, then the
contained resolver; checked again when the job runs. Texts never reach the file system. Job ids
are generated. The PDF is served only for a completed job of a valid id.

## Deviations

None. Decision: the per-field check is stricter than "any handout pattern" (a PDF as artwork or
a cover as notation is refused with 400).

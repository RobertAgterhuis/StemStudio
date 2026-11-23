# audio-ingest (worker fix agent): 020 integration

Branch `020-insights-handouts`, contract worker-api 1.11.0-delta.3 (`c5cb7f5f`). Logs:
`.claude/scratchpad/020-round/integration/worker/`.

## Commits

| Commit | Subject |
|---|---|
| `cdcf97af` | test(worker): red tests for the notation-omitted and artwork-omitted warnings |
| `ce1f566b` | feat(worker): warn notation-omitted and artwork-omitted in handouts (worker 1.11.0) |
| `87cea80b` | build(worker): declare fonttools and threadpoolctl as direct pinned dependencies |
| `07dcd6d5` | build(docker): install libharfbuzz-subset0 in the worker image for WeasyPrint |
| `fc3dfaa3` | test(insights): make the insights tests pass mypy --strict |
| `f7094483` | test(quality): red test, handouts may import PIL for the artwork size check |
| `ac6a8af4` | fix(quality): allow PIL in handouts for the artwork size check |

## 1. Warning codes (worker 1.11.0, additive)

`schemas.HandoutWarningCode` = `[missing-glyphs, notation-scaled, notation-unreadable,
notation-omitted, artwork-omitted]` (contract enum test compares with the regenerated contract).

| Code | When | Text (`ports`) |
|---|---|---|
| `notation-omitted` | an item over the per-job frame cap (600) or the distinct-notation byte cap (128 MiB) | `notation_omitted_text(title)`: `A notation file was left out because the handout reached its size limit: <title>` |
| `notation-unreadable` | missing file, gone since the job started, parse/limit error (unchanged) | `A notation file could not be read: <title>` |
| `artwork-omitted` | an allowed artwork file that `resources.artwork_fits` refuses (not a JPEG, over 40 MP) | `artwork_omitted_text(song_title)`: `The cover picture was left out because it is not a JPEG or is larger than 40 megapixels: <song title>` |

No artwork path, or no file for it: no warning (unchanged). The note on the page has the same
text as the warning. Texts are cut to 300 characters and each text is added once. A notation title
is at most 120 characters, so a notation text is never cut; the artwork text with a 300-character song
title is cut (tested).

Renderer: `_NotationBudget.admit` returns the page sizes or a `Refusal`
(`"notation-unreadable" | "notation-omitted"`) instead of `None`. `_Job.note` builds the note
and its warning.

## 2. Dependencies

`uv lock`: only the worker's own `dependencies`/`requires-dist` entries were added; no other pin
changed. OSV (`api.osv.dev`, the database pip-audit reads): no known vulnerabilities for either.

| Name | Version | Licence | URL | Reason |
|---|---|---|---|---|
| fontTools (`fonttools`) | 4.68.0 | MIT (`LICENSE.external` covers bundled third-party data) | https://github.com/fonttools/fonttools | `handouts/glyphs.py` reads the font cmaps; was transitive through WeasyPrint |
| threadpoolctl | 3.8.0 | BSD-3-Clause | https://github.com/joblib/threadpoolctl | `jobs/insights_lane.py` caps the BLAS threads; was transitive through scikit-learn |

Image: `libharfbuzz-subset0` 10.2.0-1+deb13u1 (Debian trixie, MIT, HarfBuzz). WeasyPrint subsets
the embedded fonts with it; without it, WeasyPrint falls back to fontTools with a warning. The
earlier image did not have it (`harfbuzz_subset` False), the new one does (True).

## 3. Image check

Built `cto020/worker:test` from `docker/worker.Dockerfile` (two earlier tries failed on Debian
mirror `Hash Sum mismatch` errors for ffmpeg packages, which have nothing to do with this change). The image ran
`tests/handouts`, `tests/jobs/test_handout_limits.py` and `tests/jobs/test_input_guard.py`
(script: `.claude/scratchpad/020-round/integration/worker/run-linux.sh`, worktree mounted
read-only, no network): 140 passed, 0 skipped; `stemstudio_worker.handouts` 100 % line /
100 % branch (719 statements, 124 branches).

## 4. Quality-gate dependency map

`insights` is already mapped: `tools/quality/dependency_check.py` `PYTHON_ALLOWED["insights"]`
(ports, schemas), `PYTHON_EXTERNAL_ALLOWED["insights"]` (numpy, scipy, soxr, soundfile),
`components.py` `worker/insights`, owner in `common.py` (qa-ops `da9b38e7`, tested in
`tools/quality/tests/test_insights_handouts_rules.py`). No change needed for `insights`.

Found while running the gate: `handouts/resources.py` imports `PIL` (`artwork_fits`, worker-io
`b0cc76d0`), which the map did not allow (error `handouts -> PIL`). Now allowed (`f7094483` red,
`ac6a8af4` green). Pillow is already a direct worker dependency.

## Checks

| Check | Result |
|---|---|
| Worker default suite (Windows), `pytest --cov --cov-branch --cov-fail-under=90` | 4216 passed, 29 skipped, 14 deselected, 1 xfailed; package 99.73 % (line+branch); `ports.py`, `schemas.py` 100 %; `handouts/renderer.py` 96 % on Windows (the misses are Pango-only lines, 100 % in the image) |
| Image `cto020/worker:test`: `tests/handouts` + handout limits + input guard | 140 passed; `handouts/**` 100 % line / 100 % branch |
| `tests/insights` after the typing fixes | 181 passed, 1 xfailed |
| `ruff check .`, `ruff format --check .` (worker) | clean |
| `mypy --strict src tests scripts` (worker, 541 files) | clean (38 errors in `tests/insights` existed before this work; fixed in `fc3dfaa3`, plus a `scipy.*` override) |
| `tools/quality`: pytest, ruff, `mypy --strict` | 1042 passed; clean |
| `run_all.py` (handouts, insights, ports, schemas, their tests, contract tests, tools/quality) | PASS, 0 errors; jscpd output read: no clone in worker production code (the clones are in tests and in two pre-existing `tools/quality` pairs) |

The pytest run shows 6 warnings from numba/librosa in `tests/analysis` and `tests/harmony`. Those
tests already existed and this change does not touch them.

## For the CTO (shared logs)

- THIRD-PARTY-LICENSES: the two rows above, plus `libharfbuzz-subset0` as an image system package
  if the file lists them.
- The table "Limits chosen" in `audio-ingest-020-polish.md` still says "notation-unreadable" for
  the frame and byte caps and "no warning" for artwork. 1.11.0-delta.3 supersedes both.

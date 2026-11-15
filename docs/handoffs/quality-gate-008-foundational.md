# Hand-off: quality-gate / 008 foundational (008:T007, 008:T008)

Same branch as `quality-gate-006-foundational.md` (built on top of it, so the combined wave
has a single quality-gate branch).

## What was built

- **Worker rules** (`PYTHON_ALLOWED`):
  - `transcription` -> `ports`, `schemas`, `config`, plus its own `vendor` (same top-level
    module, so no edge). It must not import `notation`, `engraving`, `jobs`, `api`, `devices` or
    `analysis`.
  - `notation` -> `ports`, `schemas`. `engraving` -> `ports`, `schemas`.
  - `jobs`, `api`, `analysis` (incl. `drum_onsets.py`) and every other adapter import none of the
    three. `main.py` composes them.
- **Third-party allow-lists** (`PYTHON_EXTERNAL_ALLOWED`, `read_external_edges`,
  `check_external`): `notation` may import only `music21` and `engraving` only `verovio`,
  `cairosvg` and `pypdf`. The standard library (`sys.stdlib_module_names` + `__future__`) is
  always allowed. Lazy imports inside functions and `TYPE_CHECKING` imports count. Error text:
  `forbidden third-party dependency notation -> numpy`. Modules that are not listed
  (`transcription`, `analysis`, ...) are not restricted.
- **Vendor exclusion**: `worker/src/stemstudio_worker/transcription/vendor/` is in
  `common.EXCLUDED_PATHS` (SIG units, dependency edges, duplication post-filter), in
  `.jscpd.json` and in `sonar.exclusions`. `detail/vendor/**` was missing from
  `sonar.exclusions` (while `common.py` says "same as"), so I added it too.
- **Licence records** (`tools/quality/vendor_check.py`, new, wired into `run_all.py`; report
  section "Vendored code"): every package directory directly below `detail/vendor/` or
  `transcription/vendor/` needs a `LICENSE` file (`LICENCE`/`COPYING` and any extension or case
  accepted) and a `NOTICE` file, following the existing `detail/vendor/msst` precedent. Each
  missing record is an error (`vendor` check, owner stem-separation).
- **Ownership**: `transcription/`, `worker/tests/transcription/` -> stem-separation.
  `notation/`, `worker/tests/notation/` -> beat-analysis (`analysis/drum_onsets.py` already is).
  `engraving/`, `worker/tests/engraving/` -> audio-ingest.
- **SIG report**: new component rows `worker/engraving`, `worker/transcription` and
  `worker/notation`. The component-row tests of 006 and 008 now live together in
  `test_components.py`, which removed a test-only clone that my first version had.

## Interpretation (please confirm or correct)

008:T007 and the plan say "`engraving` only `ports`, verovio, cairosvg, pypdf" and "`notation`
only `ports`, `schemas` and music21". I allow **`schemas`** for `engraving` too. The port
contract (`ScoreEngraver.engrave`) tells the engraver to raise
`TranscriptionError(MUSIC_FONT_MISSING_TEXT)`, and that constant lives in `schemas.py`. **`config`**
stays forbidden for both. If the architect wants `engraving` limited to `ports`, re-export the
text from `ports.py` and I will drop `schemas` from the rule (one line plus a test).

## Files changed

`tools/quality/{dependency_check,common,components,report,run_all}.py`,
`tools/quality/vendor_check.py` (new), `tools/quality/{.jscpd.json,README.md}`,
`tools/quality/tests/{test_score_rules.py (new), test_vendor_check.py (new), test_report.py,
test_components.py, test_lyrics_rule.py}`, `sonar-project.properties`,
`docs/quality/SIG-REPORT.md`, `specs/008-sheet-music/tasks.md` (T007, T008 ticked).

## Tests and coverage

TDD: failing tests came first. e4539a6 had collection errors (missing `vendor_check`,
`PYTHON_EXTERNAL_ALLOWED`) and 1 failing component test. 767f9a5 added 1 failing report-section
test. The implementation followed in 48bb218.

```text
uv run --directory tools/quality pytest --cov --cov-branch
437 passed
TOTAL 1037 stmts, 0 missed, 216 branches, 0 partial -> 100.00 % line and branch
(every module 100 %: common, components, css_tokens_check, dependency_check, duplication,
 priorities, razor, report, risk, run_all, sig_check, sonar_args, vendor_check)
```

## Quality checks

```text
uv run --directory tools/quality ruff check .            All checks passed!
uv run --directory tools/quality ruff format --check .   35 files already formatted
uv run --directory tools/quality mypy                    Success: no issues found in 34 source files
python tools/quality/sig_check.py tools/quality          34 files, 396 units, 0 errors, 0 warnings
python tools/quality/dependency_check.py                 dependency_check: 0 errors, 0 warnings
python tools/quality/vendor_check.py                     vendor_check: 0 errors, 0 warnings
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 870 files, 6927 units, 50 clones, 1 exemptions, 0 errors, 210 warnings -> exit 0
```

No 006/008 production code exists yet on this branch, so the new rules guard future code. Every
rule is exercised by the fake-tree tests.

## Dependencies added

None.

## Open issues / requests

- **beat-analysis (008:T012+)**: `notation/**` may import only `ports`, `schemas`, `music21` and
  the standard library. **No `numpy`.** Use `bisect` or plain arithmetic for the time-base
  interpolation, or ask for an allow-list change through the architect (plan amendment).
  `analysis/drum_onsets.py` must not import `notation`.
- **audio-ingest (008:T020+)**: `engraving/**` may import only `ports`, `schemas`, `verovio`,
  `cairosvg`, `pypdf` and the standard library (fontconfig via `subprocess` with an argument
  list). Lazy `import cairosvg` inside a function is fine and still checked.
- **stem-separation (008:T022+)**: put vendored code in `transcription/vendor/<package>/` with
  a `LICENSE` and a `NOTICE` (origin URL, commit or version, list of modifications, as in
  `detail/vendor/msst/NOTICE`). Otherwise `run_all.py` fails. The thin adapters in
  `transcription/*.py` are measured. `jobs/transcription_*` gets the transcribers, notator and
  engraver from `main.py`.
- **CTO**: nothing new for THIRD-PARTY-LICENSES from me. The vendor check only proves that the
  files exist.
- `tasks.md`: T007/T008 are ticked. T078 (HO:polish) is left open on purpose.

## Next owner

quality-gate 006:T063 + 008:T078 (HO:polish, `run_all.py` on the merged repo, refactor requests
per owner) after the owner branches merge. Then CTO.

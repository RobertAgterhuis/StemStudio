# quality-gate 020 foundational hand-off (T009, T010)

Lane QA-OPS, branch `020-lane-qa-ops`. Commits `8a23f58b` (T009 red: 40 failing tests, stub module),
`da9b38e7` (T010).

## Rules now in `tools/quality/run_all.py`

| Rule | Where | Owner of a breach |
|---|---|---|
| `insights` imports only `ports`, `schemas` and numpy, scipy, soxr, soundfile | `dependency_check.PYTHON_ALLOWED`, `PYTHON_EXTERNAL_ALLOWED` | beat-analysis |
| `handouts` imports only `ports`, `schemas` and jinja2, weasyprint, pypdf, fontTools | same | audio-ingest |
| neither imports the other, `jobs`, `api`, `separation`, `config`, `documents` | same (allow-list) | - |
| style drift: `worker/scripts/sync_handout_style.py --check` exits 0 (run with the gate's interpreter, `-I`, 60 s timeout; a tree without the script is skipped) | `handout_rules.check_drift` | architect (script) / whoever changed the tokens |
| no literal colours in `handouts/styles/**/*.css` (finder of `css_tokens_check`; the copied `assets/tokens.css` is not scanned) | `handout_rules.check_colours`, check `raw-colour` | audio-ingest |
| every `url_fetcher` in `handouts/**` is an instance of a `URLFetcher` subclass defined in `handouts/fetcher.py` (a call of it, a name/attribute assigned from one, or a name annotated with it or `URLFetcher`); every `HTML(...)`/`CSS(...)` call passes `url_fetcher` explicitly (`**options` is not accepted) | `handout_rules.check_fetchers` | audio-ingest |

New report section "Insights and handouts"; components `worker/handouts`, `worker/insights`; owners
for `worker/{src/stemstudio_worker,tests}/{insights,handouts}/` and `worker/scripts/sync_handout_style.py`
(architect). Shared helper `common.files_below()` (instrument_rules uses it too; removed a clone).

## Readings to confirm (audio-ingest, stem-separation)

- "never passes a plain function as `url_fetcher`" is enforced together with "every `HTML()`/`CSS()`
  passes one": without it WeasyPrint uses its default (network) fetcher, which FR-018 forbids. If the
  renderer builds `HTML()` through a helper that takes `**kwargs`, name `url_fetcher=` explicitly.
- `insights` may not import `config` (task text: ports, schemas only); thread caps reach the analyzer
  through its constructor (main.py).

## Evidence

- `tools/quality` suite: 1042 passed; coverage 100 % line and branch (1682 statements, 362 branches);
  ruff, ruff format, mypy (strict) clean; lizard: no new function above CCN 5 or 15 lines.
- `run_all.py --report docs/quality/SIG-REPORT.md`: `quality gate PASS: 3977 files, 32489 units,
  506 clones, 3 exemptions, 0 errors, 1992 warnings -> exit 0`; jscpd ran (duplication 1.11 %, its
  own output read in the log); the repository passes the new rules (`test_the_repository_follows_the_handout_rules`).

## Dependencies

None new (the drift check runs the stdlib-only sync script).

## Next

T100 at polish: `run_all.py` on all 020 code once the lanes have merged.

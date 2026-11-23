# devops 020 foundational hand-off (T011, T012)

Lane QA-OPS, branch `020-lane-qa-ops` (from design head ca82a055). Commits `438192a5` (T011 red),
`c3390d24` (T012).

## What changed

| File | Change |
|---|---|
| `scripts/check-worker-pdf.{sh,ps1}` (new) | as the worker user, no network: `weasyprint` imports and reports its version, the Pango it loads (`pango.pango_version()`) is >= 1.44, `fc-list` lists DejaVu Sans (FR-012, SC-005) |
| `docker/worker.Dockerfile` | `libpango-1.0-0`, `libpangoft2-1.0-0` listed explicitly (both variants share the runtime stage); `fc-cache -f` over every font folder |
| `docker-compose.yml` | worker `STEMSTUDIO_QUALITY_ANALYZER`, `STEMSTUDIO_INSIGHTS_THREADS` (empty = worker defaults); api `StemStudio__AutoQualityReport=${STEMSTUDIO_AUTO_QUALITY_REPORT:-true}`; header comments |
| `docker-compose.e2e.yml` | worker `STEMSTUDIO_QUALITY_ANALYZER=fake`, `STEMSTUDIO_INSIGHTS_THREADS=2` (overridable) |
| `.env.example` | the three variables (no secrets) |
| `.github/workflows/ci.yml` | `CONTRACTS_DIR` -> `specs/020-insights-handouts/contracts` (Redocly lint of the 4 documents, `merge_full.py --check`); new `INSTRUMENT_CONTRACTS` keeps the 016 lint, 016 `merge_full.py --check`, `generate_chord_spelling_vectors.py --check` and the 016 contract pytest pinned to `specs/016-instrument-views/contracts`; worker job runs `scripts/sync_handout_style.py --check`; cpu-image-guard runs `check-worker-pdf.sh` |
| `scripts/test-all.{sh,ps1}` | same: `INSIGHTS_CONTRACTS` lint + merge check (contracts group), drift check (worker group); 016 steps unchanged |
| `scripts/affected/components.json` | app `tokens.css` and `fonts/archivo/**` also select `worker` and `quality-gate` (drift check); `check-worker-pdf.*` in the image guards rule |
| `scripts/tests/test_affected_select.py` | 020 inputs select their checks (6 cases) |

Refactor 004's loopback defaults and checks are untouched; `key_volume_all` and `loopback_ports_all`
(all five combinations, the e2e override included) pass.

## Evidence

T011 red (pre-020 image `e2e020/worker:pre020`, a tag of the 016-era fake-stack image):

```
Checking e2e020/worker:pre020 for WeasyPrint, Pango >= 1.44 and DejaVu Sans ...
FAIL weasyprint does not import as the worker user (not installed, or Pango/HarfBuzz missing)
FAIL Pango version unknown (needs 1.44 or newer)
OK   fc-list lists DejaVu Sans
```

Green on both rebuilt variants (`e2e020/worker:local` CPU, `e2e020/worker:local-gpu` cu132 + ORT gpu):

```
OK   weasyprint 70.0 imports as the worker user
OK   Pango 1.56.3 >= 1.44
OK   fc-list lists DejaVu Sans
```

`check-worker-fonts.sh` still passes on the CPU image; installed: libpango-1.0-0 1.56.3-1,
libpangoft2-1.0-0 1.56.3-1, libharfbuzz0b 10.2.0-1+deb13u1. The handout assets (tokens.css,
Archivo woff2 + OFL, manifest) are in the wheel inside the image.

Local runs: Redocly lint 020 (valid, 33 warnings as in the design head), `merge_full.py --check` 020
and 016 (superset yes), chord spelling vectors up to date, `sync_handout_style.py --check` exit 0,
compose checks pass, scripts tests 213 passed / 4 skipped, coverage 100 % line+branch (657
statements), ruff/mypy clean, PSScriptAnalyzer clean on both changed .ps1, `test_ci_e2e_split.py`
20 passed.

## Decisions

- The PDF check reads the Pango version WeasyPrint actually loads (`pango_version()`; WeasyPrint 70's
  cdef has no `pango_version_string`), not the dpkg package version.
- `StemStudio__AutoQualityReport` is passed to the api (data-model 1, default true) so a test stack
  can turn the automatic report off without a rebuild.
- The e2e override pins the fake analyzer explicitly (it is also the automatic choice with the fake
  separator), so a stack started with the real separator for a spike still gets deterministic ratings.

## Dependencies and licences (for THIRD-PARTY-LICENSES, CTO)

System packages now listed explicitly in the worker image: `libpango-1.0-0`, `libpangoft2-1.0-0`
(LGPL-2.1-or-later; were already present transitively). No new Python or npm dependency.

## Open items

- CTO: README/quickstart may name `scripts/check-worker-pdf.*` next to the font check.
- `.env.example` has no owner in the plan's map; edited as before by devops (016).

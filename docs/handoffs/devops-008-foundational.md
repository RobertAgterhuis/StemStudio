# Hand-off: devops / 008 foundational (008:T009, 008:T010)

Combined 006 + 008 devops lane, same commit as `devops-006-foundational.md` (which also lists the
shared compose/CI/contracts changes and the common lint results).

## What was built

- **`scripts/check-worker-fonts.{sh,ps1}`** (008:T009, written first): in throwaway containers
  with `--network none`, `fc-list : family` inside the image must list exactly `Leipzig`, and
  `python -I -c "import verovio, cairosvg, music21"` must succeed as the image user
  (`stemstudio`). Options as `check-cpu-image.*` (`[--build] [image]` / `-Build -Image`).
- **`docker/worker.Dockerfile`** (008:T010):
  - New stage **`font`** (`FROM build`): installs `fonttools[woff]==4.68.0` (with brotli) into
    `/opt/fonttools` only, runs `worker/scripts/install_music_font.py /opt/fonts` with the venv's
    verovio, asserts `/opt/fonts/Leipzig.ttf`. fontTools never reaches the runtime image
    (verified: `fonttools absent`).
  - **Reproducible font**: fontTools stamps `head.modified` with the current time, so the script's
    output changed on every build (measured three different SHA-256 values, none equal to the
    Phase 0 `9606f129...`). The stage sets `SOURCE_DATE_EPOCH=1787148656`
    (`ARG LEIPZIG_SOURCE_DATE_EPOCH`), which is the wheel font's own `head.modified` (Mac epoch
    3869993456): output = byte-identical to a conversion with `recalcTimestamp=False`, in both
    variants: **119,416 B, SHA-256 `1dcb5a73d50f29df7b8a5c9f2865f12c7373858b89979d6c1123f42010ab81f5`**
    (verovio 6.4.0, fontTools 4.68.0).
  - Runtime: `COPY --from=font` to `/usr/local/share/fonts/verovio/Leipzig.ttf`; apt adds
    `libcairo2` and `fontconfig` explicitly (both were already pulled in by ffmpeg, so no size
    change from them) and runs `fc-cache -f`. Applies to both image variants.
- **`docker-compose.yml`**: `STEMSTUDIO_TRANSCRIBER: ${STEMSTUDIO_TRANSCRIBER:-}` (empty =
  automatic: fake with the fake separator, else real) and `STEMSTUDIO_TRANSCRIBE_THREADS` (`8`);
  header comment.
- **CI** (`.github/workflows/ci.yml`): job `cpu-image-guard` runs
  `scripts/check-worker-fonts.sh stemstudio/worker:ci-cpu` after the guard (job name extended).
  `CONTRACTS_DIR` -> 008 and `RENDER_VECTORS` -> 007 generator (see the 006 note).

## Files changed

`docker/worker.Dockerfile`, `docker-compose.yml`, `.github/workflows/ci.yml`,
`scripts/check-worker-fonts.sh` (new, mode 100755), `scripts/check-worker-fonts.ps1` (new),
`specs/008-sheet-music/tasks.md` (T009, T010 ticked), this note; shared files in the 006 note.

## Tests and coverage

No coverage-gated component (container-side checks, exercised end to end).

```text
check-worker-fonts.sh stemstudio/worker:local (pre-008 image, test-first)
  FAIL fc-list does not list Leipzig
  ModuleNotFoundError: No module named 'verovio' -> FAIL ...                    exit 1
check-worker-fonts.sh stemstudio/worker:devops-0068-cpu   OK / OK               exit 0
check-worker-fonts.sh stemstudio/worker:devops-0068-gpu   OK / OK               exit 0
check-worker-fonts.ps1 -Image stemstudio/worker:devops-0068-cpu   OK / OK       exit 0
fc-list | grep -i leipzig (quickstart M2), both images:
  /usr/local/share/fonts/verovio/Leipzig.ttf: Leipzig:style=Medium
cairosvg.svg2pdf of an SVG with font-family="Leipzig" as the worker user, both images: 952 B PDF
STEMSTUDIO_CONTACT_URL in image env: 0 (both)
```

`scripts/test-all.ps1 -ContinueOnError` (with the 008 contracts path and the 007 render-vectors
path): **all 35 steps passed**, exit 0.

```text
StemStudio.Domain.Tests       389 passed   Domain        line 98.63 %  branch 96.39 %
StemStudio.Persistence.Tests   66 passed   Persistence   line 98.35 %  branch 92.85 %
StemStudio.Web.Tests          332 passed   Web           line 99.88 %  branch 95.12 %
StemStudio.Api.Tests          734 passed   Api           line 99.46 %  branch 95.75 %
                                           Contracts     line 100 %    branch 100 %
worker pytest                 1736 passed, 7 deselected; 6668 stmts / 754 branches, 100 %
engine vitest 207 passed (100 %); web-interop 11 passed
contracts lint (Redocly 2.78.0, specs/008-sheet-music/contracts)   valid
render vectors check (specs/007-export-library/contracts)          passed
quality tools pytest 100 %; quality gate run_all: 866 files, 0 errors, 210 warnings -> exit 0
fixtures pytest 100 %; e2e lint, typecheck, playwright --list (355 tests in 29 files)   passed
```

## Image sizes (`docker image inspect .Size`)

| Image | Before (local, pre-006/008) | After (this branch) | Delta |
|---|---|---|---|
| CPU `stemstudio/worker` | 3,363,333,138 B (3.36 GB, `:local`) | 3,581,047,410 B (3.58 GB) | +217.7 MB (+6.5 %) |
| GPU `stemstudio/worker` | 11,510,265,323 B (11.5 GB, `:local-gpu`) | 10,398,492,981 B (10.4 GB) | -1.11 GB |

The CPU delta is the 006/008 Python stack plus the 119 KB font. The GPU "before" image is an older
local build (3 h old, other branch; it still contained triton from torch cu132), so its delta is not
a like-for-like measurement: this branch's GPU image has no triton (lock override) and adds the
same 006/008 stack. The "~11.6 GB" comments in `docker-compose.gpu.yml`/Dockerfile are now
conservative; CTO may update README figures to 3.6 / 10.4 GB at 008:T080.

## Quality checks

Same run as the 006 note: compose config (default + GPU) OK, actionlint 0 errors, shellcheck exit 0
(8 files incl. `check-worker-fonts.sh`), PSScriptAnalyzer 0 errors/warnings (6 Information,
pre-existing), Python scripts ruff/mypy clean.

## Dependencies added

| Name | Version | Licence | URL | Note |
|---|---|---|---|---|
| fontTools (`fonttools[woff]`) | 4.68.0 (2026-10-27) | MIT | https://pypi.org/project/fonttools/ | `font` build stage only |
| brotli | 1.2.1 (via `[woff]`) | MIT | https://pypi.org/project/Brotli/ | `font` build stage only |
| libcairo2, fontconfig (Debian) | trixie packages of `python:3.12-slim` | LGPL-2.1/MPL-1.1, MIT-style | https://packages.debian.org/ | already pulled in by ffmpeg; now explicit |
| Leipzig font | from verovio 6.4.0 wheel | OFL-1.1 | https://github.com/rism-digital/leipzig | converted at build |

## Open issues / requests

- **architect / owner of `worker/scripts/install_music_font.py`**: the script is not
  deterministic by itself (fontTools `recalcTimestamp`). Either load with
  `TTFont(..., recalcTimestamp=False)` or document `SOURCE_DATE_EPOCH`; update the Phase 0 hash in
  the docstring, data-model.md and research.md to `1dcb5a73...81f5` (verovio 6.4.0). The Docker
  build is already reproducible via `SOURCE_DATE_EPOCH`.
- **beat-analysis / audio-ingest (008)**: `import music21` prints "Certain music21 functions might
  need the optional package matplotlib" to stderr in the image (matplotlib is excluded on purpose);
  consider silencing it (music21 environment setting) to keep worker logs clean.
- **audio-ingest (engraving)**: libcairo and Leipzig are present in both images; engraving tests
  that need real Cairo can run in the CPU image (`docker run --rm ... stemstudio/worker:<tag>`).

## Next owner

devops 006:T062 + 008:T077 at HO:polish (after the wave: re-run guard/font checks on the merged
image and record the final size delta); CTO for the integration wave.

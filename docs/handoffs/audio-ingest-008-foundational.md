# Hand-off: audio-ingest (agent B) / 008 foundational (008:T020-T021)

Branch from the 006 + 008 Phase 0 commit `62d107e`. Strict TDD: red commit (failing tests)
followed by the green commit.

| Task | Red | Green |
|---|---|---|
| 008:T020/T021 engraver (`engraving/{verovio_svg,pdf,fonts,engraver}.py`) | `7c15c00` | GREEN_HASH |

## What was built

`worker/src/stemstudio_worker/engraving/` (imports `ports`, `schemas` (fixed text only), verovio,
cairosvg (lazy), pypdf and the standard library):

- `fonts.py`: `fontconfig_families()` runs `fc-list :family=Leipzig family` (resolved with
  `shutil.which`, argument list, no shell, no stdin, 10 s timeout) and returns the family names;
  empty when `fc-list` is missing (Windows), cannot run, times out or exits non-zero.
  `leipzig_available(list_families=fontconfig_families)` is True only for the exact family
  `Leipzig`. `FONT_FAMILY`, `FC_LIST_TIMEOUT_SECONDS`, `FamilyLister`.
- `verovio_svg.py`: `VEROVIO_OPTIONS` (data-model 3: `pageWidth 2100`, `pageHeight 2970`,
  `scale 40`, `adjustPageHeight false`, `footer none`, `breaks auto`); `new_toolkit()` (fresh
  toolkit per call, log off); `render_svg_pages(musicxml) -> tuple[str, ...]` (one SVG per page,
  840 x 1188 px). Non-UTF-8 bytes, unreadable MusicXML or zero pages ->
  `TranscriptionError(ENGRAVING_FAILED_TEXT)` = "The score could not be engraved.".
- `pdf.py`: `cairo_svg_to_pdf(svg)` (lazy `import cairosvg`, so the package imports on Windows;
  `output_width/height` = A4 at 96 dpi, external resources stay off) and
  `merge_pages(pages, title, subject)` (pypdf, strict reader; `/Title`, `/Producer StemStudio`,
  `/Subject` only when set). `PRODUCER`, `A4_WIDTH_PX`, `A4_HEIGHT_PX`, `SvgToPdf`.
- `engraver.py`: `VerovioScoreEngraver(font_check=leipzig_available, svg_to_pdf=cairo_svg_to_pdf)`
  implements `ports.ScoreEngraver`: font check first (missing ->
  `TranscriptionError(MUSIC_FONT_MISSING_TEXT)`, nothing rendered), Verovio pages, CairoSVG per
  page, merge; `OSError` (libcairo missing), `ValueError` (bad SVG) and `PyPdfError` become
  `TranscriptionError(ENGRAVING_FAILED_TEXT)` with the cause chained. Returns
  `EngravedScore(pdf, pages)`.
- `__init__.py`: `create_score_engraver() -> ScoreEngraver` for the composition root, plus
  `ENGRAVING_FAILED_TEXT`, `VerovioScoreEngraver`, `fonts`, `pdf`, `verovio_svg`.

### Findings in the real Linux runs (worth knowing for stem-separation, devops, architect)

1. **Page size**: Verovio's 840 x 1188 px page becomes **630 x 891 pt** in CairoSVG by default
   (not A4). Verovio's `mmOutput` does not help (gives 84 x 118.8 mm). Setting CairoSVG's
   `output_width/height` to A4 at 96 dpi gives 595.3 x 841.9 pt, with the same aspect ratio
   and no content change. Data-model 3's Verovio options stay unchanged.
2. **Leipzig in the PDF has no "Leipzig" name**: with the font installed, Cairo embeds the
   SMuFL glyph (the tempo mark's note, U+ECA5) as `XXXXXX+CairoFont-1-1`, because the converted
   `Leipzig.ttf` has no PostScript name (name ID 6). Without the font, the glyph falls back to
   `DejaVuSans-Bold` (a box). So the SC-004 test finds the fonts that draw SMuFL code points via
   their ToUnicode CMap, and asserts that they are Leipzig (by name, or Cairo's generated name).
   A negative control test (libcairo without Leipzig) proves the check tells the two apart.
3. **`install_music_font.py` is not deterministic** with the current fontTools: three container
   builds of the same verovio 6.4.0 wheel gave three different SHA-256 values (`184cc742...`,
   `c663938b...`, `1ef8939a...`); none is Phase 0's `9606f129...ef08`. This probably comes from the
   `head.modified` timestamp and an unpinned fontTools version.

## Files changed

- `worker/src/stemstudio_worker/engraving/{__init__,fonts,verovio_svg,pdf,engraver}.py` (new)
- `worker/tests/engraving/{__init__,scores,test_fonts,test_verovio_svg,test_pdf,test_engraver}.py` (new)
- `docs/handoffs/audio-ingest-008-foundational.md`

## Tests and coverage

Windows 11 (no libcairo, no fontconfig), `cd worker`:

```text
uv run pytest -q --cov --cov-branch          1772 passed, 3 skipped (the three real-Cairo tests), 7 deselected
  TOTAL 6780 stmts, 764 branches, 0 missed   100 % line and branch (whole worker; was 6662/754 at Phase 0)
  engraving/__init__.py   7 stmts / 0 br     100 %
  engraving/engraver.py  23 stmts / 2 br     100 %
  engraving/fonts.py     27 stmts / 2 br     100 %
  engraving/pdf.py       27 stmts / 4 br     100 %
  engraving/verovio_svg.py 28 stmts / 2 br   100 %
uv run pytest tests/engraving                36 passed, 3 skipped
```

Linux, in throw-away `docker run --rm` containers of the existing `stemstudio/worker:local` image.
That image has libcairo 1.18.4 and fontconfig; the 008 packages were pip-installed into `/tmp`,
and the worktree was mounted read-only. No compose project was touched:

```text
with Leipzig (install_music_font.py + fc-cache; fc-list lists "Leipzig")
  pytest tests/engraving --cov=stemstudio_worker.engraving --cov-branch
  38 passed, 1 skipped (negative control)      engraving TOTAL 112 stmts, 10 branches, 100 %
without Leipzig
  38 passed, 1 skipped (SC-004 positive test)  engraving TOTAL 112 stmts, 10 branches, 100 %
```

The Linux runs cover the real CairoSVG paths: A4 page size 595.3 x 841.9 pt per page, page count,
producer, and SC-004 (with the font, every SMuFL glyph is drawn by Leipzig on every page whose SVG
uses `font-family="Leipzig"`; without the font, the fallback is detected). Unit tests replace
`cairosvg` with a fake module in `sys.modules` and patch `fc-list`, so there is no real
subprocess or libcairo on Windows.

## Quality checks

```text
ruff check . / ruff format --check . (244 files)       clean
mypy --strict (122 source files)                       Success: no issues found
tools/quality/sig_check.py engraving src + tests       11 files, 65 units, 0 errors, 0 warnings
tools/quality/run_all.py engraving src + tests         0 errors, 0 clones; warnings: "engraving is not in
                                                       the dependency map" (quality-gate 008:T007) + 14
                                                       legacy app.css colour warnings (not mine);
                                                       jscpd tool error = no node_modules in this worktree
```

- Every unit is <= 15 LOC, has CC <= 5 and <= 4 parameters; there are no clones.
- All public modules, classes and functions have Google-style docstrings.
- `fc-list` runs with an argument list and no shell (`# noqa: S603`, same pattern as
  `ingest/normalizer.py`).
- The input is only our own MusicXML. CairoSVG runs with external resources off (`unsafe` stays
  False).

## Dependencies added

None. verovio 6.4.0 (LGPL-3.0-only), cairosvg 2.9.1 (LGPL-3.0-or-later) and pypdf 6.21.4
(BSD-3-Clause) were already locked by the architect in Phase 0; they are used unmodified, as
separately installed packages. The Linux test container additionally pip-installed
`fonttools[woff]` (MIT) **only** to build the font in a throw-away container. That is not a
project dependency.

## Open issues / requests

- **quality-gate (008:T007)**: `engraving` is not in the dependency map yet (warning). Please
  allow `stemstudio_worker.schemas` next to `ports`, verovio, cairosvg and pypdf: the engraver
  needs the fixed `MUSIC_FONT_MISSING_TEXT`, which `ports` does not re-export. The other option is
  that the architect re-exports it from `ports`.
- **architect / devops (font, 008:T010)**: (a) set a PostScript name (name ID 6 = `Leipzig`) in
  `install_music_font.py`, so PDFs name the font `Leipzig`. The SC-004 test accepts both names.
  (b) Make the output deterministic: pin fontTools in the build stage and fix
  `head.created/modified` (or drop the SHA-256 claim in DELTA.md and research.md).
- **devops (CI)**: the real-CairoSVG tests skip themselves when they cannot run:
  `needs_cairo` needs libcairo2; `needs_leipzig` needs libcairo2 and Leipzig in fontconfig;
  `needs_cairo_without_leipzig` needs libcairo2 without Leipzig. To run the SC-004 test in CI,
  run `tests/engraving` once in the worker image after the font stage (or install libcairo2 +
  fontconfig + the font on the runner). The Ubuntu runner with libcairo2 and without the font
  runs the negative control.
- **stem-separation (composition, 008:T029+)**: build the engraver with
  `engraving.create_score_engraver()`; report `Health.transcription.font.leipzig` from
  `engraving.fonts.leipzig_available()`. That runs `fc-list` once, so call it at start-up or per
  `/health`, not inside a hot loop. Engraving is synchronous and CPU-bound (about 1 s for 125
  bars, research.md S-08): run it inside the transcription job thread.
- Unit tests: `test_engraver.py` calls `fonts.leipzig_available()` once at import time to
  choose the skip markers. On Linux that runs the real `fc-list` (read-only, about 10 ms). Every
  other test patches `fc-list`.

## Next owner

stem-separation (008 transcription jobs use `create_score_engraver()`), quality-gate (dependency
rule), devops (font stage + CI run of the font-dependent tests). audio-ingest B continues with
008:T072 (HO:polish).

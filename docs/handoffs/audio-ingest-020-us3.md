# Hand-off: audio-ingest / 020 US3 (T068-T073)

Lane WORKER-IO, branch `020-lane-worker-io`.

## What was built (`worker/src/stemstudio_worker/handouts/`)

| Module | Role |
|---|---|
| `document.py` | pure view of a `HandoutDocument`: notice check, ingredient filter, facts row, form chart, chord chart (`%` for whole-bar repeats), chords over lyric words (segments at each `charIndex`; chords on one index joined; padding past the text; instrumental rows), setlist cover |
| `markup.py` + `templates/{handout,song,setlist}.html.j2` | Jinja2, `autoescape=True`, `StrictUndefined`; only the packaged style sheets and the page style are inserted unescaped (an `autoescape false` block around the `<style>` elements) |
| `styles/handout.css` | print style, colours only `var(--ss-*)` (no literal colours), 9 pt minimum, `@page` footer (fixed notice left, "Page n of N" right), running header `string(running, first-except)`, bookmarks h1/h2 |
| `layout.py` | WeasyPrint 70 `HTML(string=..., url_fetcher=AllowListFetcher(...))` (the only WeasyPrint call; no `CSS(...)`), frame boxes read from the page anchors |
| `notation.py` | `PypdfNotationReader` (`ports.NotationReader`), `merge_frames`, `fit`, `fits_paper`, `pdf_facts` |
| `glyphs.py` | characters in neither Archivo nor DejaVu Sans (fontTools cmaps) -> `missing-glyphs` warning |
| `renderer.py` | `WeasyHandoutRenderer` (`ports.HandoutRenderer`): progress 0.1/0.2 -> 0.6 (layout), 0.6-0.9 (notation), 0.9 -> 1 (writing); WeasyPrint imported on first render |

Layout per data-model 6.4: header band (artwork 40 mm, title, artist, "<family> handout", facts),
setlist notes (setlist only), form, chord chart, lyrics and chords, chord legend, practice notes,
one frame page per notation page, "About this handout" (notice, sources, version, date) last.

## Commits

| Task | Red | Green |
|---|---|---|
| T068 / T069 | 65c0d977 (T068, T070, T072 in one red commit) | 57c173e4 |
| T070 / T071 | 65c0d977 | 6681f7be |
| T072 / T073 | 65c0d977 | 7adc553e |
| refactor (complexity) | - | da906b71 |

## Tests and coverage

`tests/handouts/test_layout.py`, `test_notation.py`, `test_renderer.py` (+ `documents.py`
fixtures: requests from the `BassHandoutRequest` example, notation PDFs written with pypdf).

| Run | Result | Line | Branch |
|---|---|---|---|
| Linux worker image, `tests/handouts` + guard, `--cov=stemstudio_worker.handouts` | 98 passed | **100 %** (583 statements) | **100 %** (84) |
| Windows host, whole default suite | 3983 passed, 27 skipped (WeasyPrint/symlink) | 99 % package | - |

## Checks asked for in the brief

- **Hostile fetcher cases** (T018/T019, still green): http(s), `file://` traversal, `data:`,
  symlinks, CSS `@import`/`url()`; plus T072 "no network": a real render with hostile notes and
  title fetches exactly the artwork and the Archivo font, nothing refused (nothing else is even
  referenced, because every value is escaped text).
- **Untrusted notation PDFs**: encrypted, truncated, not a PDF, > 60 pages, odd page size,
  > 64 MiB, missing, a page whose content is not a stream -> `notation-unreadable` note page and
  warning; links, page actions (`/AA`) and document JavaScript/open actions are never copied
  (asserted on the merged bytes); a file that breaks between reading and merging fails the job
  with the fixed text.
- **font-stretch 125 %**: measured in WeasyPrint 70: `font-stretch` has **no effect** on the
  Archivo variable WOFF2 (title box 341.6 px at 75/100/125 %), `font-variation-settings: "wdth"
  125` does (433.5 px, +27 %). Decision: title uses `font-variation-settings: "wdth" 125, "wght"
  700`; no static instances needed. Checked visually (title expanded and bold in the rendered PDF).
- **Missing-glyph warning**: characters in neither Archivo nor DejaVu (fontTools cmap); fallback
  font asserted in the PDF (`DejaVu` embedded for `Ĳ`/`Ω`, no warning); CJK gives the warning.
- **014 `documents/`** untouched.

## Security self-review

Untrusted inputs: document texts (lyrics, notes, titles from users and LRCLIB), notation PDFs,
artwork. Texts: autoescaped, never in attributes without escaping, never in CSS (footer comes
from the fixed constant through `css_string`); a `"><img src=...>` title stays text. Resources:
the allow-list fetcher only. PDFs: size/page/encryption limits, broad parse-failure catch,
annotations/actions dropped, catalogue never copied. Found and fixed during the review: fontTools
`TTLibError` on a broken font file was not caught (`glyphs._cmap`), and pypdf does not decode a
content stream in `get_contents()`; both now covered by tests.

## Decisions / deviations

1. Order on the song page follows data-model 6.4 (practice notes before the notation frames);
   the contract example `CompletedHandoutJob.outline` lists "Practice notes" after the notation
   bookmarks. Outline in our PDFs: `[Song, Form, Chord chart, Lyrics and chords, Practice notes,
   Notation - ..., About this handout]`. **Please confirm** (or the architect adjusts the example
   in T108); no schema is affected.
2. Font weights in the print CSS are literal numbers (600/700): `var(--ss-type-*-weight)` in
   `font-weight` did not select the variable weight in WeasyPrint. Colours stay tokens only.
3. `notation-scaled` is warned when a notation page is not the handout paper size (portrait or
   landscape, 2 pt tolerance); every notation page is scaled into its frame anyway (centred,
   top-aligned, never cropped).
4. Warnings are unique and capped at 50 (`HandoutResult.warnings` maxItems).

## For other lanes

- quality-gate: `handouts` imports `fontTools` (glyph check) and `pypdf`, `jinja2`, `weasyprint`;
  every WeasyPrint call passes `url_fetcher` (only `layout.py`).
- devops: install `libharfbuzz-subset0` (WeasyPrint import warning); `fonts-dejavu-core` is needed
  for the fallback (present in the current image).
- architect: `fontTools` is used directly (transitive via `weasyprint` -> `fonttools[woff]`);
  consider declaring it.

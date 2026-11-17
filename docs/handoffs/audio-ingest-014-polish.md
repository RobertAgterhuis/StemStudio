# Hand-off: audio-ingest / 014 T026-T029 (L2, HO:polish)

## What was built

- `engraving/score_view.py` (`VerovioScoreViewRenderer`): only `score-partwise` MusicXML (MEI, ABC, Humdrum,
  HTML are refused); two Verovio passes with the 008 options (plain pages; `svgBoundingBoxes` for the bar map);
  bar box = union of the measure's `g.staff.bounding-box > rect` plus the `page-margin` translate, as fractions of
  21000 x 29700 (4 decimals); bar times on the 008 time base (full bar *j* at beat `j x beatsPerBar` after the
  first downbeat, interpolated over uneven beats, extrapolated with the median period; a pickup
  `pickupBeats x period` before the first downbeat; manual tempo generates constant beats; the last bar ends at
  the duration); limits 200 pages / 2000 bars; pages refused with `<script`, `foreignObject`, an external href.
- `documents/{fonts,setlist_pdf}.py` (`ReportLabSetlistRenderer`): DejaVu Sans / Bold from
  `/usr/share/fonts/truetype/dejavu` (or `/usr/share/fonts/dejavu`), `DocumentError` "DejaVu Sans is missing."
  otherwise; A4, 18 mm margins, 10/13 pt; overview (title, note, table # / Title / Artist / Tempo / Length / Note
  first 80 characters, Total), per song heading, facts line ("The Band - 120 BPM, 4/4, A minor - 3:42 - Mix: Band
  night"), note, sections in two columns, lyrics by stanza, "No lyrics", "Lyrics shortened"; page break per song
  when asked; footer "<title> - page n of m - made with StemStudio on <date>"; text escaped for Platypus;
  uncovered characters -> U+FFFD (or `?` for a font without that glyph) and "Some characters could not be shown"
  on the overview; any ReportLab failure -> `DocumentError`.

Decision / correction of research R-11 (no contract change, please note for the integration): Verovio 6.4.0 embeds
the Leipzig `@font-face` only in pages that contain Leipzig **text** (the tempo mark on page 1); later pages draw
every glyph as `<use>` paths and carry no font. The renderer therefore checks "a page that uses
`font-family="Leipzig"` has the `@font-face`" instead of "every page has it" (T026 wording).

## Tests and coverage

`tests/engraving/test_score_view.py` (29) with hand-written MusicXML (`view_scores.py`: single staff, grand staff,
percussion, pickup, with/without tempo); `tests/documents/test_setlist_pdf.py` (17, pypdf text; ReportLab's Vera
fonts stand in for DejaVu on machines without it). The 20-song x 10,000-character budget is measured in a child
process (`tests/documents/timing.py`; coverage tracing slows ReportLab about twentyfold): 0.92 s, 61 pages.
Coverage: `score_view.py`, `documents/**` 100 % line and branch.

## Quality checks

ruff, mypy clean; `sig_check` 0 warnings; scope rules (score_view: ports, schemas, engraving, verovio, stdlib;
documents: ports, schemas, reportlab) hold.

## Security self-review

MusicXML comes from the library's score asset (`SCORE_MUSICXML_PATTERN` + path guard + containment); it is read
up to 32 MiB, parsed with the standard library (expat 2.7.4: no external entities, entity-expansion protection),
and only `score-partwise` is laid out. SVG pages are checked before they are stored. PDF text is escaped
(`Rock & <Roll>` test) and bounded by the request schema.

## Dependencies added

none (ReportLab 5.1.0 locked in Phase 0; DejaVu via devops `fonts-dejavu-core`).

## Docs impact

Technical manual: setlist PDFs need the DejaVu fonts (in the image; `scripts/check-worker-fonts.*`).

## Next owner

Integration.

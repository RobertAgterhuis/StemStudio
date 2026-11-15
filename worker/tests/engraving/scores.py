"""MusicXML fixtures written by music21 and PDF helpers for the engraving tests."""

import io
import re
from collections.abc import Iterator

from music21 import metadata, meter, musicxml, note, stream, tempo
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject

FONT_FAMILY_ATTRIBUTE = re.compile(r'font-family="([^"]*)"')
TEXT_FONTS = frozenset({"Times, serif"})
"""Text font families Verovio writes besides the Leipzig music font."""


def bass_musicxml(bars: int = 4, *, tempo_mark: bool = True) -> bytes:
    """A one-part bass line of ``bars`` 4/4 bars in quarter notes (MusicXML 4.0 by music21)."""
    score = stream.Score()
    score.metadata = metadata.Metadata(title="Bass line", composer="Song - Artist")
    part = stream.Part()
    part.partName = "Bass"
    part.append(meter.TimeSignature("4/4"))
    if tempo_mark:
        part.append(tempo.MetronomeMark(number=120))
    for index in range(bars * 4):
        part.append(note.Note(40 + index % 12, quarterLength=1))
    score.insert(0, part)
    exported: bytes = musicxml.m21ToXml.GeneralObjectExporter(score).parse()
    return exported


def blank_pdf(width: float = 595.28, height: float = 841.89) -> bytes:
    """A valid one-page PDF without content (stands in for CairoSVG output)."""
    writer = PdfWriter()
    writer.add_blank_page(width=width, height=height)
    out = io.BytesIO()
    writer.write(out)
    return out.getvalue()


def font_families(svg: str) -> set[str]:
    """Every ``font-family`` value in an SVG page."""
    return set(FONT_FAMILY_ATTRIBUTE.findall(svg))


SMUFL_TARGET = re.compile(rb"<[0-9A-Fa-f]+>\s*<(?:E[0-9A-Fa-f]{3}|F[0-8][0-9A-Fa-f]{2})>")
"""A ToUnicode ``bfchar`` entry mapping a glyph to the Private Use Area, where SMuFL lives."""

CAIRO_GENERATED_NAME = "CairoFont-"
"""Cairo's name for an embedded font without a PostScript name (nameID 6); the Leipzig file
converted from Verovio's WOFF2 has none, so Cairo embeds it as ``CairoFont-<n>-<m>``."""


def music_glyph_fonts(page: DictionaryObject) -> set[str]:
    """Base font names (subset tag removed) of the fonts that draw SMuFL code points on a page."""
    return {
        str(font.get("/BaseFont", "")).split("+")[-1]
        for font in _fonts(page.get("/Resources"))
        if _maps_smufl(font)
    }


def is_leipzig(base_font: str) -> bool:
    """Whether an embedded font is the installed Leipzig (by name, or Cairo's generated name)."""
    return base_font == "Leipzig" or base_font.startswith(CAIRO_GENERATED_NAME)


def _maps_smufl(font: DictionaryObject) -> bool:
    to_unicode = font.get("/ToUnicode")
    return to_unicode is not None and bool(SMUFL_TARGET.search(to_unicode.get_object().get_data()))


def _fonts(resources: object) -> Iterator[DictionaryObject]:
    if not isinstance(resources, DictionaryObject):
        return
    fonts = resources.get("/Font")
    if isinstance(fonts, DictionaryObject):
        yield from (font.get_object() for font in fonts.values())
    xobjects = resources.get("/XObject")
    if isinstance(xobjects, DictionaryObject):
        for xobject in xobjects.values():
            yield from _fonts(xobject.get_object().get("/Resources"))


def read_pdf(data: bytes) -> PdfReader:
    """Open PDF bytes with pypdf (strict, so a broken file fails the test)."""
    return PdfReader(io.BytesIO(data), strict=True)

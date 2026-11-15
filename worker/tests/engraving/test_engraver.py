"""Tests of the ``ScoreEngraver`` (data-model.md 3, SC-004).

The fake-converter tests run everywhere (real Verovio and pypdf, CairoSVG replaced). The tests
marked ``needs_cairo`` run the real CairoSVG where libcairo is installed (the Linux image and CI);
``needs_leipzig`` additionally needs the Leipzig font installed through fontconfig.
"""

from xml.etree.ElementTree import ParseError

import pytest

from stemstudio_worker.engraving import (
    ENGRAVING_FAILED_TEXT,
    VerovioScoreEngraver,
    create_score_engraver,
    fonts,
    pdf,
    verovio_svg,
)
from stemstudio_worker.ports import EngravedScore, ScoreEngraver, TranscriptionError
from stemstudio_worker.schemas import MUSIC_FONT_MISSING_TEXT

from .scores import (
    SMUFL_TARGET,
    bass_musicxml,
    blank_pdf,
    font_families,
    is_leipzig,
    music_glyph_fonts,
    read_pdf,
)


def _cairo_available() -> bool:
    try:
        import cairosvg  # noqa: F401, PLC0415 - probe: libcairo is loaded at import time
    except OSError:
        return False
    return True


CAIRO = _cairo_available()
LEIPZIG = fonts.leipzig_available()
needs_cairo = pytest.mark.skipif(not CAIRO, reason="libcairo is not installed")
needs_leipzig = pytest.mark.skipif(
    not (CAIRO and LEIPZIG), reason="libcairo or the Leipzig font (fontconfig) is not installed"
)
needs_cairo_without_leipzig = pytest.mark.skipif(
    not CAIRO or LEIPZIG, reason="needs libcairo without the Leipzig font"
)


class FakeConverter:
    """Records the SVG pages and returns one blank A4 PDF page per SVG page."""

    def __init__(self, error: Exception | None = None) -> None:
        self.pages: list[str] = []
        self.error = error

    def __call__(self, svg: str) -> bytes:
        self.pages.append(svg)
        if self.error is not None:
            raise self.error
        return blank_pdf()


def _engraver(converter: FakeConverter, *, font: bool = True) -> VerovioScoreEngraver:
    return VerovioScoreEngraver(font_check=lambda: font, svg_to_pdf=converter)


def test_renders_every_verovio_page_into_one_pdf() -> None:
    converter = FakeConverter()

    result = _engraver(converter).engrave(bass_musicxml(200), "Bass - Song", None)

    assert isinstance(result, EngravedScore)
    assert result.pages == len(converter.pages) > 1
    assert len(read_pdf(result.pdf).pages) == result.pages
    assert all(page.startswith("<svg") for page in converter.pages)


def test_the_pdf_carries_title_producer_and_subject() -> None:
    result = _engraver(FakeConverter()).engrave(bass_musicxml(), "Bass - Song", "Model credit")

    info = read_pdf(result.pdf).metadata
    assert info is not None
    assert (info.title, info.producer, info.subject) == (
        "Bass - Song",
        "StemStudio",
        "Model credit",
    )


def test_a_missing_music_font_fails_before_anything_is_rendered() -> None:
    converter = FakeConverter()

    with pytest.raises(TranscriptionError) as raised:
        _engraver(converter, font=False).engrave(bass_musicxml(), "Bass", None)

    assert str(raised.value) == MUSIC_FONT_MISSING_TEXT == "The music font is not installed"
    assert converter.pages == []


def test_unreadable_musicxml_fails_with_the_engraving_text() -> None:
    with pytest.raises(TranscriptionError, match=ENGRAVING_FAILED_TEXT):
        _engraver(FakeConverter()).engrave(b"not musicxml", "Bass", None)


@pytest.mark.parametrize(
    "error",
    [
        OSError("no library called cairo"),
        ValueError("bad svg"),
        ParseError("not well-formed (invalid token): line 1, column 0"),
    ],
)
def test_a_failing_conversion_is_a_transcription_error(error: Exception) -> None:
    with pytest.raises(TranscriptionError, match=ENGRAVING_FAILED_TEXT) as raised:
        _engraver(FakeConverter(error)).engrave(bass_musicxml(), "Bass", None)

    assert raised.value.__cause__ is error


def test_a_broken_page_pdf_is_a_transcription_error() -> None:
    engraver = VerovioScoreEngraver(font_check=lambda: True, svg_to_pdf=lambda svg: b"%PDF-broken")

    with pytest.raises(TranscriptionError, match=ENGRAVING_FAILED_TEXT):
        engraver.engrave(bass_musicxml(), "Bass", None)


def test_the_factory_builds_the_production_engraver() -> None:
    engraver: ScoreEngraver = create_score_engraver()

    assert isinstance(engraver, VerovioScoreEngraver)
    assert engraver.font_check is fonts.leipzig_available
    assert engraver.svg_to_pdf is pdf.cairo_svg_to_pdf


@pytest.mark.parametrize(
    ("cmap_line", "smufl"),
    [(b"<0001> <eca5>", True), (b"<0002> <ECA5>", True), (b"<42> <0042>", False)],
)
def test_the_sc004_check_finds_smufl_targets_in_cairo_tounicode_lines(
    cmap_line: bytes, smufl: bool
) -> None:
    """Lines as Cairo 1.18 writes them (lower-case hex) for the glyph and for plain text."""
    assert bool(SMUFL_TARGET.search(cmap_line)) is smufl


@needs_cairo
def test_real_cairo_writes_one_a4_page_per_svg_page() -> None:
    engraver = VerovioScoreEngraver(font_check=lambda: True)

    result = engraver.engrave(bass_musicxml(200), "Bass - Song", "Model credit")

    reader = read_pdf(result.pdf)
    assert len(reader.pages) == result.pages > 1
    for page in reader.pages:
        assert float(page.mediabox.width) == pytest.approx(595.28, abs=0.5)
        assert float(page.mediabox.height) == pytest.approx(841.89, abs=0.5)
    assert reader.metadata is not None
    assert reader.metadata.producer == "StemStudio"


@needs_leipzig
def test_with_the_font_every_leipzig_text_is_drawn_with_leipzig() -> None:
    """SC-004: the tempo mark's note glyph is Leipzig in the PDF, not a fallback box."""
    musicxml = bass_musicxml(200)
    svg_pages = verovio_svg.render_svg_pages(musicxml)

    result = create_score_engraver().engrave(musicxml, "Bass - Song", None)

    pdf_pages = read_pdf(result.pdf).pages
    assert len(pdf_pages) == len(svg_pages)
    leipzig_pages = [i for i, svg in enumerate(svg_pages) if "Leipzig" in font_families(svg)]
    assert leipzig_pages
    for index in leipzig_pages:
        glyph_fonts = music_glyph_fonts(pdf_pages[index])
        assert glyph_fonts
        assert all(is_leipzig(name) for name in glyph_fonts), glyph_fonts


@needs_cairo_without_leipzig
def test_without_the_font_cairo_falls_back_which_the_check_detects() -> None:
    """Negative control of the SC-004 check (the engraver itself refuses without the font)."""
    engraver = VerovioScoreEngraver(font_check=lambda: True)

    result = engraver.engrave(bass_musicxml(4), "Bass", None)

    glyph_fonts = music_glyph_fonts(read_pdf(result.pdf).pages[0])
    assert glyph_fonts
    assert not any(is_leipzig(name) for name in glyph_fonts), glyph_fonts

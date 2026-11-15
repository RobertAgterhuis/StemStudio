"""Tests of the Verovio SVG pages (data-model.md 3) with the real Verovio wheel."""

import re

import pytest

from stemstudio_worker.engraving import verovio_svg
from stemstudio_worker.ports import TranscriptionError

from .scores import TEXT_FONTS, bass_musicxml, font_families

A4_SVG = re.compile(r'<svg width="840px" height="1188px"')
"""2100 x 2970 units at scale 40 -> 840 x 1188 px, the A4 aspect ratio."""


def test_the_options_are_the_binding_engraving_options() -> None:
    assert verovio_svg.VEROVIO_OPTIONS == {
        "pageWidth": 2100,
        "pageHeight": 2970,
        "scale": 40,
        "adjustPageHeight": False,
        "footer": "none",
        "breaks": "auto",
    }


def test_a_new_toolkit_has_the_options_applied() -> None:
    applied = verovio_svg.new_toolkit().getOptions()  # type: ignore[attr-defined]

    for name, value in verovio_svg.VEROVIO_OPTIONS.items():
        assert applied[name] == value


def test_a_short_score_is_one_a4_page() -> None:
    pages = verovio_svg.render_svg_pages(bass_musicxml(4))

    assert len(pages) == 1
    assert A4_SVG.search(pages[0])


def test_a_long_score_breaks_into_several_full_a4_pages() -> None:
    pages = verovio_svg.render_svg_pages(bass_musicxml(200))

    assert len(pages) > 1
    assert all(A4_SVG.search(page) for page in pages)


def test_the_pages_use_only_the_music_font_and_the_text_fonts() -> None:
    pages = verovio_svg.render_svg_pages(bass_musicxml(200))

    families = set().union(*(font_families(page) for page in pages))
    assert families <= TEXT_FONTS | {"Leipzig"}
    assert "Leipzig" in font_families(pages[0])


@pytest.mark.parametrize("data", [b"", b"not musicxml", b"<score-partwise/>", b"\xff\xfe\x00bad"])
def test_unreadable_musicxml_is_a_transcription_error(data: bytes) -> None:
    with pytest.raises(TranscriptionError, match=r"^The score could not be engraved\.$"):
        verovio_svg.render_svg_pages(data)


def test_a_document_without_pages_is_a_transcription_error(monkeypatch: pytest.MonkeyPatch) -> None:
    class NoPages:
        def loadData(self, data: str) -> bool:  # noqa: N802 - Verovio API
            return True

        def getPageCount(self) -> int:  # noqa: N802 - Verovio API
            return 0

    monkeypatch.setattr(verovio_svg, "new_toolkit", NoPages)

    with pytest.raises(TranscriptionError, match=verovio_svg.ENGRAVING_FAILED_TEXT):
        verovio_svg.render_svg_pages(bass_musicxml(1))

"""Tests of the per-page CairoSVG conversion and the pypdf merge (data-model.md 3).

CairoSVG needs libcairo at import time (absent on Windows), so the unit tests put a fake
``cairosvg`` module into ``sys.modules``; ``test_engraver.py`` runs the real library where present.
"""

import sys
import types
from typing import Any

import pytest

from stemstudio_worker.engraving import pdf

from .scores import blank_pdf, read_pdf


class FakeCairo:
    """Stands in for ``cairosvg.svg2pdf``: records the keyword arguments, returns a blank page."""

    def __init__(self) -> None:
        self.calls: list[dict[str, Any]] = []

    def svg2pdf(self, **kwargs: Any) -> bytes:
        self.calls.append(kwargs)
        return blank_pdf()


@pytest.fixture
def cairo(monkeypatch: pytest.MonkeyPatch) -> FakeCairo:
    fake = FakeCairo()
    module = types.ModuleType("cairosvg")
    module.svg2pdf = fake.svg2pdf  # type: ignore[attr-defined]
    monkeypatch.setitem(sys.modules, "cairosvg", module)
    return fake


def test_a_page_is_converted_from_utf8_svg_bytes_to_an_a4_page(cairo: FakeCairo) -> None:
    result = pdf.cairo_svg_to_pdf('<svg width="840px" height="1188px">é</svg>')

    assert read_pdf(result).pages[0].mediabox.width == pytest.approx(595.28, abs=0.01)
    (call,) = cairo.calls
    assert call["bytestring"] == '<svg width="840px" height="1188px">é</svg>'.encode()
    assert call["output_width"] == pdf.A4_WIDTH_PX
    assert call["output_height"] == pdf.A4_HEIGHT_PX
    assert call.get("unsafe", False) is False


def test_the_a4_output_size_is_210_by_297_mm_at_96_dpi() -> None:
    width, height = pdf.A4_WIDTH_PX, pdf.A4_HEIGHT_PX

    assert (width, height) == pytest.approx((210 / 25.4 * 96, 297 / 25.4 * 96))


def test_pages_are_merged_in_order_with_the_document_info() -> None:
    pages = [blank_pdf(width=100 + index) for index in range(3)]

    merged = read_pdf(pdf.merge_pages(pages, "Bass - Song", "Model credit"))

    assert [float(page.mediabox.width) for page in merged.pages] == [100, 101, 102]
    assert merged.metadata is not None
    assert merged.metadata.title == "Bass - Song"
    assert merged.metadata.producer == "StemStudio"
    assert merged.metadata.subject == "Model credit"


def test_without_a_subject_the_document_info_has_none() -> None:
    merged = read_pdf(pdf.merge_pages([blank_pdf()], "Bass", None))

    assert merged.metadata is not None
    assert merged.metadata.subject is None
    assert merged.metadata.producer == pdf.PRODUCER

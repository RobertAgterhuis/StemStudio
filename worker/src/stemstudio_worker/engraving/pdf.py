"""SVG page -> PDF page with CairoSVG, and the pypdf merge (feature 008, data-model.md 3).

CairoSVG (LGPL-3.0-or-later) is used unmodified. It loads libcairo when imported, which is absent
on Windows dev machines, so it is imported lazily inside ``cairo_svg_to_pdf``; an import failure
surfaces as ``OSError`` to the engraver. Verovio's 840 x 1188 px page would become 630 x 891 pt in
CairoSVG's 96 dpi mapping, so the output size is set to A4 explicitly (same aspect ratio).
"""

import io
from collections.abc import Callable, Sequence
from typing import Final

from pypdf import PdfReader, PdfWriter

PRODUCER: Final = "StemStudio"
"""PDF document info ``/Producer``."""

A4_WIDTH_PX: Final = 210 / 25.4 * 96
"""A4 width in CSS px at 96 dpi (595.28 pt in the PDF)."""

A4_HEIGHT_PX: Final = 297 / 25.4 * 96
"""A4 height in CSS px at 96 dpi (841.89 pt in the PDF)."""

SvgToPdf = Callable[[str], bytes]
"""Converts one SVG page into a one-page PDF."""


def cairo_svg_to_pdf(svg: str) -> bytes:
    """Convert one Verovio SVG page into a one-page A4 PDF with CairoSVG.

    External resources stay disabled (CairoSVG's default ``unsafe=False``).

    Raises:
        OSError: libcairo is not installed.
    """
    import cairosvg  # noqa: PLC0415 - lazy: loads libcairo at import time

    page: bytes = cairosvg.svg2pdf(
        bytestring=svg.encode("utf-8"), output_width=A4_WIDTH_PX, output_height=A4_HEIGHT_PX
    )
    return page


def merge_pages(pages: Sequence[bytes], title: str, subject: str | None) -> bytes:
    """Merge one-page PDFs in order and set the document info.

    Args:
        pages: The page PDFs.
        title: ``/Title`` (the asset name).
        subject: ``/Subject`` (the attribution), or None to leave it out.

    Returns:
        The merged PDF.
    """
    writer = PdfWriter()
    for page in pages:
        writer.append(PdfReader(io.BytesIO(page), strict=True))
    info = {"/Title": title, "/Producer": PRODUCER}
    if subject is not None:
        info["/Subject"] = subject
    writer.add_metadata(info)
    out = io.BytesIO()
    writer.write(out)
    return out.getvalue()

"""WeasyPrint layout of the handout HTML (ADR-029 D7, research.md R-07 and P0-4).

The HTML is laid out with the allow-list fetcher only (no network, no other file). After the
layout the box of every notation frame (``id="frame-..."``) is read from the page anchors, so the
notation pages can be merged exactly into the frames. Importing this module loads Pango.
"""

from collections.abc import Mapping
from dataclasses import dataclass
from typing import Final

from weasyprint import HTML

from stemstudio_worker.handouts.fetcher import AllowListFetcher
from stemstudio_worker.handouts.resources import ASSETS_DIR, AllowList

PT_PER_PX: Final = 0.75
"""CSS pixels (1/96 in) to PDF points (1/72 in)."""

FRAME_PREFIX: Final = "frame-"


@dataclass(frozen=True, slots=True)
class LaidOut:
    """The laid-out handout.

    Attributes:
        pdf: The PDF without notation.
        frames: Frame anchor -> (0-based page, ``(left, bottom, width, height)`` in points).
        fetcher: The fetcher used (what was fetched and refused).
    """

    pdf: bytes
    frames: Mapping[str, tuple[int, tuple[float, float, float, float]]]
    fetcher: AllowListFetcher


def lay_out(html: str, allowed: AllowList) -> LaidOut:
    """Lay ``html`` out and write it as a PDF; the frames are read from the page anchors."""
    fetcher = AllowListFetcher(allowed)
    document = HTML(string=html, base_url=ASSETS_DIR.as_uri() + "/", url_fetcher=fetcher).render()
    frames = {
        anchor: (number, _box(box, page.height))
        for number, page in enumerate(document.pages)
        for anchor, box in page.anchors.items()
        if anchor.startswith(FRAME_PREFIX)
    }
    return LaidOut(pdf=document.write_pdf(), frames=frames, fetcher=fetcher)


def _box(box: tuple[float, float, float, float], page_height: float
         ) -> tuple[float, float, float, float]:  # fmt: skip
    """``(left, top, right, bottom)`` in CSS px from the top -> PDF ``(left, bottom, w, h)``."""
    left, top, right, bottom = (value * PT_PER_PX for value in box)
    return left, page_height * PT_PER_PX - bottom, right - left, bottom - top

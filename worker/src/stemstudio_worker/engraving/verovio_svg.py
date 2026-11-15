"""MusicXML -> A4 SVG pages with the Verovio toolkit (feature 008, data-model.md 3).

Verovio (LGPL-3.0-only) is used as the unmodified PyPI wheel. The input is always the notator's
own MusicXML (no user-supplied XML). A fresh toolkit is made per call, so concurrent jobs share no
state; Verovio's console log is switched off.
"""

import importlib.resources
from collections.abc import Mapping
from typing import Final, Protocol

import verovio

from stemstudio_worker.ports import TranscriptionError

ENGRAVING_FAILED_TEXT: Final = "The score could not be engraved."
"""Error text when Verovio, CairoSVG or pypdf cannot process the score."""

VEROVIO_OPTIONS: Final[Mapping[str, object]] = {
    "pageWidth": 2100,
    "pageHeight": 2970,
    "scale": 40,
    "adjustPageHeight": False,
    "footer": "none",
    "breaks": "auto",
}
"""Binding layout options: A4 portrait at 10 units/mm, scale 40, automatic system breaks."""


class Toolkit(Protocol):
    """The part of ``verovio.toolkit`` the engraver uses."""

    def setResourcePath(self, path: str) -> bool:  # noqa: N802 - Verovio API
        """Point the toolkit at its font and resource folder."""
        ...

    def setOptions(self, options: dict[str, object]) -> bool:  # noqa: N802 - Verovio API
        """Apply layout options."""
        ...

    def loadData(self, data: str) -> bool:  # noqa: N802 - Verovio API
        """Load a score; False when it cannot be read."""
        ...

    def getPageCount(self) -> int:  # noqa: N802 - Verovio API
        """Number of laid-out pages."""
        ...

    def renderToSVG(self, page_no: int) -> str:  # noqa: N802 - Verovio API
        """SVG of the 1-based page."""
        ...


def new_toolkit() -> Toolkit:
    """Return a Verovio toolkit with ``VEROVIO_OPTIONS`` applied and logging off.

    Verovio keeps its resource path per thread and the wheel sets it only on the importing
    thread, so every toolkit points at the packaged ``data`` folder explicitly.
    """
    verovio.enableLog(verovio.LOG_OFF)
    toolkit: Toolkit = verovio.toolkit()
    toolkit.setResourcePath(str(importlib.resources.files("verovio") / "data"))
    toolkit.setOptions(dict(VEROVIO_OPTIONS))
    return toolkit


def render_svg_pages(musicxml: bytes) -> tuple[str, ...]:
    """Lay out ``musicxml`` and return one SVG document per A4 page.

    Raises:
        TranscriptionError: The bytes are not UTF-8 MusicXML Verovio can read, or no page results
            (``ENGRAVING_FAILED_TEXT``).
    """
    toolkit = new_toolkit()
    try:
        loaded = toolkit.loadData(musicxml.decode("utf-8"))
    except UnicodeDecodeError as exc:
        raise TranscriptionError(ENGRAVING_FAILED_TEXT) from exc
    pages = toolkit.getPageCount() if loaded else 0
    if pages < 1:
        raise TranscriptionError(ENGRAVING_FAILED_TEXT)
    return tuple(toolkit.renderToSVG(page) for page in range(1, pages + 1))

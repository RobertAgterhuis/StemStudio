"""Shared helpers of the handout tests: the WeasyPrint probe, fixture files and documents."""

import io
from pathlib import Path

import pypdf
import pytest
from PIL import Image

from stemstudio_worker.handouts.resources import AllowList


def _weasyprint_available() -> bool:
    try:
        import weasyprint  # noqa: F401, PLC0415 - probe: loads Pango at import time
    except (OSError, ImportError):
        return False
    return True


WEASYPRINT = _weasyprint_available()
needs_weasyprint = pytest.mark.skipif(
    not WEASYPRINT, reason="WeasyPrint needs Pango (the Linux worker image and CI)"
)


def jpeg(path: Path, size: tuple[int, int] = (8, 8)) -> Path:
    """Write a small JPEG to ``path`` (parents created) and return its real path."""
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    Image.new("RGB", size, (200, 30, 30)).save(buffer, format="JPEG")
    path.write_bytes(buffer.getvalue())
    return path.resolve()


class _Laid:
    """What a fake layout returns: a blank PDF with frames on pages 1.. in order."""

    def __init__(self, pages: int) -> None:
        writer = pypdf.PdfWriter()
        for _ in range(pages):
            writer.add_blank_page(595, 842)
        buffer = io.BytesIO()
        writer.write(buffer)
        self.pdf = buffer.getvalue()
        self.frames: dict[str, tuple[int, tuple[float, float, float, float]]] = {}


class FakeLayout:
    """Records the HTML and returns a blank PDF with one page per frame anchor plus one."""

    def __init__(self, error: Exception | None = None) -> None:
        self.html: list[str] = []
        self.error = error

    def __call__(self, html: str, allowed: AllowList) -> _Laid:
        self.html.append(html)
        if self.error is not None:
            raise self.error
        anchors = [part.split('"')[0] for part in html.split('id="frame-')[1:]]
        laid = _Laid(len(anchors) + 1)
        for page, anchor in enumerate(anchors, 1):
            laid.frames[f"frame-{anchor}"] = (page, (50.0, 50.0, 400.0, 600.0))
        return laid

"""Shared helpers of the handout tests: the WeasyPrint probe, fixture files and documents."""

import io
from pathlib import Path

import pytest
from PIL import Image


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

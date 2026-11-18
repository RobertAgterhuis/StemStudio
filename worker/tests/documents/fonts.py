"""Fonts for the PDF tests: the system DejaVu Sans when installed, else ReportLab's Vera.

Bitstream Vera is the font DejaVu grew from (same Latin glyphs and metrics); it ships with
ReportLab, so the tests also run on a machine without DejaVu (Windows). CI and the worker image
have DejaVu (``fonts-dejavu-core``).
"""

import importlib.resources
from pathlib import Path

from stemstudio_worker.documents.fonts import DEJAVU_DIRS, FontFiles, find_dejavu
from stemstudio_worker.ports import DocumentError


def stand_in_fonts() -> FontFiles:
    """DejaVu Sans (regular, bold) when installed, else ReportLab's Vera and Vera Bold."""
    try:
        return find_dejavu(DEJAVU_DIRS)
    except DocumentError:
        folder = Path(str(importlib.resources.files("reportlab") / "fonts"))
        return FontFiles(folder / "Vera.ttf", folder / "VeraBd.ttf")

"""DejaVu Sans for the setlist PDFs (feature 014, data-model 2.6; audio-ingest).

The worker image installs ``fonts-dejavu-core`` (``/usr/share/fonts/truetype/dejavu``,
``scripts/check-worker-fonts.*``). The fonts are registered with ReportLab under fixed names;
characters the regular face has no glyph for are replaced by U+FFFD (data-model 2.6 step 4).
"""

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final

from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFError, TTFont

from stemstudio_worker.ports import DocumentError

DEJAVU_DIRS: Final[tuple[Path, ...]] = (
    Path("/usr/share/fonts/truetype/dejavu"),
    Path("/usr/share/fonts/dejavu"),
)
"""Folders searched for ``DejaVuSans.ttf`` and ``DejaVuSans-Bold.ttf`` (Debian, Fedora)."""

REGULAR_FILE: Final = "DejaVuSans.ttf"
BOLD_FILE: Final = "DejaVuSans-Bold.ttf"
REGULAR: Final = "StemStudioSans"
"""ReportLab name of the regular face."""
BOLD: Final = "StemStudioSans-Bold"
"""ReportLab name of the bold face."""
REPLACEMENT: Final = "�"
FALLBACK_REPLACEMENT: Final = "?"
"""Replacement when the font has no U+FFFD glyph either (DejaVu Sans has one)."""
MISSING_TEXT: Final = "DejaVu Sans is missing."


@dataclass(frozen=True, slots=True)
class FontFiles:
    """The regular and bold TrueType files."""

    regular: Path
    bold: Path


FontLocator = Callable[[], FontFiles]
"""Finds the font files (raises ``DocumentError`` when they are missing)."""


def find_dejavu(dirs: Sequence[Path] = DEJAVU_DIRS) -> FontFiles:
    """The first folder of ``dirs`` holding both DejaVu Sans files.

    Raises:
        DocumentError: No folder has them (``MISSING_TEXT``).
    """
    for folder in dirs:
        files = FontFiles(folder / REGULAR_FILE, folder / BOLD_FILE)
        if files.regular.is_file() and files.bold.is_file():
            return files
    raise DocumentError(MISSING_TEXT)


@dataclass(frozen=True, slots=True)
class Glyphs:
    """The code points the regular face covers."""

    covered: frozenset[int]

    @property
    def replacement(self) -> str:
        """U+FFFD, or ``?`` for a font without that glyph."""
        return REPLACEMENT if ord(REPLACEMENT) in self.covered else FALLBACK_REPLACEMENT

    def clean(self, text: str) -> tuple[str, bool]:
        """``text`` with uncovered characters replaced, and whether one was replaced."""
        mark = self.replacement
        kept = "".join(c if ord(c) in self.covered or c in "\n\t" else mark for c in text)
        return kept, kept != text


def register(files: FontFiles) -> Glyphs:
    """Register both faces with ReportLab and return the regular face's coverage.

    Raises:
        DocumentError: A file cannot be read as a TrueType font.
    """
    try:
        regular, bold = TTFont(REGULAR, str(files.regular)), TTFont(BOLD, str(files.bold))
    except (OSError, TTFError) as exc:
        raise DocumentError from exc
    pdfmetrics.registerFont(regular)
    pdfmetrics.registerFont(bold)
    return Glyphs(frozenset(regular.face.charToGlyph))

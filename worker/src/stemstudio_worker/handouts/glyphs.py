"""Characters a handout cannot show: in neither Archivo nor the DejaVu Sans fallback (SC-005).

Pango falls back from the packaged Archivo (Latin subset) to DejaVu Sans for characters Archivo
lacks (``ij``, Greek, Cyrillic, ...). A character that neither font maps prints as an empty box,
so the handout result carries a ``missing-glyphs`` warning naming those characters. The character
maps are read once with fontTools.
"""

import functools
from collections.abc import Iterable, Sequence
from pathlib import Path
from typing import Final

from fontTools.ttLib import TTFont, TTLibError

from stemstudio_worker.handouts.health import DEJAVU_SANS
from stemstudio_worker.handouts.resources import ARCHIVO_FONT
from stemstudio_worker.schemas import HandoutDocument, HandoutWarning

MAX_LISTED: Final = 20
"""Most characters named in the warning text."""

_SKIPPED_FIELDS: Final = frozenset(
    {"artworkPath", "pdfPath", "include", "kind", "family", "keySource", "chordPlacement"}
)
"""Fields that are never printed as text."""


def _cmap(path: Path) -> frozenset[int]:
    try:
        with TTFont(path, lazy=True) as font:
            return frozenset(font.getBestCmap())
    except (OSError, TTLibError, ValueError, KeyError, AssertionError):
        return frozenset()


@functools.cache
def covered(fonts: Sequence[Path] = (ARCHIVO_FONT, *DEJAVU_SANS)) -> frozenset[int]:
    """Code points that one of ``fonts`` maps (missing font files map nothing)."""
    return frozenset().union(*(_cmap(path) for path in fonts if path.is_file()))


def printed_texts(document: HandoutDocument) -> list[str]:
    """Every text of ``document`` that the handout prints."""
    texts: list[str] = []
    _collect(document.model_dump(by_alias=True, mode="json"), texts)
    return texts


def _collect(value: object, texts: list[str]) -> None:
    if isinstance(value, str):
        texts.append(value)
        return
    for key, item in _children(value):
        if key not in _SKIPPED_FIELDS:
            _collect(item, texts)


def _children(value: object) -> list[tuple[object, object]]:
    """``(key, item)`` of a dict, ``(index, item)`` of a list, nothing for other values."""
    if isinstance(value, dict):
        return list(value.items())
    if isinstance(value, list):
        return list(enumerate(value))
    return []


def missing_characters(texts: Iterable[str], known: frozenset[int]) -> str:
    """The distinct printable characters of ``texts`` not in ``known``, in order of appearance."""
    seen: dict[str, None] = {}
    for text in texts:
        for char in text:
            if char.isprintable() and not char.isspace() and ord(char) not in known:
                seen.setdefault(char, None)
    return "".join(seen)


def glyph_warning(document: HandoutDocument, known: frozenset[int] | None = None
                  ) -> HandoutWarning | None:  # fmt: skip
    """The ``missing-glyphs`` warning of ``document``, or None when every character prints."""
    missing = missing_characters(printed_texts(document), covered() if known is None else known)
    if not missing:
        return None
    listed = " ".join(missing[:MAX_LISTED]) + (" ..." if len(missing) > MAX_LISTED else "")
    return HandoutWarning(
        code="missing-glyphs", text=f"Some characters cannot be shown and print as boxes: {listed}"
    )

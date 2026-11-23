"""Readiness of the handout renderer for ``Health.insights`` (worker API 1.11.0).

``renderer_health`` imports WeasyPrint, which loads Pango; on a machine without Pango (a Windows
dev host) both versions are None and handout jobs fail readably. ``fonts_health`` checks the
packaged Archivo copy and the DejaVu Sans fallback of the worker image (``fonts-dejavu-core``).
Both results are cached: they cannot change while the worker runs.
"""

import functools
import importlib
from collections.abc import Callable, Sequence
from pathlib import Path
from types import ModuleType
from typing import Final

from stemstudio_worker.handouts.resources import ARCHIVO_FONT
from stemstudio_worker.schemas import HandoutFontsHealth, RendererHealth

DEJAVU_SANS: Final[tuple[Path, ...]] = (
    Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    Path("/usr/share/fonts/dejavu/DejaVuSans.ttf"),
)
"""Where Debian and Fedora install DejaVu Sans (the fallback for characters Archivo lacks)."""

Loader = Callable[[str], ModuleType]
"""Imports a module by name (``importlib.import_module``; a fake in tests)."""


def pango_text(version: int) -> str:
    """``15603`` -> ``"1.56.3"`` (``pango_version()`` is major * 10000 + minor * 100 + micro)."""
    return f"{version // 10000}.{version // 100 % 100}.{version % 100}"


def probe_renderer(load: Loader = importlib.import_module) -> RendererHealth:
    """The installed WeasyPrint and Pango versions; None for both when WeasyPrint cannot load."""
    try:
        weasyprint = load("weasyprint")
        ffi = load("weasyprint.text.ffi")
    except (OSError, ImportError):
        return RendererHealth()
    return RendererHealth(weasyprint=str(weasyprint.__version__),
                          pango=pango_text(int(ffi.pango.pango_version())))  # fmt: skip


@functools.cache
def renderer_health() -> RendererHealth:
    """``Health.insights.renderer`` of this machine (probed once)."""
    return probe_renderer()


def probe_fonts(archivo: Path = ARCHIVO_FONT, fallback: Sequence[Path] = DEJAVU_SANS
                ) -> HandoutFontsHealth:  # fmt: skip
    """Whether the packaged Archivo font and a DejaVu Sans fallback are present."""
    return HandoutFontsHealth(archivo=archivo.is_file(),
                              fallback=any(path.is_file() for path in fallback))  # fmt: skip


@functools.cache
def fonts_health() -> HandoutFontsHealth:
    """``Health.insights.fonts`` of this machine (probed once)."""
    return probe_fonts()

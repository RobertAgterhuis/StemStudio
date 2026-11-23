"""Tests of the handout renderer readiness reported in ``Health.insights`` (020 T040/T041)."""

from pathlib import Path
from types import ModuleType, SimpleNamespace
from typing import Any

from stemstudio_worker.handouts.health import (
    fonts_health,
    pango_text,
    probe_fonts,
    probe_renderer,
    renderer_health,
)
from stemstudio_worker.handouts.resources import ARCHIVO_FONT
from stemstudio_worker.schemas import HandoutFontsHealth, RendererHealth

from .support import WEASYPRINT


def test_pango_versions_are_written_as_dotted_text() -> None:
    assert pango_text(15603) == "1.56.3"
    assert pango_text(14402) == "1.44.2"


def _loader(**modules: Any) -> Any:
    def load(name: str) -> ModuleType:
        found = modules.get(name.replace(".", "_"))
        if isinstance(found, Exception):
            raise found
        if found is None:
            raise ImportError(name)
        module: ModuleType = found
        return module

    return load


def test_the_renderer_versions_come_from_weasyprint_and_pango() -> None:
    ffi = SimpleNamespace(pango=SimpleNamespace(pango_version=lambda: 15603))
    load = _loader(weasyprint=SimpleNamespace(__version__="70.0"), weasyprint_text_ffi=ffi)

    assert probe_renderer(load) == RendererHealth(weasyprint="70.0", pango="1.56.3")


def test_without_pango_both_versions_are_none() -> None:
    load = _loader(weasyprint=OSError("cannot load library 'libpango-1.0-0'"))

    assert probe_renderer(load) == RendererHealth(weasyprint=None, pango=None)


def test_the_fonts_are_checked_on_disk(tmp_path: Path) -> None:
    dejavu = tmp_path / "DejaVuSans.ttf"

    assert probe_fonts(ARCHIVO_FONT, [dejavu]) == HandoutFontsHealth(archivo=True, fallback=False)
    dejavu.write_bytes(b"ttf")
    assert probe_fonts(tmp_path / "none.woff2", [dejavu]) == HandoutFontsHealth(
        archivo=False, fallback=True
    )


def test_this_machine_reports_consistently() -> None:
    health = renderer_health()

    assert (health.weasyprint is not None) is WEASYPRINT
    assert renderer_health() is health
    assert fonts_health().archivo

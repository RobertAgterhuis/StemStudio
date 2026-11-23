"""Tests of the handout renderer (020 T072, SC-005, FR-018): notice, glyphs, progress, no network.

The tests with a fake layout run everywhere; the ones with the real layout need WeasyPrint.
"""

import io
from pathlib import Path
from typing import Any

import pypdf
import pytest

from stemstudio_worker.handouts.glyphs import (
    covered,
    glyph_warning,
    missing_characters,
    printed_texts,
)
from stemstudio_worker.handouts.renderer import MAX_WARNINGS, WeasyHandoutRenderer
from stemstudio_worker.handouts.resources import ARCHIVO_FONT, AllowList
from stemstudio_worker.ports import (
    HANDOUT_FAILED_TEXT,
    HandoutCancelledError,
    HandoutRenderer,
    HandoutRenderError,
    NotationUnreadableError,
)
from stemstudio_worker.schemas import HandoutJobRequest

from .documents import ARTWORK, SCORE, TAB, full_song, library_files, request
from .support import FakeLayout, needs_weasyprint


@pytest.fixture
def files(tmp_path: Path) -> dict[str, Path]:
    return library_files(tmp_path / "library")


def test_the_renderer_satisfies_the_port() -> None:
    port: HandoutRenderer = WeasyHandoutRenderer(layout=FakeLayout())

    assert port is not None


def test_a_document_with_another_notice_is_refused(files: dict[str, Path]) -> None:
    valid = request()
    notice = type(valid.document.notice).model_construct(footer="Share freely", full="x")
    document = valid.document.model_copy(update={"notice": notice})
    forged = HandoutJobRequest.model_construct(document=document, paper="a4")

    with pytest.raises(HandoutRenderError, match="notice"):
        WeasyHandoutRenderer(layout=FakeLayout()).render(forged, files, lambda _f: None)


def test_progress_rises_through_the_three_stages(files: dict[str, Path]) -> None:
    seen: list[float] = []

    result = WeasyHandoutRenderer(layout=FakeLayout()).render(request(), files, seen.append)

    assert seen == sorted(seen)
    assert (seen[0], seen[-1]) == (0.1, 1.0)
    assert any(0.6 < value < 0.9 for value in seen)
    assert result.pages == 4
    assert result.outline == ()


def test_a_cancel_from_progress_propagates(files: dict[str, Path]) -> None:
    def cancel(_fraction: float) -> None:
        raise HandoutCancelledError("deleted")

    with pytest.raises(HandoutCancelledError):
        WeasyHandoutRenderer(layout=FakeLayout()).render(request(), files, cancel)


def test_an_unexpected_layout_failure_is_the_fixed_failure_text(files: dict[str, Path]) -> None:
    layout = FakeLayout(RuntimeError("pango exploded"))

    with pytest.raises(HandoutRenderError, match=HANDOUT_FAILED_TEXT):
        WeasyHandoutRenderer(layout=layout).render(request(), files, lambda _f: None)


def test_without_weasyprint_the_job_fails_readably(
    files: dict[str, Path], monkeypatch: pytest.MonkeyPatch
) -> None:
    def no_pango(name: str) -> Any:
        raise OSError(f"cannot load library 'libpango-1.0-0' ({name})")

    monkeypatch.setattr("importlib.import_module", no_pango)

    with pytest.raises(HandoutRenderError, match=HANDOUT_FAILED_TEXT):
        WeasyHandoutRenderer().render(request(), files, lambda _f: None)


def test_only_the_job_artwork_and_the_font_are_referenced(files: dict[str, Path]) -> None:
    layout = FakeLayout()

    WeasyHandoutRenderer(layout=layout).render(request(), files, lambda _f: None)

    (html,) = layout.html
    assert files[ARTWORK].as_uri() in html
    assert ARCHIVO_FONT.as_uri() in html
    assert files[TAB].as_uri() not in html
    assert "http" not in html.lower().replace("http-equiv", "")


def test_notation_is_read_only_when_included(files: dict[str, Path]) -> None:
    class Spy:
        def __init__(self) -> None:
            self.calls: list[Path] = []

        def page_sizes(self, path: Path) -> tuple[tuple[float, float], ...]:
            self.calls.append(path)
            return ((595.28, 841.89),)

    spy = Spy()
    WeasyHandoutRenderer(spy, FakeLayout()).render(
        request(include=["facts"]), files, lambda _f: None
    )

    assert spy.calls == []


def test_a_notation_path_without_a_file_becomes_a_note(files: dict[str, Path]) -> None:
    del files[SCORE]

    result = WeasyHandoutRenderer(layout=FakeLayout()).render(request(), files, lambda _f: None)

    assert [w.code for w in result.warnings] == ["notation-unreadable"]


def test_warnings_are_unique_and_capped(files: dict[str, Path]) -> None:
    class Unreadable:
        def page_sizes(self, path: Path) -> tuple[tuple[float, float], ...]:
            raise NotationUnreadableError(path.name)

    notation = [{"title": f"Part {n}", "kind": "score", "pdfPath": SCORE} for n in range(8)]
    songs = [request(notation=notation).document.songs[0]] * 10
    document = request().document.model_copy(update={"songs": songs})
    many = HandoutJobRequest.model_construct(document=document, paper="a4")

    result = WeasyHandoutRenderer(Unreadable(), FakeLayout()).render(many, files, lambda _f: None)

    assert len(result.warnings) == 8
    assert MAX_WARNINGS == 50


def test_characters_in_neither_font_are_found() -> None:
    known = frozenset(map(ord, "abc"))

    assert missing_characters(["abcd", " xd\n", "e\u200b"], known) == "dxe"


def test_the_fonts_cover_latin_and_the_warning_names_the_missing() -> None:
    doc = request(practiceNotes="Ĳssel café 漢字 ✓").document

    warning = glyph_warning(doc, frozenset(range(0x3000)))

    assert ord("é") in covered()
    assert warning is not None
    assert warning.code == "missing-glyphs"
    assert warning.text.endswith(": 漢 字")
    assert "Ĳssel café 漢字 ✓" in printed_texts(doc)
    assert all("assets/" not in text for text in printed_texts(doc))


def test_a_long_list_of_missing_characters_is_shortened() -> None:
    doc = request(practiceNotes="".join(chr(0x4E00 + n) for n in range(30))).document

    warning = glyph_warning(doc, frozenset())

    assert warning is not None
    assert warning.text.endswith(" ...")
    assert glyph_warning(request().document, frozenset(range(0x110000))) is None


# ------------------------------------------------------------------ WeasyPrint


def _fonts(pdf: bytes) -> set[str]:
    names: set[str] = set()
    for page in pypdf.PdfReader(io.BytesIO(pdf)).pages:
        resources: Any = page["/Resources"]
        for font in resources.get("/Font", {}).values():
            names.add(str(font.get_object()["/BaseFont"]))
    return names


@needs_weasyprint
def test_characters_archivo_lacks_use_the_dejavu_fallback(files: dict[str, Path]) -> None:
    result = WeasyHandoutRenderer().render(request(practiceNotes="Ĳssel Ωmega"), files,
                                           lambda _f: None)  # fmt: skip

    fonts = _fonts(result.pdf)
    assert any("Archivo" in name for name in fonts)
    assert any("DejaVu" in name for name in fonts)
    assert result.warnings == ()


@needs_weasyprint
def test_characters_no_font_has_are_warned(files: dict[str, Path]) -> None:
    result = WeasyHandoutRenderer().render(request(practiceNotes="漢字"), files, lambda _f: None)

    assert [w.code for w in result.warnings] == ["missing-glyphs"]


@needs_weasyprint
def test_the_same_document_gives_the_same_pages_and_outline(files: dict[str, Path]) -> None:
    first = WeasyHandoutRenderer().render(request(**full_song()), files, lambda _f: None)
    second = WeasyHandoutRenderer().render(request(**full_song()), files, lambda _f: None)

    assert (first.pages, first.outline) == (second.pages, second.outline)


@needs_weasyprint
def test_no_network_and_no_foreign_file_is_ever_fetched(files: dict[str, Path]) -> None:
    from stemstudio_worker.handouts.layout import lay_out  # noqa: PLC0415 - needs Pango

    fetchers: list[Any] = []

    def spy(html: str, allowed: AllowList) -> Any:
        laid = lay_out(html, allowed)
        fetchers.append(laid.fetcher)
        return laid

    hostile = '<img src="http://evil.example/x.png"> url(file:///etc/passwd) @import "https://x";'
    WeasyHandoutRenderer(layout=spy).render(
        request(practiceNotes=hostile, title='Song"><img src="https://evil.example/">'),
        files, lambda _f: None)  # fmt: skip

    (fetcher,) = fetchers
    assert sorted(fetcher.fetched) == sorted([files[ARTWORK].as_uri(), ARCHIVO_FONT.as_uri()])
    assert fetcher.refused == []


def test_a_font_file_that_cannot_be_read_maps_nothing(tmp_path: Path) -> None:
    broken = tmp_path / "broken.ttf"
    broken.write_bytes(b"not a font")

    assert covered((broken, tmp_path / "missing.ttf")) == frozenset()

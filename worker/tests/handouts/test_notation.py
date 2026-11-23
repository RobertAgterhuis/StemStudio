"""Tests of the notation merge (020 T070, research R-07): frames, scaling, untrusted PDFs.

pypdf runs everywhere; the tests through the whole renderer need WeasyPrint (Linux image, CI).
"""

import io
import time
from pathlib import Path

import pypdf
import pytest
from pypdf.generic import (
    ArrayObject,
    DictionaryObject,
    FloatObject,
    NameObject,
    TextStringObject,
)

from stemstudio_worker.handouts import notation
from stemstudio_worker.handouts.notation import (
    Frame,
    PypdfNotationReader,
    fit,
    fits_paper,
    merge_frames,
    pdf_facts,
)
from stemstudio_worker.handouts.renderer import WeasyHandoutRenderer, scaled_text
from stemstudio_worker.ports import (
    HandoutRenderError,
    NotationReader,
    NotationUnreadableError,
    RenderedHandout,
)

from .documents import (
    A4,
    LETTER,
    SCORE,
    TAB,
    library_files,
    notation_pdf,
    page_texts,
    request,
    token_pdf,
)
from .support import FakeLayout, needs_weasyprint

BOMB_SECONDS = 3.0
"""Time budget of a hostile notation PDF (it was 373 s for 10 pages of 1.8 M tokens)."""


def test_the_reader_is_a_notation_reader_with_page_sizes(tmp_path: Path) -> None:
    reader: NotationReader = PypdfNotationReader()

    sizes = reader.page_sizes(notation_pdf(tmp_path / "tab.pdf", 2))

    assert [tuple(round(side, 2) for side in size) for size in sizes] == [A4, A4]


def _encrypted(path: Path) -> Path:
    writer = pypdf.PdfWriter(clone_from=notation_pdf(path))
    writer.encrypt("secret")
    with path.open("wb") as handle:
        writer.write(handle)
    return path


def _truncated(path: Path) -> Path:
    data = notation_pdf(path, 2).read_bytes()
    path.write_bytes(data[: len(data) // 3])
    return path


def _not_a_pdf(path: Path) -> Path:
    path.write_bytes(b"<html>not a pdf</html>")
    return path


def _too_many_pages(path: Path) -> Path:
    writer = pypdf.PdfWriter()
    for _ in range(notation.MAX_NOTATION_PAGES + 1):
        writer.add_blank_page(*A4)
    with path.open("wb") as handle:
        writer.write(handle)
    return path


def _tiny_page(path: Path) -> Path:
    return notation_pdf(path, 1, (10, 10))


@pytest.mark.parametrize("broken", [_encrypted, _truncated, _not_a_pdf, _too_many_pages,
                                    _tiny_page])  # fmt: skip
def test_untrusted_pdfs_that_cannot_be_used_are_unreadable(tmp_path: Path, broken: object) -> None:
    path = broken(tmp_path / "bad.pdf")  # type: ignore[operator]

    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(path)


def test_a_huge_or_missing_file_is_unreadable(tmp_path: Path, monkeypatch: pytest.MonkeyPatch
                                              ) -> None:  # fmt: skip
    path = notation_pdf(tmp_path / "tab.pdf")
    monkeypatch.setattr(notation, "MAX_NOTATION_BYTES", 100)

    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(path)
    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(tmp_path / "none.pdf")


def test_paper_sizes_match_portrait_and_landscape() -> None:
    assert fits_paper(A4, "a4")
    assert fits_paper((841.89, 595.28), "a4")
    assert not fits_paper(A4, "letter")
    assert fits_paper(LETTER, "letter")


def test_an_a4_page_scales_into_a_letter_frame_without_cropping() -> None:
    box = (51.0, 60.0, 510.0, 606.6)

    matrix = fit(box, A4).ctm
    scale, left, bottom = matrix[0], matrix[4], matrix[5]

    assert scale == pytest.approx(606.6 / A4[1])
    assert left + A4[0] * scale <= box[0] + box[2] + 1e-6
    assert left >= box[0]
    assert bottom == pytest.approx(box[1])
    assert left - box[0] == pytest.approx(box[0] + box[2] - (left + A4[0] * scale))


def _blank(pages: int) -> bytes:
    writer = pypdf.PdfWriter()
    for _ in range(pages):
        writer.add_blank_page(*A4)
    buffer = io.BytesIO()
    writer.write(buffer)
    return buffer.getvalue()


def test_each_frame_gets_its_notation_page(tmp_path: Path) -> None:
    source = notation_pdf(tmp_path / "tab.pdf", 2)
    frames = [Frame(page=1, box=(50, 50, 400, 600), source=source, source_page=0),
              Frame(page=2, box=(50, 50, 400, 600), source=source, source_page=1)]  # fmt: skip
    seen: list[float] = []

    merged = merge_frames(_blank(3), frames, seen.append)

    texts = page_texts(merged)
    assert ("NOTATION tab 1" not in texts[0], "NOTATION tab 1" in texts[1],
            "NOTATION tab 2" in texts[2]) == (True, True, True)  # fmt: skip
    assert seen == [0.5, 1.0]


def _hostile(path: Path) -> Path:
    """A notation PDF with a link annotation, a page action and document JavaScript."""
    writer = pypdf.PdfWriter(clone_from=notation_pdf(path))
    page = writer.pages[0]
    link = DictionaryObject({
        NameObject("/Type"): NameObject("/Annot"), NameObject("/Subtype"): NameObject("/Link"),
        NameObject("/Rect"): ArrayObject([FloatObject(0), FloatObject(0), FloatObject(99),
                                          FloatObject(99)]),
        NameObject("/A"): DictionaryObject({NameObject("/S"): NameObject("/URI"),
                                            NameObject("/URI"): TextStringObject("https://evil.example/")}),
    })  # fmt: skip
    page[NameObject("/Annots")] = ArrayObject([writer._add_object(link)])
    action = DictionaryObject({NameObject("/S"): NameObject("/JavaScript"),
                               NameObject("/JS"): TextStringObject("app.alert(2)")})  # fmt: skip
    page[NameObject("/AA")] = DictionaryObject({NameObject("/O"): action})
    writer._root_object[NameObject("/OpenAction")] = DictionaryObject(
        {
            NameObject("/S"): NameObject("/JavaScript"),
            NameObject("/JS"): TextStringObject("app.alert(1);"),
        }
    )
    with path.open("wb") as handle:
        writer.write(handle)
    return path


def test_links_actions_and_scripts_of_a_notation_pdf_are_not_copied(tmp_path: Path) -> None:
    source = _hostile(tmp_path / "evil.pdf")
    frame = Frame(page=0, box=(50, 50, 400, 600), source=source, source_page=0)

    merged = merge_frames(_blank(1), [frame], lambda _done: None)

    reader = pypdf.PdfReader(io.BytesIO(merged))
    assert "/Annots" not in reader.pages[0]
    assert "/AA" not in reader.pages[0]
    assert b"evil.example" not in merged
    assert b"app.alert" not in merged
    assert "NOTATION evil 1" in reader.pages[0].extract_text()


def test_a_notation_file_that_breaks_before_the_merge_fails_the_render(tmp_path: Path) -> None:
    source = notation_pdf(tmp_path / "tab.pdf")
    source.write_bytes(b"gone")

    with pytest.raises(HandoutRenderError):
        merge_frames(_blank(1), [Frame(0, (50, 50, 400, 600), source, 0)], lambda _done: None)


def test_pdf_facts_flatten_the_outline_in_order() -> None:
    writer = pypdf.PdfWriter(clone_from=io.BytesIO(_blank(2)))
    song = writer.add_outline_item("Song", 0)
    writer.add_outline_item("Form", 0, parent=song)
    writer.add_outline_item("About this handout", 1)
    buffer = io.BytesIO()
    writer.write(buffer)

    assert pdf_facts(buffer.getvalue()) == (2, ("Song", "Form", "About this handout"))


# ------------------------------------------------------------------ through the renderer


def _render(tmp_path: Path, paper: str = "a4", tab_pages: int = 2) -> RenderedHandout:
    files = library_files(tmp_path / "library", tab_pages)
    return WeasyHandoutRenderer().render(request(paper), files, lambda _fraction: None)


@needs_weasyprint
def test_one_frame_page_per_notation_page_with_correct_count_and_outline(tmp_path: Path) -> None:
    result = _render(tmp_path, tab_pages=3)

    texts = page_texts(result.pdf)
    assert result.pages == len(texts) == 1 + 3 + 1 + 1
    assert ["NOTATION tab 1" in t for t in texts] == [False, True, False, False, False, False]
    assert "NOTATION tab 3" in texts[3]
    assert "NOTATION score 1" in texts[4]
    assert result.outline == ("Example Song", "Form", "Chord chart",
                              "Practice notes", "Notation - Bass - tablature",
                              "Notation - Bass - score", "About this handout")  # fmt: skip

    assert result.warnings == ()


@needs_weasyprint
def test_a4_notation_on_letter_is_scaled_and_warned(tmp_path: Path) -> None:
    result = _render(tmp_path, "letter")

    reader = pypdf.PdfReader(io.BytesIO(result.pdf))
    assert [round(float(p.mediabox.width)) for p in reader.pages] == [612] * 5
    assert [w.code for w in result.warnings] == ["notation-scaled"] * 2
    assert result.warnings[0].text == scaled_text("Bass - tablature")


@needs_weasyprint
def test_an_unreadable_notation_file_becomes_a_note_page(tmp_path: Path) -> None:
    files = library_files(tmp_path / "library")
    files[TAB].write_bytes(b"not a pdf")

    result = WeasyHandoutRenderer().render(request(), files, lambda _fraction: None)

    texts = page_texts(result.pdf)
    assert result.pages == 4
    assert "A notation file could not be read: Bass - tablature" in texts[1]
    assert [(w.code, w.text) for w in result.warnings] == [
        ("notation-unreadable", "A notation file could not be read: Bass - tablature")
    ]
    assert "Notation - Bass - tablature" in result.outline
    assert "NOTATION score 1" in texts[2]
    assert SCORE in files


def test_a_page_with_a_broken_media_box_is_unreadable(tmp_path: Path) -> None:
    writer = pypdf.PdfWriter()
    page = writer.add_blank_page(*A4)
    page[NameObject("/MediaBox")] = ArrayObject([NameObject("/a")] * 4)
    path = tmp_path / "broken.pdf"
    with path.open("wb") as handle:
        writer.write(handle)

    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(path)


def test_a_page_whose_content_is_not_a_stream_is_unreadable(tmp_path: Path) -> None:
    writer = pypdf.PdfWriter()
    writer.add_blank_page(*A4)[NameObject("/Contents")] = TextStringObject("no stream")
    path = tmp_path / "broken.pdf"
    with path.open("wb") as handle:
        writer.write(handle)

    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(path)


def test_a_blank_page_without_content_is_readable(tmp_path: Path) -> None:
    path = tmp_path / "blank.pdf"
    path.write_bytes(_blank(1))

    assert len(PypdfNotationReader().page_sizes(path)) == 1


# ------------------------------------------------------------------ parse limits (security fixes)


def test_a_page_over_the_parsers_own_token_limit_is_unreadable(tmp_path: Path) -> None:
    path = token_pdf(tmp_path / "bomb.pdf", 1, 2_100_000)

    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(path)


def test_a_pdf_over_the_token_limit_completes_the_render_with_a_warning(tmp_path: Path) -> None:
    files = library_files(tmp_path / "library")
    token_pdf(files[TAB], 1, 2_100_000)

    result = WeasyHandoutRenderer(layout=FakeLayout()).render(request(), files, lambda _f: None)

    assert [(w.code, w.text) for w in result.warnings] == [
        ("notation-unreadable", "A notation file could not be read: Bass - tablature")
    ]


def test_a_file_replaced_by_one_over_the_limit_before_the_merge_fails_the_render(
    tmp_path: Path,
) -> None:
    source = token_pdf(tmp_path / "tab.pdf", 1, 2_100_000)

    with pytest.raises(HandoutRenderError):
        merge_frames(_blank(1), [Frame(0, (50, 50, 400, 600), source, 0)], lambda _done: None)


def test_a_page_over_the_handout_parse_budget_is_unreadable_quickly(tmp_path: Path) -> None:
    path = token_pdf(tmp_path / "bomb.pdf", 3, 1_500_000)
    start = time.perf_counter()

    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(path)

    assert time.perf_counter() - start < BOMB_SECONDS


def test_a_notation_pdf_of_heavy_pages_renders_quickly_with_a_warning(tmp_path: Path) -> None:
    files = library_files(tmp_path / "library")
    token_pdf(files[TAB], 2, 1_500_000)
    start = time.perf_counter()

    result = WeasyHandoutRenderer(layout=FakeLayout()).render(request(), files, lambda _f: None)

    assert time.perf_counter() - start < BOMB_SECONDS
    assert [w.code for w in result.warnings] == ["notation-unreadable"]


def test_a_dense_engraved_page_is_within_the_parse_budget(tmp_path: Path) -> None:
    path = token_pdf(tmp_path / "dense.pdf", 2, 72_000 * 2)

    assert len(PypdfNotationReader().page_sizes(path)) == 2


def test_the_merge_opens_each_notation_file_once(tmp_path: Path, monkeypatch: pytest.MonkeyPatch
                                                 ) -> None:  # fmt: skip
    tab, score = notation_pdf(tmp_path / "tab.pdf", 2), notation_pdf(tmp_path / "score.pdf")
    opened: list[Path] = []
    real_open = notation._open

    def spy(path: Path) -> pypdf.PdfReader:
        opened.append(path)
        return real_open(path)

    monkeypatch.setattr(notation, "_open", spy)
    frames = [Frame(0, (50, 50, 400, 600), tab, 0), Frame(1, (50, 50, 400, 600), score, 0),
              Frame(2, (50, 50, 400, 600), tab, 1)]  # fmt: skip

    texts = page_texts(merge_frames(_blank(3), frames, lambda _done: None))

    assert opened == [tab, score]
    assert ["NOTATION tab 1" in texts[0], "NOTATION score 1" in texts[1],
            "NOTATION tab 2" in texts[2]] == [True, True, True]  # fmt: skip


def test_outline_titles_over_the_contract_length_are_shortened() -> None:
    writer = pypdf.PdfWriter(clone_from=io.BytesIO(_blank(1)))
    writer.add_outline_item("T" * 300, 0)
    writer.add_outline_item("Form", 0)
    buffer = io.BytesIO()
    writer.write(buffer)

    _, outline = pdf_facts(buffer.getvalue())

    assert outline == ("T" * 199 + "…", "Form")

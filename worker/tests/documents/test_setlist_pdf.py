"""Tests of the setlist PDF (014 T028, data-model 2.6, FR-018, SC-008)."""

import io
import subprocess
import sys
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import pytest
from pypdf import PdfReader
from reportlab.platypus import SimpleDocTemplate

from stemstudio_worker.documents import ReportLabSetlistRenderer
from stemstudio_worker.documents.fonts import (
    DEJAVU_DIRS,
    REPLACEMENT,
    FontFiles,
    Glyphs,
    find_dejavu,
    register,
)
from stemstudio_worker.ports import DocumentError, SetlistDocumentRenderer
from stemstudio_worker.schemas import SetlistDocumentRequest

from .fonts import stand_in_fonts

_OPTIONS = {"includeLyrics": True, "includeSections": True, "includeNotes": True,
            "pageBreakPerSong": True}  # fmt: skip


def _song(position: int, **changes: Any) -> dict[str, Any]:
    song: dict[str, Any] = {
        "position": position, "title": f"Song {position}", "artist": "The Band", "bpm": 120.4,
        "beatsPerBar": 4, "key": "A minor", "durationSeconds": 222, "mixName": "Band night",
        "note": f"Capo {position}", "sections": [
            {"label": "Intro", "kind": "intro", "startSeconds": 0},
            {"label": "Verse 1", "kind": "verse", "startSeconds": 12.4},
        ],
        "lyrics": {"lines": ["First line", "second line", "", "Next stanza"], "shortened": False},
    }  # fmt: skip
    return {**song, **changes}


def _request(songs: list[dict[str, Any]], **options: bool) -> SetlistDocumentRequest:
    return SetlistDocumentRequest.model_validate({
        "title": "Band night", "note": "Bring the capo.",
        "generatedAt": datetime(2026, 11, 16, 18, 0, tzinfo=UTC).isoformat(),
        "options": {**_OPTIONS, **options}, "songs": songs,
    })  # fmt: skip


def _render(request: SetlistDocumentRequest) -> tuple[list[str], int]:
    document = ReportLabSetlistRenderer(stand_in_fonts).render(request)
    reader = PdfReader(io.BytesIO(document.pdf))
    assert document.pdf.startswith(b"%PDF-")
    assert len(reader.pages) == document.pages
    return [page.extract_text() for page in reader.pages], document.pages


def test_the_overview_lists_the_songs_in_order_with_their_facts() -> None:
    pages, _ = _render(_request([_song(1), _song(2, title="Second", artist=None, bpm=None,
                                                 note="x" * 120)]))  # fmt: skip

    overview = pages[0]
    assert "Band night" in overview
    assert "Bring the capo." in overview
    for header in ("#", "Title", "Artist", "Tempo", "Length", "Note"):
        assert header in overview
    assert overview.index("Song 1") < overview.index("Second")
    assert "120 BPM" in overview
    assert overview.count("x") == 80  # the note preview (wrapped in its cell)
    assert "Total 7:24" in overview


def test_each_song_has_heading_facts_note_sections_and_stanzas() -> None:
    pages, _ = _render(_request([_song(1), _song(2)]))

    song = pages[1]
    assert "1. Song 1" in song
    assert "The Band - 120 BPM, 4/4, A minor - 3:42 - Mix: Band night" in song
    assert "Capo 1" in song
    assert "0:00 Intro" in song and "0:12 Verse 1" in song
    assert "First line\nsecond line" in song
    assert "Next stanza" in song
    assert "2. Song 2" in pages[2]


def test_missing_lyrics_and_shortened_lyrics_are_said() -> None:
    shortened = {"lines": ["Only line"], "shortened": True}
    pages, _ = _render(_request([_song(1, lyrics=None), _song(2, lyrics=shortened)]))

    assert "No lyrics" in pages[1]
    assert "Lyrics shortened" in pages[2]


def test_options_switch_parts_off() -> None:
    pages, _ = _render(_request([_song(1)], includeLyrics=False, includeSections=False,
                                includeNotes=False))  # fmt: skip

    text = "\n".join(pages[1:])
    assert "First line" not in text and "No lyrics" not in text
    assert "0:12 Verse 1" not in text
    assert "Capo 1" not in text


def test_page_break_per_song_changes_the_page_count() -> None:
    songs = [_song(n) for n in range(1, 5)]

    _, separate = _render(_request(songs))
    _, together = _render(_request(songs, pageBreakPerSong=False))

    assert separate == 5
    assert together < separate


def test_every_page_has_the_footer_with_page_n_of_m() -> None:
    pages, count = _render(_request([_song(1), _song(2)]))

    for number, text in enumerate(pages, 1):
        assert f"Band night - page {number} of {count} - made with StemStudio on 2026-11-16" in text


def test_uncovered_characters_become_replacement_marks_with_a_note() -> None:
    pages, _ = _render(_request([_song(1, title="Song 漢")]))

    assert f"Song {register(stand_in_fonts()).replacement}" in pages[0]
    assert "漢" not in "".join(pages)
    assert "Some characters could not be shown" in pages[0]
    assert "Some characters" not in _render(_request([_song(1)]))[0][0]


def test_markup_characters_are_printed_as_text() -> None:
    pages, _ = _render(_request([_song(1, title="Rock & <Roll>")]))

    assert "Rock & <Roll>" in pages[1]


def test_long_and_hour_long_songs_format_their_length() -> None:
    pages, _ = _render(_request([_song(1, durationSeconds=3725)]))

    assert "1:02:05" in pages[0]


def test_missing_dejavu_fails_with_a_document_error() -> None:
    def missing() -> FontFiles:
        raise DocumentError("DejaVu Sans is missing.")

    with pytest.raises(DocumentError, match="DejaVu Sans is missing"):
        ReportLabSetlistRenderer(missing).render(_request([_song(1)]))


def test_find_dejavu_looks_in_the_font_folders(tmp_path: Path) -> None:
    (tmp_path / "DejaVuSans.ttf").write_bytes(b"x")
    (tmp_path / "DejaVuSans-Bold.ttf").write_bytes(b"x")

    assert find_dejavu((tmp_path / "none", tmp_path)) == FontFiles(
        tmp_path / "DejaVuSans.ttf", tmp_path / "DejaVuSans-Bold.ttf"
    )
    with pytest.raises(DocumentError, match="DejaVu Sans is missing"):
        find_dejavu((tmp_path / "none",))
    assert Path("/usr/share/fonts/truetype/dejavu") in DEJAVU_DIRS


def test_a_broken_font_file_fails_with_a_document_error(tmp_path: Path) -> None:
    broken = tmp_path / "broken.ttf"
    broken.write_bytes(b"not a font")

    with pytest.raises(DocumentError):
        ReportLabSetlistRenderer(lambda: FontFiles(broken, broken)).render(_request([_song(1)]))


def test_twenty_songs_with_10000_character_lyrics_take_under_ten_seconds() -> None:
    fonts = stand_in_fonts()
    script = Path(__file__).with_name("timing.py")

    child = subprocess.run(  # noqa: S603 - our own script, argument list
        [sys.executable, str(script), str(fonts.regular), str(fonts.bold)],
        capture_output=True, text=True, check=True, timeout=120,
    )  # fmt: skip
    seconds, pages = child.stdout.split()

    assert float(seconds) < 10.0
    assert int(pages) > 21


def test_it_is_a_setlist_document_renderer() -> None:
    assert isinstance(ReportLabSetlistRenderer(stand_in_fonts), SetlistDocumentRenderer)


def test_the_replacement_is_u_fffd_when_the_font_has_it() -> None:
    assert Glyphs(frozenset({ord(REPLACEMENT), ord("a")})).clean("a漢") == ("a�", True)
    assert Glyphs(frozenset({ord("a")})).clean("a漢\n") == ("a?\n", True)
    assert Glyphs(frozenset({ord("a")})).clean("a") == ("a", False)


def test_a_reportlab_failure_becomes_a_document_error(monkeypatch: pytest.MonkeyPatch) -> None:
    def broken(*_args: object, **_kwargs: object) -> None:
        raise ValueError("layout failed")

    monkeypatch.setattr(SimpleDocTemplate, "build", broken)

    with pytest.raises(DocumentError):
        ReportLabSetlistRenderer(stand_in_fonts).render(_request([_song(1)]))


def test_a_setlist_without_note_and_odd_sections() -> None:
    sections = [{"label": f"Part {n}", "kind": "other", "startSeconds": n * 10} for n in range(3)]
    request = _request(
        [_song(1, sections=sections, lyrics={"lines": ["", " "], "shortened": False})]
    )
    request = request.model_copy(update={"note": ""})

    pages, _ = _render(request)

    assert "Bring the capo." not in pages[0]
    assert "0:20 Part 2" in pages[1]
    assert "No lyrics" in pages[1]

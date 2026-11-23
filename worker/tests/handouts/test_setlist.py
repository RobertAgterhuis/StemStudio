"""Tests of the setlist handout layout (020 T083, FR-009, SC-004, data-model 6.4)."""

import copy
import io
import re
import time
from pathlib import Path
from typing import Any

import pypdf

from stemstudio_worker.handouts.document import SongInputs, build_view, setlist_view
from stemstudio_worker.handouts.markup import render_html
from stemstudio_worker.handouts.renderer import WeasyHandoutRenderer
from stemstudio_worker.jobs.handout_limits import (
    MAX_HANDOUT_BODY_BYTES,
    MAX_HANDOUT_CELLS,
    MAX_HANDOUT_TEXT_CHARS,
    check_request_limits,
    layout_cells,
    text_chars,
)
from stemstudio_worker.schemas import HandoutJobRequest

from .documents import body, full_song, library_files, page_texts
from .support import needs_weasyprint

BUDGET_SECONDS = 90.0
"""SC-004: a 20-song setlist handout."""


def _setlist(songs: int) -> HandoutJobRequest:
    data = body(**full_song())
    template = data["document"]["songs"][0]
    data["document"].update(kind="setlist", title="Friday set", subtitle=f"Setlist - {songs} songs")
    data["document"]["songs"] = []
    entries: list[dict[str, Any]] = []
    for number in range(1, songs + 1):
        song = copy.deepcopy(template)
        song.update(title=f"Song {number}", entryNotes=f"Capo {number % 3}", notation=[])
        data["document"]["songs"].append(song)
        entries.append({"position": number, "title": f"Song {number}", "artist": "Band",
                        "key": "A minor", "tempoBpm": 120, "durationSeconds": 200.0,
                        "notes": "<b>loud</b>" if number == 1 else None})  # fmt: skip
    notes = "Bring spare strings.\nTune to A."
    data["document"]["setlist"] = {"name": "Friday set", "notes": notes, "entries": entries}
    return HandoutJobRequest.model_validate(data)


def test_the_cover_view_lists_the_running_order() -> None:
    request = _setlist(2)
    assert request.document.setlist is not None

    cover = setlist_view(request.document.setlist)

    assert cover.name == "Friday set"
    assert cover.entries[0] == ("1", "Song 1", "Band", "A minor", "120 BPM", "3:20", "<b>loud</b>")
    assert cover.entries[1][-1] == ""


def test_the_html_has_cover_contents_links_and_escaped_notes() -> None:
    request = _setlist(2)
    view = build_view(request, [SongInputs(None, ()), SongInputs(None, ())])

    html = render_html(view, "file:///font.woff2")

    assert "Running order" in html
    assert '<a href="#song-1">Song 1</a>' in html
    assert '<a href="#song-2">Song 2</a>' in html
    assert "&lt;b&gt;loud&lt;/b&gt;" in html
    assert "Setlist notes" in html


def _render(tmp_path: Path, songs: int) -> tuple[Any, float]:
    files = library_files(tmp_path / "library")
    start = time.perf_counter()
    result = WeasyHandoutRenderer().render(_setlist(songs), files, lambda _fraction: None)
    return result, time.perf_counter() - start


@needs_weasyprint
def test_a_setlist_has_a_cover_contents_and_every_song_on_a_new_page(tmp_path: Path) -> None:
    result, _ = _render(tmp_path, 3)

    texts = page_texts(result.pdf)
    cover = texts[0]
    expected_texts = (
        "Friday set",
        "Setlist - 3 songs",
        "Bass handout, made on 2026-11-16",
        "Bring spare strings.",
        "Running order",
        "Contents",
        "<b>loud</b>",
    )
    for expected in expected_texts:
        assert expected in cover, expected
    starts = [n for n, text in enumerate(texts) if re.search(r"^Song \d$", text, re.MULTILINE)
              and "Bass handout" in text]  # fmt: skip
    assert len(starts) == 3
    assert all(f"Song {i}" in texts[page] for i, page in enumerate(starts, 1))
    for number, page in enumerate(starts, 1):
        assert re.search(rf"Song {number} *\.+ *{page + 1}\b", cover), (number, page)
    assert "About this handout" in texts[-1]


@needs_weasyprint
def test_one_top_level_bookmark_per_song(tmp_path: Path) -> None:
    result, _ = _render(tmp_path, 3)

    reader = pypdf.PdfReader(io.BytesIO(result.pdf))
    top = [item.title for item in reader.outline if not isinstance(item, list)]

    assert top == ["Friday set", "Song 1", "Song 2", "Song 3", "About this handout"]


@needs_weasyprint
def test_a_20_song_setlist_renders_within_the_budget(tmp_path: Path) -> None:
    result, seconds = _render(tmp_path, 20)

    assert seconds < BUDGET_SECONDS
    assert result.pages >= 21
    assert result.outline.count("Form") == 20


def _at_the_limits() -> HandoutJobRequest:
    """A 50-song setlist near every job limit, in the costliest shape (chords over lyrics)."""
    data = _setlist(50).model_dump(by_alias=True, mode="json")
    for song in data["document"]["songs"]:
        song.update(practiceNotes="Keep the eighths even. " * 170, lyrics=None)
    base = HandoutJobRequest.model_validate(data).document
    rows = (MAX_HANDOUT_CELLS - layout_cells(base)) // (50 * 33)
    width = min(500, (MAX_HANDOUT_TEXT_CHARS - text_chars(base)) // (50 * rows) - 32 * 4 - 1)
    words = ("alpha bravo charlie delta echo foxtrot golf hotel " * 20)[:width]
    chords = [{"symbol": "F#m7", "charIndex": c * (width // 32)} for c in range(32)]
    line = {"text": words, "section": None, "chords": chords}
    for song in data["document"]["songs"]:
        song["lyrics"] = {"chordPlacement": "word", "source": "x", "lines": [line] * rows}
    return HandoutJobRequest.model_validate(data)


def test_the_limit_request_is_just_within_the_job_limits() -> None:
    limit = _at_the_limits()

    check_request_limits(limit)
    assert text_chars(limit.document) > 0.95 * MAX_HANDOUT_TEXT_CHARS
    assert layout_cells(limit.document) > 0.95 * MAX_HANDOUT_CELLS
    assert len(limit.model_dump_json(by_alias=True)) < MAX_HANDOUT_BODY_BYTES


@needs_weasyprint
def test_a_setlist_at_the_job_limits_renders_within_the_budget(tmp_path: Path) -> None:
    files = library_files(tmp_path / "library")
    start = time.perf_counter()

    result = WeasyHandoutRenderer().render(_at_the_limits(), files, lambda _fraction: None)

    assert time.perf_counter() - start < BUDGET_SECONDS
    assert result.outline.count("Lyrics and chords") == 50

"""Tests of the handout request limits (020 security fixes: text, cells, notation bytes)."""

import pytest

from stemstudio_worker.jobs import handout_limits
from stemstudio_worker.jobs.handout_limits import (
    HandoutLimitError,
    check_notation_bytes,
    check_request_limits,
    layout_cells,
    text_chars,
)
from stemstudio_worker.schemas import HandoutJobRequest

from .insights_requests import example, handout_request


def _with_song(**changes: object) -> HandoutJobRequest:
    data = example("BassHandoutRequest")
    song = {**data["document"]["songs"][0], **changes}
    document = {**data["document"], "songs": [song]}
    return HandoutJobRequest.model_validate({**data, "document": document})


LYRICS = {"chordPlacement": "word", "source": "LRCLIB", "lines": [
    {"text": "one two", "section": 0, "chords": [{"symbol": "Am", "charIndex": 0},
                                                {"symbol": "F", "charIndex": 4}]},
    {"text": "", "section": None, "chords": [{"symbol": "C", "charIndex": 0}]},
]}  # fmt: skip


def test_every_string_of_the_document_counts_as_text() -> None:
    base = text_chars(handout_request().document)
    longer = _with_song(practiceNotes="x" * 1000)
    old = len(handout_request().document.songs[0].practice_notes or "")

    assert base > 500
    assert text_chars(longer.document) == base - old + 1000


def test_sections_bars_chords_and_lyric_rows_count_as_cells() -> None:
    # 2 sections + 4 bars + 4 chords in the bars + 4 legend chords
    assert layout_cells(handout_request().document) == 14
    # + 2 lyric lines + 3 lyric chords
    assert layout_cells(_with_song(lyrics=LYRICS).document) == 19


def test_setlist_entries_count_as_cells() -> None:
    data = example("BassHandoutRequest")
    entry = {"position": 1, "title": "Example Song", "artist": None, "key": None,
             "tempoBpm": None, "durationSeconds": None, "notes": None}  # fmt: skip
    setlist = {"name": "Friday", "notes": None, "entries": [entry, {**entry, "position": 2}]}
    document = {**data["document"], "kind": "setlist", "setlist": setlist}

    request = HandoutJobRequest.model_validate({**data, "document": document})

    assert layout_cells(request.document) == 16


def test_a_request_at_the_limits_passes(monkeypatch: pytest.MonkeyPatch) -> None:
    request = handout_request()
    monkeypatch.setattr(handout_limits, "MAX_HANDOUT_TEXT_CHARS", text_chars(request.document))
    monkeypatch.setattr(handout_limits, "MAX_HANDOUT_CELLS", 14)

    check_request_limits(request)
    check_notation_bytes(handout_limits.MAX_HANDOUT_NOTATION_BYTES)


def test_a_request_over_the_text_or_cell_limit_is_refused(monkeypatch: pytest.MonkeyPatch) -> None:
    request = handout_request()
    monkeypatch.setattr(handout_limits, "MAX_HANDOUT_TEXT_CHARS", 10)
    monkeypatch.setattr(handout_limits, "MAX_HANDOUT_CELLS", 13)

    with pytest.raises(HandoutLimitError) as raised:
        check_request_limits(request)

    chars = text_chars(request.document)
    assert raised.value.errors() == [
        {"loc": ("body", "document"), "type": "value_error",
         "msg": f"The handout has more than 10 characters of text ({chars})."},
        {"loc": ("body", "document"), "type": "value_error",
         "msg": "The handout has more than 13 sections, bars, chords and lyric lines (14)."},
    ]  # fmt: skip


def test_notation_files_over_the_byte_limit_are_refused() -> None:
    with pytest.raises(HandoutLimitError) as raised:
        check_notation_bytes(handout_limits.MAX_HANDOUT_NOTATION_BYTES + 1)

    assert raised.value.errors() == [{
        "loc": ("body", "document"), "type": "value_error",
        "msg": "The notation files of the handout are larger than 128 MiB together.",
    }]  # fmt: skip


def test_the_limits_are_the_documented_numbers() -> None:
    assert (handout_limits.MAX_HANDOUT_BODY_BYTES, handout_limits.MAX_HANDOUT_TEXT_CHARS,
            handout_limits.MAX_HANDOUT_CELLS, handout_limits.MAX_HANDOUT_NOTATION_BYTES) == (
        8 * 1024 * 1024, 500_000, 30_000, 128 * 1024 * 1024)  # fmt: skip

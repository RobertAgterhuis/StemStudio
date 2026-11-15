"""Record selection of data-model 2.3 over the F-01/F-02 record sets (self-written text)."""

from typing import Any

import pytest

from stemstudio_worker.lyrics.lrclib import LrclibRecord
from stemstudio_worker.lyrics.select import (
    MATCH_SECONDS,
    PLAIN_MATCH_SECONDS,
    kind_of,
    select_record,
)
from stemstudio_worker.ports import LyricsQuery

from .records import (
    SEARCH_262,
    SEARCH_INSTRUMENTAL,
    instrumental_record,
    line_record,
    plain_record,
    record,
)


def records(*bodies: dict[str, Any]) -> list[LrclibRecord]:
    return [LrclibRecord.model_validate(body) for body in bodies]


def query(duration: float, album: str | None = None) -> LyricsQuery:
    return LyricsQuery("Fake Song", duration, artist="The Fixture Band", album=album)


class TestKind:
    def test_word_needs_the_flag_and_a_lyricsfile(self) -> None:
        assert kind_of(records(record(1, 1.0))[0]) == "word"
        assert kind_of(records(record(1, 1.0, lyricsfile=None))[0]) == "line"
        assert kind_of(records(record(1, 1.0, hasWordSync=False))[0]) == "line"

    def test_plain_and_instrumental(self) -> None:
        assert kind_of(records(plain_record(1, 1.0))[0]) == "plain"
        assert kind_of(records(instrumental_record(1, 1.0))[0]) == "instrumental"

    def test_a_record_without_anything_has_no_kind(self) -> None:
        assert kind_of(records(plain_record(1, 1.0, plainLyrics=""))[0]) is None


class TestF02Search:
    def test_262_s_picks_a_word_synced_record_with_the_lowest_id(self) -> None:
        choice = select_record(records(*SEARCH_262), query(262.0))
        assert choice is not None
        assert (choice.record.id, choice.kind) == (546388, "word")

    def test_a_matching_album_wins_the_tie(self) -> None:
        choice = select_record(records(*SEARCH_262), query(261.0, album="album 3"))
        assert choice is not None
        assert choice.record.id == 546391

    def test_270_s_picks_the_line_synced_record(self) -> None:
        sets = records(*SEARCH_262, line_record(13446363, 271.0, albumName="Other"))
        choice = select_record(sets, query(270.0))
        assert choice is not None
        assert (choice.record.id, choice.kind) == (13446363, "line")

    def test_200_s_finds_nothing(self) -> None:
        assert select_record(records(*SEARCH_262), query(200.0)) is None

    def test_exactly_two_seconds_still_matches(self) -> None:
        choice = select_record(records(*SEARCH_262), query(262.0 + MATCH_SECONDS))
        assert choice is not None
        assert choice.kind == "word"

    def test_word_sync_beats_line_sync_beats_plain(self) -> None:
        sets = records(plain_record(1, 100.0), line_record(2, 100.0), record(3, 101.0))
        choice = select_record(sets, query(100.0))
        assert choice is not None
        assert choice.record.id == 3


class TestPlainFallback:
    def test_plain_text_within_15_s_when_nothing_matches_within_2_s(self) -> None:
        choice = select_record(records(*SEARCH_262), query(276.0))
        assert choice is not None
        assert (choice.record.id, choice.kind) == (3780737, "plain")

    def test_the_closest_duration_wins(self) -> None:
        sets = records(plain_record(5, 110.0), plain_record(9, 106.0), plain_record(1, 94.0))
        choice = select_record(sets, query(100.0))
        assert choice is not None
        assert choice.record.id == 1

    def test_beyond_15_s_finds_nothing(self) -> None:
        sets = records(plain_record(1, 100.0 + PLAIN_MATCH_SECONDS + 0.5))
        assert select_record(sets, query(100.0)) is None

    def test_records_without_plain_text_are_not_a_fallback(self) -> None:
        sets = records(line_record(1, 110.0, plainLyrics=None))
        assert select_record(sets, query(100.0)) is None


class TestInstrumental:
    def test_the_instrumental_record_is_chosen(self) -> None:
        choice = select_record(records(*SEARCH_INSTRUMENTAL), query(125.0))
        assert choice is not None
        assert (choice.record.id, choice.kind) == (3879894, "instrumental")

    def test_a_record_with_lyrics_beats_an_instrumental_one(self) -> None:
        sets = records(instrumental_record(1, 125.0), plain_record(2, 125.0))
        choice = select_record(sets, query(125.0))
        assert choice is not None
        assert choice.kind == "plain"

    @pytest.mark.parametrize("duration", [140.0, 173.0])
    def test_instrumental_records_are_no_plain_fallback(self, duration: float) -> None:
        choice = select_record(records(*SEARCH_INSTRUMENTAL), query(duration + 5))
        assert choice is None

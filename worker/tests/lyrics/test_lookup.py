"""``LyricsLookup`` over LRCLIB: request order, outcomes, documents, factory (FR-001/003/012)."""

import logging
from pathlib import Path

import pytest

from stemstudio_worker.config import LyricsSettings, OnlineSettings, WorkerSettings
from stemstudio_worker.lyrics.fake import FakeLyricsLookup
from stemstudio_worker.lyrics.lookup import (
    DisabledLyricsLookup,
    LrclibLookup,
    create_lyrics_lookup,
)
from stemstudio_worker.lyrics.lrclib import (
    LRCLIB_GET_URL,
    LRCLIB_SEARCH_URL,
    MIN_REQUEST_GAP_SECONDS,
    LrclibClient,
)
from stemstudio_worker.metadata.http import OnlineRequestError, OnlineResponse
from stemstudio_worker.metadata.ratelimit import RateLimiter
from stemstudio_worker.ports import (
    LyricsQuery,
    LyricsUnavailableError,
    LyricsUnusableError,
)
from stemstudio_worker.schemas import LYRICS_STAGE_SEARCHING, MAX_LYRICS_CHARS, LyricsResult

from .records import (
    PLAIN,
    SEARCH_262,
    SEARCH_INSTRUMENTAL,
    WORDS,
    FakeClock,
    FakeGetter,
    answer,
    instrumental_record,
    line_record,
    plain_record,
    record,
)

FULL = LyricsQuery("Fake Song", 262.0, artist="The Fixture Band", album="Synthetic Sessions")
NO_ALBUM = LyricsQuery("Fake Song", 262.0, artist="The Fixture Band")
TITLE_ONLY = LyricsQuery("Fake Song", 262.0)


class Progress:
    def __init__(self) -> None:
        self.calls: list[tuple[float, str]] = []

    def __call__(self, fraction: float, stage: str) -> None:
        self.calls.append((fraction, stage))


def lookup(*answers: object) -> tuple[LrclibLookup, FakeGetter, FakeClock]:
    getter = FakeGetter(list(answers))  # type: ignore[arg-type]
    clock = FakeClock()
    limiter = RateLimiter(clock, clock.sleep, MIN_REQUEST_GAP_SECONDS)
    return LrclibLookup(LrclibClient(getter, limiter)), getter, clock


def run(found: LrclibLookup, query: LyricsQuery = FULL) -> LyricsResult:
    return found.lookup(query, Progress())


class TestRequestOrder:
    def test_get_with_album_then_get_then_search_and_no_more(self) -> None:
        finder, getter, clock = lookup(None, None, answer([], LRCLIB_SEARCH_URL))
        result = run(finder)
        assert result.outcome == "not-found"
        assert result.lyrics is None
        assert [url for url, _ in getter.calls] == [
            LRCLIB_GET_URL,
            LRCLIB_GET_URL,
            LRCLIB_SEARCH_URL,
        ]
        assert "album_name" in getter.calls[0][1]
        assert "album_name" not in getter.calls[1][1]
        assert clock.sleeps == [MIN_REQUEST_GAP_SECONDS] * 2

    def test_a_match_on_the_first_get_stops_there(self) -> None:
        finder, getter, _ = lookup(answer(record(546388, 262.0)))
        assert run(finder).outcome == "found"
        assert len(getter.calls) == 1

    def test_without_album_the_first_request_is_the_plain_get(self) -> None:
        finder, getter, _ = lookup(None, answer(SEARCH_262, LRCLIB_SEARCH_URL))
        assert run(finder, NO_ALBUM).outcome == "found"
        assert [url for url, _ in getter.calls] == [LRCLIB_GET_URL, LRCLIB_SEARCH_URL]

    def test_without_artist_only_search_is_used(self) -> None:
        finder, getter, _ = lookup(answer(SEARCH_262, LRCLIB_SEARCH_URL))
        assert run(finder, TITLE_ONLY).outcome == "found"
        assert [url for url, _ in getter.calls] == [LRCLIB_SEARCH_URL]

    def test_a_get_answer_outside_2_s_is_skipped(self) -> None:
        far = answer(record(1, 300.0))
        finder, getter, _ = lookup(far, far, answer([record(2, 262.0)], LRCLIB_SEARCH_URL))
        lyrics = run(finder).lyrics
        assert lyrics is not None
        assert lyrics.source_id == "2"
        assert len(getter.calls) == 3

    def test_a_non_json_get_answer_counts_as_not_found(self) -> None:
        html = OnlineResponse(LRCLIB_GET_URL, b"<html>")
        finder, getter, _ = lookup(html, html, OnlineResponse(LRCLIB_SEARCH_URL, b"<html>"))
        assert run(finder).outcome == "not-found"
        assert len(getter.calls) == 3

    def test_progress_reports_the_searching_stage(self) -> None:
        finder, _, _ = lookup(answer(record(546388, 262.0)))
        progress = Progress()
        finder.lookup(FULL, progress)
        assert progress.calls == [(0.0, LYRICS_STAGE_SEARCHING), (1.0, LYRICS_STAGE_SEARCHING)]


class TestDocuments:
    def test_word_synced_record_gives_the_contract_word_document(self) -> None:
        lyrics = run(lookup(answer(record(1000001, 262.0)))[0]).lyrics
        assert lyrics is not None
        assert (lyrics.sync_level, lyrics.origin, lyrics.language) == ("word", "source", "en")
        assert (lyrics.source, lyrics.source_id, lyrics.source_duration_seconds) == (
            "lrclib",
            "1000001",
            262.0,
        )
        assert lyrics.plain_text == PLAIN
        assert [(w.start_ms, w.end_ms) for w in lyrics.lines[0].words] == [
            (10950, 11400),
            (11400, 11900),
            (11900, 13200),
        ]

    def test_line_synced_record_gives_the_contract_line_document(self) -> None:
        lyrics = run(lookup(answer(line_record(1000003, 261.0)))[0]).lyrics
        assert lyrics is not None
        assert (lyrics.sync_level, lyrics.origin, lyrics.language) == ("line", "interpolated", None)
        assert [(line.start_ms, line.end_ms) for line in lyrics.lines] == [
            (10950, 13230),
            (14000, 15980),
        ]
        assert lyrics.plain_text == PLAIN

    def test_plain_record_gives_a_plain_document(self) -> None:
        lyrics = run(
            lookup(answer(plain_record(1000004, 266.0)))[0], LyricsQuery("Fake", 266.0, artist="A")
        ).lyrics
        assert lyrics is not None
        assert (lyrics.sync_level, lyrics.origin) == ("plain", "none")
        assert lyrics.plain_text == PLAIN

    def test_plain_fallback_within_15_s_from_search(self) -> None:
        finder, _, _ = lookup(None, None, answer(SEARCH_262, LRCLIB_SEARCH_URL))
        result = run(finder, LyricsQuery("Fake", 276.0, artist="A", album="B"))
        assert result.lyrics is not None
        assert (result.lyrics.sync_level, result.lyrics.source_duration_seconds) == ("plain", 285.0)

    def test_instrumental_record_gives_an_empty_instrumental_document(self) -> None:
        finder, _, _ = lookup(answer(SEARCH_INSTRUMENTAL, LRCLIB_SEARCH_URL))
        result = run(finder, LyricsQuery("Fake Instrumental", 125.0))
        assert result.outcome == "instrumental"
        assert result.lyrics is not None
        assert (result.lyrics.instrumental, result.lyrics.lines, result.lyrics.plain_text) == (
            True,
            [],
            "",
        )
        assert (result.lyrics.source_id, result.lyrics.source_duration_seconds) == (
            "3879894",
            125.0,
        )

    def test_an_instrumental_record_without_duration_has_none(self) -> None:
        body = instrumental_record(5, 0.0)
        finder, _, _ = lookup(answer([body], LRCLIB_SEARCH_URL))
        result = run(finder, LyricsQuery("Fake", 1.0))
        assert result.lyrics is not None
        assert result.lyrics.source_duration_seconds is None

    def test_carriage_returns_in_the_plain_text_are_normalised(self) -> None:
        body = line_record(1, 262.0, plainLyrics="Fake line one\r\nLa la la")
        lyrics = run(lookup(answer(body))[0]).lyrics
        assert lyrics is not None
        assert lyrics.plain_text == PLAIN

    def test_a_too_long_plain_text_is_replaced_by_the_line_texts(self) -> None:
        body = line_record(1, 262.0, plainLyrics="la " * (MAX_LYRICS_CHARS // 3 + 1))
        lyrics = run(lookup(answer(body))[0]).lyrics
        assert lyrics is not None
        assert lyrics.plain_text == PLAIN

    def test_an_unreadable_lyricsfile_falls_back_to_the_lrc_lines(self) -> None:
        lyrics = run(lookup(answer(record(1, 262.0, lyricsfile="lines: [")))[0]).lyrics
        assert lyrics is not None
        assert (lyrics.sync_level, lyrics.origin) == ("line", "interpolated")

    def test_an_unreadable_lyricsfile_without_lrc_is_unusable(self) -> None:
        body = record(1, 262.0, lyricsfile="lines: [", syncedLyrics=None)
        with pytest.raises(LyricsUnusableError):
            run(lookup(answer(body))[0])

    def test_unreadable_lrc_is_unusable(self) -> None:
        body = line_record(1, 262.0, syncedLyrics="no stamps at all")
        with pytest.raises(LyricsUnusableError):
            run(lookup(answer(body))[0])


class TestFailures:
    def test_lrclib_unreachable_is_unavailable(self) -> None:
        finder, getter, _ = lookup(OnlineRequestError("down"))
        with pytest.raises(LyricsUnavailableError):
            run(finder)
        assert len(getter.calls) == 1

    def test_no_lyrics_appear_in_the_log(self, caplog: pytest.LogCaptureFixture) -> None:
        caplog.set_level(logging.DEBUG)
        for body in (record(1, 262.0), line_record(2, 262.0), plain_record(3, 262.0)):
            run(lookup(answer(body))[0])
        assert not any(word in caplog.text for word in WORDS)


def settings(tmp_path: Path, **changes: object) -> WorkerSettings:
    return WorkerSettings(data_dir=tmp_path, **changes)  # type: ignore[arg-type]


class TestFactory:
    def test_lookups_off_give_disabled_without_requests(self, tmp_path: Path) -> None:
        getter = FakeGetter()
        off = settings(tmp_path, online=OnlineSettings(lookups_enabled=False))
        found = create_lyrics_lookup(off, getter)
        assert isinstance(found, DisabledLyricsLookup)
        assert found.lookup(FULL, Progress()) == LyricsResult(outcome="disabled")
        assert getter.calls == []

    def test_lookups_off_win_over_the_fake_stack(self, tmp_path: Path) -> None:
        off = settings(tmp_path, separator="fake", online=OnlineSettings(lookups_enabled=False))
        assert isinstance(create_lyrics_lookup(off, FakeGetter()), DisabledLyricsLookup)

    def test_the_fake_stack_uses_the_fake_provider(self, tmp_path: Path) -> None:
        fake = settings(tmp_path, separator="fake", lyrics=LyricsSettings(fake_mode="line"))
        found = create_lyrics_lookup(fake, FakeGetter())
        assert isinstance(found, FakeLyricsLookup)
        result = found.lookup(FULL, Progress())
        assert result.lyrics is not None
        assert result.lyrics.sync_level == "line"

    def test_the_real_stack_uses_lrclib(self, tmp_path: Path) -> None:
        getter = FakeGetter([answer(record(1, 262.0))])
        found = create_lyrics_lookup(settings(tmp_path), getter)
        assert isinstance(found, LrclibLookup)
        assert found.lookup(FULL, Progress()).outcome == "found"
        assert len(getter.calls) == 1

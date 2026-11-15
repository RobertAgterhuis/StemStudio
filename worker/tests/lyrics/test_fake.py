"""Fake LRCLIB provider of the fake stack (``STEMSTUDIO_FAKE_LYRICS``): deterministic modes."""

import pytest
from stemstudio_worker.lyrics.fake import FAKE_LINES, FAKE_SOURCE_ID, FakeLyricsLookup

from stemstudio_worker.ports import LyricsQuery, LyricsUnavailableError
from stemstudio_worker.schemas import LYRICS_STAGE_SEARCHING, LyricsResult

QUERY = LyricsQuery("Fake Song", 30.0, artist="The Fixture Band")


def progress_log() -> tuple[list[tuple[float, str]], object]:
    calls: list[tuple[float, str]] = []
    return calls, lambda fraction, stage: calls.append((fraction, stage))


def run(mode: str, query: LyricsQuery = QUERY) -> LyricsResult:
    return FakeLyricsLookup(mode).lookup(query, lambda fraction, stage: None)  # type: ignore[arg-type]


class TestTimedModes:
    def test_word_mode_gives_source_word_timing(self) -> None:
        lyrics = run("word").lyrics
        assert lyrics is not None
        assert (lyrics.sync_level, lyrics.origin, lyrics.language) == ("word", "source", "en")
        assert {w.origin for line in lyrics.lines for w in line.words} == {"source"}

    def test_line_mode_gives_interpolated_word_timing(self) -> None:
        lyrics = run("line").lyrics
        assert lyrics is not None
        assert (lyrics.sync_level, lyrics.origin) == ("line", "interpolated")

    @pytest.mark.parametrize("mode", ["word", "line"])
    def test_lines_are_spread_over_the_track(self, mode: str) -> None:
        lyrics = run(mode).lyrics
        assert lyrics is not None
        assert [line.text for line in lyrics.lines] == list(FAKE_LINES)
        assert [line.start_ms for line in lyrics.lines] == [6000, 12000, 18000, 24000]
        assert all(line.end_ms is not None and line.end_ms <= 30_000 for line in lyrics.lines)

    @pytest.mark.parametrize("mode", ["word", "line", "plain"])
    def test_the_document_names_the_fake_record(self, mode: str) -> None:
        lyrics = run(mode).lyrics
        assert lyrics is not None
        assert (lyrics.source, lyrics.source_id, lyrics.source_duration_seconds) == (
            "lrclib",
            FAKE_SOURCE_ID,
            30.0,
        )
        assert lyrics.plain_text == "\n".join(FAKE_LINES)

    def test_results_are_deterministic(self) -> None:
        assert run("word") == run("word")

    def test_a_very_short_track_still_gives_a_valid_document(self) -> None:
        assert run("line", LyricsQuery("Fake", 0.5)).outcome == "found"


class TestOtherModes:
    def test_plain_mode(self) -> None:
        lyrics = run("plain").lyrics
        assert lyrics is not None
        assert (lyrics.sync_level, lyrics.origin) == ("plain", "none")

    def test_instrumental_mode(self) -> None:
        result = run("instrumental")
        assert result.outcome == "instrumental"
        assert result.lyrics is not None
        assert (result.lyrics.instrumental, result.lyrics.lines) == (True, [])

    def test_not_found_mode(self) -> None:
        assert run("not-found") == LyricsResult(outcome="not-found")

    def test_failed_mode_raises_the_unreachable_error(self) -> None:
        with pytest.raises(LyricsUnavailableError):
            run("failed")

    def test_reports_the_searching_stage(self) -> None:
        calls, progress = progress_log()
        FakeLyricsLookup("word").lookup(QUERY, progress)  # type: ignore[arg-type]
        assert calls == [(0.0, LYRICS_STAGE_SEARCHING), (1.0, LYRICS_STAGE_SEARCHING)]

"""``lyricsfile`` YAML parsing (research F-03, R-03): word starts -> word ends."""

import pytest

from stemstudio_worker.lyrics.lyricsfile import parse_lyricsfile as parse
from stemstudio_worker.lyrics.normalize import MAX_INPUT_BYTES, MAX_WORDS_PER_LINE, DocumentMeta
from stemstudio_worker.ports import LyricsUnusableError
from stemstudio_worker.schemas import MAX_LYRICS_LINES, MAX_WORD_MS, TimedLyrics

# Shape of research F-03 with self-written text (no third-party lyrics in the repository).
SONG = """version: '1.0'
metadata:
  title: Fake Song
  artist: The Fixture Band
  album: Synthetic Sessions
  duration_ms: 262000
  instrumental: false
  language: en
lines:
  - text: Fake line one
    start_ms: 10950
    end_ms: 13200
    words:
      - {text: 'Fake ', start_ms: 10950}
      - {text: 'line ', start_ms: 11400}
      - {text: one, start_ms: 11900}
  - text: La la la
    start_ms: 14000
    end_ms: 16000
    words:
      - {text: La, start_ms: 14000}
      - {text: la, start_ms: 14500}
      - {text: la, start_ms: 15000}
"""


META = DocumentMeta(source="lrclib", source_id="1000001", source_duration_seconds=262.0)


def parse_lyricsfile(text: str, meta: DocumentMeta = META) -> TimedLyrics:
    return parse(text, meta)


def song_with_line(line: str) -> str:
    return f"version: '1.0'\nlines:\n{line}"


def spans(text: str) -> list[list[tuple[str, int | None, int | None]]]:
    lines = parse_lyricsfile(text).lines
    return [[(w.text, w.start_ms, w.end_ms) for w in line.words] for line in lines]


class TestWordTimes:
    def test_matches_the_word_synced_contract_example(self) -> None:
        assert spans(SONG) == [
            [("Fake", 10950, 11400), ("line", 11400, 11900), ("one", 11900, 13200)],
            [("La", 14000, 14500), ("la", 14500, 15000), ("la", 15000, 16000)],
        ]

    def test_lines_keep_their_times_text_and_origin(self) -> None:
        line = parse_lyricsfile(SONG).lines[0]
        assert (line.start_ms, line.end_ms, line.text) == (10950, 13200, "Fake line one")
        assert {word.origin for word in line.words} == {"source"}

    def test_language_comes_from_the_metadata(self) -> None:
        assert parse_lyricsfile(SONG).language == "en"

    def test_language_is_none_without_metadata(self) -> None:
        text = song_with_line(
            "  - {text: a, start_ms: 0, end_ms: 900, words: [{text: a, start_ms: 0}]}"
        )
        assert parse_lyricsfile(text).language is None

    def test_last_word_is_capped_at_three_seconds(self) -> None:
        text = song_with_line(
            "  - {text: Ooh, start_ms: 1000, end_ms: 9000, words: [{text: Ooh, start_ms: 1000}]}"
        )
        assert spans(text) == [[("Ooh", 1000, 1000 + MAX_WORD_MS)]]

    def test_inner_words_are_capped_at_three_seconds(self) -> None:
        text = song_with_line(
            "  - text: a b\n    start_ms: 0\n    end_ms: 9000\n"
            "    words: [{text: a, start_ms: 0}, {text: b, start_ms: 5000}]"
        )
        assert spans(text) == [[("a", 0, MAX_WORD_MS), ("b", 5000, 5000 + MAX_WORD_MS)]]

    def test_words_with_the_same_start_get_one_millisecond(self) -> None:
        text = song_with_line(
            "  - text: a b\n    start_ms: 100\n    end_ms: 900\n"
            "    words: [{text: a, start_ms: 100}, {text: b, start_ms: 100}]"
        )
        assert spans(text) == [[("a", 100, 101), ("b", 100, 900)]]

    def test_the_line_grows_to_hold_its_words(self) -> None:
        text = song_with_line(
            "  - text: a b\n    start_ms: 500\n    end_ms: 600\n"
            "    words: [{text: a, start_ms: 400}, {text: b, start_ms: 600}]"
        )
        line = parse_lyricsfile(text).lines[0]
        assert (line.start_ms, line.end_ms) == (400, 601)
        assert [(w.start_ms, w.end_ms) for w in line.words] == [(400, 600), (600, 601)]

    def test_empty_words_and_lines_are_skipped(self) -> None:
        text = song_with_line(
            "  - {text: '', start_ms: 0, end_ms: 100, words: []}\n"
            "  - text: a\n    start_ms: 200\n    end_ms: 900\n"
            "    words: [{text: ' ', start_ms: 200}, {text: a, start_ms: 300}]"
        )
        assert spans(text) == [[("a", 300, 900)]]

    def test_a_timed_line_without_words_is_interpolated_from_its_text(self) -> None:
        text = song_with_line("  - {text: a b, start_ms: 0, end_ms: 1000}")
        line = parse_lyricsfile(text).lines[0]
        assert [(w.text, w.start_ms, w.end_ms) for w in line.words] == [
            ("a", 0, 500),
            ("b", 500, 1000),
        ]
        assert {word.origin for word in line.words} == {"interpolated"}

    def test_the_document_is_word_synced_from_the_source(self) -> None:
        document = parse_lyricsfile(SONG)
        assert (document.sync_level, document.origin, document.source) == (
            "word",
            "source",
            "lrclib",
        )
        assert document.source_id == "1000001"
        assert document.plain_text == "Fake line one\nLa la la"

    def test_the_record_plain_text_is_kept(self) -> None:
        meta = DocumentMeta(source="lrclib", plain_text="Fake line one\n\nLa la la")
        assert parse_lyricsfile(SONG, meta).plain_text == "Fake line one\n\nLa la la"

    def test_the_metadata_language_wins_over_the_meta(self) -> None:
        meta = DocumentMeta(source="lrclib", language="nl")
        assert parse_lyricsfile(SONG, meta).language == "en"

    def test_the_meta_language_is_kept_without_metadata_language(self) -> None:
        meta = DocumentMeta(source="lrclib", language="nl")
        text = song_with_line(
            "  - {text: a, start_ms: 0, end_ms: 900, words: [{text: a, start_ms: 0}]}"
        )
        assert parse_lyricsfile(text, meta).language == "nl"


class TestUnusable:
    @pytest.mark.parametrize(
        "text",
        [
            "lines: [",
            "- just\n- a list",
            "lines: 5",
            "lines:\n  - {text: a, start_ms: x, end_ms: 9, words: []}",
            "lines:\n  - {text: a, end_ms: 9, words: []}",
            "lines:\n  - {text: a, start_ms: -5, end_ms: 9, words: [{text: a, start_ms: -5}]}",
            "lines:\n  - {text: a, start_ms: -5, end_ms: 9, words: [{text: a, start_ms: 0}]}",
            "!!python/object/apply:os.system ['echo unsafe']",
            "version: '1.0'\nmetadata: {instrumental: true}\nlines: []",
        ],
        ids=[
            "yaml",
            "not-a-mapping",
            "lines-type",
            "start-type",
            "missing-start",
            "negative-word",
            "negative-line",
            "unsafe-tag",
            "no-lines",
        ],
    )
    def test_malformed_input_is_unusable(self, text: str) -> None:
        with pytest.raises(LyricsUnusableError):
            parse_lyricsfile(text)

    def test_more_than_400_lines_is_unusable(self) -> None:
        line = "  - {text: a, start_ms: 0, end_ms: 9, words: [{text: a, start_ms: 0}]}\n"
        with pytest.raises(LyricsUnusableError):
            parse_lyricsfile(song_with_line(line * (MAX_LYRICS_LINES + 1)))

    def test_more_than_40_words_is_unusable(self) -> None:
        words = ", ".join(f"{{text: a, start_ms: {i}}}" for i in range(MAX_WORDS_PER_LINE + 1))
        with pytest.raises(LyricsUnusableError):
            parse_lyricsfile(
                song_with_line(f"  - {{text: a, start_ms: 0, end_ms: 99, words: [{words}]}}")
            )

    def test_a_line_over_500_characters_is_unusable(self) -> None:
        text = "a" * 501
        line = f"  - {{text: {text}, start_ms: 0, end_ms: 9, words: [{{text: a, start_ms: 0}}]}}"
        with pytest.raises(LyricsUnusableError):
            parse_lyricsfile(song_with_line(line))

    def test_input_over_one_megabyte_is_unusable(self) -> None:
        with pytest.raises(LyricsUnusableError):
            parse_lyricsfile("version: '1.0'\n#" + "a" * MAX_INPUT_BYTES)

    def test_words_going_back_in_time_are_unusable_in_the_document(self) -> None:
        text = song_with_line(
            "  - text: a b\n    start_ms: 0\n    end_ms: 900\n"
            "    words: [{text: a, start_ms: 500}, {text: b, start_ms: 100}]"
        )
        with pytest.raises(LyricsUnusableError):
            parse_lyricsfile(text)

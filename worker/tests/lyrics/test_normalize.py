"""Shared normalisation: input limits, word splitting, plain documents (research R-03)."""

import pytest

from stemstudio_worker.lyrics.normalize import (
    MAX_INPUT_BYTES,
    MAX_LINE_CHARS,
    MAX_WORD_CHARS,
    MAX_WORDS_PER_LINE,
    DocumentMeta,
    check_input,
    check_line,
    finish,
    plain_document,
    plain_lines,
    split_words,
)
from stemstudio_worker.ports import LyricsUnusableError
from stemstudio_worker.schemas import (
    LYRICS_UNUSABLE_TEXT,
    MAX_LYRICS_CHARS,
    MAX_LYRICS_LINES,
    LyricsLine,
    LyricsWord,
)

META = DocumentMeta(source="lrclib", source_id="1000004", source_duration_seconds=266.0)


def timed_line(start: int, end: int, *words: tuple[str, int, int]) -> LyricsLine:
    return LyricsLine(
        start_ms=start,
        end_ms=end,
        text=" ".join(text for text, _, _ in words),
        words=[LyricsWord(text=t, start_ms=s, end_ms=e, origin="source") for t, s, e in words],
    )


class TestCheckInput:
    def test_normalises_line_breaks(self) -> None:
        assert check_input("one\r\ntwo\rthree\n") == "one\ntwo\nthree\n"

    def test_accepts_exactly_one_megabyte(self) -> None:
        assert len(check_input("a" * MAX_INPUT_BYTES)) == MAX_INPUT_BYTES

    def test_rejects_more_than_one_megabyte_of_utf8(self) -> None:
        with pytest.raises(LyricsUnusableError) as caught:
            check_input("é" * (MAX_INPUT_BYTES // 2 + 1))
        assert str(caught.value) == LYRICS_UNUSABLE_TEXT


class TestSplitWords:
    def test_splits_on_any_whitespace(self) -> None:
        assert split_words("  Fake\tline  one ") == ["Fake", "line", "one"]

    def test_splits_cjk_per_character(self) -> None:
        assert split_words("你好 世界") == ["你", "好", "世", "界"]

    def test_keeps_latin_runs_next_to_cjk_together(self) -> None:
        assert split_words("愛loveよ") == ["愛", "love", "よ"]

    def test_attaches_punctuation_to_the_previous_cjk_character(self) -> None:
        assert split_words("ラ、ラ?") == ["ラ、", "ラ?"]

    def test_keeps_leading_punctuation_as_its_own_word(self) -> None:
        assert split_words("「愛") == ["「", "愛"]

    def test_hangul_syllables_are_split(self) -> None:
        assert split_words("사랑") == ["사", "랑"]

    def test_right_to_left_words_split_on_spaces(self) -> None:
        assert split_words("שלום עולם") == [
            "שלום",
            "עולם",
        ]

    def test_empty_text_has_no_words(self) -> None:
        assert split_words("   ") == []


class TestCheckLine:
    def test_accepts_the_limits(self) -> None:
        words = ["w" * MAX_WORD_CHARS] + ["a"] * (MAX_WORDS_PER_LINE - 1)
        check_line(" ".join(words)[:MAX_LINE_CHARS], words[:1])
        check_line("a " * 20, ["a"] * MAX_WORDS_PER_LINE)

    @pytest.mark.parametrize(
        ("text", "words"),
        [
            ("a" * (MAX_LINE_CHARS + 1), ["a"]),
            ("many", ["a"] * (MAX_WORDS_PER_LINE + 1)),
            ("long", ["w" * (MAX_WORD_CHARS + 1)]),
            ("none", []),
        ],
        ids=["line-chars", "word-count", "word-chars", "no-words"],
    )
    def test_rejects_lines_over_the_limits(self, text: str, words: list[str]) -> None:
        with pytest.raises(LyricsUnusableError):
            check_line(text, words)


class TestPlainLines:
    def test_skips_empty_lines_and_strips(self) -> None:
        lines = plain_lines("  Fake line one \n\n La la la\n")
        assert [line.text for line in lines] == ["Fake line one", "La la la"]
        assert [word.text for word in lines[1].words] == ["La", "la", "la"]

    def test_plain_lines_have_no_times(self) -> None:
        line = plain_lines("Fake line one")[0]
        assert (line.start_ms, line.end_ms) == (None, None)
        assert all(w.start_ms is None and w.origin is None for w in line.words)

    def test_rejects_more_than_400_lines(self) -> None:
        with pytest.raises(LyricsUnusableError):
            plain_lines("la\n" * (MAX_LYRICS_LINES + 1))

    def test_accepts_400_lines(self) -> None:
        assert len(plain_lines("la\n" * MAX_LYRICS_LINES)) == MAX_LYRICS_LINES


class TestPlainDocument:
    def test_builds_a_plain_document_like_the_contract_example(self) -> None:
        document = plain_document("Fake line one\r\nLa la la", META)
        assert document.sync_level == "plain"
        assert document.origin == "none"
        assert document.source == "lrclib"
        assert document.source_id == "1000004"
        assert document.source_duration_seconds == 266.0
        assert document.plain_text == "Fake line one\nLa la la"
        assert [line.text for line in document.lines] == ["Fake line one", "La la la"]

    def test_text_without_words_is_unusable(self) -> None:
        with pytest.raises(LyricsUnusableError):
            plain_document(" \n \n", META)

    def test_text_over_the_document_limit_is_unusable(self) -> None:
        with pytest.raises(LyricsUnusableError):
            plain_document("la " * (MAX_LYRICS_CHARS // 3 + 1), META)


class TestFinish:
    def test_builds_a_timed_document_with_the_meta(self) -> None:
        line = timed_line(10950, 13200, ("Fake", 10950, 11400), ("one", 11400, 13200))
        meta = DocumentMeta(source="lrclib", source_id="1", language="en")
        document = finish([line], "word", "source", meta)
        assert document.language == "en"
        assert document.plain_text == "Fake one"
        assert document.lines == [line]

    def test_uses_the_meta_plain_text_when_given(self) -> None:
        line = timed_line(0, 500, ("Fake", 0, 500))
        meta = DocumentMeta(source="user", plain_text="Fake\n")
        assert finish([line], "word", "source", meta).plain_text == "Fake\n"

    def test_invariant_violations_are_unusable(self) -> None:
        late = timed_line(5000, 6000, ("late", 5000, 6000))
        early = timed_line(1000, 2000, ("early", 1000, 2000))
        with pytest.raises(LyricsUnusableError):
            finish([late, early], "word", "source", META)

    def test_a_document_without_lines_is_unusable(self) -> None:
        with pytest.raises(LyricsUnusableError):
            finish([], "line", "interpolated", META)

    def test_rejects_more_than_400_lines(self) -> None:
        lines = [timed_line(i * 10, i * 10 + 5, ("la", i * 10, i * 10 + 5)) for i in range(401)]
        with pytest.raises(LyricsUnusableError):
            finish(lines, "word", "source", META)

    def test_an_unknown_language_is_dropped(self) -> None:
        line = timed_line(0, 500, ("Fake", 0, 500))
        meta = DocumentMeta(source="lrclib", language="English (US)")
        assert finish([line], "word", "source", meta).language is None

    def test_a_language_tag_is_reduced_to_its_primary_subtag(self) -> None:
        line = timed_line(0, 500, ("Fake", 0, 500))
        meta = DocumentMeta(source="lrclib", language="PT-br")
        assert finish([line], "word", "source", meta).language == "pt"

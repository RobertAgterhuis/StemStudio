"""Word timing interpolated from line timing (research R-03, FR-003)."""

import pytest

from stemstudio_worker.lyrics.interpolate import (
    interpolate_line,
    interpolate_lrc,
    line_end_ms,
    vowel_weight,
)
from stemstudio_worker.lyrics.lrc import LrcLine
from stemstudio_worker.ports import LyricsUnusableError
from stemstudio_worker.schemas import MAX_WORD_MS, LyricsLine


def spans(line: LyricsLine) -> list[tuple[str, int | None, int | None]]:
    return [(word.text, word.start_ms, word.end_ms) for word in line.words]


class TestVowelWeight:
    @pytest.mark.parametrize(
        ("word", "weight"),
        [
            ("Fake", 2),
            ("la", 1),
            ("beautiful", 3),
            ("rhythm", 1),
            ("psst", 1),
            ("AEIOU", 1),
            ("café", 2),
            ("naïve", 2),
            ("øl", 1),
            ("καλημέρα", 4),
            ("привет", 2),
            ("愛", 1),
            ("...", 1),
        ],
        ids=[
            "fake",
            "la",
            "beautiful",
            "y-vowel",
            "none",
            "upper",
            "accent",
            "diaeresis",
            "o-slash",
            "greek",
            "cyrillic",
            "cjk",
            "punctuation",
        ],
    )
    def test_counts_vowel_groups_with_a_minimum_of_one(self, word: str, weight: int) -> None:
        assert vowel_weight(word) == weight


class TestLineEnd:
    def test_ends_at_the_next_line_when_that_is_sooner(self) -> None:
        assert line_end_ms("Fake line one", 0, 1000) == 1000

    def test_is_capped_at_60_ms_per_character_plus_1500(self) -> None:
        assert line_end_ms("Fake line one", 10_950, 14_000) == 10_950 + 60 * 13 + 1500

    def test_the_last_line_gets_the_cap(self) -> None:
        assert line_end_ms("La la la", 14_000, None) == 14_000 + 60 * 8 + 1500


class TestInterpolateLine:
    def test_matches_the_line_synced_contract_example(self) -> None:
        line = interpolate_line("Fake line one", 10_950, 14_000)
        assert (line.start_ms, line.end_ms, line.text) == (10_950, 13_230, "Fake line one")
        assert spans(line) == [
            ("Fake", 10_950, 11_710),
            ("line", 11_710, 12_470),
            ("one", 12_470, 13_230),
        ]

    def test_words_are_proportional_to_their_weight(self) -> None:
        line = interpolate_line("a beautiful", 0, None)
        assert spans(line) == [("a", 0, 540), ("beautiful", 540, 2160)]

    def test_words_are_contiguous_and_fill_the_line(self) -> None:
        line = interpolate_line("one two three four five six seven", 1000, 4321)
        assert line.words[0].start_ms == 1000
        assert line.words[-1].end_ms == line.end_ms == 4321
        for word, following in zip(line.words, line.words[1:], strict=False):
            assert word.end_ms == following.start_ms

    def test_every_word_is_interpolated_and_confident(self) -> None:
        line = interpolate_line("Fake line one", 0, None)
        assert {(w.origin, w.low_confidence) for w in line.words} == {("interpolated", False)}

    def test_cjk_lines_are_timed_per_character(self) -> None:
        line = interpolate_line("你好世界", 0, None)
        assert [(w.start_ms, w.end_ms) for w in line.words] == [
            (0, 435),
            (435, 870),
            (870, 1305),
            (1305, 1740),
        ]

    def test_a_long_single_word_is_capped_at_three_seconds(self) -> None:
        line = interpolate_line("O" + "o" * 38 + "h", 0, None)
        assert line.end_ms == 60 * 40 + 1500
        assert spans(line)[0][1:] == (0, MAX_WORD_MS)

    def test_a_line_without_words_is_unusable(self) -> None:
        with pytest.raises(LyricsUnusableError):
            interpolate_line("   ", 0, None)


class TestInterpolateLrc:
    def test_each_line_ends_at_the_next_start_or_its_cap(self) -> None:
        lines = interpolate_lrc([LrcLine(10_950, "Fake line one"), LrcLine(14_000, "La la la")])
        assert [(line.start_ms, line.end_ms) for line in lines] == [
            (10_950, 13_230),
            (14_000, 15_980),
        ]

    def test_matches_the_second_line_of_the_contract_example(self) -> None:
        line = interpolate_lrc([LrcLine(14_000, "La la la")])[0]
        assert spans(line) == [
            ("La", 14_000, 14_660),
            ("la", 14_660, 15_320),
            ("la", 15_320, 15_980),
        ]

    def test_a_break_ends_the_line_before_it_and_is_dropped(self) -> None:
        lines = interpolate_lrc(
            [LrcLine(0, "Fake line one"), LrcLine(1000, ""), LrcLine(5000, "La")]
        )
        assert [(line.text, line.end_ms) for line in lines] == [
            ("Fake line one", 1000),
            ("La", 6620),
        ]

    def test_lines_with_the_same_start_are_merged(self) -> None:
        lines = interpolate_lrc([LrcLine(1000, "Fake"), LrcLine(1000, "line")])
        assert [(line.start_ms, line.text) for line in lines] == [(1000, "Fake line")]

    def test_a_line_too_short_for_its_words_is_merged_with_the_next(self) -> None:
        lines = interpolate_lrc([LrcLine(0, "Fake line one"), LrcLine(5, "La"), LrcLine(9000, "b")])
        assert [(line.start_ms, line.text) for line in lines] == [
            (0, "Fake line one La"),
            (9000, "b"),
        ]

    def test_a_break_right_after_a_line_does_not_shrink_it_below_its_words(self) -> None:
        lines = interpolate_lrc([LrcLine(0, "Fake line one"), LrcLine(3, ""), LrcLine(9000, "b")])
        assert [(line.text, line.end_ms) for line in lines] == [
            ("Fake line one", 2280),
            ("b", 10_560),
        ]

    def test_no_lines_give_no_lines(self) -> None:
        assert interpolate_lrc([]) == []

    def test_breaks_alone_give_no_lines(self) -> None:
        assert interpolate_lrc([LrcLine(0, ""), LrcLine(10, "")]) == []

    def test_a_merged_line_over_the_word_limit_is_unusable(self) -> None:
        many = [LrcLine(0, " ".join(["la"] * 30)), LrcLine(0, " ".join(["la"] * 11))]
        with pytest.raises(LyricsUnusableError):
            interpolate_lrc(many)

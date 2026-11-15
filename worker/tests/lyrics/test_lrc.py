"""LRC parsing (research R-03): stamps, several stamps per line, offset, tags, breaks."""

import pytest

from stemstudio_worker.lyrics.lrc import LrcLine, parse_lrc
from stemstudio_worker.lyrics.normalize import MAX_INPUT_BYTES
from stemstudio_worker.ports import LyricsUnusableError
from stemstudio_worker.schemas import MAX_LYRICS_LINES

SONG = """[ti:Fake Song]
[ar:The Fixture Band]
[al:Synthetic Sessions]
[length:04:22]
[by:nobody]
[00:10.95]Fake line one
[00:14.00]La la la
[00:20.00]
[00:25.50]Back again
"""


class TestStamps:
    @pytest.mark.parametrize(
        ("stamp", "millis"),
        [
            ("[00:10.95]", 10_950),
            ("[00:10.950]", 10_950),
            ("[00:10.9]", 10_900),
            ("[01:02]", 62_000),
            ("[00:10:95]", 10_950),
            ("[12:00.01]", 720_010),
            ("[100:00.00]", 6_000_000),
        ],
        ids=["centis", "millis", "tenths", "no-fraction", "colon-fraction", "minutes", "long"],
    )
    def test_stamp_formats(self, stamp: str, millis: int) -> None:
        assert parse_lrc(f"{stamp}Fake line") == [LrcLine(millis, "Fake line")]

    def test_text_is_stripped(self) -> None:
        assert parse_lrc("[00:01.00]   Fake line  \n") == [LrcLine(1000, "Fake line")]

    def test_several_stamps_repeat_the_line(self) -> None:
        lines = parse_lrc("[00:10.00][00:50.00]Chorus la\n[00:30.00]Verse")
        assert lines == [
            LrcLine(10_000, "Chorus la"),
            LrcLine(30_000, "Verse"),
            LrcLine(50_000, "Chorus la"),
        ]

    def test_lines_are_sorted_by_start_and_stable(self) -> None:
        lines = parse_lrc("[00:05.00]b\n[00:01.00]a\n[00:05.00]c")
        assert [line.text for line in lines] == ["a", "b", "c"]

    def test_seconds_out_of_range_are_not_stamps(self) -> None:
        assert parse_lrc("[00:61.00]Fake") == []


class TestTagsAndBreaks:
    def test_metadata_tags_are_ignored(self) -> None:
        assert [line.text for line in parse_lrc(SONG)] == [
            "Fake line one",
            "La la la",
            "",
            "Back again",
        ]

    def test_an_empty_stamped_line_is_a_break(self) -> None:
        assert parse_lrc(SONG)[2] == LrcLine(20_000, "")

    def test_lines_without_a_stamp_are_ignored(self) -> None:
        assert parse_lrc("just text\n\n[00:01.00]Fake") == [LrcLine(1000, "Fake")]

    def test_empty_input_has_no_lines(self) -> None:
        assert parse_lrc("") == []


class TestOffset:
    def test_positive_offset_shows_lyrics_earlier(self) -> None:
        assert parse_lrc("[offset:+500]\n[00:10.00]Fake") == [LrcLine(9_500, "Fake")]

    def test_negative_offset_shows_lyrics_later(self) -> None:
        assert parse_lrc("[00:10.00]Fake\n[offset:-250]") == [LrcLine(10_250, "Fake")]

    def test_unsigned_offset_counts_as_positive(self) -> None:
        assert parse_lrc("[offset: 1000 ]\n[00:10.00]Fake") == [LrcLine(9_000, "Fake")]

    def test_offset_never_goes_below_zero(self) -> None:
        assert parse_lrc("[offset:+5000]\n[00:01.00]Fake") == [LrcLine(0, "Fake")]

    def test_a_very_long_offset_is_ignored(self) -> None:
        text = "[offset:+" + "9" * 5000 + "]\n[00:01.00]Fake"
        assert parse_lrc(text) == [LrcLine(1000, "Fake")]

    def test_a_large_negative_offset_is_clamped_to_ten_minutes(self) -> None:
        assert parse_lrc("[offset:-9999999]\n[00:01.00]Fake") == [LrcLine(601_000, "Fake")]

    def test_a_large_positive_offset_is_clamped_to_ten_minutes(self) -> None:
        text = "[offset:+9999999]\n[10:01.00]Fake\n[20:00.00]Late"
        assert parse_lrc(text) == [LrcLine(1000, "Fake"), LrcLine(600_000, "Late")]

    def test_an_unreadable_offset_is_ignored(self) -> None:
        assert parse_lrc("[offset:soon]\n[00:01.00]Fake") == [LrcLine(1000, "Fake")]


class TestLimits:
    def test_more_than_400_stamped_lines_is_unusable(self) -> None:
        text = "\n".join(f"[00:{i % 60:02d}.00]la" for i in range(MAX_LYRICS_LINES + 1))
        with pytest.raises(LyricsUnusableError):
            parse_lrc(text)

    def test_400_lines_are_accepted(self) -> None:
        text = "\n".join(f"[00:{i % 60:02d}.00]la" for i in range(MAX_LYRICS_LINES))
        assert len(parse_lrc(text)) == MAX_LYRICS_LINES

    def test_input_over_one_megabyte_is_unusable(self) -> None:
        with pytest.raises(LyricsUnusableError):
            parse_lrc("[00:01.00]" + "a" * MAX_INPUT_BYTES)

    def test_carriage_returns_are_normalised(self) -> None:
        assert parse_lrc("[00:01.00]a\r\n[00:02.00]b\r") == [LrcLine(1000, "a"), LrcLine(2000, "b")]

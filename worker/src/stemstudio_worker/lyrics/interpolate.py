"""Word timing interpolated from line timing (research R-03, FR-003).

A line spans ``[start, next line start)`` but at most ``60 ms * characters + 1500 ms`` (so a line
before a long instrumental break does not stretch); its words get consecutive slots proportional
to their weight (vowel groups, Unicode-aware, at least 1; CJK characters are words of their own)
and last at most 3000 ms. A line that is too short to give each weight unit 1 ms (for instance
two LRC lines with the same stamp) is merged with the line after it.
"""

import re
import unicodedata
from collections.abc import Sequence
from itertools import accumulate
from typing import Final

from stemstudio_worker.lyrics.lrc import LrcLine, parse_lrc
from stemstudio_worker.lyrics.normalize import (
    DocumentMeta,
    WordSpan,
    finish,
    split_words,
    timed_line,
)
from stemstudio_worker.schemas import MAX_WORD_MS, LyricsLine, TimedLyrics

MS_PER_CHARACTER: Final = 60
"""Longest line duration per character (ms)."""

LINE_PADDING_MS: Final = 1500
"""Added to the per-character line duration (ms)."""

_VOWELS: Final = re.compile(
    r"[aeiouy\u00e6\u00f8\u0153"  # Latin
    r"\u03b1\u03b5\u03b7\u03b9\u03bf\u03c5\u03c9"  # Greek
    r"\u0430\u0435\u0451\u0438\u043e\u0443\u044b\u044d\u044e\u044f\u0456\u0457\u0454]+"  # Cyrillic
)


def vowel_weight(word: str) -> int:
    """Number of vowel groups in ``word`` (accents ignored; Latin, Greek, Cyrillic), at least 1."""
    folded = unicodedata.normalize("NFKD", word.casefold())
    letters = "".join(char for char in folded if not unicodedata.combining(char))
    return max(1, len(_VOWELS.findall(letters)))


def line_end_ms(text: str, start_ms: int, next_start_ms: int | None) -> int:
    """End of a line: the next start, at most ``60 ms * len(text) + 1500 ms`` after its start."""
    cap = start_ms + MS_PER_CHARACTER * len(text) + LINE_PADDING_MS
    return cap if next_start_ms is None else min(next_start_ms, cap)


def interpolate_line(text: str, start_ms: int, next_start_ms: int | None) -> LyricsLine:
    """Time the words of one line proportionally to their weights (origin ``interpolated``).

    Args:
        text: The line text.
        start_ms: Line start.
        next_start_ms: Start of the next line or break; None for the last line.

    Raises:
        LyricsUnusableError: No words, over a limit, or too short for its words.
    """
    text = text.strip()
    end_ms = line_end_ms(text, start_ms, next_start_ms)
    spans = _slots(split_words(text), start_ms, end_ms)
    return timed_line(text, (start_ms, end_ms), spans, "interpolated")


def _slots(words: Sequence[str], start_ms: int, end_ms: int) -> list[WordSpan]:
    """Consecutive word slots over ``[start_ms, end_ms]`` proportional to the vowel weights."""
    weights = [vowel_weight(word) for word in words]
    total = sum(weights) or 1
    bounds = [
        start_ms + (end_ms - start_ms) * part // total for part in accumulate(weights, initial=0)
    ]
    return [
        (word, begin, min(until, begin + MAX_WORD_MS))
        for word, begin, until in zip(words, bounds, bounds[1:], strict=False)
    ]


def lrc_document(text: str, meta: DocumentMeta) -> TimedLyrics:
    """Build a line-synced document (origin ``interpolated``) from LRC text.

    Raises:
        LyricsUnusableError: No stamped lines, over a limit or a broken invariant.
    """
    return finish(interpolate_lrc(parse_lrc(text)), "line", "interpolated", meta)


def interpolate_lrc(lines: Sequence[LrcLine]) -> list[LyricsLine]:
    """Time parsed LRC lines; breaks end the line before them and are dropped.

    Raises:
        LyricsUnusableError: A (merged) line is over a limit.
    """
    merged = _merge_crowded(lines)
    nexts: list[int | None] = [line.start_ms for line in merged[1:]]
    nexts.append(None)
    return [
        interpolate_line(line.text, line.start_ms, following)
        for line, following in zip(merged, nexts, strict=False)
        if line.text
    ]


def _merge_crowded(lines: Sequence[LrcLine]) -> list[LrcLine]:
    """Merge every line that is too short for its words into the line (or break) after it."""
    merged: list[LrcLine] = []
    for line in lines:
        if merged and _crowded(merged[-1], line.start_ms):
            merged[-1] = _join(merged[-1], line)
        else:
            merged.append(line)
    return merged


def _join(first: LrcLine, second: LrcLine) -> LrcLine:
    """``first`` with the text of ``second`` appended (a break adds nothing)."""
    return LrcLine(first.start_ms, " ".join(part for part in (first.text, second.text) if part))


def _crowded(line: LrcLine, next_start_ms: int) -> bool:
    """True when ``line`` has words and less than 1 ms per weight unit before the next start."""
    weight = sum(vowel_weight(word) for word in split_words(line.text))
    return bool(line.text) and next_start_ms - line.start_ms < weight

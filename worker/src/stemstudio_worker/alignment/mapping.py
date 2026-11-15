"""Aligned words -> our ``TimedLyrics`` document, and the collapse check (research.md R-04, P0-2).

Our tokens are the whitespace-separated words of the non-empty text lines. stable-ts keeps the
given text but may split or join words differently, so its words are mapped back to our tokens
by character position (whitespace ignored, Unicode NFC + case-folded); any other difference fails
the alignment. Times become integer milliseconds and are repaired into valid spans: starts never
decrease, every word lasts 1 .. ``MAX_WORD_MS`` ms and lies inside its line. Words with a
probability below 0.2 or shorter than ``COLLAPSE_SHORT_WORD_MS`` are flagged ``lowConfidence``.
"""

import itertools
import unicodedata
from collections.abc import Sequence
from dataclasses import dataclass

from pydantic import ValidationError

from stemstudio_worker.ports import (
    COLLAPSE_OVERLAP_SHARE,
    COLLAPSE_SHORT_WORD_MS,
    COLLAPSE_SHORT_WORD_SHARE,
    AlignmentError,
)
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    MAX_WORD_MS,
    LyricsLine,
    LyricsWord,
    TimedLyrics,
)

LOW_PROBABILITY = 0.2
"""Aligned words below this probability are flagged ``lowConfidence``."""


@dataclass(frozen=True, slots=True)
class AlignedWord:
    """One word as the aligner returned it (times in seconds)."""

    text: str
    start: float
    end: float
    probability: float


@dataclass(frozen=True, slots=True)
class _Timed:
    """One of our tokens with its aligned span in ms."""

    text: str
    start: int
    end: int
    probability: float


def line_tokens(text: str) -> list[list[str]]:
    """The words of every non-empty line of ``text``."""
    return [line.split() for line in text.splitlines() if line.strip()]


def _squash(text: str) -> str:
    return "".join(unicodedata.normalize("NFC", text).casefold().split())


def _assign(tokens: Sequence[str], words: Sequence[AlignedWord]) -> list[_Timed]:
    """Give each token the span of the aligned words that cover its characters."""
    if _squash("".join(w.text for w in words)) != _squash("".join(tokens)):
        raise AlignmentError(ALIGNMENT_FAILED_TEXT)
    ends, position = [], 0
    for word in words:
        position += len(_squash(word.text))
        ends.append(position)
    return _cover(tokens, words, ends)


def _cover(tokens: Sequence[str], words: Sequence[AlignedWord], ends: list[int]) -> list[_Timed]:
    timed, first, offset = [], 0, 0
    for token in tokens:
        offset += len(_squash(token))
        last = first
        while ends[last] < offset:
            last += 1
        span = words[first : last + 1]
        timed.append(_Timed(token, round(span[0].start * 1000), round(span[-1].end * 1000),
                            min(w.probability for w in span)))  # fmt: skip
        first = last + 1 if ends[last] == offset else last
    return timed


def _word(item: _Timed, bounds: tuple[int, int], cursor: int) -> LyricsWord:
    """Repair one token's span into ``bounds`` (start never before ``cursor``)."""
    low, high = bounds
    start = min(max(item.start, cursor, low), high - 1)
    end = min(max(item.end, start + 1), start + MAX_WORD_MS, high)
    short = item.end - item.start < COLLAPSE_SHORT_WORD_MS
    return LyricsWord(text=item.text[:100], start_ms=start, end_ms=end, origin="aligned",
                      low_confidence=short or item.probability < LOW_PROBABILITY)  # fmt: skip


_UNBOUNDED = (0, 1 << 31)


def _span(item: LyricsWord | LyricsLine) -> tuple[int, int]:
    """``(start, end)`` of a timed word or line (0 for a missing time)."""
    return item.start_ms or 0, item.end_ms or 0


def _line(items: Sequence[_Timed], bounds: tuple[int, int] | None, cursor: int) -> LyricsLine:
    """One line: its words repaired into ``bounds`` (kept as the line span) or unbounded."""
    words = []
    for item in items:
        words.append(_word(item, bounds or _UNBOUNDED, cursor))
        cursor = _span(words[-1])[0]
    start, end = bounds or _words_span(words)
    text = " ".join(item.text for item in items)[:500]
    return LyricsLine(start_ms=start, end_ms=end, text=text, words=words)


def _words_span(words: Sequence[LyricsWord]) -> tuple[int, int]:
    """From the first word's start to the latest word end."""
    return _span(words[0])[0], max(_span(w)[1] for w in words)


def _bounds(refine: TimedLyrics | None, count: int) -> list[tuple[int, int] | None]:
    """Per line: the refined line span, or None (unbounded)."""
    if refine is None:
        return [None] * count
    if len(refine.lines) != count:
        raise AlignmentError(ALIGNMENT_FAILED_TEXT)
    return [_span(line) for line in refine.lines]


def _document(
    text: str, lines: list[list[str]], timed: list[_Timed], refine: TimedLyrics | None
) -> TimedLyrics:
    built: list[LyricsLine] = []
    cursor, index = 0, 0
    for tokens, bounds in zip(lines, _bounds(refine, len(lines)), strict=True):
        built.append(_line(timed[index : index + len(tokens)], bounds, cursor))
        index, cursor = index + len(tokens), _span(built[-1].words[-1])[0]
    return TimedLyrics(sync_level="word", origin="aligned", source="user", lines=built,
                       plain_text=text)  # fmt: skip


def build_document(
    text: str,
    words: Sequence[AlignedWord],
    language: str | None,
    refine: TimedLyrics | None = None,
) -> TimedLyrics:
    """Map ``words`` onto the lines of ``text`` (inside ``refine``'s line times when given).

    Raises:
        AlignmentError: The words do not spell ``text``, a refine document has other lines, or
            the document breaks the limits (``ALIGNMENT_FAILED_TEXT``).
    """
    lines = line_tokens(text)
    timed = _assign([t for line in lines for t in line], words)
    try:
        document = _document(text, lines, timed, refine)
    except ValidationError as exc:
        raise AlignmentError(ALIGNMENT_FAILED_TEXT) from exc
    return document.model_copy(update={"language": language})


def _short_share(words: Sequence[LyricsWord]) -> float:
    """Share of words shorter than ``COLLAPSE_SHORT_WORD_MS``."""
    short = [end - start < COLLAPSE_SHORT_WORD_MS for start, end in map(_span, words)]
    return sum(short) / len(short)


def _overlap_share(lines: Sequence[LyricsLine]) -> float:
    """Share of lines (after the first) that start before the previous line ends."""
    spans = [_span(line) for line in lines]
    overlaps = [nxt[0] < prev[1] for prev, nxt in itertools.pairwise(spans)]
    return sum(overlaps) / max(len(overlaps), 1)


def is_collapsed(document: TimedLyrics) -> bool:
    """Whether an alignment collapsed (P0-2): too many very short words or overlapping lines."""
    words = [w for line in document.lines for w in line.words]
    if not words:
        return False
    return (
        _short_share(words) > COLLAPSE_SHORT_WORD_SHARE
        or _overlap_share(document.lines) > COLLAPSE_OVERLAP_SHARE
    )

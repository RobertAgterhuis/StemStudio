"""LRC parser (LRCLIB ``syncedLyrics``; research R-03).

Stamps ``[mm:ss]``, ``[mm:ss.x]``, ``[mm:ss.xx]``, ``[mm:ss.xxx]`` (also ``:`` before the
fraction); several stamps in front of one text repeat it; ``[offset:+/-ms]`` is applied (positive
= lyrics earlier, never below 0); other tags (``[ar:]``, ``[ti:]``, ...) and unstamped rows are
ignored; a stamp without text is a break (it ends the previous line).
"""

import re
from collections.abc import Iterator
from dataclasses import dataclass
from typing import Final

from stemstudio_worker.lyrics.normalize import check_input, check_line_count

_STAMP: Final = re.compile(r"\[(\d{1,3}):([0-5]\d)(?:[.:](\d{1,3}))?\]")
_OFFSET: Final = re.compile(r"\[offset:\s*([+-]?\d+)\s*\]", re.IGNORECASE)


@dataclass(frozen=True, slots=True)
class LrcLine:
    """One stamped LRC line.

    Attributes:
        start_ms: Start after the offset (>= 0).
        text: The stripped text; "" for a break.
    """

    start_ms: int
    text: str


def parse_lrc(text: str) -> list[LrcLine]:
    """Parse LRC text into lines and breaks ordered by start (stable for equal starts).

    Raises:
        LyricsUnusableError: Over 1 MB or more than 400 lines with text.
    """
    rows = check_input(text).split("\n")
    offset = _offset(rows)
    lines = [LrcLine(max(0, start - offset), body) for row in rows for start, body in _entries(row)]
    check_line_count(sum(1 for line in lines if line.text))
    return sorted(lines, key=lambda line: line.start_ms)


def _offset(rows: list[str]) -> int:
    """The ``[offset:]`` value in ms (0 when absent or unreadable)."""
    for row in rows:
        match = _OFFSET.search(row)
        if match:
            return int(match.group(1))
    return 0


def _entries(row: str) -> Iterator[tuple[int, str]]:
    """``(start_ms, text)`` per leading stamp of one row."""
    row = row.strip()
    starts: list[int] = []
    position = 0
    while match := _STAMP.match(row, position):
        starts.append(_millis(match))
        position = match.end()
    body = row[position:].strip()
    return ((start, body) for start in starts)


def _millis(match: re.Match[str]) -> int:
    """Milliseconds of one stamp match."""
    minutes, seconds, fraction = match.groups()
    return int(minutes) * 60_000 + int(seconds) * 1000 + int((fraction or "").ljust(3, "0"))

"""Section times at the output boundary (data-model 2.2 step 9).

The analysis keeps precise frame times; only the result is rounded: inner boundaries to 0.1 s,
the first section starts at 0 and the last ends at the exact duration. Every end is the next
start, so rounding keeps the sections contiguous.
"""

from collections.abc import Sequence
from typing import Final

SECTION_TIME_DIGITS: Final = 1
"""Decimals of a section boundary (0.1 s)."""


def section_time(seconds: float) -> float:
    """``seconds`` rounded to a section boundary (0.1 s)."""
    return round(seconds, SECTION_TIME_DIGITS)


def contiguous_spans(inner_starts: Sequence[float], duration: float) -> list[tuple[float, float]]:
    """``(start, end)`` of the sections that start at 0 and at each of ``inner_starts`` (rounded).

    The caller passes ascending starts inside the song; the last section ends at ``duration``.
    """
    starts = [0.0, *(section_time(t) for t in inner_starts)]
    return list(zip(starts, [*starts[1:], duration], strict=True))

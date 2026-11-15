"""The beat grid as the time base of a score (data-model.md 2.4, binding; ADR-017 D4).

Seconds map to a fractional beat index by linear interpolation over the beats (tempo drift and
rubato land in the right bar) and by extrapolation with the median period outside them. Bar 1
starts at the beat nearest to the first downbeat; positions are quarter notes from there,
snapped to sixteenths or eighths. A manual tempo generates constant beats over the input.
"""

import bisect
import itertools
import math
import statistics
from collections.abc import Sequence
from dataclasses import dataclass
from fractions import Fraction
from typing import Final

from stemstudio_worker.schemas import BeatGridTempo, ManualTempo, ScoreResolution

_STEPS: Final[dict[ScoreResolution, Fraction]] = {
    "sixteenth": Fraction(1, 4),
    "eighth": Fraction(1, 2),
}
_EPSILON: Final = 1e-9


def grid_step(resolution: ScoreResolution) -> Fraction:
    """Return the grid step of ``resolution`` in quarter notes."""
    return _STEPS[resolution]


def round_bpm(bpm: float) -> int:
    """Round a tempo half up (the tempo mark)."""
    return math.floor(bpm + 0.5)


def manual_beats(grid: ManualTempo, duration_seconds: float) -> tuple[list[float], int]:
    """Generate constant beats around ``[0, duration]`` through the first downbeat.

    Returns:
        The beats (the first at or before 0, the last at or after the end, at least two) and
        the index of the first downbeat among them.
    """
    period = 60.0 / grid.bpm
    first = grid.first_downbeat_seconds
    before = math.ceil(first / period - _EPSILON)
    after = max(1, math.ceil((duration_seconds - first) / period - _EPSILON))
    return [first + k * period for k in range(-before, after + 1)], before


def shift_grid(
    grid: BeatGridTempo | ManualTempo, offset_seconds: float
) -> BeatGridTempo | ManualTempo:
    """Move time 0 of ``grid`` to ``offset_seconds`` (a range or asset start).

    Beat-grid beats before the new start are kept (negative) for interpolation; a manual
    downbeat moves by whole bars so it stays at or after 0.
    """
    if isinstance(grid, ManualTempo):
        bar_seconds = 60.0 / grid.bpm * grid.beats_per_bar
        first = (grid.first_downbeat_seconds - offset_seconds) % bar_seconds
        return grid.model_copy(update={"first_downbeat_seconds": first})
    return grid.model_copy(
        update={
            "beats": [b - offset_seconds for b in grid.beats],
            "downbeats": [d - offset_seconds for d in grid.downbeats],
        }
    )


@dataclass(frozen=True, slots=True)
class Bar:
    """One measure of the score layout.

    Attributes:
        number: Measure number (0 = pickup).
        start: Start in quarters from the grid's first downbeat.
        length: Length in quarters (shorter than a bar only for the pickup).
    """

    number: int
    start: Fraction
    length: Fraction

    @property
    def end(self) -> Fraction:
        """End in quarters."""
        return self.start + self.length


def bar_layout(first_onset: Fraction, last_end: Fraction, beats_per_bar: int) -> tuple[Bar, ...]:
    """Lay out the measures from the first onset to the last end.

    Notes before the first downbeat form a pickup bar shortened to the used length; when they
    reach back more than a bar, the whole bars before it stay whole and bar 1 is the first full
    bar (the meter phase of the grid is kept). The layout always contains bar 1.
    """
    lead = max(Fraction(0), -first_onset)
    whole_before, partial = divmod(lead, beats_per_bar)
    bars = [Bar(0, -lead, partial)] if partial else []
    start = Fraction(-int(whole_before) * beats_per_bar)
    number = 1
    while number == 1 or start < last_end:
        bars.append(Bar(number, start, Fraction(beats_per_bar)))
        start += beats_per_bar
        number += 1
    return tuple(bars)


@dataclass(frozen=True, slots=True)
class GridQuantiser:
    """A time base with a resolution (the cleaning's ``Quantiser``)."""

    timebase: "TimeBase"
    step: Fraction

    @property
    def beats_per_bar(self) -> int:
        """Quarters per bar."""
        return self.timebase.beats_per_bar

    def snap(self, seconds: float) -> Fraction:
        """Return the grid position (quarters from bar 1) nearest to ``seconds`` (half up)."""
        slots = math.floor(self.timebase.quarters(seconds) / self.step + 0.5)
        return slots * self.step


@dataclass(frozen=True, slots=True)
class TimeBase:
    """Seconds -> quarter notes from bar 1.

    Attributes:
        beats: Beat times (ascending, at least two).
        downbeat_index: Index of the beat that starts bar 1.
        beats_per_bar: Quarters per bar.
        bpm: Rounded tempo of the tempo mark.
    """

    beats: tuple[float, ...]
    downbeat_index: int
    beats_per_bar: int
    bpm: int

    @classmethod
    def from_grid(cls, grid: BeatGridTempo | ManualTempo, duration_seconds: float) -> "TimeBase":
        """Build the time base of a (shifted) beat grid or a manual tempo."""
        if isinstance(grid, ManualTempo):
            beats, index = manual_beats(grid, duration_seconds)
        else:
            beats, index = grid.beats, _nearest(grid.beats, grid.downbeats[0])
        return cls(tuple(beats), index, grid.beats_per_bar, round_bpm(grid.bpm))

    @property
    def period(self) -> float:
        """Median beat period in seconds."""
        return statistics.median(b - a for a, b in itertools.pairwise(self.beats))

    @property
    def time_signature(self) -> str:
        """``"<beatsPerBar>/4"``."""
        return f"{self.beats_per_bar}/4"

    def quarters(self, seconds: float) -> float:
        """Return the position of ``seconds`` in quarters from bar 1 (``b(t) - k0``)."""
        return self._beat_index(seconds) - self.downbeat_index

    def quantiser(self, resolution: ScoreResolution) -> GridQuantiser:
        """Return the quantiser of this time base at ``resolution``."""
        return GridQuantiser(self, grid_step(resolution))

    def _beat_index(self, seconds: float) -> float:
        first, last = self.beats[0], self.beats[-1]
        if seconds < first:
            return (seconds - first) / self.period
        if seconds > last:
            return len(self.beats) - 1 + (seconds - last) / self.period
        upper = min(bisect.bisect_right(self.beats, seconds), len(self.beats) - 1)
        left, right = self.beats[upper - 1], self.beats[upper]
        return upper - 1 + (seconds - left) / (right - left)


def _nearest(beats: Sequence[float], seconds: float) -> int:
    """Return the index of the beat nearest to ``seconds``."""
    return min(range(len(beats)), key=lambda i: abs(beats[i] - seconds))

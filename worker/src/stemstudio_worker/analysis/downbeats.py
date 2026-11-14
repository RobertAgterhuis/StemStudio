"""Meter (beats per bar) and downbeat detection from per-beat accent strength.

Every candidate meter (3 or 4 beats per bar) and phase is scored by how much louder the beats at
that phase are than the other beats. The best candidate wins when its accent contrast is clear;
otherwise the time signature cannot be determined and 4/4 starting at the first beat is assumed.
"""

from dataclasses import dataclass

import numpy as np

from stemstudio_worker.analysis.tempo import FloatArray

DEFAULT_BEATS_PER_BAR = 4
"""Assumed beats per bar when no accent pattern is found (spec: assume 4/4)."""

CANDIDATE_METERS = (4, 3)
"""Meters tested, in tie-break order."""

MIN_ACCENT_CONTRAST = 1.25
"""Accented beats must be at least this many times stronger than the others on average."""

_EPSILON = 1e-9


@dataclass(frozen=True, slots=True)
class Meter:
    """A bar structure on the beat sequence.

    Attributes:
        beats_per_bar: Beats per bar (1..12).
        phase: Index of the first downbeat in the beat sequence (0 <= phase < beats_per_bar).
    """

    beats_per_bar: int
    phase: int


def detect_meter(strengths: FloatArray) -> Meter:
    """Detect the meter from the accent strength of every beat.

    Args:
        strengths: Onset strength at each beat, in beat order.

    Returns:
        The best meter, or 4/4 at phase 0 when no clear accent pattern exists.
    """
    candidates = [
        Meter(m, p) for m in CANDIDATE_METERS if len(strengths) >= 2 * m for p in range(m)
    ]
    scored = [(_contrast(strengths, c), c) for c in candidates]
    best_score, best = max(scored, key=lambda item: item[0], default=(0.0, None))
    if best is None or best_score < MIN_ACCENT_CONTRAST:
        return Meter(DEFAULT_BEATS_PER_BAR, 0)
    return best


def downbeat_times(beats: FloatArray, meter: Meter) -> list[float]:
    """Return the beats that start a bar under ``meter`` (a subset of ``beats``)."""
    return [float(t) for t in beats[meter.phase :: meter.beats_per_bar]]


def _contrast(strengths: FloatArray, meter: Meter) -> float:
    """Mean strength of the beats at ``meter.phase`` divided by the mean of the other beats."""
    accented = np.zeros(len(strengths), dtype=bool)
    accented[meter.phase :: meter.beats_per_bar] = True
    others = max(float(np.mean(strengths[~accented])), _EPSILON)
    return float(np.mean(strengths[accented])) / others

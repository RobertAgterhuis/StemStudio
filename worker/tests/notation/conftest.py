"""Synthetic note lists and a constant-tempo quantiser for the notation tests."""

from collections.abc import Iterable
from dataclasses import dataclass
from fractions import Fraction

from stemstudio_worker.ports import NoteEvent

BPM = 120.0
QUARTER_SECONDS = 60.0 / BPM


@dataclass(frozen=True, slots=True)
class ConstantQuantiser:
    """120 BPM, beat 0 at t = 0: one quarter is 0.5 s (a stand-in for the real time base)."""

    step: Fraction = Fraction(1, 4)
    beats_per_bar: int = 4

    def snap(self, seconds: float) -> Fraction:
        """Snap ``seconds`` to the nearest grid step in quarter notes."""
        return round(Fraction(seconds / QUARTER_SECONDS) / self.step) * self.step


def note(start: float, end: float, pitch: int = 60, velocity: float = 0.8) -> NoteEvent:
    """Return a note event (times in seconds)."""
    return NoteEvent(start=start, end=end, pitch=pitch, velocity=velocity)


def at_quarters(
    quarters: list[float], pitch: int = 60, length: float = 1.0, velocity: float = 0.8
) -> list[NoteEvent]:
    """Return one note per quarter position (120 BPM), ``length`` quarters long."""
    return [
        note(q * QUARTER_SECONDS, (q + length) * QUARTER_SECONDS, pitch, velocity) for q in quarters
    ]


def rows(items: Iterable[tuple[object, ...]]) -> list[tuple[object, ...]]:
    """Return ``items`` as a list (lets exact Fractions be compared with int/float literals)."""
    return list(items)

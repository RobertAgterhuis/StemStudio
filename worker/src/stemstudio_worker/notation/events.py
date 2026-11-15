"""Cleaned notes and hits -> timed events per voice, and events -> measure contents.

An event is one chord, note or drum stroke with an onset and an end in quarters; the events of
a voice never overlap. Pitched staves: notes with the same onset form a chord that lasts until
its longest note ends or the next onset of the staff, whichever comes first. Drums: the hands
voice (chords of simultaneous hits) and the feet voice (kick); a stroke lasts until the next
onset of its voice, at most a quarter and never past the bar line.
"""

import bisect
import itertools
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from fractions import Fraction
from typing import Any

from music21 import note as m21_note
from music21 import tie as m21_tie

from stemstudio_worker.notation.cleaning import PlacedHit, PlacedNote
from stemstudio_worker.notation.timebase import Bar

Make = Callable[[Fraction], m21_note.GeneralNote]
"""Builds the music21 element of an event (or a tied piece of it) for a length in quarters."""

Placed = tuple[Fraction, m21_note.GeneralNote]
"""An element and its offset inside a measure."""

_TIE: Callable[[str], Any] = m21_tie.Tie


@dataclass(frozen=True, slots=True)
class Event:
    """One chord, note or stroke of a voice.

    Attributes:
        onset: Start in quarters from bar 1.
        end: End in quarters (> onset).
        make: Builds the element for a length.
    """

    onset: Fraction
    end: Fraction
    make: Make


def chord_events[T: (PlacedNote, PlacedHit)](
    items: Sequence[T],
    end_of: Callable[[Sequence[T], Fraction | None], Fraction],
    make: Callable[[Sequence[T]], Make],
) -> list[Event]:
    """Group ``items`` by onset into events; ``end_of(group, next_onset)`` gives each end."""
    groups: list[tuple[Fraction, list[T]]] = _by_onset(items)
    nexts: list[Fraction | None] = [*(onset for onset, _ in groups[1:]), None]
    return [
        Event(onset, end_of(group, after), make(group))
        for (onset, group), after in zip(groups, nexts, strict=False)  # one None too many if empty
    ]


def _by_onset[T: (PlacedNote, PlacedHit)](items: Sequence[T]) -> list[tuple[Fraction, list[T]]]:
    """Return the items grouped by onset, in onset order."""
    ordered = sorted(items, key=_onset)
    return [(onset, list(group)) for onset, group in itertools.groupby(ordered, key=_onset)]


def note_end(group: Sequence[PlacedNote], next_onset: Fraction | None) -> Fraction:
    """End of a chord: its longest note, cut at the next onset of the staff."""
    longest = max(n.end for n in group)
    return longest if next_onset is None else min(longest, next_onset)


def hit_end_rule(beats_per_bar: int) -> Callable[[Sequence[PlacedHit], Fraction | None], Fraction]:
    """Return the end rule of drum strokes: next onset, at most a quarter, within the bar."""

    def end_of(group: Sequence[PlacedHit], next_onset: Fraction | None) -> Fraction:
        onset = group[0].onset
        bar_end = (onset // beats_per_bar + 1) * beats_per_bar
        limits = [onset + 1, Fraction(bar_end)]
        return min([*limits, next_onset] if next_onset is not None else limits)

    return end_of


def measure_contents(events: Sequence[Event], bar: Bar) -> list[Placed]:
    """Return the elements of one voice in ``bar``.

    Pieces of events are tied across bar lines; every gap gets a rest.
    """
    placed: list[Placed] = []
    cursor = bar.start
    for event in _overlapping(events, bar):
        start, stop = max(event.onset, bar.start), min(event.end, bar.end)
        if start > cursor:
            placed.append(_rest(cursor, start, bar))
        element = event.make(stop - start)
        _tie(element, event.onset < start, event.end > stop)
        placed.append((start - bar.start, element))
        cursor = stop
    if cursor < bar.end:
        placed.append(_rest(cursor, bar.end, bar))
    return placed


def _overlapping(events: Sequence[Event], bar: Bar) -> Sequence[Event]:
    """Return the events that overlap ``bar`` (events are sorted and do not overlap)."""
    low = bisect.bisect_right([e.end for e in events], bar.start)
    high = bisect.bisect_left([e.onset for e in events], bar.end)
    return events[low:high]


def _rest(start: Fraction, stop: Fraction, bar: Bar) -> Placed:
    return start - bar.start, m21_note.Rest(quarterLength=stop - start)


def _tie(element: m21_note.GeneralNote, from_before: bool, to_after: bool) -> None:
    """Mark a piece that continues from the previous and/or into the next bar."""
    if from_before and to_after:
        element.tie = _TIE("continue")
    elif from_before:
        element.tie = _TIE("stop")
    elif to_after:
        element.tie = _TIE("start")


def _onset(item: PlacedNote | PlacedHit) -> Fraction:
    return item.onset

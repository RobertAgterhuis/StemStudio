"""Strings and frets for guitar and bass tablature (data-model 4.5, research R-08).

:class:`ViterbiTabFingerer` fits every event into the tuning (octave moves, chord notes beyond
the string count dropped), lists the string/fret candidates of each event (chords: distinct
strings, the 64 cheapest), and picks the path with the lowest cost: hand-position change (mean
fretted fret; open strings are free) + 0.3 x stretch + 0.05 x frets above 12 + 2 for a chord
span over 4 frets (rejected when another candidate exists). Ties prefer lower frets, then
lower-pitched strings. :func:`fit_notes` applies the same range fit to the notator's notes
before the score is built, keeping the loudest chord notes.
"""

import itertools
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

from stemstudio_worker.notation.cleaning import PlacedNote
from stemstudio_worker.ports import TAB_MAX_FRET, TabAssignment, Tuning

MAX_CANDIDATES: Final = 64
"""Chord candidates kept per event."""

STRETCH_COST: Final = 0.3
HIGH_FRET_COST: Final = 0.05
HIGH_FRET: Final = 12
WIDE_COST: Final = 2.0
MAX_SPAN: Final = 4
"""A chord whose fretted frets span more than 4 frets (``max - min >= 4``) is a wide stretch."""

Position = tuple[int, int]
"""``(string, fret)``: string 1 = highest-pitched string, fret 0 = the capo."""


@dataclass(frozen=True, slots=True)
class _Candidate:
    """One way to play an event."""

    positions: tuple[Position, ...]
    cost: float
    hand: float | None
    wide: bool

    @property
    def order(self) -> tuple[int, tuple[int, ...]]:
        """Tie-break key: lower frets first, then lower-pitched strings (higher numbers)."""
        return sum(f for _, f in self.positions), tuple(-s for s, _ in self.positions)


@dataclass(frozen=True, slots=True)
class FitResult:
    """Notes fitted into a tuning and what it took."""

    notes: tuple[PlacedNote, ...]
    octave_moved: int
    notes_dropped: int


def _fit_pitch(pitch: int, tuning: Tuning, capo: int) -> int:
    """Move ``pitch`` by octaves into ``[lowest open string, top string fret 24]``."""
    low, high = tuning.pitches[0] + capo, tuning.pitches[-1] + TAB_MAX_FRET
    while pitch < low:
        pitch += 12
    while pitch > high:
        pitch -= 12
    return pitch


def fit_notes(notes: Sequence[PlacedNote], tuning: Tuning, capo: int) -> FitResult:
    """Range fit per note and the loudest ``strings`` notes per onset (data-model 4.5 steps 1-2)."""
    moved = dropped = 0
    kept: list[PlacedNote] = []
    for _, group in itertools.groupby(notes, key=lambda n: n.onset):
        chord = sorted(group, key=lambda n: -n.velocity)
        dropped += max(0, len(chord) - len(tuning.pitches))
        for placed in chord[: len(tuning.pitches)]:
            pitch = _fit_pitch(placed.pitch, tuning, capo)
            moved += pitch != placed.pitch
            kept.append(PlacedNote(placed.onset, placed.end, pitch, placed.velocity))
    ordered = sorted(kept, key=lambda n: (n.onset, n.pitch))
    return FitResult(tuple(ordered), moved, dropped)


def _cost(positions: tuple[Position, ...]) -> _Candidate:
    fretted = [f for _, f in positions if f > 0]
    stretch = max(fretted) - min(fretted) if fretted else 0
    wide = len(positions) > 1 and stretch >= MAX_SPAN
    cost = STRETCH_COST * stretch + HIGH_FRET_COST * sum(max(0, f - HIGH_FRET) for f in fretted)
    hand = sum(fretted) / len(fretted) if fretted else None
    return _Candidate(positions, cost + (WIDE_COST if wide else 0.0), hand, wide)


def _options(pitch: int, tuning: Tuning, capo: int) -> list[Position]:
    count = len(tuning.pitches)
    frets = (
        (count - i, pitch - (open_pitch + capo)) for i, open_pitch in enumerate(tuning.pitches)
    )
    return [(s, f) for s, f in frets if 0 <= f <= TAB_MAX_FRET - capo]


def _candidates(pitches: Sequence[int], tuning: Tuning, capo: int) -> list[_Candidate]:
    """The cheapest distinct-string assignments; wide ones only when nothing else exists."""
    choices = [_options(p, tuning, capo) for p in pitches]
    combos = (c for c in itertools.product(*choices) if len({s for s, _ in c}) == len(c))
    found = sorted((_cost(c) for c in combos), key=lambda c: (c.cost, c.order))[:MAX_CANDIDATES]
    narrow = [c for c in found if not c.wide]
    return narrow or found


def _playable(pitches: list[int], tuning: Tuning, capo: int) -> tuple[list[_Candidate], int]:
    """Candidates of the event, dropping trailing pitches until it can be played."""
    dropped = 0
    while pitches:
        found = _candidates(pitches, tuning, capo)
        if found:
            return found, dropped
        pitches, dropped = pitches[:-1], dropped + 1
    return [_Candidate((), 0.0, None, False)], dropped


def _move(a: _Candidate, b: _Candidate) -> float:
    return abs(a.hand - b.hand) if a.hand is not None and b.hand is not None else 0.0


def _best_link(previous: Sequence[_Candidate], scores: Sequence[float], here: _Candidate) -> int:
    """Index of the cheapest predecessor of ``here`` (ties: the lower index)."""
    totals = [score + _move(before, here) for before, score in zip(previous, scores, strict=True)]
    return min(range(len(totals)), key=lambda i: (totals[i], i))


def _viterbi(layers: Sequence[Sequence[_Candidate]]) -> list[_Candidate]:
    """The cheapest path through the candidate layers."""
    scores = [c.cost for c in layers[0]]
    back: list[list[int]] = []
    for previous, layer in itertools.pairwise(layers):
        best = [_best_link(previous, scores, c) for c in layer]
        scores = [
            scores[b] + _move(previous[b], c) + c.cost for b, c in zip(best, layer, strict=True)
        ]
        back.append(best)
    index = min(range(len(scores)), key=lambda i: (scores[i], layers[-1][i].order))
    path = [index]
    for links in reversed(back):
        path.append(links[path[-1]])
    return [layer[i] for layer, i in zip(layers, reversed(path), strict=True)]


class ViterbiTabFingerer:
    """``ports.TabFingerer``: Viterbi over string/fret candidates (research R-08)."""

    def assign(self, events: Sequence[Sequence[int]], tuning: Tuning, capo: int) -> TabAssignment:
        """Assign every event (sounding MIDI pitches, loudest first) to strings and frets."""
        if not events:
            return TabAssignment(positions=())
        moved = dropped = 0
        layers: list[list[_Candidate]] = []
        for event in events:
            fitted = [_fit_pitch(p, tuning, capo) for p in event[: len(tuning.pitches)]]
            moved += sum(a != b for a, b in zip(fitted, event, strict=False))
            found, lost = _playable(fitted, tuning, capo)
            dropped += max(0, len(event) - len(tuning.pitches)) + lost
            layers.append(found)
        path = _viterbi(layers)
        return TabAssignment(
            positions=tuple(c.positions for c in path),
            octave_moved=moved,
            notes_dropped=dropped,
            wide_stretches=sum(c.wide for c in path),
        )

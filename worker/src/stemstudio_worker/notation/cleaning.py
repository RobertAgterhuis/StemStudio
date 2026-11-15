"""Per-profile note cleaning before notation (data-model.md 2.3, binding order; ADR-017 D3).

Steps for pitched profiles: (1) drop pitches outside the range, (2) single voice: one note per
60 ms onset window (the louder wins), (3) quantise and, for single voice, end every note at the
next onset, (4) drop notes shorter than one grid step and merge duplicates, (5) cap the onsets
per voice per bar, (6) warnings from the counts. Drums: quantise, merge duplicates per piece and
cap the hands and feet voices (the bleed gate runs in the drum transcriber).
"""

import itertools
from collections import defaultdict
from collections.abc import Callable, Iterable, Sequence
from dataclasses import dataclass, replace
from fractions import Fraction
from typing import Final, Protocol

from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.notation.profiles import MAX_NOTES_PER_BAR, Profile, staff_of
from stemstudio_worker.ports import DrumHit, NoteEvent
from stemstudio_worker.schemas import DrumPiece, ScoreWarning

MONOPHONIC_WINDOW_SECONDS: Final = 0.060
"""Onsets closer than this to the previous kept onset compete (single-voice profiles)."""

NOTES_CLEANED_RATIO: Final = 0.10
"""``notes-cleaned`` is raised when more than this share of the notes was removed."""

PIECE_ORDER: Final[tuple[DrumPiece, ...]] = ("kick", "snare", "toms", "hihat", "cymbals")
"""Order of simultaneous drum hits (also the order of a hands chord, bottom to top)."""


class Quantiser(Protocol):
    """Maps seconds onto grid positions in quarter notes (the time base with a resolution)."""

    @property
    def step(self) -> Fraction:
        """Grid step in quarter notes (1/4 = sixteenth, 1/2 = eighth)."""
        ...

    @property
    def beats_per_bar(self) -> int:
        """Quarters per bar."""
        ...

    def snap(self, seconds: float) -> Fraction:
        """Return the grid position (quarters from bar 1) nearest to ``seconds``."""
        ...


class CleaningCounts(Protocol):
    """The counts that drive the cleaning warnings."""

    @property
    def total(self) -> int:
        """Notes (hits) given to the cleaning."""
        ...

    @property
    def removed(self) -> int:
        """Out-of-range and too-short notes."""
        ...

    @property
    def capped(self) -> int:
        """Notes left out by the per-bar cap."""
        ...


@dataclass(frozen=True, slots=True)
class PlacedNote:
    """A note on the grid; positions in quarter notes from bar 1 (negative = pickup).

    Attributes:
        onset: Snapped onset.
        end: Snapped end (> onset after cleaning).
        pitch: MIDI pitch.
        velocity: Loudness 0..1.
    """

    onset: Fraction
    end: Fraction
    pitch: int
    velocity: float

    @property
    def duration(self) -> Fraction:
        """Length in quarter notes."""
        return self.end - self.onset


@dataclass(frozen=True, slots=True)
class PlacedHit:
    """A drum hit on the grid.

    Attributes:
        onset: Snapped onset in quarters from bar 1.
        piece: Drum piece.
        strength: Onset strength 0..1.
    """

    onset: Fraction
    piece: DrumPiece
    strength: float

    @property
    def hands(self) -> bool:
        """True for the hands voice (everything but the kick)."""
        return self.piece != "kick"


@dataclass(frozen=True, slots=True)
class CleanedNotes:
    """Result of :func:`clean_notes`."""

    notes: tuple[PlacedNote, ...]
    total: int
    removed: int
    capped: int


@dataclass(frozen=True, slots=True)
class CleanedHits:
    """Result of :func:`clean_hits` (``removed`` is always 0: the gate ran before)."""

    hits: tuple[PlacedHit, ...]
    total: int
    removed: int
    capped: int


def keep_in_range(notes: Iterable[NoteEvent], profile: Profile) -> list[NoteEvent]:
    """Step 1: keep the notes inside the profile's pitch range."""
    return [n for n in notes if profile.contains(n.pitch)]


def reduce_monophonic(notes: Iterable[NoteEvent]) -> list[NoteEvent]:
    """Step 2: within 60 ms of the previous kept onset keep only the louder note."""
    kept: list[NoteEvent] = []
    for current in sorted(notes, key=lambda n: (n.start, -n.velocity)):
        if not kept or current.start - kept[-1].start >= MONOPHONIC_WINDOW_SECONDS:
            kept.append(current)
        elif current.velocity > kept[-1].velocity:
            kept[-1] = current
    return kept


def quantise(notes: Iterable[NoteEvent], quantiser: Quantiser) -> list[PlacedNote]:
    """Step 3: snap onsets and ends to the grid."""
    snap = quantiser.snap
    return [PlacedNote(snap(n.start), snap(n.end), n.pitch, n.velocity) for n in notes]


def end_at_next_onset(notes: Iterable[PlacedNote]) -> list[PlacedNote]:
    """Step 3 (single voice): one note per onset (the loudest), ending at the next onset."""
    ordered = sorted(notes, key=lambda n: (n.onset, -n.velocity))
    firsts = [next(group) for _, group in itertools.groupby(ordered, key=lambda n: n.onset)]
    nexts = [n.onset for n in firsts[1:]]
    return [replace(n, end=min([n.end, *nexts[i : i + 1]])) for i, n in enumerate(firsts)]


def drop_short(notes: Iterable[PlacedNote], step: Fraction) -> list[PlacedNote]:
    """Step 4: drop notes shorter than one grid step."""
    return [n for n in notes if n.duration >= step]


def merge_duplicates(notes: Iterable[PlacedNote]) -> list[PlacedNote]:
    """Step 4: merge notes of the same pitch and onset (longest end, loudest velocity)."""
    return _merge_by(notes, lambda n: (n.onset, n.pitch), _merge_notes)


def cap_per_bar[T](
    items: Sequence[T],
    slot: Callable[[T], tuple[int, int]],
    loudness: Callable[[T], float],
    limit: int,
) -> tuple[list[T], int]:
    """Step 5: per ``slot`` (bar, voice) keep the ``limit`` loudest items, in input order.

    Returns:
        The kept items and how many were left out.
    """
    groups: defaultdict[tuple[int, int], list[int]] = defaultdict(list)
    for index, item in enumerate(items):
        groups[slot(item)].append(index)
    keep: set[int] = set()
    for indexes in groups.values():
        keep.update(sorted(indexes, key=lambda i: -loudness(items[i]))[:limit])
    kept = [item for index, item in enumerate(items) if index in keep]
    return kept, len(items) - len(kept)


def clean_notes(notes: Sequence[NoteEvent], profile: Profile, quantiser: Quantiser) -> CleanedNotes:
    """Run cleaning steps 1-5 for a pitched profile."""
    in_range = keep_in_range(notes, profile)
    placed = _place(in_range, profile, quantiser)
    long_enough = drop_short(placed, quantiser.step)
    slot = _note_slot(profile, quantiser.beats_per_bar)
    kept, capped = cap_per_bar(
        merge_duplicates(long_enough), slot, _velocity, profile.max_notes_per_bar
    )
    removed = len(notes) - len(in_range) + len(placed) - len(long_enough)
    return CleanedNotes(tuple(kept), len(notes), removed, capped)


def clean_hits(hits: Sequence[DrumHit], quantiser: Quantiser) -> CleanedHits:
    """Quantise drum hits, merge one hit per piece and slot, cap hands and feet per bar."""
    placed = (PlacedHit(quantiser.snap(h.time), h.piece, h.strength) for h in hits)
    merged = _merge_by(placed, lambda h: (h.onset, PIECE_ORDER.index(h.piece)), _merge_hits)
    bars = quantiser.beats_per_bar
    kept, capped = cap_per_bar(
        merged, lambda h: (int(h.onset // bars), int(h.hands)), _strength, MAX_NOTES_PER_BAR
    )
    return CleanedHits(tuple(kept), len(hits), 0, capped)


def cleaning_warnings(counts: CleaningCounts) -> tuple[ScoreWarning, ...]:
    """Step 6: ``notes-cleaned`` (> 10 % removed) and ``dense-capped`` (any capped)."""
    warnings: list[ScoreWarning] = []
    if counts.removed > NOTES_CLEANED_RATIO * counts.total:
        warnings.append(score_warning("notes-cleaned", n=counts.removed))
    if counts.capped:
        warnings.append(score_warning("dense-capped", n=counts.capped))
    return tuple(warnings)


def _place(notes: list[NoteEvent], profile: Profile, quantiser: Quantiser) -> list[PlacedNote]:
    """Steps 2-3: monophonic reduction, quantisation and next-onset ends (single voice)."""
    if not profile.single_voice:
        return quantise(notes, quantiser)
    return end_at_next_onset(quantise(reduce_monophonic(notes), quantiser))


def _note_slot(profile: Profile, beats_per_bar: int) -> Callable[[PlacedNote], tuple[int, int]]:
    """Return the (bar, staff) key of a note for the cap."""
    return lambda n: (int(n.onset // beats_per_bar), staff_of(profile, n.pitch))


def _velocity(note: PlacedNote) -> float:
    return note.velocity


def _strength(hit: PlacedHit) -> float:
    return hit.strength


def _merge_notes(first: PlacedNote, second: PlacedNote) -> PlacedNote:
    return replace(
        first, end=max(first.end, second.end), velocity=max(first.velocity, second.velocity)
    )


def _merge_hits(first: PlacedHit, second: PlacedHit) -> PlacedHit:
    return replace(first, strength=max(first.strength, second.strength))


def _merge_by[T](
    items: Iterable[T], key: Callable[[T], tuple[Fraction, int]], combine: Callable[[T, T], T]
) -> list[T]:
    """Merge items with equal (onset, rank) keys and return them sorted by key."""
    merged: dict[tuple[Fraction, int], T] = {}
    for item in items:
        k = key(item)
        merged[k] = combine(merged[k], item) if k in merged else item
    return [merged[k] for k in sorted(merged)]

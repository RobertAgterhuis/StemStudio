"""Chord symbols above the top staff of a score (data-model 4.7, FR-017).

Each ``ScoreChordSymbol`` start is quantised with the score's time base and inserted as a music21
``ChordSymbol`` (MusicXML ``<harmony>``) into the measure of the top staff that holds it; symbols
outside the score are skipped. Concert symbols are spelled with ``prefer_flats``; a transposed
part moves them by the instrument interval and spells them in the written key.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from fractions import Fraction
from typing import Final

from music21 import harmony as m21_harmony
from music21 import stream as m21_stream

from stemstudio_worker.harmony.labels import FLAT_NAMES, SHARP_NAMES
from stemstudio_worker.notation.keys import spell
from stemstudio_worker.notation.timebase import GridQuantiser
from stemstudio_worker.schemas import ChordQuality, KeyInfo, ScoreChordSymbol

KINDS: Final[dict[ChordQuality, str]] = {
    "maj": "major",
    "min": "minor",
    "dim": "diminished",
    "aug": "augmented",
    "min6": "minor-sixth",
    "maj6": "major-sixth",
    "min7": "minor-seventh",
    "minmaj7": "major-minor",
    "maj7": "major-seventh",
    "7": "dominant",
    "dim7": "diminished-seventh",
    "hdim7": "half-diminished",
    "sus2": "suspended-second",
    "sus4": "suspended-fourth",
}
"""MusicXML ``<kind>`` of every quality."""

_QUALITY_OF_KIND: Final = {kind: quality for quality, kind in KINDS.items()}


def _concert_name(pitch_class: int, prefer_flats: bool) -> str:
    name = (FLAT_NAMES if prefer_flats else SHARP_NAMES)[pitch_class]
    return name[0] + name[1:].replace("b", "-")


def _symbol(root: str, quality: ChordQuality, bass: str | None) -> m21_harmony.ChordSymbol:
    symbol = m21_harmony.ChordSymbol(root=root, bass=bass, kind=KINDS[quality])
    symbol.writeAsChord = False
    return symbol


def _measure_at(part: m21_stream.Part, position: Fraction) -> m21_stream.Measure | None:
    for measure in part.getElementsByClass(m21_stream.Measure):
        start = Fraction(measure.offset)
        if start <= position < start + Fraction(measure.duration.quarterLength):
            return measure
    return None


@dataclass(frozen=True, slots=True)
class Placement:
    """Where and how concert symbols are written.

    Attributes:
        quantiser: The score's grid.
        lead: How far the first measure starts before bar 1 (the pickup length).
        prefer_flats: Spelling of the concert symbols.
    """

    quantiser: GridQuantiser
    lead: Fraction
    prefer_flats: bool


def insert_symbols(
    score: m21_stream.Score, symbols: Sequence[ScoreChordSymbol], placement: Placement
) -> int:
    """Insert the symbols into the top staff of ``score``; returns how many were placed."""
    placed = 0
    part = score.parts[0]
    flats = placement.prefer_flats
    for item in symbols:
        position = placement.quantiser.snap(item.start_ms / 1000) + placement.lead
        measure = _measure_at(part, position)
        if measure is None:
            continue
        bass = _concert_name(item.bass, flats) if item.bass is not None else None
        symbol = _symbol(_concert_name(item.root, flats), item.quality, bass)
        measure.insert(float(position - Fraction(measure.offset)), symbol)
        placed += 1
    return placed


def _written_name(pitch_class: int, semitones: int, written: KeyInfo) -> str:
    return spell(60 + (pitch_class + semitones) % 12, written).rstrip("0123456789")


def transpose_symbols(score: m21_stream.Score, semitones: int, written: KeyInfo) -> None:
    """Move every chord symbol by ``semitones`` and spell it in the written key."""
    for old in list(score.recurse().getElementsByClass(m21_harmony.ChordSymbol)):
        quality = _QUALITY_OF_KIND.get(old.chordKind)
        if quality is None:  # a kind StemStudio never writes (e.g. a power chord) stays
            continue
        root_class, bass_class = old.root().pitchClass, old.bass().pitchClass
        new_bass = None
        if bass_class != root_class:
            new_bass = _written_name(bass_class, semitones, written)
        root = _written_name(root_class, semitones, written)
        old.activeSite.replace(old, _symbol(root, quality, new_bass))

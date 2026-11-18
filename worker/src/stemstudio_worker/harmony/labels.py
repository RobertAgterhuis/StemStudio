"""Harte chord labels: parse, format, chord tones and display symbols (data-model 2.1, 2.5).

Labels follow the contract pattern ``N | X | <root>:<quality>[/<b?degree>]`` (root ``A``-``G``
with an optional ``#`` or ``b``). The worker writes roots with sharps (``A#:maj``); display
spelling by key happens in :func:`display_symbol` (and in the backend and engine from the shared
spelling vectors).
"""

import re
from typing import Final, NamedTuple, cast, get_args

from stemstudio_worker.schemas import ChordQuality, KeyMode

SHARP_NAMES: Final = ("C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B")
"""Pitch-class names with sharps (C = 0)."""

FLAT_NAMES: Final = ("C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B")
"""Pitch-class names with flats (C = 0)."""

NO_CHORD: Final = "N"
"""Harte label of "no chord"."""

UNKNOWN: Final = "X"
"""Harte label of an unknown chord."""

QUALITY_INTERVALS: Final[dict[ChordQuality, tuple[int, ...]]] = {
    "maj": (0, 4, 7),
    "min": (0, 3, 7),
    "dim": (0, 3, 6),
    "aug": (0, 4, 8),
    "min6": (0, 3, 7, 9),
    "maj6": (0, 4, 7, 9),
    "min7": (0, 3, 7, 10),
    "minmaj7": (0, 3, 7, 11),
    "maj7": (0, 4, 7, 11),
    "7": (0, 4, 7, 10),
    "dim7": (0, 3, 6, 9),
    "hdim7": (0, 3, 6, 10),
    "sus2": (0, 2, 7),
    "sus4": (0, 5, 7),
}
"""Semitones above the root of every chord tone, per quality."""

DISPLAY_SUFFIX: Final[dict[ChordQuality, str]] = {
    "maj": "",
    "min": "m",
    "dim": "dim",
    "aug": "aug",
    "min6": "m6",
    "maj6": "6",
    "min7": "m7",
    "minmaj7": "m(maj7)",
    "maj7": "maj7",
    "7": "7",
    "dim7": "dim7",
    "hdim7": "m7b5",
    "sus2": "sus2",
    "sus4": "sus4",
}
"""Display suffix per quality (data-model 2.1)."""

FLAT_MAJOR_TONICS: Final = frozenset({5, 10, 3, 8, 1, 6})
"""Major tonics spelled with flats: F, Bb, Eb, Ab, Db, Gb (data-model 2.5)."""

_DEGREE_SEMITONES: Final = {"1": 0, "2": 2, "3": 4, "4": 5, "5": 7, "6": 9, "7": 11}
_SEMITONE_DEGREES: Final = ("1", "b2", "2", "b3", "3", "4", "b5", "5", "b6", "6", "b7", "7")
_SUS: Final = frozenset({"sus2", "sus4"})
_THIRD: Final = 1
_ADDED: Final = 3
_SIXTH: Final = frozenset({"min6", "maj6"})
_QUALITIES: Final = "|".join(re.escape(q) for q in get_args(ChordQuality))
_HARTE: Final = re.compile(rf"([A-G])(#|b)?:({_QUALITIES})(?:/(b?)([1-7]))?")
_ACCIDENTAL: Final = {"": 0, "#": 1, "b": -1}


class Chord(NamedTuple):
    """A parsed label: root and bass pitch classes (C = 0) and the quality; all None for N/X."""

    root: int | None
    quality: ChordQuality | None
    bass: int | None


def parse_label(label: str) -> Chord:
    """Parse a Harte label.

    Raises:
        ValueError: The label does not match the contract's Harte pattern.
    """
    if label in (NO_CHORD, UNKNOWN):
        return Chord(None, None, None)
    match = _HARTE.fullmatch(label)
    if match is None:
        msg = f"not a Harte label: {label!r}"
        raise ValueError(msg)
    letter, accidental, quality, flat, degree = match.groups()
    root = (SHARP_NAMES.index(letter) + _ACCIDENTAL[accidental or ""]) % 12
    bass = None
    if degree is not None:
        bass = (root + _DEGREE_SEMITONES[degree] - (1 if flat else 0)) % 12
    return Chord(root, cast("ChordQuality", quality), bass)


def is_chord(label: str) -> bool:
    """True for every label except ``N`` and ``X``."""
    return label not in (NO_CHORD, UNKNOWN)


def format_label(
    root: int, quality: ChordQuality, bass: int | None = None, *, prefer_flats: bool = False
) -> str:
    """Write a Harte label; a bass equal to the root is left out."""
    names = FLAT_NAMES if prefer_flats else SHARP_NAMES
    label = f"{names[root % 12]}:{quality}"
    if bass is None or bass % 12 == root % 12:
        return label
    return f"{label}/{_SEMITONE_DEGREES[(bass - root) % 12]}"


def chord_tones(root: int, quality: ChordQuality) -> tuple[int, ...]:
    """Pitch classes of the chord, root first."""
    return tuple((root + step) % 12 for step in QUALITY_INTERVALS[quality])


def is_inversion(root: int, quality: ChordQuality, bass: int) -> bool:
    """True when ``bass`` is the third, fifth or seventh of the chord (data-model 2.3 step 4)."""
    tones = QUALITY_INTERVALS[quality]
    step = (bass - root) % 12
    if step not in tones[1:]:
        return False
    position = tones.index(step)
    third_less = position == _THIRD and quality in _SUS
    sixth = position == _ADDED and quality in _SIXTH
    return not (third_less or sixth)


def simple_labels() -> tuple[str, ...]:
    """The 25 ``simple`` classes: index ``root * 2 + (0 maj, 1 min)``, then ``N`` (index 24)."""
    chords = tuple(format_label(root, q) for root in range(12) for q in ("maj", "min"))
    return (*chords, NO_CHORD)


def prefer_flats(tonic: int, mode: KeyMode) -> bool:
    """True when the key spells with flats (its relative major tonic is F Bb Eb Ab Db Gb)."""
    major = tonic if mode == "major" else (tonic + 3) % 12
    return major in FLAT_MAJOR_TONICS


def display_symbol(chord: Chord, *, prefer_flats: bool = False) -> str:
    """Display symbol such as ``Am7/G``; ``N.C.`` for no chord (data-model 2.1, 2.5)."""
    if chord.root is None or chord.quality is None:
        return "N.C."
    names = FLAT_NAMES if prefer_flats else SHARP_NAMES
    symbol = names[chord.root] + DISPLAY_SUFFIX[chord.quality]
    if chord.bass is None or chord.bass == chord.root:
        return symbol
    return f"{symbol}/{names[chord.bass]}"

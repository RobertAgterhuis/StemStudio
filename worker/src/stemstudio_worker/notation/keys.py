"""Key estimation, key overrides and pitch spelling (data-model.md 2.5, ADR-017 D5).

The key is estimated with music21's Krumhansl-Schmuckler analysis over the cleaned notes
(weighted by duration); its correlation coefficient, clamped to 0..1, is the confidence. Pitches
in the key's scale use the scale's spelling; the others use sharps for sharp keys (and C major /
A minor) and flats for flat keys.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from functools import lru_cache
from typing import Final, Literal

from music21 import key as m21_key
from music21 import note as m21_note
from music21 import stream as m21_stream

from stemstudio_worker.notation.cleaning import PlacedNote
from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.schemas import KeyInfo, KeyName, ScoreWarning

MIN_KEY_NOTES: Final = 8
"""Fewer notes than this give C major with confidence 0 and ``few-notes``."""

KEY_CONFIDENCE_THRESHOLD: Final = 0.6
"""A lower confidence raises ``low-key-confidence``."""

SHARP_NAMES: Final = ("C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B")
FLAT_NAMES: Final = ("C", "D-", "D", "E-", "E", "F", "G-", "G", "A-", "A", "B-", "B")
_FLAT_MAJOR_TONICS: Final = frozenset({1, 3, 5, 6, 8, 10})
_STEP_CLASSES: Final = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
_FALLBACK: Final = KeyInfo(tonic="C", mode="major", confidence=0.0, source="estimated")


def prefers_flats(tonic: int, mode: str) -> bool:
    """True when the key spells with flats (its relative major tonic is F Bb Eb Ab Db Gb)."""
    major = tonic if mode == "major" else (tonic + 3) % 12
    return major in _FLAT_MAJOR_TONICS


@dataclass(frozen=True, slots=True)
class KeyResult:
    """The key of a score and the warnings about it.

    Attributes:
        info: Tonic, mode, confidence and source.
        warnings: ``few-notes`` and/or ``low-key-confidence``.
    """

    info: KeyInfo
    warnings: tuple[ScoreWarning, ...] = ()


def estimate_key(notes: Sequence[PlacedNote]) -> KeyResult:
    """Estimate the key of ``notes`` (data-model.md 2.5)."""
    if len(notes) < MIN_KEY_NOTES:
        return KeyResult(_FALLBACK, (score_warning("few-notes"),))
    pitches = [m21_note.Note(n.pitch, quarterLength=n.duration) for n in notes]
    found = m21_stream.Stream(pitches).analyze("key")
    confidence = min(1.0, max(0.0, float(found.correlationCoefficient)))
    info = KeyInfo(
        tonic=found.tonic.name, mode=found.mode, confidence=confidence, source="estimated"
    )
    return KeyResult(info, _confidence_warnings(confidence))


def key_from_name(name: KeyName) -> KeyInfo:
    """Return the override ``name`` (e.g. ``"B- major"``) as a key with confidence 1."""
    tonic, mode = name.split(" ")
    return KeyInfo(tonic=tonic, mode=_mode(mode), confidence=1.0, source="override")


def resolve_key(notes: Sequence[PlacedNote], override: KeyName | None) -> KeyResult:
    """Return the override (with ``few-notes`` when due) or the estimate."""
    if override is None:
        return estimate_key(notes)
    few = (score_warning("few-notes"),) if len(notes) < MIN_KEY_NOTES else ()
    return KeyResult(key_from_name(override), few)


def key_sharps(info: KeyInfo) -> int:
    """Return the key signature (sharps > 0, flats < 0)."""
    return _signature(info.tonic, info.mode)[0]


def spell(pitch: int, info: KeyInfo) -> str:
    """Return the spelled name with octave of MIDI ``pitch`` in the key (``"G-4"``)."""
    sharps, scale = _signature(info.tonic, info.mode)
    chromatic = SHARP_NAMES if sharps >= 0 else FLAT_NAMES
    name = scale.get(pitch % 12, chromatic[pitch % 12])
    return f"{name}{_octave(name, pitch)}"


def _confidence_warnings(confidence: float) -> tuple[ScoreWarning, ...]:
    if confidence >= KEY_CONFIDENCE_THRESHOLD:
        return ()
    return (score_warning("low-key-confidence", n=round(confidence * 100)),)


def _mode(text: str) -> Literal["major", "minor"]:
    return "minor" if text == "minor" else "major"


@lru_cache(maxsize=64)
def _signature(tonic: str, mode: str) -> tuple[int, dict[int, str]]:
    """Return the key signature and the spelled scale (pitch class -> name) of a key."""
    found = m21_key.Key(tonic, mode)
    return int(found.sharps), {p.pitchClass: p.name for p in found.pitches}


def _octave(name: str, pitch: int) -> int:
    """Return the octave label of ``name`` for MIDI ``pitch`` (C4 = 60, C-4 = 59, B#3 = 60)."""
    alter = name.count("#") - name.count("-")
    natural = _STEP_CLASSES[name[0]] + alter
    return (pitch - natural) // 12 - 1

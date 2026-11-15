"""Key estimation, override and spelling (008 T016; data-model 2.5; research S-07/S-08; FR-005)."""

from collections.abc import Sequence
from fractions import Fraction
from typing import get_args

import pytest

from stemstudio_worker.notation.cleaning import PlacedNote
from stemstudio_worker.notation.keys import (
    MIN_KEY_NOTES,
    estimate_key,
    key_from_name,
    key_sharps,
    resolve_key,
    spell,
)
from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.schemas import KeyInfo, KeyName

# A G-major tune: tonic and dominant weighted by long notes, F# as the leading tone.
G_MAJOR_LINE = [
    (67, 2), (71, 1), (74, 1), (67, 1), (69, 1), (71, 2), (72, 1), (74, 2), (66, 1), (67, 4),
    (62, 2), (71, 2), (67, 4),
]  # fmt: skip


def line(pitches_and_lengths: Sequence[tuple[int, float]]) -> list[PlacedNote]:
    notes, onset = [], Fraction(0)
    for pitch, length in pitches_and_lengths:
        notes.append(PlacedNote(onset, onset + Fraction(length), pitch, 0.8))
        onset += Fraction(length)
    return notes


def key(tonic: str, mode: str) -> KeyInfo:
    return KeyInfo(tonic=tonic, mode=mode, confidence=1.0, source="override")  # type: ignore[arg-type]


# ------------------------------------------------------------------ estimate


def test_a_g_major_line_is_estimated_as_g_major() -> None:
    result = estimate_key(line(G_MAJOR_LINE))
    assert (result.info.tonic, result.info.mode, result.info.source) == ("G", "major", "estimated")
    assert result.info.confidence > 0.6
    assert result.warnings == ()


def test_the_estimate_is_weighted_by_duration() -> None:
    # The same eight pitches: long C/G/C lean to C major, long A/E/E lean to A minor.
    pitches = [60, 64, 67, 69, 72, 76, 71, 62]
    c_long = [(p, 6.0 if p in {60, 67, 72} else 0.5) for p in pitches]
    a_long = [(p, 6.0 if p in {69, 64, 76} else 0.5) for p in pitches]
    assert (estimate_key(line(c_long)).info.tonic, estimate_key(line(c_long)).info.mode) == (
        "C",
        "major",
    )
    assert (estimate_key(line(a_long)).info.tonic, estimate_key(line(a_long)).info.mode) == (
        "A",
        "minor",
    )


def test_a_chromatic_line_has_low_confidence() -> None:
    result = estimate_key(line([(60 + i % 12, 1) for i in range(24)]))
    assert result.info.confidence < 0.6
    percent = round(result.info.confidence * 100)
    assert result.warnings == (score_warning("low-key-confidence", n=percent),)
    assert result.warnings[0].text == f"The key is uncertain (confidence {percent} %)."


def test_fewer_than_eight_notes_give_c_major_without_confidence() -> None:
    result = estimate_key(line(G_MAJOR_LINE[: MIN_KEY_NOTES - 1]))
    assert result.info == KeyInfo(tonic="C", mode="major", confidence=0.0, source="estimated")
    assert result.warnings == (score_warning("few-notes"),)


def test_eight_notes_are_enough() -> None:
    assert estimate_key(line(G_MAJOR_LINE[:MIN_KEY_NOTES])).info.confidence > 0


# ------------------------------------------------------------------ override


@pytest.mark.parametrize("name", get_args(KeyName))
def test_every_override_name_is_parsed(name: str) -> None:
    info = key_from_name(name)  # type: ignore[arg-type]
    assert f"{info.tonic} {info.mode}" == name
    assert (info.confidence, info.source) == (1.0, "override")


def test_there_are_thirty_key_names() -> None:
    assert len(get_args(KeyName)) == 30


def test_resolve_key_prefers_the_override() -> None:
    result = resolve_key(line(G_MAJOR_LINE), "E- major")
    assert (result.info.tonic, result.info.source, result.warnings) == ("E-", "override", ())


def test_resolve_key_with_an_override_still_reports_few_notes() -> None:
    result = resolve_key(line(G_MAJOR_LINE[:3]), "D minor")
    assert result.info.tonic == "D"
    assert result.warnings == (score_warning("few-notes"),)


def test_resolve_key_without_override_estimates() -> None:
    assert resolve_key(line(G_MAJOR_LINE), None).info.source == "estimated"


# ------------------------------------------------------------------ spelling


@pytest.mark.parametrize(
    ("pitch", "tonic", "mode", "spelled"),
    [
        (66, "G", "major", "F#4"),
        (66, "D-", "major", "G-4"),
        (68, "A-", "major", "A-4"),
        (68, "F", "major", "A-4"),  # out of scale, flat key -> flat
        (63, "G", "major", "D#4"),  # out of scale, sharp key -> sharp
        (61, "C", "major", "C#4"),  # C major uses sharps
        (70, "A", "minor", "A#4"),  # A minor uses sharps
        (70, "D", "minor", "B-4"),  # in scale (one flat)
        (59, "C-", "major", "C-4"),  # C-flat sits an octave label above B3
        (64, "C-", "major", "F-4"),
        (60, "C#", "major", "B#3"),  # B-sharp sits an octave label below C4
        (36, "E-", "major", "C2"),
        (101, "B", "major", "F7"),  # out of scale: natural name of the sharp list
    ],
)
def test_spelling_follows_the_key(pitch: int, tonic: str, mode: str, spelled: str) -> None:
    assert spell(pitch, key(tonic, mode)) == spelled


@pytest.mark.parametrize(
    ("tonic", "mode", "sharps"),
    [
        ("G", "major", 1),
        ("F", "major", -1),
        ("A-", "major", -4),
        ("A", "minor", 0),
        ("C#", "minor", 4),
    ],
)
def test_key_signature_sharps(tonic: str, mode: str, sharps: int) -> None:
    assert key_sharps(key(tonic, mode)) == sharps

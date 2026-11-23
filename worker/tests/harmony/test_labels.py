"""Harte labels: parse, format, chord tones and display symbols (016 T015, data-model 2.1)."""

import json
from pathlib import Path
from typing import Any, get_args

import pytest

from stemstudio_worker.harmony import labels
from stemstudio_worker.schemas import ChordQuality

QUALITIES: tuple[ChordQuality, ...] = get_args(ChordQuality)
VECTORS = (
    Path(__file__).parents[3] / "specs/016-instrument-views/contracts/chord-spelling-vectors.json"
)


@pytest.mark.parametrize("quality", QUALITIES)
@pytest.mark.parametrize("root", range(12))
def test_format_then_parse_round_trips_every_quality(root: int, quality: ChordQuality) -> None:
    label = labels.format_label(root, quality)
    assert labels.parse_label(label) == labels.Chord(root, quality, None)


@pytest.mark.parametrize(
    ("label", "expected"),
    [
        ("C:maj", labels.Chord(0, "maj", None)),
        ("A:min7/b7", labels.Chord(9, "min7", 7)),
        ("F:maj/3", labels.Chord(5, "maj", 9)),
        ("Bb:7/5", labels.Chord(10, "7", 5)),
        ("C#:min/b3", labels.Chord(1, "min", 4)),
        ("Db:maj7/7", labels.Chord(1, "maj7", 0)),
        ("Cb:maj", labels.Chord(11, "maj", None)),
        ("E#:min", labels.Chord(5, "min", None)),
        ("G:sus4", labels.Chord(7, "sus4", None)),
    ],
)
def test_parse_reads_roots_qualities_and_bass_intervals(label: str, expected: labels.Chord) -> None:
    assert labels.parse_label(label) == expected


@pytest.mark.parametrize("label", ["N", "X"])
def test_no_chord_and_unknown_have_no_root(label: str) -> None:
    assert labels.parse_label(label) == labels.Chord(None, None, None)
    assert not labels.is_chord(label)


@pytest.mark.parametrize(
    "label", ["", "H:maj", "C:major", "C", "C:maj/8", "C:maj/#3", "c:maj", "C:maj/"]
)
def test_parse_rejects_labels_outside_the_harte_pattern(label: str) -> None:
    with pytest.raises(ValueError, match="Harte"):
        labels.parse_label(label)


@pytest.mark.parametrize(
    ("root", "quality", "bass", "expected"),
    [
        (9, "min7", 7, "A:min7/b7"),
        (5, "maj", 9, "F:maj/3"),
        (0, "maj", 7, "C:maj/5"),
        (0, "maj7", 11, "C:maj7/7"),
        (9, "min", 0, "A:min/b3"),
        (0, "maj", 0, "C:maj"),
        (10, "maj", None, "A#:maj"),
    ],
)
def test_format_writes_inversions_as_bass_intervals(
    root: int, quality: ChordQuality, bass: int | None, expected: str
) -> None:
    assert labels.format_label(root, quality, bass) == expected


def test_format_uses_flat_roots_when_asked() -> None:
    assert labels.format_label(10, "min", None, prefer_flats=True) == "Bb:min"


@pytest.mark.parametrize(
    ("quality", "tones"),
    [
        ("maj", (0, 4, 7)),
        ("min", (0, 3, 7)),
        ("dim", (0, 3, 6)),
        ("aug", (0, 4, 8)),
        ("min6", (0, 3, 7, 9)),
        ("maj6", (0, 4, 7, 9)),
        ("min7", (0, 3, 7, 10)),
        ("minmaj7", (0, 3, 7, 11)),
        ("maj7", (0, 4, 7, 11)),
        ("7", (0, 4, 7, 10)),
        ("dim7", (0, 3, 6, 9)),
        ("hdim7", (0, 3, 6, 10)),
        ("sus2", (0, 2, 7)),
        ("sus4", (0, 5, 7)),
    ],
)
def test_chord_tones_are_the_quality_intervals(
    quality: ChordQuality, tones: tuple[int, ...]
) -> None:
    assert labels.chord_tones(2, quality) == tuple((2 + t) % 12 for t in tones)


@pytest.mark.parametrize(
    ("root", "quality", "bass", "inverted"),
    [
        (0, "maj", 4, True),
        (0, "maj", 7, True),
        (0, "7", 10, True),
        (0, "maj7", 11, True),
        (9, "min", 0, True),
        (0, "maj", 2, False),
        (0, "maj", 0, False),
        (0, "sus4", 5, False),
        (0, "maj6", 9, False),
    ],
)
def test_inversion_bass_is_only_a_third_fifth_or_seventh(
    root: int, quality: ChordQuality, bass: int, inverted: bool
) -> None:
    assert labels.is_inversion(root, quality, bass) is inverted


def test_simple_vocabulary_has_25_classes_in_root_major_minor_order() -> None:
    simple = labels.simple_labels()
    assert len(simple) == 25
    assert simple[:4] == ("C:maj", "C:min", "C#:maj", "C#:min")
    assert simple[23] == "B:min"
    assert simple[24] == "N"


def _display_cases() -> list[dict[str, Any]]:
    data = json.loads(VECTORS.read_text(encoding="utf-8"))
    return [c for c in data["cases"] if c["transpose"] == 0 and c["capo"] == 0]


@pytest.mark.parametrize("case", _display_cases(), ids=lambda c: c["symbol"])
def test_display_symbol_matches_the_shared_spelling_vectors(case: dict[str, Any]) -> None:
    flats = (
        labels.prefer_flats(case["keyTonic"], case["keyMode"])
        if case["keyTonic"] is not None
        else False
    )
    chord = labels.Chord(case["root"], case["quality"], case["bass"])
    assert labels.display_symbol(chord, prefer_flats=flats) == case["symbol"]


@pytest.mark.parametrize(
    ("tonic", "mode", "flats"),
    [(5, "major", True), (10, "major", True), (1, "major", True), (6, "major", True),
     (0, "major", False), (7, "major", False), (11, "major", False),
     (2, "minor", True), (3, "minor", True), (9, "minor", False), (4, "minor", False)],
)  # fmt: skip
def test_prefer_flats_follows_the_relative_major(tonic: int, mode: str, flats: bool) -> None:
    assert labels.prefer_flats(tonic, mode) is flats  # type: ignore[arg-type]


def test_display_of_no_chord_is_nc() -> None:
    assert labels.display_symbol(labels.Chord(None, None, None)) == "N.C."

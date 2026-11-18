"""Transposed parts, clefs and the section profiles (016 T025, data-model 4.2-4.6)."""

import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any

import pytest
from music21 import converter, stream

from stemstudio_worker.notation.notator import MusicNotator
from stemstudio_worker.notation.parts import Music21PartWriter, written_key_name
from stemstudio_worker.notation.profiles import get_profile, suggest_profile
from stemstudio_worker.ports import WRITTEN_INSTRUMENTS, NotationSettings, NoteEvent, ScoreDocument
from stemstudio_worker.schemas import (
    BeatGridTempo,
    ClefChoice,
    KeyName,
    PartSettings,
    ScoreProfile,
    ScoreTitles,
    WrittenInstrument,
)

from .conftest import QUARTER_SECONDS, note

GRID = BeatGridTempo(
    bpm=120, beats_per_bar=4, beats=[i * QUARTER_SECONDS for i in range(80)], downbeats=[0.0]
)
VECTORS = (
    Path(__file__).parents[3] / "specs/016-instrument-views/contracts/chord-spelling-vectors.json"
)
SCALE = [60, 62, 64, 65, 67, 69, 71, 72]
_STEPS = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
_CONCERT_KEYS: dict[tuple[int, str], KeyName] = {
    (0, "major"): "C major",
    (3, "major"): "E- major",
    (2, "minor"): "D minor",
}


def _settings(
    profile: ScoreProfile = "vocals",
    part: PartSettings | None = None,
    key: KeyName | None = "C major",
) -> NotationSettings:
    return NotationSettings(
        profile=profile,
        grid=GRID,
        resolution="sixteenth",
        key_override=key,
        titles=ScoreTitles(title="Song - Part"),
        duration_seconds=40.0,
        part=part,
    )


def _line(pitches: list[int]) -> list[NoteEvent]:
    return [note(i * QUARTER_SECONDS, (i + 1) * QUARTER_SECONDS, p) for i, p in enumerate(pitches)]


def _notate(pitches: list[int], settings: NotationSettings) -> ScoreDocument:
    return MusicNotator().notate(_line(pitches), settings)


def _root(document: ScoreDocument) -> ET.Element:
    return ET.fromstring(document.musicxml)  # noqa: S314 - our own notator output


def _midi_of(element: ET.Element) -> int:
    step, octave = element.findtext("pitch/step", ""), int(element.findtext("pitch/octave", "0"))
    return 12 * (octave + 1) + _STEPS[step] + round(float(element.findtext("pitch/alter", "0")))


def _written(document: ScoreDocument) -> list[int]:
    return [_midi_of(n) for n in _root(document).iter("note") if n.find("pitch") is not None]


def _stable(musicxml: bytes) -> bytes:
    """MusicXML without music21's per-run part ids."""
    return re.sub(rb'"P[0-9a-f]{20,}"', b'"P1"', musicxml)


def _vector_cases() -> list[dict[str, Any]]:
    data = json.loads(VECTORS.read_text(encoding="utf-8"))
    return [k for k in data["keys"] if k["writtenKey"] and k["transpose"] == 0]


@pytest.mark.parametrize("case", _vector_cases(), ids=lambda k: f"{k['display']}-{k['instrument']}")
def test_every_written_instrument_matches_the_shared_key_vectors(case: dict[str, Any]) -> None:
    instrument: WrittenInstrument = case["instrument"]
    info = WRITTEN_INSTRUMENTS[instrument]
    key = _CONCERT_KEYS[(case["tonic"], case["mode"])]
    document = _notate(SCALE, _settings(part=PartSettings(instrument=instrument), key=key))
    assert _written(document) == [p + info.semitones for p in SCALE]
    assert document.part is not None
    assert document.part.written_key == written_key_name(case["writtenKey"])
    assert document.part.name == info.name
    assert document.part.interval == info.interval
    assert _root(document).findtext(".//part-list/score-part/part-name") == info.name


@pytest.mark.parametrize(
    ("instrument", "diatonic", "chromatic", "octave"),
    [
        ("alto-sax-eb", "-5", "-9", None),
        ("tenor-sax-bb", "-1", "-2", "-1"),
        ("clarinet-eb", "2", "3", None),
        ("double-bass", "0", "0", "-1"),
        ("horn-f", "-4", "-7", None),
        ("baritone-sax-eb", "-5", "-9", "-1"),
    ],
)
def test_transpose_element_gives_written_to_sounding(
    instrument: WrittenInstrument, diatonic: str, chromatic: str, octave: str | None
) -> None:
    document = _notate(SCALE, _settings(part=PartSettings(instrument=instrument)))
    transpose = _root(document).find(".//attributes/transpose")
    assert transpose is not None
    assert (transpose.findtext("diatonic"), transpose.findtext("chromatic")) == (
        diatonic,
        chromatic,
    )
    assert transpose.findtext("octave-change") == octave


def test_alto_sax_part_of_concert_c_is_in_a_major() -> None:
    document = _notate(SCALE, _settings(part=PartSettings(instrument="alto-sax-eb")))
    assert _root(document).findtext(".//key/fifths") == "3"
    assert document.part is not None
    assert document.part.written_key == "A major"


def test_written_keys_beyond_six_accidentals_are_respelled() -> None:
    e_major = [64, 66, 68, 69, 71, 73, 75, 76]
    document = _notate(
        e_major, _settings(part=PartSettings(instrument="alto-sax-eb"), key="E major")
    )
    assert _root(document).findtext(".//key/fifths") == "-5"
    assert document.part is not None
    assert document.part.written_key == "D- major"
    names = [n.findtext("pitch/step") for n in _root(document).iter("note")]
    assert names[:3] == ["D", "E", "F"]


def test_midi_stays_at_concert_pitch() -> None:
    concert = _notate(SCALE, _settings())
    part = _notate(SCALE, _settings(part=PartSettings(instrument="clarinet-bb")))
    assert part.midi == concert.midi


def test_concert_and_auto_are_byte_identical_to_the_008_output() -> None:
    plain = _notate(SCALE, _settings())
    concert = _notate(SCALE, _settings(part=PartSettings(instrument="concert")))
    assert (_stable(concert.musicxml), concert.midi) == (_stable(plain.musicxml), plain.midi)
    assert concert.part is not None
    assert (concert.part.instrument, concert.part.interval, concert.part.clef) == (
        "concert",
        "P1",
        "treble",
    )
    assert concert.part.name == "Vocals"


CLEF_CASES: list[tuple[WrittenInstrument, ClefChoice, tuple[str, str], str]] = [
    ("viola", "auto", ("C", "3"), "alto"),
    ("cello", "tenor", ("C", "4"), "tenor"),
    ("cello", "auto", ("F", "4"), "bass"),
    ("concert", "bass", ("F", "4"), "bass"),
    ("violin", "treble-8vb", ("G", "2"), "treble-8vb"),
    ("trombone", "bass-8vb", ("F", "4"), "bass-8vb"),
]


@pytest.mark.parametrize("case", CLEF_CASES, ids=lambda c: f"{c[0]}-{c[1]}")
def test_clef_choices(case: tuple[WrittenInstrument, ClefChoice, tuple[str, str], str]) -> None:
    instrument, clef, sign_line, written = case
    document = _notate(
        [48, 50, 52], _settings("strings", PartSettings(instrument=instrument, clef=clef))
    )
    clef_element = _root(document).find(".//clef")
    assert clef_element is not None
    assert (clef_element.findtext("sign"), clef_element.findtext("line")) == sign_line
    assert document.part is not None
    assert document.part.clef == written


def test_grand_staff_profiles_keep_both_clefs() -> None:
    document = _notate(
        [48, 72], _settings("keys", PartSettings(instrument="alto-sax-eb", clef="alto"))
    )
    signs = [c.findtext("sign") for c in _root(document).iter("clef")]
    assert signs == ["G", "F"]


def test_notes_outside_the_instrument_range_are_counted() -> None:
    document = _notate([50, 62, 64, 98], _settings(part=PartSettings(instrument="flute")))
    assert document.part is not None
    assert document.part.out_of_range == 1
    assert "part-out-of-range" in [w.code for w in document.warnings]
    assert len(_written(document)) == 3  # 98 is above the vocals profile and cleaned away


def test_part_writer_protocol_transposes_concert_musicxml() -> None:
    concert = _notate(SCALE, _settings())
    result = Music21PartWriter().write(
        concert.musicxml, PartSettings(instrument="trumpet-bb"), "vocals"
    )
    root = ET.fromstring(result.musicxml)  # noqa: S314 - our own output
    assert [_midi_of(n) for n in root.iter("note") if n.find("pitch") is not None] == [
        p + 2 for p in SCALE
    ]
    assert result.summary.written_key == "D major"


@pytest.mark.parametrize(
    ("profile", "low", "high"), [("strings", 28, 103), ("brass", 28, 84), ("woodwinds", 34, 98)]
)
def test_new_profiles_have_their_range_and_a_single_voice(
    profile: ScoreProfile, low: int, high: int
) -> None:
    constants = get_profile(profile)
    assert (constants.low, constants.high, constants.single_voice, constants.clefs) == (
        low, high, True, ("treble",)
    )  # fmt: skip
    assert constants.default_resolution == "sixteenth"


@pytest.mark.parametrize("profile", ["strings", "brass", "woodwinds"])
def test_section_stems_are_reduced_to_one_line_with_a_warning(profile: ScoreProfile) -> None:
    chord = [note(0.0, 1.0, 60, 0.5), note(0.0, 1.0, 64, 0.9), note(0.0, 1.0, 67, 0.4)]
    document = MusicNotator().notate([*chord, *_line([65, 67])], _settings(profile, key=None))
    score = converter.parse(document.musicxml, format="musicxml")
    assert isinstance(score, stream.Score)
    assert all(len(n.pitches) == 1 for n in score.recurse().notes)
    assert "section-reduced" in [w.code for w in document.warnings]


def test_a_single_line_has_no_section_warning() -> None:
    document = _notate([60, 62, 64], _settings("brass"))
    assert "section-reduced" not in [w.code for w in document.warnings]


@pytest.mark.parametrize(
    ("channel", "profile"),
    [
        ("other.strings", "strings"),
        ("other.brass", "brass"),
        ("other.woodwinds", "woodwinds"),
        ("piano.strings", "strings"),
        ("other", "keys"),
        ("other.synth", "keys"),
        ("piano.keys", "keys"),
    ],
)
def test_section_detail_stems_suggest_the_new_profiles(channel: str, profile: str) -> None:
    assert suggest_profile(channel) == profile


@pytest.mark.parametrize(
    ("display", "name"),
    [("Db major", "D- major"), ("F# minor", "F# minor"), ("A major", "A major")],
)
def test_written_key_names_use_the_music21_flat_sign(display: str, name: str) -> None:
    assert written_key_name(display) == name


def test_chords_of_a_grand_staff_part_are_transposed() -> None:
    chord = [note(0.0, 1.0, p) for p in (60, 64, 67)]
    settings = _settings("keys", PartSettings(instrument="clarinet-bb"))
    document = MusicNotator().notate(chord, settings)
    assert sorted(_written(document)) == [62, 66, 69]

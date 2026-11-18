"""Chord symbols in scores and General MIDI drums (016 T027, data-model 4.7-4.8, FR-017/023)."""

import xml.etree.ElementTree as ET
from typing import get_args

import pytest
from music21 import harmony, midi, stream

from stemstudio_worker.notation.chord_symbols import transpose_symbols
from stemstudio_worker.notation.notator import MusicNotator
from stemstudio_worker.ports import GM_DRUM_MAP, DrumHit, NotationSettings, ScoreDocument
from stemstudio_worker.schemas import (
    BeatGridTempo,
    ChordQuality,
    DrumPiece,
    KeyInfo,
    PartSettings,
    ScoreChordSymbol,
    ScoreProfile,
    ScoreTitles,
)

from .conftest import QUARTER_SECONDS, note

GRID = BeatGridTempo(
    bpm=120, beats_per_bar=4, beats=[i * QUARTER_SECONDS for i in range(80)], downbeats=[0.0]
)
LINE = [note(i * QUARTER_SECONDS, (i + 1) * QUARTER_SECONDS, 60 + i % 5) for i in range(8)]
KINDS: dict[ChordQuality, str] = {
    "maj": "major", "min": "minor", "dim": "diminished", "aug": "augmented",
    "min6": "minor-sixth", "maj6": "major-sixth", "min7": "minor-seventh",
    "minmaj7": "major-minor", "maj7": "major-seventh", "7": "dominant",
    "dim7": "diminished-seventh", "hdim7": "half-diminished", "sus2": "suspended-second",
    "sus4": "suspended-fourth",
}  # fmt: skip


def _settings(
    profile: ScoreProfile = "vocals",
    chords: tuple[ScoreChordSymbol, ...] = (),
    part: PartSettings | None = None,
    prefer_flats: bool = False,
) -> NotationSettings:
    return NotationSettings(
        profile=profile,
        grid=GRID,
        resolution="sixteenth",
        key_override="C major",
        titles=ScoreTitles(title="Song"),
        duration_seconds=40.0,
        part=part,
        chords=chords,
        prefer_flats=prefer_flats,
    )


def _symbol(
    start_ms: int, root: int, quality: ChordQuality = "maj", bass: int | None = None
) -> ScoreChordSymbol:
    return ScoreChordSymbol(start_ms=start_ms, root=root, quality=quality, bass=bass)


def _harmonies(document: ScoreDocument) -> list[tuple[str, str, str, str]]:
    """(measure, root-step + alter, kind, offset-in-measure as the note count before it)."""
    root = ET.fromstring(document.musicxml)  # noqa: S314 - our own notator output
    found = []
    for measure in root.iter("measure"):
        position = 0
        for child in measure:
            if child.tag == "harmony":
                step = child.findtext("root/root-step", "") + child.findtext("root/root-alter", "")
                found.append(
                    (measure.get("number", ""), step, child.findtext("kind", ""), str(position))
                )
            if child.tag == "note" and child.find("chord") is None:
                position += int(child.findtext("duration", "0"))
    return found


PROGRESSION = (_symbol(0, 0), _symbol(1490, 7), _symbol(2000, 9, "min"), _symbol(3500, 5))


def test_symbols_are_quantised_onto_the_top_staff() -> None:
    document = MusicNotator().notate(LINE, _settings(chords=PROGRESSION))
    harmonies = _harmonies(document)
    assert [(m, s, k) for m, s, k, _ in harmonies] == [
        ("1", "C", "major"), ("1", "G", "major"), ("2", "A", "minor"), ("2", "F", "major")
    ]  # fmt: skip
    divisions = int(ET.fromstring(document.musicxml).findtext(".//divisions", "1"))  # noqa: S314
    assert [int(p) / divisions for _, _, _, p in harmonies] == [0, 3, 0, 3]
    assert document.chords_included


@pytest.mark.parametrize("quality", get_args(ChordQuality))
def test_every_quality_has_its_musicxml_kind(quality: ChordQuality) -> None:
    document = MusicNotator().notate(LINE, _settings(chords=(_symbol(0, 2, quality),)))
    assert _harmonies(document)[0][1:3] == ("D", KINDS[quality])


def test_slash_bass_is_written() -> None:
    document = MusicNotator().notate(LINE, _settings(chords=(_symbol(0, 9, "min7", 7),)))
    root = ET.fromstring(document.musicxml)  # noqa: S314 - our own notator output
    assert root.findtext(".//harmony/bass/bass-step") == "G"


@pytest.mark.parametrize(("flats", "step"), [(True, "B-1"), (False, "A1")])
def test_concert_symbols_follow_prefer_flats(flats: bool, step: str) -> None:
    document = MusicNotator().notate(LINE, _settings(chords=(_symbol(0, 10),), prefer_flats=flats))
    assert _harmonies(document)[0][1] == step


@pytest.mark.parametrize(
    ("instrument", "steps"), [("clarinet-bb", ["D", "A", "B"]), ("alto-sax-eb", ["A", "E", "F1"])]
)
def test_symbols_are_transposed_with_the_part(instrument: str, steps: list[str]) -> None:
    chords = (_symbol(0, 0), _symbol(2000, 7), _symbol(3000, 9, "min"))
    part = PartSettings(instrument=instrument)  # type: ignore[arg-type]
    document = MusicNotator().notate(LINE, _settings(chords=chords, part=part))
    assert [s for _, s, _, _ in _harmonies(document)] == steps


def test_grand_staff_gets_symbols_only_on_the_top_staff() -> None:
    notes = [note(0.0, 2.0, 72), note(0.0, 2.0, 48)]
    document = MusicNotator().notate(notes, _settings("keys", chords=(_symbol(0, 0),)))
    root = ET.fromstring(document.musicxml)  # noqa: S314 - our own notator output
    harmonies = list(root.iter("harmony"))
    assert len(harmonies) == 1
    assert harmonies[0].findtext("staff") in (None, "1")


def test_midi_has_no_chord_symbol_notes() -> None:
    plain = MusicNotator().notate(LINE, _settings())
    with_chords = MusicNotator().notate(LINE, _settings(chords=PROGRESSION))
    assert with_chords.midi == plain.midi


@pytest.mark.parametrize("profile", ["bass"])
def test_profiles_without_chord_symbols_ignore_them(profile: ScoreProfile) -> None:
    bass_line = [note(i * 0.5, (i + 1) * 0.5, 40) for i in range(4)]
    document = MusicNotator().notate(bass_line, _settings(profile, chords=PROGRESSION))
    assert _harmonies(document) == []
    assert not document.chords_included


def test_symbols_outside_the_score_are_skipped() -> None:
    document = MusicNotator().notate(LINE, _settings(chords=(_symbol(0, 0), _symbol(39_000, 7))))
    assert [s for _, s, _, _ in _harmonies(document)] == ["C"]


def test_without_symbols_the_score_is_not_rewritten() -> None:
    document = MusicNotator().notate(LINE, _settings())
    assert not document.chords_included
    assert _harmonies(document) == []


# ------------------------------------------------------------------------------- drum MIDI

DRUM_GRID = BeatGridTempo(
    bpm=120, beats_per_bar=4, beats=[i * 0.5 for i in range(40)], downbeats=[0.0]
)
PIECES: tuple[DrumPiece, ...] = get_args(DrumPiece)


def _drum_settings(single: DrumPiece | None = None) -> NotationSettings:
    return NotationSettings(
        profile="drums",
        grid=DRUM_GRID,
        resolution="sixteenth",
        key_override=None,
        titles=ScoreTitles(title="Drums"),
        duration_seconds=20.0,
        single_piece=single,
    )


def _note_ons(data: bytes) -> list[tuple[int, int]]:
    parsed = midi.MidiFile()
    parsed.readstr(data)
    events = (e for track in parsed.tracks for e in track.events)
    ons = (e for e in events if e.type == midi.ChannelVoiceMessages.NOTE_ON and e.velocity)
    return [(int(e.channel or 0), int(e.pitch or 0)) for e in ons]


def test_symbols_all_outside_the_score_leave_it_unchanged() -> None:
    document = MusicNotator().notate(LINE, _settings(chords=(_symbol(39_000, 7),)))
    assert not document.chords_included
    assert _harmonies(document) == []


def test_slash_symbols_are_transposed_with_the_part() -> None:
    part = PartSettings(instrument="clarinet-bb")
    document = MusicNotator().notate(LINE, _settings(chords=(_symbol(0, 9, "min7", 7),), part=part))
    root = ET.fromstring(document.musicxml)  # noqa: S314 - our own notator output
    assert root.findtext(".//harmony/root/root-step") == "B"
    assert root.findtext(".//harmony/bass/bass-step") == "A"


def test_unknown_symbol_kinds_are_left_as_they_are() -> None:
    score = stream.Score([stream.Part([stream.Measure([harmony.ChordSymbol("C5")])])])  # type: ignore[no-untyped-call]
    transpose_symbols(score, 2, KeyInfo(tonic="D", mode="major", confidence=1.0, source="override"))
    (symbol,) = score.recurse().getElementsByClass(harmony.ChordSymbol)
    assert symbol.root().name == "C"


def test_drum_midi_uses_channel_10_and_the_general_midi_map() -> None:
    hits = [DrumHit(time=i * 0.5, piece=piece, strength=0.8) for i, piece in enumerate(PIECES)]
    document = MusicNotator().notate_drums(hits, _drum_settings())
    assert set(_note_ons(document.midi)) == {(10, GM_DRUM_MAP[p]) for p in PIECES}


def test_a_single_piece_score_has_only_that_piece_and_no_missing_piece_warning() -> None:
    hits = [DrumHit(time=i * 0.5, piece="snare", strength=0.8) for i in range(8)]
    document = MusicNotator().notate_drums(hits, _drum_settings("snare"))
    assert set(_note_ons(document.midi)) == {(10, 38)}
    assert "missing-drum-piece" not in [w.code for w in document.warnings]

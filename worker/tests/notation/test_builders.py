"""Score building, MusicXML/MIDI writing and the Notator (008 T018; data-model 2.3-2.7, 3).

FR-002, FR-003, FR-004, FR-009, SC-003. Every score is parsed back with music21.
"""

from collections.abc import Sequence
from fractions import Fraction
from typing import Any

import pytest
from music21 import clef, converter, midi, percussion, stream, tempo
from music21 import key as m21_key
from music21 import meter as m21_meter

from stemstudio_worker.notation import create_notator
from stemstudio_worker.notation.notator import MusicNotator
from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.ports import DrumHit, NoNotesError, NotationSettings, NoteEvent, Notator
from stemstudio_worker.schemas import (
    BeatGridTempo,
    KeyName,
    ManualTempo,
    ScoreProfile,
    ScoreResolution,
    ScoreTitles,
)

from .conftest import QUARTER_SECONDS, note

GRID = BeatGridTempo(
    bpm=120, beats_per_bar=4, beats=[i * QUARTER_SECONDS for i in range(80)], downbeats=[0.0]
)
TITLES = ScoreTitles(title="Song - Bass (sheet music)", subtitle="Song - Artist")


def settings(
    profile: ScoreProfile,
    resolution: ScoreResolution = "sixteenth",
    key: KeyName | None = None,
    grid: BeatGridTempo | ManualTempo = GRID,
) -> NotationSettings:
    return NotationSettings(
        profile=profile,
        grid=grid,
        resolution=resolution,
        key_override=key,
        titles=TITLES,
        duration_seconds=40.0,
    )


def q_notes(rows: Sequence[tuple[float, float, int]]) -> list[NoteEvent]:
    """Notes from (onset quarter, length in quarters, pitch) at 120 BPM."""
    return [note(q * QUARTER_SECONDS, (q + n) * QUARTER_SECONDS, p) for q, n, p in rows]


def parse(document: bytes) -> Any:
    score = converter.parse(document, format="musicxml")
    assert isinstance(score, stream.Score)
    return score


def measures(part: Any) -> list[Any]:
    return list(part.getElementsByClass(stream.Measure))


def midi_note_ons(data: bytes) -> list[Any]:
    parsed = midi.MidiFile()
    parsed.readstr(data)
    return [
        event
        for track in parsed.tracks
        for event in track.events
        if event.type == midi.ChannelVoiceMessages.NOTE_ON and event.velocity > 0
    ]


NOTATOR = MusicNotator()

# Bar 1: G2 B2 D3 G2 (the last one tied into bar 2); bar 2: rest, A2 half note.
BASS_LINE = [(0, 1, 43), (1, 1, 47), (2, 1, 50), (3, 2, 43), (6, 2, 45)]


# ------------------------------------------------------------------ pitched staves


def test_bass_score_has_clef_meter_tempo_key_and_bars() -> None:
    document = NOTATOR.notate(q_notes(BASS_LINE), settings("bass", key="G major"))
    part = parse(document.musicxml).parts[0]
    first = measures(part)[0]
    assert isinstance(first.getElementsByClass(clef.Clef)[0], clef.Bass8vbClef)
    assert first.getElementsByClass(m21_meter.TimeSignature)[0].ratioString == "4/4"
    assert first.getElementsByClass(m21_key.KeySignature)[0].sharps == 1
    mark = next(iter(part.recurse().getElementsByClass(tempo.MetronomeMark)))
    assert (mark.number, mark.referent.quarterLength) == (120, 1.0)
    assert len(measures(part)) == 2
    assert (document.bars, document.notes, document.bpm, document.time_signature) == (
        2,
        5,
        120,
        "4/4",
    )


def test_rests_fill_gaps_and_ties_cross_bar_lines() -> None:
    document = NOTATOR.notate(q_notes(BASS_LINE), settings("bass", key="G major"))
    bar1, bar2 = measures(parse(document.musicxml).parts[0])
    assert [(n.offset, n.quarterLength) for n in bar1.notesAndRests] == [
        (0, 1),
        (1, 1),
        (2, 1),
        (3, 1),
    ]
    assert bar1.notesAndRests[-1].tie.type == "start"
    tied, rest, last = bar2.notesAndRests
    assert (tied.tie.type, tied.quarterLength) == ("stop", 1)
    assert (rest.isRest, rest.quarterLength, last.quarterLength) == (True, 1, 2)


def test_every_bar_is_filled_and_no_duration_is_below_the_resolution() -> None:
    rows = [(0, 0.5, 60), (0.5, 1.5, 62), (3.5, 2.5, 64), (7, 0.5, 65), (9.5, 0.5, 67)]
    document = NOTATOR.notate(q_notes(rows), settings("vocals", resolution="eighth"))
    for bar in measures(parse(document.musicxml).parts[0]):
        assert sum(n.quarterLength for n in bar.notesAndRests) == 4
        assert min(n.quarterLength for n in bar.notesAndRests) >= 0.5


def test_notes_before_the_first_downbeat_form_a_pickup_bar() -> None:
    late = GRID.model_copy(update={"downbeats": [1.0]})  # bar 1 starts at quarter 2
    rows = [(0.5, 0.5, 60), (1, 1, 62), (2, 4, 64), (6, 2, 65)]
    document = NOTATOR.notate(q_notes(rows), settings("vocals", grid=late))
    bars = measures(parse(document.musicxml).parts[0])
    assert [bar.number for bar in bars] == [0, 1, 2]
    assert sum(n.quarterLength for n in bars[0].notesAndRests) == 1.5
    assert document.bars == 3


@pytest.mark.parametrize(
    ("profile", "clef_class"),
    [("vocals", clef.TrebleClef), ("guitar", clef.Treble8vbClef), ("bass", clef.Bass8vbClef)],
)
def test_clef_per_profile(profile: ScoreProfile, clef_class: type) -> None:
    document = NOTATOR.notate(q_notes([(0, 1, 60), (1, 1, 62)]), settings(profile))
    found = parse(document.musicxml).parts[0].recurse().getElementsByClass(clef.Clef)[0]
    assert type(found) is clef_class


@pytest.mark.parametrize("profile", ["keys", "piano"])
def test_grand_staff_splits_at_middle_c(profile: ScoreProfile) -> None:
    rows = [(0, 2, 60), (0, 2, 64), (0, 2, 67), (0, 4, 48), (2, 2, 59)]
    document = NOTATOR.notate(q_notes(rows), settings(profile, key="C major"))
    upper, lower = parse(document.musicxml).parts
    assert type(upper.recurse().getElementsByClass(clef.Clef)[0]) is clef.TrebleClef
    assert type(lower.recurse().getElementsByClass(clef.Clef)[0]) is clef.BassClef
    upper_pitches = [p.midi for n in upper.recurse().notes for p in n.pitches]
    lower_pitches = [p.midi for n in lower.recurse().notes for p in n.pitches]
    assert sorted(upper_pitches) == [60, 64, 67]
    assert sorted(lower_pitches) == [48, 59]
    chord = next(iter(upper.recurse().notes))
    assert chord.isChord and chord.quarterLength == 2


def test_overlapping_notes_of_a_staff_end_at_the_next_onset() -> None:
    rows = [(0, 4, 60), (1, 1, 64)]
    document = NOTATOR.notate(q_notes(rows), settings("guitar", key="C major"))
    bar = measures(parse(document.musicxml).parts[0])[0]
    assert [(n.offset, n.quarterLength, n.isRest) for n in bar.notesAndRests] == [
        (0, 1, False),
        (1, 1, False),
        (2, 2, True),
    ]


def test_pitches_are_spelled_in_the_key() -> None:
    rows = [(0, 1, 68), (1, 1, 70), (2, 1, 66)]
    document = NOTATOR.notate(q_notes(rows), settings("vocals", key="A- major"))
    names = [n.pitch.name for n in parse(document.musicxml).recurse().notes]
    assert names == ["A-", "B-", "G-"]
    assert document.key is not None
    assert (document.key.tonic, document.key.source) == ("A-", "override")


def test_the_key_is_estimated_without_an_override() -> None:
    line = [(67, 2), (71, 1), (74, 1), (67, 1), (69, 1), (71, 2), (72, 1), (74, 2), (66, 1)]
    rows, onset = [], 0.0
    for pitch, length in line:
        rows.append((onset, float(length), pitch))
        onset += length
    document = NOTATOR.notate(q_notes(rows), settings("vocals"))
    assert document.key is not None
    assert (document.key.tonic, document.key.mode, document.key.source) == (
        "G",
        "major",
        "estimated",
    )


def test_midi_has_one_note_on_per_note_with_ties_merged() -> None:
    document = NOTATOR.notate(q_notes(BASS_LINE), settings("bass"))
    ons = midi_note_ons(document.midi)
    assert [event.pitch for event in ons] == [43, 47, 50, 43, 45]


def test_titles_and_part_name_are_written() -> None:
    titled = NotationSettings(
        profile="bass",
        grid=GRID,
        resolution="sixteenth",
        key_override=None,
        titles=ScoreTitles(title="My score", subtitle="Song - Artist", instrument_name="Bass gtr"),
        duration_seconds=10.0,
    )
    document = NOTATOR.notate(q_notes(BASS_LINE), titled)
    score = parse(document.musicxml)
    assert (score.metadata.title, score.metadata.composer) == ("My score", "Song - Artist")
    assert score.parts[0].partName == "Bass gtr"
    assert b"StemStudio" in document.musicxml
    assert b'<score-partwise version="4.0">' in document.musicxml


def test_the_part_name_defaults_to_the_profile() -> None:
    document = NOTATOR.notate(q_notes(BASS_LINE), settings("bass"))
    assert parse(document.musicxml).parts[0].partName == "Bass"


# ------------------------------------------------------------------ notator rules


def test_nothing_left_after_cleaning_raises_no_notes() -> None:
    with pytest.raises(NoNotesError):
        NOTATOR.notate(q_notes([(0, 1, 10), (1, 1, 11)]), settings("bass"))
    with pytest.raises(NoNotesError):
        NOTATOR.notate([], settings("vocals"))


def test_warnings_are_collected_in_order() -> None:
    manual = ManualTempo(bpm=120, beats_per_bar=4, first_downbeat_seconds=0.0)
    rows = [(0, 1, 43), (1, 1, 10), (2, 1, 11)]
    document = NOTATOR.notate(q_notes(rows), settings("bass", grid=manual))
    assert [w.code for w in document.warnings] == ["manual-tempo", "notes-cleaned", "few-notes"]
    assert document.warnings[1] == score_warning("notes-cleaned", n=2)


def test_manual_grid_places_bar_one_at_the_first_downbeat() -> None:
    manual = ManualTempo(bpm=120, beats_per_bar=3, first_downbeat_seconds=1.0)
    rows = [(0, 1, 60), (2, 1, 62), (3, 1, 64), (5, 1, 65)]  # 0 s .. 3 s at 120 BPM
    document = NOTATOR.notate(q_notes(rows), settings("vocals", grid=manual))
    bars = measures(parse(document.musicxml).parts[0])
    assert [bar.number for bar in bars] == [0, 1, 2]
    assert document.time_signature == "3/4"


def test_the_factory_returns_a_notator() -> None:
    notator: Notator = create_notator()
    assert isinstance(notator, MusicNotator)


# ------------------------------------------------------------------ drums


def hit(quarter: float, piece: str, strength: float = 0.9) -> DrumHit:
    return DrumHit(time=quarter * QUARTER_SECONDS, piece=piece, strength=strength)  # type: ignore[arg-type]


ROCK_BAR = [
    *(hit(q, "kick") for q in (0, 2)),
    *(hit(q, "snare") for q in (1, 3)),
    *(hit(q / 2, "hihat", 0.6) for q in range(8)),
]


def test_drums_use_a_percussion_staff_with_two_voices() -> None:
    document = NOTATOR.notate_drums(ROCK_BAR, settings("drums"))
    part = parse(document.musicxml).parts[0]
    assert isinstance(part.recurse().getElementsByClass(clef.Clef)[0], clef.PercussionClef)
    bar = measures(part)[0]
    hands, feet = bar.voices
    assert [n.offset for n in hands.notes] == [0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5]
    assert [n.offset for n in feet.notes] == [0, 2]
    assert all(sum(n.quarterLength for n in v.notesAndRests) == 4 for v in (hands, feet))
    assert (document.key, document.notes, document.bars) == (None, 12, 1)


def test_hand_hits_at_the_same_time_form_a_chord_with_x_noteheads_for_cymbals() -> None:
    document = NOTATOR.notate_drums(ROCK_BAR, settings("drums"))
    hands = measures(parse(document.musicxml).parts[0])[0].voices[0]
    on_two = hands.notes[2]
    assert isinstance(on_two, percussion.PercussionChord)
    heads = sorted(n.notehead for n in on_two.notes)
    assert heads == ["normal", "x"]
    assert hands.notes[1].notehead == "x"
    assert b"<stem>down</stem>" in document.musicxml


def test_drum_hits_last_at_most_a_quarter_and_never_cross_a_bar() -> None:
    hits = [hit(0, "kick"), hit(3.5, "cymbals"), hit(7, "toms")]
    document = NOTATOR.notate_drums(hits, settings("drums"))
    bars = measures(parse(document.musicxml).parts[0])
    assert len(bars) == 2
    lengths = [n.quarterLength for bar in bars for n in bar.recurse().notes]
    assert lengths == [0.5, 1, 1]  # bar 1: hands (cymbals), feet (kick); bar 2: toms
    assert not any(n.tie for bar in bars for n in bar.recurse().notes)


def test_drum_midi_uses_the_general_midi_drum_map() -> None:
    document = NOTATOR.notate_drums(ROCK_BAR, settings("drums"))
    ons = midi_note_ons(document.midi)
    assert len(ons) == 12
    assert {event.channel for event in ons} == {10}
    assert {event.pitch for event in ons} == {35, 38, 44}


def test_no_drum_hits_raise_no_notes() -> None:
    with pytest.raises(NoNotesError):
        NOTATOR.notate_drums([], settings("drums"))


def test_drum_warnings() -> None:
    manual = ManualTempo(bpm=100, beats_per_bar=4, first_downbeat_seconds=0.0)
    document = NOTATOR.notate_drums([hit(0, "kick")], settings("drums", grid=manual))
    assert [w.code for w in document.warnings] == ["manual-tempo", "few-notes"]
    assert document.bpm == 100

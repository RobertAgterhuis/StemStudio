"""Tablature: string/fret assignment and the TAB staff in MusicXML (016 T023, data-model 4.5, SC-002)."""

import xml.etree.ElementTree as ET
from collections.abc import Sequence
from typing import get_args

import pytest

from stemstudio_worker.notation.notator import MusicNotator
from stemstudio_worker.notation.tablature import ViterbiTabFingerer
from stemstudio_worker.ports import TUNINGS, NotationSettings, NoteEvent, TabAssignment, Tuning
from stemstudio_worker.schemas import (
    BeatGridTempo,
    ScoreProfile,
    ScoreTitles,
    TablatureSettings,
    TabLayout,
    TuningId,
)

from .conftest import QUARTER_SECONDS, note

STANDARD = TUNINGS["guitar-standard"]
GRID = BeatGridTempo(
    bpm=120, beats_per_bar=4, beats=[i * QUARTER_SECONDS for i in range(80)], downbeats=[0.0]
)
_STEPS = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def _assign(
    events: Sequence[Sequence[int]], tuning: Tuning = STANDARD, capo: int = 0
) -> TabAssignment:
    return ViterbiTabFingerer().assign(events, tuning, capo)


def _sounding(tuning: Tuning, capo: int, string: int, fret: int) -> int:
    return tuning.pitches[len(tuning.pitches) - string] + capo + fret


@pytest.mark.parametrize("tuning_id", get_args(TuningId))
def test_every_open_string_of_every_tuning_is_fret_0(tuning_id: TuningId) -> None:
    tuning = TUNINGS[tuning_id]
    result = _assign([[p] for p in tuning.pitches], tuning)
    count = len(tuning.pitches)
    assert [pos[0] for pos in result.positions] == [(count - i, 0) for i in range(count)]


@pytest.mark.parametrize("tuning_id", get_args(TuningId))
def test_every_assigned_fret_sounds_its_pitch(tuning_id: TuningId) -> None:
    tuning = TUNINGS[tuning_id]
    low, high = tuning.pitches[0], tuning.pitches[-1] + 24
    events = [[p] for p in range(low, high + 1, 3)] + [[low + 7, low + 12, low + 16]]
    result = _assign(events, tuning)
    for event, positions in zip(events, result.positions, strict=True):
        assert sorted(_sounding(tuning, 0, s, f) for s, f in positions) == sorted(event)
        assert all(0 <= f <= 24 for _, f in positions)
        assert len({s for s, _ in positions}) == len(positions)


def test_drop_d_low_d_is_string_6_open() -> None:
    assert _assign([[38]], TUNINGS["guitar-drop-d"]).positions == (((6, 0),),)


def test_five_string_bass_uses_the_low_b() -> None:
    result = _assign([[23], [25], [28]], TUNINGS["bass-5-standard"])
    assert result.positions == (((5, 0),), ((5, 2),), ((4, 0),))


def test_capo_frets_are_counted_from_the_capo() -> None:
    result = _assign([[42], [47], [52]], STANDARD, capo=2)
    assert result.positions == (((6, 0),), ((5, 0),), ((4, 0),))


def test_notes_out_of_range_move_by_octaves_and_are_counted() -> None:
    result = _assign([[30], [100], [52]], STANDARD)
    pitches = [_sounding(STANDARD, 0, s, f) for event in result.positions for s, f in event]
    assert pitches == [42, 88, 52]
    assert result.octave_moved == 2


def test_a_note_below_the_capo_moves_up_an_octave() -> None:
    result = _assign([[40]], STANDARD, capo=3)
    ((string, fret),) = result.positions[0]
    assert _sounding(STANDARD, 3, string, fret) == 52
    assert result.octave_moved == 1


def test_chords_with_more_pitches_than_strings_drop_the_last_ones() -> None:
    result = _assign([[40, 45, 50, 55, 59, 64, 67]], STANDARD)
    assert len(result.positions[0]) == 6
    assert result.notes_dropped == 1
    bass = _assign([[28, 33, 38, 43, 47]], TUNINGS["bass-4-standard"])
    assert (len(bass.positions[0]), bass.notes_dropped) == (4, 1)


def test_unplayable_unisons_are_dropped() -> None:
    result = _assign([[64, 64]], STANDARD)
    assert len(result.positions[0]) == 2
    assert {_sounding(STANDARD, 0, s, f) for s, f in result.positions[0]} == {64}


def test_chords_keep_a_span_of_at_most_four_fretted_frets() -> None:
    chords = [
        [48, 52, 55, 60, 64],
        [43, 47, 50, 55, 59, 67],
        [45, 52, 57, 60, 64],
        [40, 47, 52, 56, 59, 64],
    ]
    result = _assign(chords)
    for positions in result.positions:
        fretted = [f for _, f in positions if f > 0]
        assert max(fretted) - min(fretted) <= 3
    assert result.wide_stretches == 0


def test_a_chord_that_needs_a_wide_stretch_is_counted() -> None:
    result = _assign([[41, 72]])
    assert result.wide_stretches == 1
    assert sorted(_sounding(STANDARD, 0, s, f) for s, f in result.positions[0]) == [41, 72]


GOLDEN: list[tuple[list[list[int]], list[list[tuple[int, int]]]]] = [
    (  # C major scale, open position
        [[48], [50], [52], [53], [55], [57], [59], [60]],
        [[(5, 3)], [(4, 0)], [(4, 2)], [(4, 3)], [(3, 0)], [(3, 2)], [(2, 0)], [(2, 1)]],
    ),
    (  # open C, G, Am, E, D chords
        [
            [48, 52, 55, 60, 64],
            [43, 47, 50, 55, 59, 67],
            [45, 52, 57, 60, 64],
            [40, 47, 52, 56, 59, 64],
            [50, 57, 62, 66],
        ],
        [
            [(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)],
            [(6, 3), (5, 2), (4, 0), (3, 0), (2, 0), (1, 3)],
            [(5, 0), (4, 2), (3, 2), (2, 1), (1, 0)],
            [(6, 0), (5, 2), (4, 2), (3, 1), (2, 0), (1, 0)],
            [(4, 0), (3, 2), (2, 3), (1, 2)],
        ],
    ),
    (  # E minor pentatonic, open position
        [[40], [43], [45], [47], [50], [52], [55], [57], [59], [62], [64], [67]],
        [
            [(6, 0)],
            [(6, 3)],
            [(5, 0)],
            [(5, 2)],
            [(4, 0)],
            [(4, 2)],
            [(3, 0)],
            [(3, 2)],
            [(2, 0)],
            [(2, 3)],
            [(1, 0)],
            [(1, 3)],
        ],
    ),  # fmt: skip
    (  # bass line E-A-D-G, open strings and fifth frets
        [[28], [33], [35], [38], [40], [43]],
        [[(4, 0)], [(3, 0)], [(3, 2)], [(2, 0)], [(2, 2)], [(1, 0)]],
    ),
]


def test_golden_fingerings_put_at_least_95_percent_on_the_expected_string() -> None:
    hits = total = 0
    for index, (events, expected) in enumerate(GOLDEN):
        tuning = TUNINGS["bass-4-standard"] if index == 3 else STANDARD
        result = _assign(events, tuning)
        for got, want in zip(result.positions, expected, strict=True):
            got_strings = sorted(s for s, _ in got)
            want_strings = sorted(s for s, _ in want)
            hits += sum(a == b for a, b in zip(got_strings, want_strings, strict=True))
            total += len(want)
    assert hits / total >= 0.95


def test_an_empty_event_list_gives_no_positions() -> None:
    assert _assign([]) == TabAssignment(positions=())


# ------------------------------------------------------------------------------- MusicXML


def _settings(profile: ScoreProfile, tab: TablatureSettings | None) -> NotationSettings:
    return NotationSettings(
        profile=profile,
        grid=GRID,
        resolution="sixteenth",
        key_override=None,
        titles=ScoreTitles(title="Song - Guitar"),
        duration_seconds=40.0,
        tablature=tab,
    )


def _riff(pitches: Sequence[int]) -> list[NoteEvent]:
    return [note(i * QUARTER_SECONDS, (i + 1) * QUARTER_SECONDS, p) for i, p in enumerate(pitches)]


RIFF = [38, 45, 50, 53, 55, 57, 62, 64, 38, 50, 57, 62]


def _xml(
    profile: ScoreProfile, tab: TablatureSettings | None, notes: Sequence[NoteEvent]
) -> ET.Element:
    document = MusicNotator().notate(notes, _settings(profile, tab))
    return ET.fromstring(document.musicxml)


def _midi_of(element: ET.Element) -> int:
    pitch = element.find("pitch")
    assert pitch is not None
    step, octave = pitch.findtext("step", ""), int(pitch.findtext("octave", "0"))
    return 12 * (octave + 1) + _STEPS[step] + int(float(pitch.findtext("alter", "0")))


def _tab_notes(root: ET.Element, staff: str | None) -> list[ET.Element]:
    notes = [n for n in root.iter("note") if n.find("pitch") is not None]
    return [n for n in notes if staff is None or n.findtext("staff") == staff]


@pytest.mark.parametrize("layout", get_args(TabLayout))
def test_drop_d_score_has_a_six_line_tab_staff_with_tuning(layout: TabLayout) -> None:
    tab = TablatureSettings(tuning="guitar-drop-d", layout=layout)
    root = _xml("guitar", tab, _riff(RIFF))
    signs = [c.findtext("sign") for c in root.iter("clef")]
    assert "TAB" in signs
    assert (len(set(signs)) == 2) is (layout == "notation-and-tab")
    details = root.find(".//staff-details")
    assert details is not None
    assert details.findtext("staff-lines") == "6"
    tunings = details.findall("staff-tuning")
    assert [t.get("line") for t in tunings] == ["1", "2", "3", "4", "5", "6"]
    assert (tunings[0].findtext("tuning-step"), tunings[0].findtext("tuning-octave")) == ("D", "2")
    assert root.find(".//capo") is None


@pytest.mark.parametrize("layout", get_args(TabLayout))
def test_every_tab_note_has_a_string_and_fret_that_sound_its_pitch(layout: TabLayout) -> None:
    tuning = TUNINGS["guitar-drop-d"]
    root = _xml("guitar", TablatureSettings(tuning="guitar-drop-d", layout=layout), _riff(RIFF))
    staff = "2" if layout == "notation-and-tab" else None
    tab_notes = _tab_notes(root, staff)
    assert len(tab_notes) == len(RIFF)
    for element in tab_notes:
        string = int(element.findtext("notations/technical/string", "0"))
        fret = int(element.findtext("notations/technical/fret", "-1"))
        assert _sounding(tuning, 0, string, fret) == _midi_of(element)
    assert int(tab_notes[0].findtext("notations/technical/string", "0")) == 6


def test_notation_and_tab_keeps_the_notation_staff_without_technical_marks() -> None:
    root = _xml("guitar", TablatureSettings(tuning="guitar-standard"), _riff(RIFF[1:]))
    assert root.findtext(".//attributes/staves") == "2"
    notation = _tab_notes(root, "1")
    assert len(notation) == len(RIFF) - 1
    assert all(n.find("notations/technical") is None for n in notation)


def test_capo_is_written_in_the_staff_details_and_as_a_direction() -> None:
    tab = TablatureSettings(tuning="guitar-standard", capo=3, layout="tab-only")
    root = _xml("guitar", tab, _riff([55, 57, 59, 60, 62, 64, 67]))
    assert root.findtext(".//staff-details/capo") == "3"
    words = [w.text for w in root.iter("words")]
    assert "Capo 3" in words
    for element in _tab_notes(root, None):
        string = int(element.findtext("notations/technical/string", "0"))
        fret = int(element.findtext("notations/technical/fret", "-1"))
        assert _sounding(STANDARD, 3, string, fret) == _midi_of(element)


def test_five_string_bass_tab_only_has_five_lines() -> None:
    tab = TablatureSettings(tuning="bass-5-standard", layout="tab-only")
    root = _xml("bass", tab, _riff([23, 28, 30, 33, 35, 38]))
    assert root.findtext(".//staff-details/staff-lines") == "5"
    assert len(root.findall(".//staff-details/staff-tuning")) == 5
    assert [c.findtext("sign") for c in root.iter("clef")] == ["TAB"]


def test_tied_notes_keep_their_string_and_fret() -> None:
    long_note = [note(1.5, 3.5, 50)]
    root = _xml("guitar", TablatureSettings(tuning="guitar-standard", layout="tab-only"), long_note)
    positions = {
        (n.findtext("notations/technical/string"), n.findtext("notations/technical/fret"))
        for n in _tab_notes(root, None)
    }
    assert len(_tab_notes(root, None)) == 2
    assert positions == {("4", "0")}


def test_tablature_summary_reports_the_counts() -> None:
    notes = _riff([30, 52, 55]) + [note(2.0, 2.5, p) for p in (40, 45, 50, 55, 59, 64, 67)]
    document = MusicNotator().notate(
        notes, _settings("guitar", TablatureSettings(tuning="guitar-standard"))
    )
    summary = document.tablature
    assert summary is not None
    assert (summary.tuning, summary.strings, summary.capo, summary.layout) == (
        "guitar-standard",
        6,
        0,
        "notation-and-tab",
    )
    assert (summary.octave_moved, summary.notes_dropped) == (1, 1)
    codes = [w.code for w in document.warnings]
    assert "tab-octave-moved" in codes
    assert "tab-notes-dropped" in codes


def test_midi_is_the_same_with_and_without_tablature() -> None:
    notes = _riff(RIFF)
    plain = MusicNotator().notate(notes, _settings("guitar", None))
    tabbed = MusicNotator().notate(
        notes, _settings("guitar", TablatureSettings(tuning="guitar-drop-d"))
    )
    assert plain.midi == tabbed.midi
    assert plain.tablature is None


def test_without_tablature_the_musicxml_has_no_tab_staff() -> None:
    root = _xml("guitar", None, _riff(RIFF))
    assert "TAB" not in [c.findtext("sign") for c in root.iter("clef")]

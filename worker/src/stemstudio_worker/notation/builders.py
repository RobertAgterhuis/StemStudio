"""music21 scores from cleaned notes and hits (data-model.md 2.1, 2.3-2.5, 3; ADR-017 D5).

Pitched profiles: one staff (treble, bass 8vb, treble 8vb) or a grand staff split at middle C,
chords per staff, pitches spelled to the key. Drums: one five-line percussion staff with two
voices (hands up: hi-hat, cymbals, snare, toms; feet down: kick) and x noteheads for hi-hat and
cymbals. Every score gets the clef, key signature (pitched), time signature and tempo mark in
its first measure, a pickup measure when needed, rests in every gap and ties across bar lines.
"""

import itertools
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from fractions import Fraction
from typing import Any, Final, cast

from music21 import chord as m21_chord
from music21 import clef as m21_clef
from music21 import key as m21_key
from music21 import layout as m21_layout
from music21 import metadata as m21_metadata
from music21 import meter as m21_meter
from music21 import note as m21_note
from music21 import percussion as m21_percussion
from music21 import stream as m21_stream
from music21 import tempo as m21_tempo

from stemstudio_worker.notation.cleaning import PIECE_ORDER, PlacedHit, PlacedNote
from stemstudio_worker.notation.drum_midi import drum_instrument
from stemstudio_worker.notation.events import (
    Event,
    Make,
    chord_events,
    hit_end_rule,
    measure_contents,
    note_end,
)
from stemstudio_worker.notation.keys import key_sharps, spell
from stemstudio_worker.notation.profiles import Clef, Profile, staff_of
from stemstudio_worker.notation.timebase import Bar, TimeBase, bar_layout
from stemstudio_worker.schemas import DrumPiece, KeyInfo, ScoreTitles

SOFTWARE: Final = "StemStudio"
"""Written as the encoding software of the MusicXML."""

_CLEFS: Final[dict[Clef, Callable[[], m21_clef.Clef]]] = {
    "treble": m21_clef.TrebleClef,
    "bass": m21_clef.BassClef,
    "bass-8vb": m21_clef.Bass8vbClef,
    "treble-8vb": m21_clef.Treble8vbClef,
    "percussion": m21_clef.PercussionClef,
}

_DRUMS: Final[dict[DrumPiece, tuple[str, str]]] = {
    "kick": ("F4", "normal"),
    "snare": ("C5", "normal"),
    "toms": ("A4", "normal"),
    "hihat": ("G5", "x"),
    "cymbals": ("A5", "x"),
}
"""Display position and notehead per drum piece (the instrument: ``drum_midi``)."""

_PART: Final[Callable[[], Any]] = m21_stream.Part
_PART_STAFF: Final[Callable[[], Any]] = m21_stream.PartStaff

_C_MAJOR: Final = KeyInfo(tonic="C", mode="major", confidence=0.0, source="estimated")


@dataclass(frozen=True, slots=True)
class ScoreSpec:
    """What the score needs besides the notes.

    Attributes:
        profile: Instrument profile (clefs, staves).
        timebase: Meter and tempo.
        key: Key of a pitched profile; None for drums.
        titles: Title, subtitle and part name.
    """

    profile: Profile
    timebase: TimeBase
    key: KeyInfo | None
    titles: ScoreTitles


@dataclass(frozen=True, slots=True)
class BuiltScore:
    """A music21 score and its measure count (including a pickup)."""

    score: m21_stream.Score
    bars: int


def build_pitched_score(notes: Sequence[PlacedNote], spec: ScoreSpec) -> BuiltScore:
    """Build the score of a pitched profile from cleaned notes (``spec.key`` is set)."""
    info = spec.key or _C_MAJOR
    staves: list[list[PlacedNote]] = [[] for _ in spec.profile.clefs]
    for placed in notes:
        staves[staff_of(spec.profile, placed.pitch)].append(placed)
    voices = [[chord_events(staff, note_end, _pitched_maker(info))] for staff in staves]
    return _assemble(voices, spec)


def build_drum_score(hits: Sequence[PlacedHit], spec: ScoreSpec) -> BuiltScore:
    """Build the two-voice percussion score from cleaned hits."""
    end_of = hit_end_rule(spec.timebase.beats_per_bar)
    hands = chord_events([h for h in hits if h.hands], end_of, _drum_maker("up"))
    feet = chord_events([h for h in hits if not h.hands], end_of, _drum_maker("down"))
    return _assemble([[hands, feet]], spec)


def _assemble(staves: list[list[list[Event]]], spec: ScoreSpec) -> BuiltScore:
    """Lay out the bars over all events and build one part per staff."""
    events = list(itertools.chain.from_iterable(itertools.chain.from_iterable(staves)))
    first = min(e.onset for e in events)
    bars = bar_layout(first, max(e.end for e in events), spec.timebase.beats_per_bar)
    parts = [
        _part(voices, bars, _header(spec, index), len(staves) > 1)
        for index, voices in enumerate(staves)
    ]
    return BuiltScore(_score(parts, spec), len(bars))


def _part(
    voices: list[list[Event]], bars: Sequence[Bar], header: list[Any], grand: bool
) -> m21_stream.Part:
    """Build the measures of one staff."""
    part: Any = _PART_STAFF() if grand else _PART()
    beats_per_bar = int(bars[-1].length)  # the last bar is always a whole bar
    for index, bar in enumerate(bars):
        measure = _measure(voices, bar, beats_per_bar)
        for element in header if index == 0 else ():
            measure.insert(0, element)
        part.append(measure)
    return cast(m21_stream.Part, part)


def _measure(voices: list[list[Event]], bar: Bar, beats_per_bar: int) -> m21_stream.Measure:
    """Build one measure; several voices become music21 voices (ids 1, 2)."""
    measure = m21_stream.Measure(number=bar.number)
    if bar.length < beats_per_bar:
        measure.paddingLeft = beats_per_bar - bar.length
    if len(voices) == 1:
        _insert_all(measure, measure_contents(voices[0], bar))
        return measure
    for number, events in enumerate(voices, start=1):
        voice = m21_stream.Voice(id=str(number))
        _insert_all(voice, measure_contents(events, bar))
        measure.insert(0, voice)
    return measure


def _insert_all(target: Any, placed: list[tuple[Fraction, m21_note.GeneralNote]]) -> None:
    for offset, element in placed:
        target.insert(offset, element)


def _header(spec: ScoreSpec, staff: int) -> list[Any]:
    """Clef, key signature (pitched), time signature and, on the top staff, the tempo mark."""
    header: list[Any] = [_CLEFS[spec.profile.clefs[staff]]()]
    if spec.key is not None:
        header.append(m21_key.KeySignature(key_sharps(spec.key)))
    header.append(m21_meter.TimeSignature(spec.timebase.time_signature))
    if staff == 0:
        header.append(
            m21_tempo.MetronomeMark(
                number=spec.timebase.bpm, referent=m21_note.Note(type="quarter")
            )
        )
    return header


def _score(parts: list[m21_stream.Part], spec: ScoreSpec) -> m21_stream.Score:
    """Put the parts, the titles and (grand staff) the brace into a score."""
    score = m21_stream.Score()
    score.insert(0, _metadata(spec.titles))
    parts[0].partName = spec.titles.instrument_name or spec.profile.name.capitalize()
    for part in parts:
        score.insert(0, part)
    if len(parts) > 1:
        score.insert(0, m21_layout.StaffGroup(parts, symbol="brace"))
    return score


def _metadata(titles: ScoreTitles) -> m21_metadata.Metadata:
    info = m21_metadata.Metadata()
    info.title = titles.title
    info.composer = titles.subtitle or ""  # empty: music21 would write "Music21" otherwise
    info.add("software", SOFTWARE)
    return info


def _pitched_maker(info: KeyInfo) -> Callable[[Sequence[PlacedNote]], Make]:
    """Return the element factory of a chord (or single note) spelled in ``info``."""

    def for_group(group: Sequence[PlacedNote]) -> Make:
        names = [spell(n.pitch, info) for n in sorted(group, key=lambda n: n.pitch)]
        velocity = _midi_velocity(max(n.velocity for n in group))

        def make(length: Fraction) -> m21_note.GeneralNote:
            element: Any = m21_note.Note(names[0]) if len(names) == 1 else m21_chord.Chord(names)
            element.quarterLength = length
            element.volume.velocity = velocity
            return cast(m21_note.GeneralNote, element)

        return make

    return for_group


def _drum_maker(stem: str) -> Callable[[Sequence[PlacedHit]], Make]:
    """Return the element factory of simultaneous drum hits of one voice."""

    def for_group(group: Sequence[PlacedHit]) -> Make:
        pieces = sorted({h.piece for h in group}, key=PIECE_ORDER.index)
        velocity = _midi_velocity(max(h.strength for h in group))

        def make(length: Fraction) -> m21_note.GeneralNote:
            strokes = [_stroke(piece, velocity) for piece in pieces]
            element: Any = (
                strokes[0] if len(strokes) == 1 else m21_percussion.PercussionChord(strokes)
            )
            element.quarterLength = length
            element.stemDirection = stem
            return cast(m21_note.GeneralNote, element)

        return make

    return for_group


def _stroke(piece: DrumPiece, velocity: int) -> m21_note.Unpitched:
    display, head = _DRUMS[piece]
    stroke = m21_note.Unpitched(displayName=display)
    stroke.notehead = head
    stroke.storedInstrument = drum_instrument(piece)
    stroke.volume.velocity = velocity
    return stroke


def _midi_velocity(loudness: float) -> int:
    """Map 0..1 to MIDI velocity 1..127."""
    return max(1, min(127, round(loudness * 127)))

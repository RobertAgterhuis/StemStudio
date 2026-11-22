"""The TAB staff of a guitar or bass score (data-model 4.5 step 5, ADR-033 D6).

A post-processor over our own music21 MusicXML (no user XML): the notes of the one-staff part
are read in document order, chords grouped (``<chord/>``), and fingered by the
:class:`~stemstudio_worker.ports.TabFingerer`; tied continuations keep the string and fret of
their start. ``tab-only`` turns the staff into a TAB staff; ``notation-and-tab`` keeps it as
staff 1 and adds a copy as staff 2 (``<backup>`` + notes with ``<staff>2</staff>``). The TAB
staff gets ``<staff-lines>``, one ``<staff-tuning>`` per line and ``<capo>``; a capo is also
written as a "Capo n" direction above bar 1 because Verovio does not print ``<capo>``.
"""

import copy
import xml.etree.ElementTree as ET
from collections.abc import Iterator
from dataclasses import dataclass, replace
from typing import Final

from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.notation.tablature import FitResult
from stemstudio_worker.ports import TUNINGS, ScoreDocument, TabFingerer, Tuning
from stemstudio_worker.schemas import ScoreWarningCode, TablatureSettings, TablatureSummary

_STEPS: Final = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
_NAMES: Final = (
    ("C", 0), ("C", 1), ("D", 0), ("D", 1), ("E", 0), ("F", 0), ("F", 1), ("G", 0), ("G", 1),
    ("A", 0), ("A", 1), ("B", 0),
)  # fmt: skip
_BEFORE_STAFF: Final = frozenset(
    {"grace", "cue", "chord", "pitch", "rest", "unpitched", "duration", "tie", "instrument",
     "voice", "type", "dot", "accidental", "time-modification", "stem", "notehead",
     "notehead-text"}
)  # fmt: skip
_TAB_VOICE_OFFSET: Final = 4
_DROPPED_IN_TAB: Final = ("stem", "beam", "accidental", "lyric")


@dataclass(frozen=True, slots=True)
class TabResult:
    """The MusicXML with its TAB staff and the stretch count of the chosen fingering."""

    musicxml: bytes
    wide_stretches: int


def _midi(note: ET.Element) -> int:
    step, octave = note.findtext("pitch/step", "C"), int(note.findtext("pitch/octave", "4"))
    return 12 * (octave + 1) + _STEPS[step] + round(float(note.findtext("pitch/alter", "0")))


def _is_tie_stop(note: ET.Element) -> bool:
    types = {t.get("type") for t in note.findall("tie")}
    return "stop" in types and "start" not in types


def _pitched(part: ET.Element) -> Iterator[ET.Element]:
    return (n for n in part.iter("note") if n.find("pitch") is not None)


def _events(part: ET.Element) -> list[list[ET.Element]]:
    """Attacked notes grouped into chords, in document order."""
    events: list[list[ET.Element]] = []
    for note in _pitched(part):
        if _is_tie_stop(note):
            continue
        if note.find("chord") is not None and events:
            events[-1].append(note)
        else:
            events.append([note])
    return events


def _positions(
    part: ET.Element, tuning: Tuning, settings: TablatureSettings, fingerer: TabFingerer
) -> tuple[dict[int, tuple[int, int]], int]:
    """``(string, fret)`` per note element (by ``id``), ties included, and the stretch count."""
    events = _events(part)
    assignment = fingerer.assign([[_midi(n) for n in e] for e in events], tuning, settings.capo)
    found: dict[int, tuple[int, int]] = {}
    for event, positions in zip(events, assignment.positions, strict=True):
        for note, position in zip(event, positions, strict=False):
            found[id(note)] = position
    _carry_ties(part, found)
    return found, assignment.wide_stretches


def _carry_ties(part: ET.Element, found: dict[int, tuple[int, int]]) -> None:
    """Give every tied continuation the string and fret of the note it continues."""
    last: dict[int, tuple[int, int]] = {}
    for note in _pitched(part):
        if _is_tie_stop(note) and _midi(note) in last:
            found[id(note)] = last[_midi(note)]
        if id(note) in found:
            last[_midi(note)] = found[id(note)]


def _technical(note: ET.Element, position: tuple[int, int]) -> None:
    notations = note.find("notations")
    if notations is None:
        notations = ET.SubElement(note, "notations")
    technical = ET.SubElement(notations, "technical")
    ET.SubElement(technical, "string").text = str(position[0])
    ET.SubElement(technical, "fret").text = str(position[1])


def _staff_details(tuning: Tuning, capo: int, number: str | None) -> ET.Element:
    details = ET.Element("staff-details", {"number": number} if number else {})
    ET.SubElement(details, "staff-lines").text = str(len(tuning.pitches))
    for line, pitch in enumerate(tuning.pitches, start=1):
        step, alter = _NAMES[pitch % 12]
        staff_tuning = ET.SubElement(details, "staff-tuning", {"line": str(line)})
        ET.SubElement(staff_tuning, "tuning-step").text = step
        if alter:
            ET.SubElement(staff_tuning, "tuning-alter").text = str(alter)
        ET.SubElement(staff_tuning, "tuning-octave").text = str(pitch // 12 - 1)
    if capo:
        ET.SubElement(details, "capo").text = str(capo)
    return details


def _tab_clef(number: str | None) -> ET.Element:
    clef = ET.Element("clef", {"number": number} if number else {})
    ET.SubElement(clef, "sign").text = "TAB"
    ET.SubElement(clef, "line").text = "5"
    return clef


def _insert_after(parent: ET.Element, anchor: ET.Element, *elements: ET.Element) -> None:
    index = list(parent).index(anchor) + 1
    for offset, element in enumerate(elements):
        parent.insert(index + offset, element)


def _set_staff(element: ET.Element, staff: str) -> None:
    """Insert ``<staff>`` at its MusicXML position inside a ``<note>`` or ``<forward>``."""
    index = max((i + 1 for i, c in enumerate(element) if c.tag in _BEFORE_STAFF), default=0)
    marker = ET.Element("staff")
    marker.text = staff
    element.insert(index, marker)


def _direction(child: ET.Element) -> int:
    """+1 when ``child`` moves the position forward, -1 for ``<backup>``, 0 otherwise."""
    if child.tag == "backup":
        return -1
    return int(child.tag == "forward" or (child.tag == "note" and child.find("chord") is None))


def _measure_length(measure: ET.Element) -> int:
    return sum(_direction(c) * int(c.findtext("duration", "0")) for c in measure)


def _tab_copy(element: ET.Element) -> ET.Element:
    twin = copy.deepcopy(element)
    for tag in _DROPPED_IN_TAB:
        for child in twin.findall(tag):
            twin.remove(child)
    _shift_voice(twin)
    if twin.tag != "backup":
        _set_staff(twin, "2")
    return twin


def _shift_voice(element: ET.Element) -> None:
    """Move the voice of a TAB copy out of the notation staff's voices."""
    voice = element.find("voice")
    if voice is not None:
        voice.text = str(int(voice.text or "1") + _TAB_VOICE_OFFSET)


def _second_staff(part: ET.Element) -> dict[int, ET.Element]:
    """Append a copy of every measure's notes as staff 2; returns copy by original ``id``."""
    copies: dict[int, ET.Element] = {}
    for measure in part.iter("measure"):
        _copy_measure(measure, copies)
    return copies


def _copy_measure(measure: ET.Element, copies: dict[int, ET.Element]) -> None:
    """Back up over ``measure`` and append the staff-2 copy of its timed elements."""
    timed = [c for c in measure if c.tag in ("note", "forward", "backup")]
    backup = ET.SubElement(measure, "backup")
    ET.SubElement(backup, "duration").text = str(_measure_length(measure))
    for element in timed:
        twin = _tab_copy(element)
        measure.append(twin)
        copies[id(element)] = twin
        if element.tag != "backup":
            _set_staff(element, "1")


def _capo_direction(capo: int) -> ET.Element:
    direction = ET.Element("direction", {"placement": "above"})
    words = ET.SubElement(ET.SubElement(direction, "direction-type"), "words")
    words.text = f"Capo {capo}"
    return direction


def _attributes(part: ET.Element, tuning: Tuning, settings: TablatureSettings) -> None:
    """Clefs, ``<staves>`` and staff details of the first measure."""
    first = _first_clef(part)
    if first is None:
        return
    measure, attributes, clef = first
    details = _staff_details(tuning, settings.capo, None if settings.layout == "tab-only" else "2")
    if settings.layout == "tab-only":
        _insert_after(attributes, clef, _tab_clef(None), details)
        attributes.remove(clef)
    else:
        _two_staves(attributes, clef, details)
    if settings.capo:
        _insert_after(measure, attributes, _capo_direction(settings.capo))


def _first_clef(part: ET.Element) -> tuple[ET.Element, ET.Element, ET.Element] | None:
    """The first measure, its ``<attributes>`` and its ``<clef>``, or None when one is missing."""
    measure = part.find("measure")
    if measure is None:
        return None
    attributes = measure.find("attributes")
    clef = None if attributes is None else attributes.find("clef")
    return None if attributes is None or clef is None else (measure, attributes, clef)


def _two_staves(attributes: ET.Element, clef: ET.Element, details: ET.Element) -> None:
    """``<staves>2</staves>``, the notation clef as staff 1 and the TAB clef as staff 2."""
    clef.set("number", "1")
    staves = ET.Element("staves")
    staves.text = "2"
    attributes.insert(list(attributes).index(clef), staves)
    _insert_after(attributes, clef, _tab_clef("2"), details)


def _targets(part: ET.Element, settings: TablatureSettings) -> dict[int, ET.Element]:
    """The note that gets the frets, by the ``id`` of the notation note (staff 2 copy or itself)."""
    targets = {id(n): n for n in _pitched(part)}
    if settings.layout != "notation-and-tab":
        return targets
    copies = _second_staff(part)
    return {key: copies[key] for key in targets}


def add_tab_staff(musicxml: bytes, settings: TablatureSettings, fingerer: TabFingerer) -> TabResult:
    """Add the TAB staff to the one-part MusicXML of a guitar or bass score."""
    root = ET.fromstring(musicxml)  # noqa: S314 - our own music21 output, never user XML
    part = root.find("part")
    if part is None:
        return TabResult(musicxml, 0)
    tuning = TUNINGS[settings.tuning]
    positions, wide = _positions(part, tuning, settings, fingerer)
    targets = _targets(part, settings)
    for key, position in positions.items():
        _technical(targets[key], position)
    _attributes(part, tuning, settings)
    return TabResult(ET.tostring(root, encoding="utf-8", xml_declaration=True), wide)


def tablature_document(
    document: ScoreDocument, settings: TablatureSettings, fit: FitResult, fingerer: TabFingerer
) -> ScoreDocument:
    """``document`` with the TAB staff, ``tablature`` summary and the tab warnings."""
    result = add_tab_staff(document.musicxml, settings, fingerer)
    summary = _summary(settings, fit, result.wide_stretches)
    counts: tuple[tuple[ScoreWarningCode, int], ...] = (
        ("tab-octave-moved", fit.octave_moved),
        ("tab-notes-dropped", fit.notes_dropped),
        ("tab-wide-stretch", result.wide_stretches),
    )
    extra = tuple(score_warning(code, n=n) for code, n in counts if n)
    return replace(
        document, musicxml=result.musicxml, tablature=summary, warnings=document.warnings + extra
    )


def _summary(settings: TablatureSettings, fit: FitResult, wide: int) -> TablatureSummary:
    """The ``tablature`` facts of the score item."""
    return TablatureSummary(
        tuning=settings.tuning,
        strings=len(TUNINGS[settings.tuning].pitches),
        capo=settings.capo,
        layout=settings.layout,
        octave_moved=fit.octave_moved,
        notes_dropped=fit.notes_dropped,
        wide_stretches=wide,
    )

"""Transposed parts for every written instrument (data-model 4.2, 4.3, 4.6; research R-09).

The concert score is copied and every pitch is moved by the instrument interval, then spelled in
the written key: concert tonic + interval semitones, named by the shared spelling rule (flats
when the relative major is F Bb Eb Ab Db Gb), so written keys never exceed six accidentals (a
concert E major for an E-flat instrument is written in D-flat major). Key signatures follow,
the part name becomes the instrument name, the clef is the chosen or default one (single-staff
profiles), and ``<transpose>`` (written -> sounding) is added to the MusicXML. MIDI is always
written from the concert score. ``concert`` with ``auto`` changes nothing (008 output).
"""

import copy
import re
import xml.etree.ElementTree as ET
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from typing import Any, Final, cast

from music21 import clef as m21_clef
from music21 import converter as m21_converter
from music21 import key as m21_key
from music21 import pitch as m21_pitch
from music21 import stream as m21_stream

from stemstudio_worker.harmony.labels import FLAT_NAMES, SHARP_NAMES, prefer_flats
from stemstudio_worker.notation.cleaning import PlacedNote
from stemstudio_worker.notation.keys import key_sharps, spell
from stemstudio_worker.notation.profiles import Clef, Profile, get_profile
from stemstudio_worker.notation.writer import musicxml_bytes
from stemstudio_worker.ports import WRITTEN_INSTRUMENTS, PartResult, WrittenInstrumentInfo
from stemstudio_worker.schemas import (
    KeyInfo,
    KeyName,
    PartSettings,
    PartSummary,
    ScoreProfile,
    WrittenClef,
)

_CLEFS: Final[dict[WrittenClef, Callable[[], m21_clef.Clef]]] = {
    "treble": m21_clef.TrebleClef,
    "bass": m21_clef.BassClef,
    "alto": m21_clef.AltoClef,
    "tenor": m21_clef.TenorClef,
    "treble-8vb": m21_clef.Treble8vbClef,
    "bass-8vb": m21_clef.Bass8vbClef,
}
_PROFILE_CLEFS: Final[dict[Clef, WrittenClef]] = {
    "treble": "treble",
    "bass": "bass",
    "bass-8vb": "bass-8vb",
    "treble-8vb": "treble-8vb",
    "percussion": "treble",
}
_INTERVAL: Final = re.compile(r"^-?[PMm](\d{1,2})$")


@dataclass(frozen=True, slots=True)
class PartPlan:
    """What a part changes.

    Attributes:
        info: The written instrument.
        clef: Clef of a single-staff part (the grand staff keeps its clefs).
        written: Written key (concert key moved by the interval).
        name: Part name.
    """

    info: WrittenInstrumentInfo
    clef: WrittenClef
    written: KeyInfo
    name: str


def written_key_name(display: str) -> KeyName:
    """``"Db major"`` (display spelling) -> ``"D- major"`` (music21 / ``KeyName``)."""
    tonic, mode = display.split(" ")
    return cast("KeyName", f"{tonic[0]}{tonic[1:].replace('b', '-')} {mode}")


def written_key(concert: KeyInfo, semitones: int) -> KeyInfo:
    """The concert key moved by ``semitones`` and spelled by the shared rule."""
    tonic = (m21_pitch.Pitch(concert.tonic).pitchClass + semitones) % 12
    names = FLAT_NAMES if prefer_flats(tonic, concert.mode) else SHARP_NAMES
    name = written_key_name(f"{names[tonic]} {concert.mode}").split(" ")[0]
    return KeyInfo(
        tonic=name, mode=concert.mode, confidence=concert.confidence, source=concert.source
    )


def plan_part(part: PartSettings, profile: Profile, concert: KeyInfo, name: str) -> PartPlan:
    """Resolve the instrument, clef (``auto`` = instrument default, else profile) and key."""
    info = WRITTEN_INSTRUMENTS[part.instrument]
    default = info.default_clef or _PROFILE_CLEFS[profile.clefs[0]]
    clef: WrittenClef = default if part.clef == "auto" else part.clef
    return PartPlan(info, clef, written_key(concert, info.semitones), info.name or name)


def out_of_range(notes: Sequence[PlacedNote], info: WrittenInstrumentInfo) -> int:
    """Notes outside the instrument's usual sounding range (``part-out-of-range``)."""
    if info.low_midi is None or info.high_midi is None:
        return 0
    return sum(not info.low_midi <= n.pitch <= info.high_midi for n in notes)


def summary(plan: PartPlan, outside: int) -> PartSummary:
    """``ScoreSummary.part`` of a plan."""
    key = cast("KeyName", f"{plan.written.tonic} {plan.written.mode}")
    return PartSummary(
        instrument=plan.info.instrument_id,
        name=plan.name,
        interval=plan.info.interval,
        written_key=key,
        clef=plan.clef,
        out_of_range=outside,
    )


def _respell(element: Any, plan: PartPlan) -> None:
    pitches = [
        m21_pitch.Pitch(spell(p.midi + plan.info.semitones, plan.written)) for p in element.pitches
    ]
    if len(pitches) == 1 and not element.isChord:
        element.pitch = pitches[0]
    else:
        element.pitches = pitches


def written_score(score: m21_stream.Score, plan: PartPlan, grand: bool) -> m21_stream.Score:
    """A transposed copy of ``score``: pitches, key signatures, clef and part name."""
    written = copy.deepcopy(score)
    for element in written.recurse().notes:
        _respell(element, plan)
    for signature in written.recurse().getElementsByClass(m21_key.KeySignature):
        signature.sharps = key_sharps(plan.written)
    if not grand:
        for old in list(written.recurse().getElementsByClass(m21_clef.Clef)):
            old.activeSite.replace(old, _CLEFS[plan.clef]())
    written.parts[0].partName = plan.name
    return written


def _signed(value: int, sign: int) -> str:
    return str(value * sign)


def _transpose_element(info: WrittenInstrumentInfo) -> ET.Element:
    """``<transpose>`` from written to sounding pitch (the reverse of the catalogue interval)."""
    match = _INTERVAL.match(info.interval)
    number = int(match.group(1)) if match else 1
    down = -1 if info.semitones >= 0 else 1  # written -> sounding goes the other way
    steps = number - 1
    element = ET.Element("transpose")
    ET.SubElement(element, "diatonic").text = _signed(steps % 7, down)
    ET.SubElement(element, "chromatic").text = _signed(abs(info.semitones) % 12, down)
    if steps >= 7:  # noqa: PLR2004 - an octave or more
        ET.SubElement(element, "octave-change").text = _signed(steps // 7, down)
    return element


def with_transpose(musicxml: bytes, info: WrittenInstrumentInfo) -> bytes:
    """Add ``<transpose>`` to the first ``<attributes>`` of every part (nothing for P1)."""
    if info.semitones == 0:
        return musicxml
    root = ET.fromstring(musicxml)  # noqa: S314 - our own music21 output, never user XML
    for attributes in (p.find("measure/attributes") for p in root.iter("part")):
        if attributes is not None:
            attributes.append(_transpose_element(info))
    return bytes(ET.tostring(root, encoding="utf-8", xml_declaration=True))


class Music21PartWriter:
    """``ports.PartWriter``: concert MusicXML -> written part (music21)."""

    def write(self, musicxml: bytes, part: PartSettings, profile: ScoreProfile) -> PartResult:
        """Transpose, respell, set part name, ``<transpose>`` and clef; MIDI is untouched."""
        score = cast(m21_stream.Score, m21_converter.parse(musicxml, format="musicxml"))
        constants = get_profile(profile)
        plan = plan_part(part, constants, _concert_key(score), constants.name.capitalize())
        written = written_score(score, plan, len(constants.clefs) > 1)
        xml = with_transpose(musicxml_bytes(written), plan.info)
        return PartResult(musicxml=xml, summary=summary(plan, 0))


def _concert_key(score: m21_stream.Score) -> KeyInfo:
    """The key of a parsed score from its first key signature (major unless it names a mode)."""
    signatures = list(score.recurse().getElementsByClass(m21_key.KeySignature))
    found = signatures[0] if signatures else m21_key.KeySignature(0)
    mode = getattr(found, "mode", "major")
    key = found.asKey(mode="minor" if mode == "minor" else "major")
    return KeyInfo(tonic=key.tonic.name, mode=key.mode, confidence=1.0, source="override")

"""Hand-written MusicXML for the score-view tests (014 T026): single staff, grand staff, drums.

Every measure holds one whole note per staff (a pickup holds ``pickup_beats`` quarters), so the
bar count and the layout are exactly known.
"""

from typing import Final

_HEADER: Final = (
    '<?xml version="1.0" encoding="UTF-8"?>\n'
    '<score-partwise version="4.0"><part-list><score-part id="P1"><part-name>{name}</part-name>'
    '</score-part></part-list><part id="P1">'
)
_TEMPO: Final = (
    '<direction placement="above"><direction-type><metronome><beat-unit>quarter</beat-unit>'
    '<per-minute>120</per-minute></metronome></direction-type><sound tempo="120"/></direction>'
)
_CLEFS: Final = {
    "single": "<clef><sign>F</sign><line>4</line></clef>",
    "grand": ('<staves>2</staves><clef number="1"><sign>G</sign><line>2</line></clef>'
              '<clef number="2"><sign>F</sign><line>4</line></clef>'),
    "drums": "<clef><sign>percussion</sign></clef>",
}  # fmt: skip
_TYPES: Final = {1: "quarter", 2: "half", 3: "half", 4: "whole"}
_UNPITCHED: Final = (
    "<unpitched><display-step>C</display-step><display-octave>5</display-octave></unpitched>"
)


def _note(kind: str, beats: int, staff: int) -> str:
    if kind == "drums":
        sound = _UNPITCHED
    else:
        step = "C" if staff == 1 else "E"
        sound = f"<pitch><step>{step}</step><octave>{4 - staff}</octave></pitch>"
    dot = "<dot/>" if beats == 3 else ""
    place = f"<staff>{staff}</staff>" if kind == "grand" else ""
    return (f"<note>{sound}<duration>{beats}</duration><voice>{staff}</voice>"
            f"<type>{_TYPES[beats]}</type>{dot}{place}</note>")  # fmt: skip


def _content(kind: str, beats: int) -> str:
    if kind != "grand":
        return _note(kind, beats, 1)
    return (
        _note(kind, beats, 1)
        + f"<backup><duration>{beats}</duration></backup>"
        + _note(kind, beats, 2)
    )


def score(bars: int, kind: str = "single", pickup_beats: int = 0, *, tempo: bool = True) -> bytes:
    """MusicXML with ``bars`` full 4/4 bars (plus a pickup of ``pickup_beats`` quarters)."""
    attributes = ("<attributes><divisions>1</divisions><key><fifths>0</fifths></key>"
                  f"<time><beats>4</beats><beat-type>4</beat-type></time>{_CLEFS[kind]}"
                  "</attributes>")  # fmt: skip
    measures = []
    if pickup_beats:
        measures.append(('0" implicit="yes', pickup_beats))
    measures += [(str(n), 4) for n in range(1, bars + 1)]
    body = []
    for position, (number, beats) in enumerate(measures):
        head = attributes + (_TEMPO if tempo else "") if position == 0 else ""
        body.append(f'<measure number="{number}">{head}{_content(kind, beats)}</measure>')
    text = _HEADER.format(name=kind) + "".join(body) + "</part></score-partwise>"
    return text.encode("utf-8")

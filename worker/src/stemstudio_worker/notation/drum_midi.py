"""General MIDI drums of the ``drums`` profile (data-model 4.8, research R-10, FR-023).

Every drum piece is played on MIDI channel 10 (music21 writes unpitched percussion there) with
the note of ``ports.GM_DRUM_MAP``: kick 36, snare 38, toms 47 (one stem for all toms), hi-hat
42 (closed) and cymbals 49 (crash 1).
"""

from collections.abc import Callable
from typing import Final

from music21 import instrument as m21_instrument

from stemstudio_worker.ports import GM_DRUM_MAP
from stemstudio_worker.schemas import DrumPiece

_INSTRUMENTS: Final[dict[DrumPiece, Callable[[], m21_instrument.UnpitchedPercussion]]] = {
    "kick": m21_instrument.BassDrum,
    "snare": m21_instrument.SnareDrum,
    "toms": m21_instrument.TomTom,
    "hihat": m21_instrument.HiHatCymbal,
    "cymbals": m21_instrument.CrashCymbals,
}


def drum_instrument(piece: DrumPiece) -> m21_instrument.UnpitchedPercussion:
    """The music21 instrument of ``piece`` with its General MIDI note."""
    found = _INSTRUMENTS[piece]()
    found.percMapPitch = GM_DRUM_MAP[piece]
    return found

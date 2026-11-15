"""Notes to scores (feature 008, beat-analysis agent; data-model.md 2.1-2.7, ADR-017 D3-D6).

Public API:

* :func:`create_notator` - factory for the composition root (``main.create_app``); imports
  music21 lazily so the worker starts without it.
* :class:`~stemstudio_worker.notation.notator.MusicNotator` - the
  :class:`~stemstudio_worker.ports.Notator`: per-profile cleaning, beat-grid quantisation,
  key estimation and spelling, music21 score, MusicXML and MIDI bytes.
* :mod:`~stemstudio_worker.notation.profiles` - profile constants, channel suggestions,
  default resolution and engine per profile (also for the transcription jobs).
"""

from stemstudio_worker.ports import Notator

__all__ = ["create_notator"]


def create_notator() -> Notator:
    """Build the production notator (music21 is imported on the first call)."""
    from stemstudio_worker.notation.notator import MusicNotator  # noqa: PLC0415 - lazy music21

    return MusicNotator()

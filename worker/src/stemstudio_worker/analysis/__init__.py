"""librosa beat-grid analysis (beat-analysis agent).

Public API:

* :func:`create_beat_analyzer` - factory called by the composition root (``main.create_app``).
* :class:`LibrosaBeatAnalyzer` - the :class:`~stemstudio_worker.ports.BeatAnalyzer`
  implementation: BPM, beat times, downbeats, beats per bar (4 when undeterminable), confidence
  and a ``reliable`` flag; silence, noise, very short or undecodable audio yield an unreliable
  grid instead of an exception.
"""

from stemstudio_worker.analysis.librosa_analyzer import LibrosaBeatAnalyzer
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import BeatAnalyzer

__all__ = ["LibrosaBeatAnalyzer", "create_beat_analyzer"]


def create_beat_analyzer(settings: WorkerSettings) -> BeatAnalyzer:
    """Build the production beat analyzer.

    Args:
        settings: Worker settings; the analyzer currently needs none of them (it always runs
            librosa on the CPU, also when the fake separator is configured).

    Returns:
        A new :class:`LibrosaBeatAnalyzer`.
    """
    del settings  # part of the fixed factory signature; no analyzer setting exists yet
    return LibrosaBeatAnalyzer()

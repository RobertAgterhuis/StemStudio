"""librosa beat-grid analysis (beat-analysis agent).

Contract (binding for the parallel wave): the composition root calls
``create_beat_analyzer(settings)``.
The beat-analysis agent replaces the placeholder body; the signature is fixed.
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import BeatAnalyzer

__all__ = ["create_beat_analyzer"]


def create_beat_analyzer(settings: WorkerSettings) -> BeatAnalyzer:
    """Build the production beat analyzer.

    Args:
        settings: Worker settings (data directory, separator kind, device, limits).

    Returns:
        The beat analyzer.

    Raises:
        NotImplementedError: Placeholder until the beat-analysis agent implements it.
    """
    msg = "create_beat_analyzer is implemented by the beat-analysis agent"
    raise NotImplementedError(msg)

"""Artefact estimate (data-model 4.4). Stub for the red commit of T032."""

from stemstudio_worker.insights.representation import Spectrogram
from stemstudio_worker.insights.thresholds import ARTEFACT_K

__all__ = ["ARTEFACT_K", "artefact_index", "dropout_share", "normalise"]


def dropout_share(spec: Spectrogram) -> float:
    """Stub."""
    raise NotImplementedError


def normalise(ratio: float) -> float:
    """Stub."""
    raise NotImplementedError


def artefact_index(stem: Spectrogram, mix: Spectrogram) -> float:
    """Stub."""
    raise NotImplementedError

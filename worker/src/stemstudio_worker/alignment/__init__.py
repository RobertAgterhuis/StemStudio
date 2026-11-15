"""Lyrics alignment (feature 006, ADR-015 D3): stable-ts + Whisper behind the ``Aligner`` port.

Only the composition root (``main.py``) imports this package. ``create_aligner`` gives the
deterministic fake for ``STEMSTUDIO_SEPARATOR=fake`` and the stable-ts adapter otherwise (models
loaded lazily, weights in ``STEMSTUDIO_MODEL_DIR/whisper``); ``create_vocal_loader`` prepares the
vocal stem (mono, 16 kHz, soxr); ``alignment_status`` reports ``Health.lyricsAlignment``.
"""

from typing import Protocol

from stemstudio_worker.alignment.audio_prep import VocalAudioLoader
from stemstudio_worker.alignment.fake import FakeAligner
from stemstudio_worker.alignment.stable_ts_aligner import StableTsAligner
from stemstudio_worker.alignment.status import AlignmentStatus, alignment_status
from stemstudio_worker.alignment.weights import WhisperWeights, whisper_weights
from stemstudio_worker.alignment.whisper_runtime import WhisperRuntime
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import Aligner

__all__ = [
    "AlignmentStatus",
    "ReleasableAligner",
    "VocalAudioLoader",
    "WhisperWeights",
    "alignment_status",
    "create_aligner",
    "create_vocal_loader",
    "whisper_weights",
]


class ReleasableAligner(Aligner, Protocol):
    """An aligner whose loaded models can be dropped when the worker is idle."""

    def release(self) -> None:
        """Drop loaded models."""
        ...


def create_aligner(settings: WorkerSettings) -> ReleasableAligner:
    """The fake aligner (``separator=fake``) or the stable-ts adapter."""
    if settings.separator == "fake":
        return FakeAligner(settings.lyrics.fake_mode)
    return StableTsAligner(whisper_weights(settings), WhisperRuntime())


def create_vocal_loader() -> VocalAudioLoader:
    """The vocal stem loader (mono float32 at 16 kHz)."""
    return VocalAudioLoader()

"""Alignment readiness: ``Health.lyricsAlignment`` and the "weights cached" fact of the estimate."""

import importlib.util

from stemstudio_worker.alignment.weights import WhisperWeights, whisper_weights
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.schemas import AlignModelName, LyricsAlignmentHealth

PACKAGES_MISSING_TEXT = "The speech alignment packages are not installed."
"""``Health.lyricsAlignment.reason`` when stable-ts or Whisper cannot be imported."""

_MODEL_NAMES: dict[str, AlignModelName] = {"turbo": "large-v3-turbo", "small": "small"}


class AlignmentStatus:
    """Whether alignments can run, with which model, and whether its weights are cached."""

    def __init__(self, settings: WorkerSettings, weights: WhisperWeights) -> None:
        """Report for ``settings`` (fake stack: no model needed) and ``weights``."""
        self._settings = settings
        self._weights = weights
        self._fake = settings.separator == "fake"

    def model_cached(self) -> bool:
        """True when no download is needed (fake stack, or complete weights in the cache)."""
        return self._fake or self._weights.cached()

    def health(self) -> LyricsAlignmentHealth:
        """The ``Health.lyricsAlignment`` member (checked on every call)."""
        available = self._fake or all(
            importlib.util.find_spec(name) is not None for name in ("stable_whisper", "whisper")
        )
        lyrics = self._settings.lyrics
        return LyricsAlignmentHealth(
            available=available, model=_MODEL_NAMES[lyrics.align_model],
            model_cached=self.model_cached(), auto=lyrics.align_auto,
            reason=None if available else PACKAGES_MISSING_TEXT,
        )  # fmt: skip


def alignment_status(settings: WorkerSettings) -> AlignmentStatus:
    """The status of the aligner ``create_aligner(settings)`` builds."""
    return AlignmentStatus(settings, whisper_weights(settings))

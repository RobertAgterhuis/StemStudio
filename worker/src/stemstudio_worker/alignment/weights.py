"""Where the Whisper weights live (``STEMSTUDIO_MODEL_DIR/whisper``, data-model.md 1)."""

from dataclasses import dataclass
from pathlib import Path

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import WHISPER_TURBO_WEIGHTS


@dataclass(frozen=True, slots=True)
class WhisperWeights:
    """One Whisper checkpoint in the model cache.

    Attributes:
        name: Whisper model name passed to ``load_model`` (``turbo`` or ``small``).
        root: ``download_root`` (on the model-cache volume, never ``~/.cache/whisper``).
        file_name: Checkpoint file Whisper writes into ``root``.
        size_bytes: Expected size of a complete file; None when not pinned.
    """

    name: str
    root: Path
    file_name: str
    size_bytes: int | None

    @property
    def path(self) -> Path:
        """The checkpoint file."""
        return self.root / self.file_name

    def cached(self) -> bool:
        """Whether the checkpoint is complete in the cache (exists, with the pinned size)."""
        try:
            size = self.path.stat().st_size
        except OSError:
            return False
        return self.size_bytes is None or size == self.size_bytes


def whisper_weights(settings: WorkerSettings) -> WhisperWeights:
    """The weights of ``STEMSTUDIO_ALIGN_MODEL`` below ``STEMSTUDIO_MODEL_DIR/whisper``."""
    root = settings.model_dir / "whisper"
    if settings.lyrics.align_model == "small":
        return WhisperWeights("small", root, "small.pt", None)
    turbo = WHISPER_TURBO_WEIGHTS
    return WhisperWeights("turbo", root, f"{turbo.model_id}.pt", turbo.size_bytes)

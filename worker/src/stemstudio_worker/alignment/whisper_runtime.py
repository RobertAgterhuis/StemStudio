"""openai-whisper + stable-ts behind ``WhisperRuntimePort`` (imported lazily: never at start-up)."""

import importlib
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt


def _module(name: str) -> Any:
    return importlib.import_module(name)


class WhisperRuntime:
    """Downloads with ``whisper._download`` (SHA-256 in the URL) and loads with stable-ts."""

    def download(self, name: str, root: Path) -> Path:
        """Download the checkpoint of ``name`` into ``root``; return the file path."""
        whisper = _module("whisper")
        return Path(whisper._download(whisper._MODELS[name], str(root), False))

    def load(self, name: str, device: str, root: Path) -> Any:
        """Load the stable-ts model ``name`` on ``device`` with ``download_root=root``."""
        return _module("stable_whisper").load_model(name, device=device, download_root=str(root))

    def detect_language(self, model: Any, audio: npt.NDArray[np.float32]) -> str:
        """Whisper language detection on the first 30 s of ``audio``."""
        whisper = _module("whisper")
        mel = whisper.log_mel_spectrogram(whisper.pad_or_trim(audio), n_mels=model.dims.n_mels)
        _tokens, probabilities = model.detect_language(mel.to(model.device))
        language: str = max(probabilities, key=probabilities.get)
        return language

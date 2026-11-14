"""Audio I/O for the separators: read the normalised WAV, write 16-bit FLAC stems (soundfile)."""

from pathlib import Path

import numpy as np
import numpy.typing as npt
import soundfile as sf

from stemstudio_worker.schemas import SAMPLE_RATE, StemName

SIX_STEMS: tuple[StemName, ...] = ("vocals", "drums", "bass", "guitar", "piano", "other")
"""Stem names in display order (``htdemucs_6s`` and ``fake``)."""

STAGE = "Separating stems"
"""Stage text reported by every separator."""


def read_audio(path: Path, dtype: str = "float32") -> npt.NDArray[np.generic]:
    """Read ``path`` as a ``(frames, channels)`` array of ``dtype``."""
    data, _ = sf.read(path, dtype=dtype, always_2d=True)
    return np.asarray(data)


def write_flac(path: Path, audio: npt.NDArray[np.generic]) -> None:
    """Write ``(frames, channels)`` audio as 44.1 kHz 16-bit FLAC; floats are clipped to [-1, 1]."""
    if np.issubdtype(audio.dtype, np.floating):
        audio = np.clip(audio, -1.0, 1.0)
    sf.write(path, audio, SAMPLE_RATE, format="FLAC", subtype="PCM_16")

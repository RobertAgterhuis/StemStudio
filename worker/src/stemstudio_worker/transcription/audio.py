"""Reading a library file (or one range of it) as mono float32 at a model's sample rate."""

from pathlib import Path
from typing import Final

import numpy as np
import numpy.typing as npt
import soundfile as sf
import soxr

from stemstudio_worker.ports import TranscriptionError
from stemstudio_worker.schemas import RenderRange

AUDIO_UNREADABLE_TEXT: Final = "The audio file could not be read."
"""Error of an input that soundfile cannot decode."""


def _read(path: Path, window: RenderRange | None) -> tuple[npt.NDArray[np.float32], int]:
    """The frames of ``window`` (all without one) and the file's sample rate."""
    with sf.SoundFile(path) as source:
        native = source.samplerate
        frames = -1
        if window is not None:
            source.seek(min(int(window.start_seconds * native), source.frames))
            frames = int((window.end_seconds - window.start_seconds) * native)
        return source.read(frames, dtype="float32", always_2d=True), native


def load_mono(path: Path, window: RenderRange | None, rate: int) -> npt.NDArray[np.float32]:
    """Read ``path`` (only ``window`` when given), downmix to mono and resample to ``rate``.

    Raises:
        TranscriptionError: The file cannot be read (``AUDIO_UNREADABLE_TEXT``).
    """
    try:
        data, native = _read(path, window)
    except (OSError, sf.LibsndfileError) as exc:
        raise TranscriptionError(AUDIO_UNREADABLE_TEXT) from exc
    mono = data.mean(axis=1)
    if native != rate:
        mono = soxr.resample(mono, native, rate)
    return np.ascontiguousarray(mono, dtype=np.float32)

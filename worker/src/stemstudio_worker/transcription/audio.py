"""Reading a library file (or one range of it) as mono float32 at a model's sample rate."""

from pathlib import Path
from typing import Final

import numpy as np
import numpy.typing as npt
import soundfile as sf
import soxr  # type: ignore[import-untyped]  # no stubs; mypy override requested

from stemstudio_worker.ports import TranscriptionError
from stemstudio_worker.schemas import RenderRange

AUDIO_UNREADABLE_TEXT: Final = "The audio file could not be read."
"""Error of an input that soundfile cannot decode."""


def load_mono(path: Path, window: RenderRange | None, rate: int) -> npt.NDArray[np.float32]:
    """Read ``path`` (only ``window`` when given), downmix to mono and resample to ``rate``.

    Raises:
        TranscriptionError: The file cannot be read (``AUDIO_UNREADABLE_TEXT``).
    """
    try:
        with sf.SoundFile(path) as source:
            native = source.samplerate
            if window is not None:
                source.seek(min(int(window.start_seconds * native), source.frames))
            frames = (
                -1 if window is None else int((window.end_seconds - window.start_seconds) * native)
            )
            data = source.read(frames, dtype="float32", always_2d=True)
    except (OSError, sf.LibsndfileError) as exc:
        raise TranscriptionError(AUDIO_UNREADABLE_TEXT) from exc
    mono = data.mean(axis=1)
    if native != rate:
        mono = soxr.resample(mono, native, rate)
    return np.ascontiguousarray(mono, dtype=np.float32)

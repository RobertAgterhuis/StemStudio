"""Vocal stem -> mono float32 at 16 kHz for the aligner (research.md R-04; soxr resampling)."""

from pathlib import Path

import numpy as np
import numpy.typing as npt
import soundfile as sf
import soxr

from stemstudio_worker.ports import ALIGN_SAMPLE_RATE, AlignmentError
from stemstudio_worker.schemas import VOCAL_STEM_MISSING_TEXT, VOCAL_STEM_SILENT_TEXT

SILENCE_PEAK = 10 ** (-60 / 20)
"""A stem whose peak stays below -60 dBFS is silent."""


class VocalAudioLoader:
    """Reads a vocal stem (FLAC), downmixes it to mono and resamples it to 16 kHz with soxr."""

    def load(self, path: Path) -> npt.NDArray[np.float32]:
        """Load ``path`` for alignment.

        Raises:
            AlignmentError: The file is missing or unreadable (``VOCAL_STEM_MISSING_TEXT``) or
                silent (``VOCAL_STEM_SILENT_TEXT``).
        """
        try:
            data, rate = sf.read(path, dtype="float32", always_2d=True)
        except (OSError, sf.LibsndfileError) as exc:
            raise AlignmentError(VOCAL_STEM_MISSING_TEXT) from exc
        mono = data.mean(axis=1)
        if mono.size == 0 or float(np.max(np.abs(mono))) < SILENCE_PEAK:
            raise AlignmentError(VOCAL_STEM_SILENT_TEXT)
        if rate != ALIGN_SAMPLE_RATE:
            mono = soxr.resample(mono, rate, ALIGN_SAMPLE_RATE)
        return np.ascontiguousarray(mono, dtype=np.float32)

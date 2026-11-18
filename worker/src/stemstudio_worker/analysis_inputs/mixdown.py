"""Sum library stems to mono at an analysis rate (``ports.HarmonicMixer``, research R-04).

Chords and key read the sum of the harmonic stems at 22,050 Hz; the reference melody reads the
vocal stem at 16 kHz. Each stem is read once (soundfile), downmixed to mono and resampled with
soxr; stems of different lengths are zero-padded to the longest. A sum above full scale is
scaled down to peak 1.0 (never up). The paths come resolved from ``LibraryReader.resolve``; with
``allowed_root`` the mixer checks the containment again (defence in depth).
"""

from collections.abc import Sequence
from pathlib import Path
from typing import Final

import numpy as np
import numpy.typing as npt
import soundfile as sf
import soxr

from stemstudio_worker.ports import AnalysisError, InputNotFoundError, InputPathError, MonoAudio

MAX_STEMS: Final = 4
"""Most stems per mix (``ChordsJobRequest.inputs``)."""

UNREADABLE_TEXT: Final = "A stem could not be read."
"""Failure text of a stem that exists but is not readable audio."""


class SoundfileHarmonicMixer:
    """``ports.HarmonicMixer`` reading FLAC/WAV stems with soundfile and soxr."""

    def __init__(self, allowed_root: Path | None = None) -> None:
        """Create the mixer; with ``allowed_root`` every path must resolve inside it."""
        self._root = allowed_root.resolve() if allowed_root is not None else None

    def mix(self, paths: Sequence[Path], sample_rate: int) -> MonoAudio:
        """Sum 1-4 stems of possibly different lengths (longest wins), clipping-safe.

        Raises:
            ValueError: Not 1-4 paths.
            InputPathError: A path lies outside ``allowed_root``.
            InputNotFoundError: A file is missing.
            AnalysisError: A file is not readable audio.
        """
        if not 1 <= len(paths) <= MAX_STEMS:
            msg = f"a mix needs 1-4 stems, got {len(paths)}"
            raise ValueError(msg)
        stems = [self._load(path, sample_rate) for path in paths]
        total = np.zeros(max(len(s) for s in stems), dtype=np.float64)
        for stem in stems:
            total[: len(stem)] += stem
        peak = float(np.max(np.abs(total))) if total.size else 0.0
        return np.asarray(total / max(1.0, peak), dtype=np.float32)

    def _load(self, path: Path, sample_rate: int) -> npt.NDArray[np.float64]:
        """One stem as mono float64 at ``sample_rate``."""
        real = self._checked(path)
        try:
            data, rate = sf.read(str(real), dtype="float32", always_2d=True)
        except (OSError, sf.LibsndfileError) as exc:
            raise AnalysisError(UNREADABLE_TEXT) from exc
        mono = data.mean(axis=1)
        if rate != sample_rate and mono.size:
            mono = soxr.resample(mono, rate, sample_rate)
        return np.asarray(mono, dtype=np.float64)

    def _checked(self, path: Path) -> Path:
        """The resolved path, inside ``allowed_root`` when set, that exists."""
        real = path.resolve()
        if self._root is not None and not real.is_relative_to(self._root):
            msg = f"Input path not allowed: {path.name}"
            raise InputPathError(msg)
        if not real.is_file():
            raise InputNotFoundError(path.name)
        return real

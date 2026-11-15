"""``FakePitchTracker``: a small autocorrelation F0 tracker standing in for RMVPE (010).

Deterministic and model-free: a frame is voiced when its normalised autocorrelation has a peak
above ``VOICED_CORRELATION`` between 60 and 1,000 Hz (after the first zero crossing) and it is
not silent. A tone reads as voiced at its pitch, white noise and silence as unvoiced, so the
voice-sample check and the ``unvoicedTarget`` rule can be exercised in CI and E2E. In fake mode
``unvoiced-sample`` every frame is unvoiced.
"""

from pathlib import Path
from typing import Final

import numpy as np
import numpy.typing as npt
import soundfile as sf

from stemstudio_generator.config import FakeGeneratorMode
from stemstudio_generator.ports import GeneratorError, InvalidInputError, Signal

F0_FRAMES_PER_SECOND: Final = 100
"""One F0 value per 10 ms, as RMVPE."""

F0_MIN_HZ: Final = 60.0
F0_MAX_HZ: Final = 1000.0

VOICED_CORRELATION: Final = 0.5
"""Normalised autocorrelation peak above which a frame is voiced."""

SILENCE_RMS: Final = 1e-3
"""Frames quieter than this (about -60 dBFS) are unvoiced."""

UNREADABLE_TEXT: Final = "The audio could not be read."

_CHUNK: Final = 256
"""Frames analysed per FFT batch (bounds the memory of long targets)."""

F0 = npt.NDArray[np.float32]


def read_mono(path: Path, error: type[GeneratorError] = InvalidInputError) -> tuple[Signal, int]:
    """Decode ``path`` and average its channels.

    Raises:
        GeneratorError: Of type ``error`` when the file is not readable audio.
    """
    try:
        data, rate = sf.read(path, dtype="float32", always_2d=True)
    except (sf.LibsndfileError, RuntimeError, OSError) as exc:
        raise error(UNREADABLE_TEXT) from exc
    return data.mean(axis=1, dtype=np.float32), int(rate)


class FakePitchTracker:
    """``PitchTracker`` fake (autocorrelation instead of RMVPE)."""

    def __init__(self, mode: FakeGeneratorMode = "ready") -> None:
        """Create the tracker; mode ``unvoiced-sample`` hears no voice at all."""
        self._mode = mode

    def f0(self, audio: Path) -> F0:
        """F0 per 10 ms frame in Hz; 0 marks an unvoiced frame.

        Raises:
            InvalidInputError: The file is not readable audio.
        """
        mono, rate = read_mono(audio)
        if self._mode == "unvoiced-sample":
            return np.zeros(_frame_count(mono.size, rate), dtype=np.float32)
        return autocorrelation_f0(mono, rate)


def _hop(rate: int) -> int:
    """Samples per 10 ms frame."""
    return max(1, rate // F0_FRAMES_PER_SECOND)


def _frame_count(samples: int, rate: int) -> int:
    """Number of 10 ms frames covering ``samples``."""
    return -(-samples // _hop(rate))


def autocorrelation_f0(mono: Signal, rate: int) -> F0:
    """F0 track of ``mono`` at ``rate`` (10 ms hop, window of at least two 60 Hz periods)."""
    window = 1 << int(np.ceil(np.log2(2 * rate / F0_MIN_HZ)))
    padded = np.pad(mono.astype(np.float32), (0, window))
    views = np.lib.stride_tricks.sliding_window_view(padded, window)
    starts = np.arange(_frame_count(mono.size, rate)) * _hop(rate)
    parts = [_frames_f0(views[starts[i : i + _CHUNK]], rate) for i in range(0, starts.size, _CHUNK)]
    return np.concatenate(parts) if parts else np.zeros(0, dtype=np.float32)


def _frames_f0(frames: Signal, rate: int) -> F0:
    """F0 of each row of ``frames`` (0 when unvoiced)."""
    window = frames.shape[1]
    spectrum = np.fft.rfft(frames, n=2 * window, axis=1)
    corr = np.fft.irfft(np.abs(spectrum) ** 2, axis=1)[:, : int(rate / F0_MIN_HZ) + 1]
    energy = corr[:, 0]
    norm = corr / np.maximum(energy, 1e-12)[:, None]
    lag = np.argmax(_searchable(norm, int(rate / F0_MAX_HZ)), axis=1)
    peak = norm[np.arange(norm.shape[0]), lag]
    voiced = (peak > VOICED_CORRELATION) & (np.sqrt(energy / window) > SILENCE_RMS)
    return np.where(voiced, rate / np.maximum(lag, 1), 0.0).astype(np.float32)


def _searchable(norm: npt.NDArray[np.float64], min_lag: int) -> npt.NDArray[np.float64]:
    """``norm`` with lags before ``min_lag`` and before the first zero crossing set to -inf."""
    lags = np.arange(norm.shape[1])
    first_negative = np.argmax(norm < 0, axis=1)
    first_negative[~(norm < 0).any(axis=1)] = norm.shape[1]
    hidden = (lags[None, :] < min_lag) | (lags[None, :] < first_negative[:, None])
    return np.where(hidden, -np.inf, norm)

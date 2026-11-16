"""Band-limited resampling with numpy only (no scipy/librosa in the base image).

FFT resampling per channel: the signal gets a silent tail (at least 0.1 s, rounded so the padded
length maps to a whole number of output frames), is transformed, its spectrum is cut or
zero-extended to the new rate and transformed back. The tail keeps the circular wrap of the FFT
away from the start of the signal; the output is trimmed to ``round(frames * target / rate)``.
"""

from math import gcd
from typing import Final

import numpy as np

from stemstudio_generator.ports import Signal

TAIL_SECONDS: Final = 0.1
"""Minimum silent tail added before the FFT."""


def resample(samples: Signal, rate: int, target_rate: int) -> Signal:
    """Resample ``(frames, channels)`` float32 audio from ``rate`` to ``target_rate``.

    Returns ``samples`` itself when the rates are equal or there are no frames.
    """
    frames = samples.shape[0]
    if rate == target_rate or frames == 0:
        return samples
    step_in, step_out = rate // gcd(rate, target_rate), target_rate // gcd(rate, target_rate)
    padded = -(-(frames + int(rate * TAIL_SECONDS)) // step_in) * step_in
    channels = [_resample_channel(samples[:, c], padded, padded * step_out // step_in)
                for c in range(samples.shape[1])]  # fmt: skip
    out_frames = round(frames * target_rate / rate)
    return np.stack(channels, axis=1)[:out_frames].astype(np.float32)


def _resample_channel(channel: Signal, padded: int, padded_out: int) -> Signal:
    """FFT-resample one channel zero-padded to ``padded`` frames into ``padded_out`` frames."""
    spectrum = np.fft.rfft(channel, n=padded)
    resized = np.zeros(padded_out // 2 + 1, dtype=spectrum.dtype)
    keep = min(spectrum.size, resized.size)
    resized[:keep] = spectrum[:keep]
    out: Signal = np.fft.irfft(resized, n=padded_out) * (padded_out / padded)
    return out

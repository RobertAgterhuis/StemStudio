"""Sample-peak levels in dBFS and the peak limiter (a plain gain change, no distortion)."""

from typing import Final

import numpy as np

from stemstudio_generator.ports import Signal

SILENCE_DBFS: Final = -120.0
"""Level reported for digital silence (the contract only bounds ``peakDbfs`` above by 0)."""


def peak(samples: Signal) -> float:
    """Largest absolute sample value (0.0 for an empty signal)."""
    return float(np.max(np.abs(samples))) if samples.size else 0.0


def peak_dbfs(samples: Signal) -> float:
    """Sample peak in dBFS, between ``SILENCE_DBFS`` and 0.0 (over-full-scale reads as 0.0)."""
    value = peak(samples)
    if value <= 0.0:
        return SILENCE_DBFS
    return min(0.0, max(SILENCE_DBFS, 20.0 * float(np.log10(value))))


def db_to_gain(decibels: float) -> float:
    """Linear gain of ``decibels``."""
    return float(10.0 ** (decibels / 20.0))


def limit_peak(samples: Signal, ceiling_dbfs: float) -> tuple[Signal, bool]:
    """Scale ``samples`` down so the peak sits at ``ceiling_dbfs`` when it is above it.

    Args:
        samples: Mono or ``(frames, channels)`` float32 audio.
        ceiling_dbfs: Highest allowed sample peak.

    Returns:
        The (possibly scaled) float32 samples and whether they were scaled.
    """
    ceiling = db_to_gain(ceiling_dbfs)
    value = peak(samples)
    if value <= ceiling:
        return samples, False
    return (samples * np.float32(ceiling / value)).astype(np.float32), True

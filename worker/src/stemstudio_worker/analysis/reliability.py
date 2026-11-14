"""Confidence of a detected beat grid.

Confidence is the pulse clarity of the onset envelope: its normalised autocorrelation at the
detected beat period. A steady pulse (clicks, drums) scores close to 1; noise scores near 0.
"""

import librosa
import numpy as np

from stemstudio_worker.analysis.tempo import FloatArray, OnsetEnvelope

LAG_SEARCH_FRAMES = 2
"""Half-width of the window around the beat-period lag searched for the autocorrelation peak."""


def pulse_confidence(envelope: OnsetEnvelope, bpm: float) -> float:
    """Return the pulse clarity (0..1) of ``envelope`` at the period of ``bpm``."""
    centred = envelope.values - float(np.mean(envelope.values))
    correlation: FloatArray = np.asarray(librosa.autocorrelate(centred), dtype=np.float64)
    lag = round(envelope.frame_rate * 60.0 / bpm)
    window = correlation[max(1, lag - LAG_SEARCH_FRAMES) : lag + LAG_SEARCH_FRAMES + 1]
    return float(np.clip(np.max(window) / correlation[0], 0.0, 1.0))

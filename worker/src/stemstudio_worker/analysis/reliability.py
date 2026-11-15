"""Confidence and reliability of a detected beat grid ("no clear pulse" handling).

Confidence is the pulse clarity of the onset envelope: its normalised autocorrelation at the
detected beat period. A steady pulse (clicks, drums) scores close to 1; noise scores near 0.
A grid is reliable when the pulse is clear and there are enough beats to trust the tempo;
otherwise the metronome starts disabled and the user can enter a BPM manually.
"""

import librosa
import numpy as np

from stemstudio_worker.analysis.tempo import FloatArray, OnsetEnvelope
from stemstudio_worker.schemas import BeatGrid

RELIABLE_CONFIDENCE = 0.2
"""Minimum pulse clarity of a reliable grid (white noise scores about 0.04, clicks 0.7-0.9)."""

MIN_RELIABLE_BEATS = 4
"""Minimum number of detected beats of a reliable grid."""

LAG_SEARCH_FRAMES = 2
"""Half-width of the window around the beat-period lag searched for the autocorrelation peak."""


def pulse_confidence(envelope: OnsetEnvelope, bpm: float) -> float:
    """Return the pulse clarity (0..1) of ``envelope`` at the period of ``bpm``; 0 if undefined."""
    centred = envelope.values - float(np.mean(envelope.values))
    correlation: FloatArray = np.asarray(librosa.autocorrelate(centred), dtype=np.float64)
    lag = round(envelope.frame_rate * 60.0 / bpm) if bpm > 0 else 0
    window = correlation[max(1, lag - LAG_SEARCH_FRAMES) : lag + LAG_SEARCH_FRAMES + 1]
    if lag == 0 or len(window) == 0 or correlation[0] <= 0:
        return 0.0
    return float(np.clip(np.max(window) / correlation[0], 0.0, 1.0))


def is_reliable(confidence: float, beat_count: int) -> bool:
    """Return True when the pulse is clear and enough beats were detected."""
    return confidence >= RELIABLE_CONFIDENCE and beat_count >= MIN_RELIABLE_BEATS


def unreliable_grid() -> BeatGrid:
    """Return the grid of a failed detection: BPM 0, no beats, 4/4 assumed, unreliable."""
    return BeatGrid(
        bpm=0.0, beats=[], downbeats=[], beats_per_bar=4, confidence=0.0, reliable=False
    )

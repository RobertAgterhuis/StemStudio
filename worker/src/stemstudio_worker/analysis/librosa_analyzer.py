"""``LibrosaBeatAnalyzer``: the production :class:`~stemstudio_worker.ports.BeatAnalyzer`."""

from pathlib import Path

import librosa
import numpy as np

from stemstudio_worker.analysis.downbeats import detect_meter, downbeat_times
from stemstudio_worker.analysis.reliability import pulse_confidence
from stemstudio_worker.analysis.tempo import (
    ANALYSIS_SAMPLE_RATE,
    FloatArray,
    compute_onset_envelope,
    estimate_bpm,
    track_beats,
)
from stemstudio_worker.schemas import BeatGrid

RELIABLE_CONFIDENCE = 0.2
"""Grids with a pulse clarity below this are flagged unreliable (metronome starts disabled)."""


class LibrosaBeatAnalyzer:
    """Detects tempo, beats, downbeats and beats per bar of a mix with librosa.

    The analyzer is stateless and safe to reuse across jobs.
    """

    def analyze(self, wav_path: Path) -> BeatGrid:
        """Analyse the full mix at ``wav_path``.

        Args:
            wav_path: Normalised input audio (any format soundfile can read).

        Returns:
            The beat grid; ``reliable=False`` when there is no clear pulse.
        """
        envelope = compute_onset_envelope(_load_mono(wav_path))
        beats = track_beats(envelope)
        bpm = estimate_bpm(beats)
        meter = detect_meter(envelope.strength_at(beats))
        confidence = pulse_confidence(envelope, bpm)
        return BeatGrid(
            bpm=bpm,
            beats=[float(t) for t in beats],
            downbeats=downbeat_times(beats, meter),
            beats_per_bar=meter.beats_per_bar,
            confidence=confidence,
            reliable=confidence >= RELIABLE_CONFIDENCE,
        )


def _load_mono(wav_path: Path) -> FloatArray:
    """Load ``wav_path`` as mono at :data:`ANALYSIS_SAMPLE_RATE`."""
    samples, _ = librosa.load(wav_path, sr=ANALYSIS_SAMPLE_RATE, mono=True)
    return np.asarray(samples, dtype=np.float64)

"""``LibrosaBeatAnalyzer``: the production :class:`~stemstudio_worker.ports.BeatAnalyzer`."""

import logging
from pathlib import Path

import librosa
import numpy as np

from stemstudio_worker.analysis.downbeats import detect_meter, downbeat_times
from stemstudio_worker.analysis.reliability import (
    MIN_RELIABLE_BEATS,
    is_reliable,
    pulse_confidence,
    unreliable_grid,
)
from stemstudio_worker.analysis.tempo import (
    ANALYSIS_SAMPLE_RATE,
    N_FFT,
    FloatArray,
    OnsetEnvelope,
    compute_onset_envelope,
    estimate_bpm,
    track_beats,
)
from stemstudio_worker.schemas import BeatGrid

SILENCE_PEAK = 1e-4
"""Audio whose absolute peak stays below this (about -80 dBFS) is treated as silence."""

_LOGGER = logging.getLogger(__name__)


class LibrosaBeatAnalyzer:
    """Detects tempo, beats, downbeats and beats per bar of a mix with librosa.

    The analyzer is stateless and safe to reuse across jobs. It never raises for audio without
    a clear pulse (silence, noise, very short or undecodable files); it returns a grid with
    ``reliable=False`` instead.
    """

    def analyze(self, wav_path: Path) -> BeatGrid:
        """Analyse the full mix at ``wav_path``.

        Args:
            wav_path: Normalised input audio (any format soundfile can read).

        Returns:
            The beat grid (beats ascending, downbeats a subset of beats); ``reliable=False``
            when there is no clear pulse.
        """
        samples = _load_mono(wav_path)
        if samples is None or not _has_signal(samples):
            return unreliable_grid()
        envelope = compute_onset_envelope(samples)
        beats = track_beats(envelope)
        if len(beats) < MIN_RELIABLE_BEATS:
            return unreliable_grid()
        return _build_grid(envelope, beats)


def _build_grid(envelope: OnsetEnvelope, beats: FloatArray) -> BeatGrid:
    """Assemble the grid from the tracked beats."""
    bpm = estimate_bpm(beats)
    meter = detect_meter(envelope.strength_at(beats))
    confidence = pulse_confidence(envelope, bpm)
    return BeatGrid(
        bpm=bpm,
        beats=[float(t) for t in beats],
        downbeats=downbeat_times(beats, meter),
        beats_per_bar=meter.beats_per_bar,
        confidence=confidence,
        reliable=is_reliable(confidence, len(beats)),
    )


def _load_mono(wav_path: Path) -> FloatArray | None:
    """Load ``wav_path`` as mono at :data:`ANALYSIS_SAMPLE_RATE`; None when it cannot be decoded."""
    try:
        samples, _ = librosa.load(wav_path, sr=ANALYSIS_SAMPLE_RATE, mono=True)
    except (OSError, RuntimeError, ValueError, EOFError) as error:
        _LOGGER.warning("Beat analysis skipped, audio not decodable: %s", type(error).__name__)
        return None
    return np.asarray(samples, dtype=np.float64)


def _has_signal(samples: FloatArray) -> bool:
    """Return True when there are enough samples for one analysis frame and they are not silent."""
    return len(samples) >= N_FFT and float(np.max(np.abs(samples))) >= SILENCE_PEAK

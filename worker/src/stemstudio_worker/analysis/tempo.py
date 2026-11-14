"""Onset envelope, beat tracking and global tempo estimation with librosa.

Beats come from librosa's dynamic-programming beat tracker and are then refined to the nearest
onset peak with sub-frame (parabolic) interpolation, which keeps them well within 30 ms of the
audible onset. Weak leading/trailing beats that the tracker extrapolates past the music are
trimmed. The global BPM is the median inter-beat interval of the refined beats.
"""

from dataclasses import dataclass

import librosa
import numpy as np
import numpy.typing as npt

FloatArray = npt.NDArray[np.float64]

ANALYSIS_SAMPLE_RATE = 22_050
"""Sample rate the mix is resampled to for analysis (Hz)."""

HOP_LENGTH = 256
"""Onset-envelope hop (samples); one frame is about 11.6 ms."""

N_FFT = 1_024
"""STFT size of the onset envelope (about 46 ms); short enough for precise onset timing."""

PEAK_SEARCH_FRAMES = 4
"""Half-width of the window in which a tracked beat is snapped to its onset peak (~46 ms)."""

EDGE_TRIM_RATIO = 0.2
"""Leading/trailing beats weaker than this fraction of the median beat strength are dropped."""

MAX_BPM = 400.0
"""Upper bound of the contract's ``bpm`` field."""


@dataclass(frozen=True, slots=True)
class OnsetEnvelope:
    """Onset strength per analysis frame.

    Attributes:
        values: Non-negative onset strength of every frame.
        frame_rate: Frames per second.
    """

    values: FloatArray
    frame_rate: float

    def strength_at(self, times: FloatArray) -> FloatArray:
        """Return the onset strength of the frames nearest to ``times`` (seconds)."""
        frames = np.clip(np.rint(times * self.frame_rate).astype(int), 0, len(self.values) - 1)
        return np.asarray(self.values[frames], dtype=np.float64)


def compute_onset_envelope(samples: FloatArray) -> OnsetEnvelope:
    """Compute the onset envelope of mono ``samples`` at :data:`ANALYSIS_SAMPLE_RATE`."""
    values = librosa.onset.onset_strength(
        y=samples.astype(np.float32), sr=ANALYSIS_SAMPLE_RATE, hop_length=HOP_LENGTH, n_fft=N_FFT
    )
    return OnsetEnvelope(np.asarray(values, dtype=np.float64), ANALYSIS_SAMPLE_RATE / HOP_LENGTH)


def track_beats(envelope: OnsetEnvelope) -> FloatArray:
    """Track beats and return their refined times in seconds, strictly ascending."""
    _, frames = librosa.beat.beat_track(
        onset_envelope=envelope.values, sr=ANALYSIS_SAMPLE_RATE, hop_length=HOP_LENGTH
    )
    peaks = np.asarray([_refine(envelope.values, int(f)) for f in frames], dtype=np.float64)
    times = np.unique(np.round(peaks / envelope.frame_rate, 4))
    return _trim_weak_edges(times, envelope.strength_at(times))


def estimate_bpm(beats: FloatArray) -> float:
    """Return the global tempo (BPM) from the median inter-beat interval; 0 for < 2 beats."""
    if len(beats) < 2:  # noqa: PLR2004 - an interval needs two beats
        return 0.0
    return min(MAX_BPM, 60.0 / float(np.median(np.diff(beats))))


def _refine(values: FloatArray, frame: int) -> float:
    """Snap ``frame`` to the strongest frame nearby and interpolate the peak (fractional frame)."""
    low = max(0, frame - PEAK_SEARCH_FRAMES)
    peak = low + int(np.argmax(values[low : frame + PEAK_SEARCH_FRAMES + 1]))
    if peak in (0, len(values) - 1):
        return float(peak)
    before, at, after = values[peak - 1], values[peak], values[peak + 1]
    curvature = before - 2.0 * at + after
    offset = 0.5 * (before - after) / curvature if curvature < 0 else 0.0
    return float(peak + offset)


def _trim_weak_edges(times: FloatArray, strengths: FloatArray) -> FloatArray:
    """Drop leading and trailing beats without a real onset under them."""
    if len(times) == 0:
        return times
    strong = np.flatnonzero(strengths >= EDGE_TRIM_RATIO * float(np.median(strengths)))
    return times[strong[0] : strong[-1] + 1]

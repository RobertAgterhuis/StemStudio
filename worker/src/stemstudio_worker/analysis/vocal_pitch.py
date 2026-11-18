"""Reference melody of a vocal stem (``ports.PitchAnalyzer``, research R-06, data-model 3.1).

``pyin`` (default): ``librosa.pyin`` at 16 kHz, 65-1047 Hz, frame 2048, hop 160 (10 ms); a frame
is voiced when its voicing probability is >= 0.5. ``yin`` (fast fallback): ``librosa.yin`` with
the same frames, voiced when the frame RMS is >= -45 dBFS, then a 5-frame median (the voicing
rule of the live tracker, R-05). The stem is analysed in 60 s chunks with a small overlap, so
progress and cancellation are reported between chunks. Output: centi-semitones per 10 ms
(0 = unvoiced), at most 90,000 frames.
"""

from collections.abc import Callable
from typing import Final, Literal

import librosa
import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import PITCH_SAMPLE_RATE, AnalysisProgress, MonoAudio, NoMelodyError
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    MAX_CENTS,
    MAX_PITCH_FRAMES,
    AnalysisWarning,
    AnalysisWarningCode,
    PitchEngine,
    PitchRange,
    VocalPitch,
    VocalSource,
)

Floats = npt.NDArray[np.float64]
Voicing = tuple[Floats, npt.NDArray[np.bool_]]

HOP: Final = 160
"""10 ms at 16 kHz."""

FRAME_LENGTH: Final = 1024
"""YIN frame (64 ms)."""

PYIN_FRAME_LENGTH: Final = 2048
"""pYIN frame (128 ms): with 1024 the voicing probability of an 80 Hz tone stays below 0.5 (its
internal window is half the frame); 2048 costs about the same (measured 1.27 s vs 1.19 s per
minute of audio)."""

FMIN_HZ: Final = 65.0
FMAX_HZ: Final = 1047.0
VOICED_PROBABILITY: Final = 0.5
"""pYIN frames below this voicing probability are unvoiced."""

YIN_MIN_DB: Final = -45.0
"""YIN frames quieter than this RMS level (dBFS) are unvoiced."""

YIN_THRESHOLD: Final = 0.15
"""YIN trough threshold (absolute threshold of the normalised difference)."""

MEDIAN_FRAMES: Final = 5
SILENT_PEAK: Final = 10 ** (-60 / 20)
"""A stem whose peak stays below -60 dBFS is silent."""

FEW_VOICED: Final = 0.1
"""A voiced share below this warns ``few-voiced``."""

CHUNK_FRAMES: Final = 6000
"""Frames per analysis chunk (60 s)."""

CHUNK_MARGIN: Final = 32
"""Overlap of a chunk with its neighbours in frames (320 ms each side)."""


def _pyin(audio: npt.NDArray[np.float32]) -> Voicing:
    f0, _, probability = librosa.pyin(
        audio,
        fmin=FMIN_HZ,
        fmax=FMAX_HZ,
        sr=PITCH_SAMPLE_RATE,
        frame_length=PYIN_FRAME_LENGTH,
        hop_length=HOP,
    )
    voiced = np.isfinite(f0) & (probability >= VOICED_PROBABILITY)
    return np.nan_to_num(np.asarray(f0, dtype=np.float64)), np.asarray(voiced, dtype=np.bool_)


def _median(values: Floats) -> Floats:
    """Running median over ``MEDIAN_FRAMES`` frames (edges repeated)."""
    half = MEDIAN_FRAMES // 2
    padded = np.pad(values, half, mode="edge")
    windows = np.lib.stride_tricks.sliding_window_view(padded, MEDIAN_FRAMES)
    return np.asarray(np.median(windows, axis=1), dtype=np.float64)


def _yin(audio: npt.NDArray[np.float32]) -> Voicing:
    f0 = librosa.yin(
        audio,
        fmin=FMIN_HZ,
        fmax=FMAX_HZ,
        sr=PITCH_SAMPLE_RATE,
        frame_length=FRAME_LENGTH,
        hop_length=HOP,
        trough_threshold=YIN_THRESHOLD,
    )
    rms = librosa.feature.rms(y=audio, frame_length=FRAME_LENGTH, hop_length=HOP)[0]
    count = min(len(f0), len(rms))
    voiced = librosa.amplitude_to_db(rms[:count], ref=1.0, amin=1e-10) >= YIN_MIN_DB
    return _median(np.asarray(f0[:count], dtype=np.float64)), np.asarray(voiced, dtype=np.bool_)


_ENGINES: Final[dict[str, Callable[[npt.NDArray[np.float32]], Voicing]]] = {
    "pyin": _pyin,
    "yin": _yin,
}


class LibrosaPitchAnalyzer:
    """``ports.PitchAnalyzer`` with the ``pyin`` or ``yin`` engine."""

    def __init__(self, engine: Literal["pyin", "yin"] = "pyin") -> None:
        """Create the analyzer for one engine.

        Raises:
            ValueError: The engine is neither ``pyin`` nor ``yin``.
        """
        if engine not in _ENGINES:
            msg = f"unknown pitch engine: {engine!r}"
            raise ValueError(msg)
        self._engine: Literal["pyin", "yin"] = engine

    @property
    def engine(self) -> PitchEngine:
        """Engine name recorded in the result."""
        return self._engine

    def analyze(
        self, audio: MonoAudio, source: VocalSource, progress: AnalysisProgress
    ) -> VocalPitch:
        """10 ms frames in centi-semitones (``audio`` at ``PITCH_SAMPLE_RATE``).

        Raises:
            NoMelodyError: The stem is silent or has no voiced frame at all.
            AnalysisCancelledError: Propagated from ``progress``.
        """
        progress(0.0)
        samples = np.asarray(audio[: MAX_PITCH_FRAMES * HOP], dtype=np.float32)
        if samples.size == 0 or float(np.max(np.abs(samples))) < SILENT_PEAK:
            raise NoMelodyError
        progress(0.1)
        cents = self._cents(samples, progress)
        if not np.any(cents):
            raise NoMelodyError
        progress(0.95)
        result = _result(cents, source, self._engine)
        progress(1.0)
        return result

    def _cents(
        self, samples: npt.NDArray[np.float32], progress: AnalysisProgress
    ) -> npt.NDArray[np.int64]:
        """Centi-semitones of every frame (``ceil(len / HOP)`` frames), chunk by chunk."""
        total = -(-len(samples) // HOP)
        out = np.zeros(total, dtype=np.int64)
        starts = range(0, total, CHUNK_FRAMES)
        for done, first in enumerate(starts, start=1):
            last = min(total, first + CHUNK_FRAMES)
            out[first:last] = self._chunk(samples, first, last)
            progress(0.1 + 0.85 * done / len(starts))
        return out

    def _chunk(
        self, samples: npt.NDArray[np.float32], first: int, last: int
    ) -> npt.NDArray[np.int64]:
        """Frames ``[first, last)`` analysed with ``CHUNK_MARGIN`` frames of context."""
        lead = min(first, CHUNK_MARGIN)
        piece = samples[(first - lead) * HOP : (last + CHUNK_MARGIN) * HOP]
        f0, voiced = _ENGINES[self._engine](piece)
        f0, voiced = f0[lead : lead + last - first], voiced[lead : lead + last - first]
        midi = librosa.hz_to_midi(np.where(voiced & (f0 > 0), f0, 1.0))
        cents = np.clip(np.round(np.asarray(midi) * 100), 1, MAX_CENTS).astype(np.int64)
        return np.where(voiced & (f0 > 0), cents, 0)


def _result(cents: npt.NDArray[np.int64], source: VocalSource, engine: PitchEngine) -> VocalPitch:
    """Pack the frames with voiced share, range and warnings."""
    voiced = cents[cents > 0]
    ratio = len(voiced) / len(cents)
    codes: list[AnalysisWarningCode] = []
    if ratio < FEW_VOICED:
        codes.append("few-voiced")
    if source == "vocals.lead":
        codes.append("detail-bleed")
    return VocalPitch(
        frames=cents.tolist(),
        source=source,
        engine=engine,
        voiced_ratio=ratio,
        range=PitchRange(
            low_cents=round(float(np.percentile(voiced, 5))),
            high_cents=round(float(np.percentile(voiced, 95))),
        ),
        warnings=[AnalysisWarning(code=c, text=ANALYSIS_WARNING_TEXTS[c]) for c in codes],
    )

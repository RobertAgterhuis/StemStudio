"""``LibrosaDrumTranscriber``: drum detail stems -> drum hits (008 data-model 2.3, research S-05).

Each detail stem (kick, snare, toms, hihat, cymbals) is loaded mono at 22,050 Hz, onsets are
detected with librosa (``delta`` 0.2 for kick and snare, 0.3 otherwise, hop 256 = 11.6 ms), and
every onset gets a linear loudness: the absolute peak of the stem just after it. The bleed gate
drops onsets quieter than 10 % of the piece's 99th-percentile peak (another piece leaking into
the stem); a hit's ``strength`` is its peak relative to that percentile, clamped to 0..1. Stems
whose peak stays below -60 dBFS, and stems that cannot be decoded, give no hits.
"""

import logging
from collections.abc import Mapping
from pathlib import Path
from typing import Final

import librosa
import numpy as np

from stemstudio_worker.analysis.tempo import FloatArray
from stemstudio_worker.ports import DrumHit, TranscriptionProgress
from stemstudio_worker.schemas import DrumPiece, RenderRange

ONSET_SAMPLE_RATE: Final = 22_050
"""Sample rate of the onset analysis (research.md S-05)."""

HOP_LENGTH: Final = 256
"""Onset frame hop (11.6 ms at 22,050 Hz)."""

BLEED_GATE: Final = 0.10
"""Onsets below this share of the piece's 99th-percentile peak are bleed (data-model 2.3)."""

GATE_PERCENTILE: Final = 99.0
"""Percentile of the onset peaks that the gate and the strengths refer to."""

SILENCE_PEAK: Final = 1e-3
"""Onsets (and stems) whose peak stays below this (-60 dBFS) are ignored."""

PEAK_BEFORE_SECONDS: Final = 0.010
PEAK_AFTER_SECONDS: Final = 0.030
"""The loudness of an onset is the absolute peak from 10 ms before to 30 ms after it."""

_DELTAS: Final[dict[DrumPiece, float]] = {"kick": 0.2, "snare": 0.2}
_DEFAULT_DELTA: Final = 0.3
_PIECES: Final[tuple[DrumPiece, ...]] = ("kick", "snare", "toms", "hihat", "cymbals")

_LOGGER = logging.getLogger(__name__)


class LibrosaDrumTranscriber:
    """The production :class:`~stemstudio_worker.ports.DrumTranscriber` (CPU only, stateless)."""

    def transcribe(
        self,
        inputs: Mapping[DrumPiece, Path],
        window: RenderRange | None,
        progress: TranscriptionProgress,
    ) -> tuple[DrumHit, ...]:
        """Detect the onsets of every given piece (missing pieces give no hits).

        Args:
            inputs: Resolved detail stem file per piece.
            window: Part to transcribe; times in the result are relative to its start.
            progress: Receives 0..1, one step per piece.

        Returns:
            Hits in time order (ties in the order kick, snare, toms, hihat, cymbals).

        Raises:
            TranscriptionCancelledError: Propagated from ``progress``.
        """
        pieces = [piece for piece in _PIECES if piece in inputs]
        hits: list[DrumHit] = []
        for done, piece in enumerate(pieces, start=1):
            hits.extend(piece_hits(piece, inputs[piece], window))
            progress(done / len(pieces))
        return tuple(sorted(hits, key=lambda h: (h.time, _PIECES.index(h.piece))))


def piece_hits(piece: DrumPiece, path: Path, window: RenderRange | None) -> list[DrumHit]:
    """Return the gated hits of one detail stem."""
    samples = _load_mono(path, window)
    if samples is None or float(np.max(np.abs(samples), initial=0.0)) < SILENCE_PEAK:
        return []
    times = _onset_times(samples, _DELTAS.get(piece, _DEFAULT_DELTA))
    peaks = _onset_peaks(samples, times)
    return gate_hits(piece, times, peaks)


def gate_hits(piece: DrumPiece, times: FloatArray, peaks: FloatArray) -> list[DrumHit]:
    """Apply the silence floor and the bleed gate; strengths relative to the 99th percentile."""
    loud = peaks >= SILENCE_PEAK
    if not np.any(loud):
        return []
    reference = float(np.percentile(peaks[loud], GATE_PERCENTILE))
    kept = loud & (peaks >= BLEED_GATE * reference)
    return [
        DrumHit(time=float(t), piece=piece, strength=min(1.0, float(p) / reference))
        for t, p in zip(times[kept], peaks[kept], strict=True)
    ]


def _onset_times(samples: FloatArray, delta: float) -> FloatArray:
    """Return the onset times (s) of ``samples``."""
    envelope = librosa.onset.onset_strength(y=samples, sr=ONSET_SAMPLE_RATE, hop_length=HOP_LENGTH)
    frames = librosa.onset.onset_detect(
        onset_envelope=envelope, sr=ONSET_SAMPLE_RATE, hop_length=HOP_LENGTH, delta=delta
    )
    times = librosa.frames_to_time(frames, sr=ONSET_SAMPLE_RATE, hop_length=HOP_LENGTH)
    return np.asarray(times, dtype=np.float64)


def _onset_peaks(samples: FloatArray, times: FloatArray) -> FloatArray:
    """Return the absolute peak around every onset (linear loudness for the gate)."""
    magnitude = np.abs(samples)
    starts = np.clip(((times - PEAK_BEFORE_SECONDS) * ONSET_SAMPLE_RATE).astype(int), 0, None)
    stops = ((times + PEAK_AFTER_SECONDS) * ONSET_SAMPLE_RATE).astype(int) + 1
    peaks = [float(np.max(magnitude[a:b], initial=0.0)) for a, b in zip(starts, stops, strict=True)]
    return np.asarray(peaks, dtype=np.float64)


def _load_mono(path: Path, window: RenderRange | None) -> FloatArray | None:
    """Load ``path`` (or its window) mono at 22,050 Hz; None when it cannot be decoded."""
    offset = window.start_seconds if window is not None else 0.0
    duration = window.end_seconds - window.start_seconds if window is not None else None
    try:
        samples, _ = librosa.load(
            path, sr=ONSET_SAMPLE_RATE, mono=True, offset=offset, duration=duration
        )
    except (OSError, RuntimeError, ValueError, EOFError) as error:
        _LOGGER.warning("Drum stem skipped, audio not decodable: %s", type(error).__name__)
        return None
    return np.asarray(samples, dtype=np.float64)

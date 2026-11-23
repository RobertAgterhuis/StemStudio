"""Artefact estimate: A2 dropouts relative to the mix (data-model 4.4, R-03, Q2 decision).

A dropout (gating hole) is a run of 23 ms blocks that lies ``DROPOUT_DEPTH_DB`` below the median
of its +-0.5 s neighbourhood, lasts at most 0.3 s, starts with an abrupt fall and ends with an
abrupt recovery (``DROPOUT_EDGE_DB`` within two blocks). The abrupt edges judge a stem against
its own envelope: natural decays of drums, plucks or piano notes fall slowly and are never holes,
for every family alike. The share of hole blocks among the active frames is divided by the
mix's share (floor ``MIX_FLOOR``; holes the mix has too are the music) and mapped to 0..1 with
``x / (x + K)``.

The A1 flicker ratio is not part of the index: it did not respond to the V1 musical-noise
injection and failed the one-hour redesign (orchestrator Q2, 2026-11-23).
"""

import numpy as np
import numpy.typing as npt

from stemstudio_worker.insights.representation import Spectrogram
from stemstudio_worker.insights.thresholds import (
    ARTEFACT_K,
    DROPOUT_DEPTH_DB,
    DROPOUT_EDGE_DB,
    DROPOUT_MAX_FRAMES,
    DROPOUT_NEIGHBOURHOOD_FRAMES,
    MIX_FLOOR,
)

__all__ = ["ARTEFACT_K", "artefact_index", "dropout_share", "normalise"]

Float64 = npt.NDArray[np.float64]
Int = npt.NDArray[np.intp]


def _runs(mask: npt.NDArray[np.bool_]) -> tuple[Int, Int]:
    """Start and end (exclusive) of every run of True values."""
    edges = np.diff(np.concatenate([[0], mask.astype(np.int8), [0]]))
    return np.flatnonzero(edges == 1), np.flatnonzero(edges == -1)


def _running_median(level: Float64) -> Float64:
    half = DROPOUT_NEIGHBOURHOOD_FRAMES // 2
    windows = np.lib.stride_tricks.sliding_window_view(
        np.pad(level, half, mode="edge"), 2 * half + 1
    )
    return np.asarray(np.median(windows, axis=1))


def _hole_blocks(level: Float64) -> int:
    median = _running_median(level)
    starts, ends = _runs(level < median - DROPOUT_DEPTH_DB)
    padded = np.pad(level, 2, mode="edge")  # padded[k + 2] == level[k]
    before = np.maximum(padded[starts + 1], padded[starts])  # level[start - 1], level[start - 2]
    after = np.maximum(padded[ends + 2], padded[ends + 3])  # level[end], level[end + 1]
    fall = before - level[starts] >= DROPOUT_EDGE_DB
    rise = after - level[ends - 1] >= DROPOUT_EDGE_DB
    inside = (starts > 0) & (ends < len(level))
    holes = fall & rise & inside & (ends - starts <= DROPOUT_MAX_FRAMES)
    return int(np.sum((ends - starts)[holes]))


def dropout_share(spec: Spectrogram) -> float:
    """Hole blocks per active frame (0 without active frames)."""
    active = int(np.count_nonzero(spec.active()))
    if active == 0:
        return 0.0
    return _hole_blocks(spec.block_db.astype(np.float64)) / active


def normalise(ratio: float) -> float:
    """Map a stem/mix ratio to 0..1 with ``x / (x + K)``."""
    return ratio / (ratio + ARTEFACT_K)


def artefact_index(stem: Spectrogram, mix: Spectrogram) -> float:
    """Artefact index of a stem: its dropout share relative to the mix's, normalised."""
    return normalise(dropout_share(stem) / max(dropout_share(mix), MIX_FLOOR))

"""Synthetic chord frames and grids for the harmony tests (016, beat-analysis)."""

from collections.abc import Sequence

import numpy as np

from stemstudio_worker.harmony import labels
from stemstudio_worker.ports import CHORD_HOP, CHORD_SAMPLE_RATE, ChordFrames
from stemstudio_worker.schemas import BeatGridTempo, ChordEngine

FRAME_S = CHORD_HOP / CHORD_SAMPLE_RATE
SIMPLE = labels.simple_labels()


def frames_of(
    timeline: Sequence[tuple[str, float]],
    *,
    engine: ChordEngine = "btc",
    weight: float = 0.9,
    fallback: bool = False,
) -> ChordFrames:
    """Frames whose argmax follows ``timeline`` ((label, seconds) pairs) with ``weight`` mass."""
    names = [label for label, seconds in timeline for _ in range(round(seconds / FRAME_S))]
    posteriors = np.full((len(names), len(SIMPLE)), (1 - weight) / (len(SIMPLE) - 1), np.float32)
    for row, name in enumerate(names):
        posteriors[row, SIMPLE.index(name)] = weight
    chroma = np.full((len(names), 12), 1 / 12, np.float32)
    return ChordFrames(posteriors, SIMPLE, FRAME_S, chroma, engine, fallback=fallback)


def grid_of(
    bpm: float, seconds: float, first: float = 0.5, beats_per_bar: int = 4
) -> BeatGridTempo:
    """A constant-tempo beat grid from ``first`` to ``seconds``."""
    period = 60.0 / bpm
    beats = [round(first + k * period, 6) for k in range(int((seconds - first) / period) + 1)]
    return BeatGridTempo(
        bpm=bpm, beats_per_bar=beats_per_bar, beats=beats, downbeats=beats[::beats_per_bar]
    )

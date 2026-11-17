"""Boundary rules of the ``stems-novelty`` analyser (data-model 2.2 steps 5, 6 and 9).

A boundary is the frame index where a segment starts (never 0). Novelty peaks and vocal switches
are joined (within one frame the novelty boundary wins), segments shorter than 4 frames (first and
last: 2) merge into the more similar neighbour, and while there are more than ``maxSections``
segments the shortest one merges (``merged-to-limit``).
"""

import itertools
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

import numpy as np
import numpy.typing as npt

from stemstudio_worker.analysis.structure.features import FrameGrid

MIN_FRAMES: Final = 4
EDGE_MIN_FRAMES: Final = 2
VOCAL_HOLD: Final = 2
VOCAL_STRENGTH: Final = 1.0

Vector = npt.NDArray[np.float64]
Span = tuple[int, int]


@dataclass(frozen=True, slots=True)
class Boundary:
    """Start frame of a segment and its strength (normalised novelty, 1.0 for a vocal switch)."""

    index: int
    strength: float


def vocal_switches(active: npt.NDArray[np.bool_], hold: int = VOCAL_HOLD) -> list[int]:
    """Frames where the vocal state changes and the new state holds for ``hold`` frames."""
    switches, stable = [], bool(active[0]) if len(active) else False
    for i in range(1, len(active) - hold + 1):
        window = active[i : i + hold]
        if bool(active[i]) != stable and bool(np.all(window == active[i])):
            switches.append(i)
            stable = bool(active[i])
    return switches


def union(novel: Sequence[tuple[int, float]], vocal: Sequence[int]) -> list[Boundary]:
    """Novelty boundaries plus the vocal switches more than one frame away from them."""
    near = _within_one({i for i, _ in novel})
    kept = [Boundary(i, s) for i, s in novel]
    kept += [Boundary(i, VOCAL_STRENGTH) for i in vocal if i not in near]
    return sorted(kept, key=lambda b: b.index)


def _within_one(frames: set[int]) -> set[int]:
    return frames | {i - 1 for i in frames} | {i + 1 for i in frames}


def segment_spans(bounds: Sequence[Boundary], frames: int) -> list[Span]:
    """``(start, end)`` frame spans of the segments between ``bounds`` over ``frames`` frames."""
    edges = [0, *(b.index for b in bounds), frames]
    return list(itertools.pairwise(edges))


def merge_short(bounds: Sequence[Boundary], features: Vector) -> list[Boundary]:
    """Merge too short segments into their more similar neighbour, shortest first."""
    kept = list(bounds)
    while (index := _too_short(kept, features.shape[1])) is not None:
        kept.pop(_boundary_to_drop(kept, index, features))
    return kept


def limit_sections(
    bounds: Sequence[Boundary], features: Vector, max_sections: int
) -> tuple[list[Boundary], bool]:
    """Merge the shortest segments until at most ``max_sections`` remain; True when merged."""
    kept = list(bounds)
    while len(kept) + 1 > max_sections:
        spans = segment_spans(kept, features.shape[1])
        shortest = min(range(len(spans)), key=lambda k: spans[k][1] - spans[k][0])
        kept.pop(_boundary_to_drop(kept, shortest, features))
    return kept, len(kept) < len(bounds)


def spans_in_seconds(spans: Sequence[Span], frames: FrameGrid) -> list[tuple[float, float]]:
    """Segment spans as seconds: first starts at 0, last ends at the duration, inner snapped."""
    digits = 1 if frames.snap == "time" else 3
    starts = [0.0] + [round(frames.bounds[start], digits) for start, _ in spans[1:]]
    ends = [*starts[1:], frames.bounds[-1]]
    return list(zip(starts, ends, strict=True))


def _too_short(bounds: Sequence[Boundary], frames: int) -> int | None:
    """The shortest segment below its minimum length, or None (never with one segment)."""
    spans = segment_spans(bounds, frames)
    if len(spans) < 2:  # noqa: PLR2004 - one segment cannot merge
        return None
    short = [k for k, (a, b) in enumerate(spans) if b - a < _minimum(k, len(spans))]
    return min(short, key=lambda k: spans[k][1] - spans[k][0], default=None)


def _minimum(position: int, count: int) -> int:
    return EDGE_MIN_FRAMES if position in (0, count - 1) else MIN_FRAMES


def _boundary_to_drop(bounds: Sequence[Boundary], segment: int, features: Vector) -> int:
    """Index in ``bounds`` of the boundary joining ``segment`` to its more similar neighbour."""
    spans = segment_spans(bounds, features.shape[1])
    if segment == 0:
        return 0
    if segment == len(spans) - 1:
        return segment - 1
    mean = _mean(features, spans[segment])
    before = _cosine(mean, _mean(features, spans[segment - 1]))
    after = _cosine(mean, _mean(features, spans[segment + 1]))
    return segment - 1 if before >= after else segment


def _mean(features: Vector, span: Span) -> Vector:
    return np.asarray(features[:, span[0] : span[1]].mean(axis=1))


def _cosine(a: Vector, b: Vector) -> float:
    norm = float(np.linalg.norm(a) * np.linalg.norm(b))
    return float(a @ b) / norm if norm > 0 else 0.0

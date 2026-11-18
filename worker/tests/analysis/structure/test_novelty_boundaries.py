"""Tests of the novelty curve and the boundary rules (014 T022, data-model 2.2 steps 4-6, 9)."""

import itertools

import numpy as np
import pytest

from stemstudio_worker.analysis.structure.boundaries import (
    Boundary,
    limit_sections,
    merge_short,
    segment_spans,
    spans_in_seconds,
    union,
    vocal_switches,
)
from stemstudio_worker.analysis.structure.features import FrameGrid
from stemstudio_worker.analysis.structure.novelty import KERNEL_HALF, checkerboard, novelty, peaks


def _blocks(*sizes: int, dims: int = 8) -> np.ndarray:
    """Feature columns: one orthogonal unit vector per block."""
    columns = [np.eye(dims)[k] for k, size in enumerate(sizes) for _ in range(size)]
    return np.array(columns).T


def test_the_kernel_is_a_tapered_checkerboard() -> None:
    kernel = checkerboard(KERNEL_HALF)

    assert kernel.shape == (12, 12)
    assert kernel[0, 0] > 0 and kernel[11, 11] > 0
    assert kernel[0, 11] < 0 and kernel[11, 0] < 0
    assert abs(kernel[5, 5]) > abs(kernel[0, 0])
    assert kernel.sum() == pytest.approx(0, abs=1e-9)


def test_novelty_peaks_at_the_block_changes() -> None:
    features = _blocks(8, 12, 10)

    curve = novelty(features.T @ features)

    assert curve.max() == pytest.approx(1.0)
    assert curve.min() >= 0
    assert peaks(curve) == [8, 20]


def test_peaks_are_at_least_four_frames_apart() -> None:
    curve = np.zeros(30)
    curve[[5, 7, 9, 20]] = [0.9, 1.0, 0.8, 0.7]

    found = peaks(curve)

    assert found == [7, 20]
    assert all(b - a >= 4 for a, b in itertools.pairwise(found))


def test_a_flat_curve_has_no_peaks() -> None:
    assert peaks(np.zeros(20)) == []


def test_vocal_switches_must_hold_two_frames() -> None:
    active = np.array([0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1], dtype=bool)

    assert vocal_switches(active) == [2, 7]


def test_union_dedupes_within_one_frame_and_novelty_wins() -> None:
    merged = union([(8, 0.6), (20, 0.9)], [9, 14, 21, 30])

    assert merged == [Boundary(8, 0.6), Boundary(14, 1.0), Boundary(20, 0.9), Boundary(30, 1.0)]


def test_short_middle_segments_merge_into_the_more_similar_neighbour() -> None:
    # Frames 8..10 (3 frames) look like the block after them: the boundary at 11 goes.
    features = _blocks(8, 3, 12)
    features[:, 8:11] = np.eye(8)[2][:, None] * 0.9 + np.eye(8)[0][:, None] * 0.1
    features /= np.linalg.norm(features, axis=0)
    bounds = [Boundary(8, 0.5), Boundary(11, 0.9)]

    kept = merge_short(bounds, features)

    assert kept == [Boundary(8, 0.5)]


def test_first_and_last_segments_may_be_two_frames() -> None:
    features = _blocks(2, 16, 2)
    bounds = [Boundary(2, 1.0), Boundary(18, 1.0)]

    assert merge_short(bounds, features) == bounds
    assert merge_short([Boundary(1, 1.0), Boundary(18, 1.0)], features) == [Boundary(18, 1.0)]


def test_a_single_short_song_keeps_one_segment() -> None:
    assert merge_short([Boundary(1, 1.0)], _blocks(1, 2)) == []


def test_the_limit_merges_the_shortest_segments_first() -> None:
    features = _blocks(4, 4, 8, 4, 8)
    bounds = [Boundary(4, 1.0), Boundary(8, 1.0), Boundary(16, 1.0), Boundary(20, 1.0)]

    kept, merged = limit_sections(bounds, features, 3)

    assert len(kept) == 2
    assert merged is True
    assert limit_sections(bounds, features, 64) == (bounds, False)


def test_spans_cover_the_frames() -> None:
    assert segment_spans([Boundary(4, 1.0), Boundary(9, 1.0)], 12) == [(0, 4), (4, 9), (9, 12)]


@pytest.mark.parametrize(
    ("bounds", "snap", "expected"),
    [
        ((0.0, 0.48, 2.48, 4.48, 6.5), "downbeat", [(0.0, 2.48), (2.48, 6.5)]),
        ((0.0, 0.31, 1.63, 2.96, 4.5), "beat", [(0.0, 1.63), (1.63, 4.5)]),
        ((0.0, 2.0, 4.0, 6.04, 7.33), "time", [(0.0, 4.0), (4.0, 7.33)]),
    ],
)
def test_spans_in_seconds_snap_and_cover_the_song(
    bounds: tuple[float, ...], snap: str, expected: list[tuple[float, float]]
) -> None:
    frames = FrameGrid(bounds=bounds, snap=snap)  # type: ignore[arg-type]

    spans = spans_in_seconds([(0, 2), (2, 4)], frames)

    assert spans == expected


def test_time_snap_rounds_inner_boundaries_to_a_tenth() -> None:
    frames = FrameGrid(bounds=(0.0, 2.04, 4.04, 7.33), snap="time")

    assert spans_in_seconds([(0, 1), (1, 3)], frames) == [(0.0, 2.0), (2.0, 7.33)]

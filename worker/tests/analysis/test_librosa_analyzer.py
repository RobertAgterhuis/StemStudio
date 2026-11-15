"""Accuracy tests of ``LibrosaBeatAnalyzer`` on synthetic tracks with known tempo (T052, FR-007)."""

import numpy as np
import pytest

from stemstudio_worker.analysis.librosa_analyzer import LibrosaBeatAnalyzer
from stemstudio_worker.ports import BeatAnalyzer
from stemstudio_worker.schemas import BeatGrid

from .conftest import ClickTrack, MakeClickTrack

BPM_TOLERANCE = 1.0
BEAT_TOLERANCE = 0.030
MIN_RECALL = 0.9


def _distances(found: list[float], truth: list[float]) -> np.ndarray:
    """Distance of every ``found`` time to its nearest ``truth`` time."""
    diff = np.abs(np.subtract.outer(np.asarray(found), np.asarray(truth)))
    return np.asarray(diff.min(axis=1))


def _assert_beats_match(grid: BeatGrid, track: ClickTrack) -> None:
    assert grid.beats, "no beats detected"
    assert _distances(grid.beats, track.beats).max() <= BEAT_TOLERANCE
    recall = np.mean(_distances(track.beats, grid.beats) <= BEAT_TOLERANCE)
    assert recall >= MIN_RECALL


@pytest.fixture(scope="module")
def analyzer() -> LibrosaBeatAnalyzer:
    return LibrosaBeatAnalyzer()


def test_implements_the_beat_analyzer_port(analyzer: LibrosaBeatAnalyzer) -> None:
    port: BeatAnalyzer = analyzer
    assert callable(port.analyze)


@pytest.mark.parametrize("bpm", [60.0, 90.0, 120.0, 150.0, 180.0])
def test_click_track_tempo_and_beats(
    analyzer: LibrosaBeatAnalyzer, make_click_track: MakeClickTrack, bpm: float
) -> None:
    track = make_click_track(bpm)

    grid = analyzer.analyze(track.path)

    assert grid.bpm == pytest.approx(bpm, abs=BPM_TOLERANCE)
    _assert_beats_match(grid, track)
    assert grid.reliable is True


@pytest.mark.parametrize("bpm", [97.0, 133.0])
def test_non_round_tempo_is_estimated_within_one_bpm(
    analyzer: LibrosaBeatAnalyzer, make_click_track: MakeClickTrack, bpm: float
) -> None:
    grid = analyzer.analyze(make_click_track(bpm).path)

    assert grid.bpm == pytest.approx(bpm, abs=BPM_TOLERANCE)


@pytest.mark.parametrize(("bpm", "beats_per_bar"), [(120.0, 4), (90.0, 3), (150.0, 3), (100.0, 4)])
def test_meter_and_downbeats_follow_the_accents(
    analyzer: LibrosaBeatAnalyzer,
    make_click_track: MakeClickTrack,
    bpm: float,
    beats_per_bar: int,
) -> None:
    track = make_click_track(bpm, beats_per_bar)

    grid = analyzer.analyze(track.path)

    assert grid.beats_per_bar == beats_per_bar
    assert grid.downbeats
    assert _distances(grid.downbeats, track.downbeats).max() <= BEAT_TOLERANCE


def test_grid_invariants(analyzer: LibrosaBeatAnalyzer, make_click_track: MakeClickTrack) -> None:
    grid = analyzer.analyze(make_click_track(120.0).path)

    assert all(b > a for a, b in zip(grid.beats, grid.beats[1:], strict=False))
    assert set(grid.downbeats) <= set(grid.beats)
    assert all(t >= 0 for t in grid.beats)
    assert 0.0 <= grid.confidence <= 1.0


def test_unaccented_clicks_default_to_four_beats_per_bar(
    analyzer: LibrosaBeatAnalyzer, make_click_track: MakeClickTrack
) -> None:
    track = make_click_track(110.0, beats_per_bar=1)

    grid = analyzer.analyze(track.path)

    assert grid.beats_per_bar == 4
    assert grid.downbeats == grid.beats[::4]


def test_music_like_mix(analyzer: LibrosaBeatAnalyzer, make_music_track: MakeClickTrack) -> None:
    track = make_music_track(100.0)

    grid = analyzer.analyze(track.path)

    assert grid.bpm == pytest.approx(100.0, abs=BPM_TOLERANCE)
    _assert_beats_match(grid, track)
    assert grid.beats_per_bar == 4
    assert grid.reliable is True

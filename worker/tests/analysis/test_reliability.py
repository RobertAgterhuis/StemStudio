"""No-clear-pulse edge cases: the analyzer never raises and flags the grid unreliable (T054)."""

from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.analysis.librosa_analyzer import LibrosaBeatAnalyzer
from stemstudio_worker.analysis.reliability import is_reliable, unreliable_grid
from stemstudio_worker.schemas import BeatGrid

from .conftest import SR, MakeClickTrack, MakeWav, white_noise


@pytest.fixture(scope="module")
def analyzer() -> LibrosaBeatAnalyzer:
    return LibrosaBeatAnalyzer()


def _assert_unreliable(grid: BeatGrid) -> None:
    assert grid.reliable is False
    assert grid.bpm == 0.0 or grid.confidence < 0.2
    assert set(grid.downbeats) <= set(grid.beats)
    assert 1 <= grid.beats_per_bar <= 12


def test_silence_returns_the_empty_unreliable_grid(
    analyzer: LibrosaBeatAnalyzer, make_wav: MakeWav
) -> None:
    grid = analyzer.analyze(make_wav("silence.wav", np.zeros(5 * SR)))

    assert grid == unreliable_grid()


def test_white_noise_is_unreliable(analyzer: LibrosaBeatAnalyzer, make_wav: MakeWav) -> None:
    _assert_unreliable(analyzer.analyze(make_wav("noise.wav", white_noise(10.0))))


def test_clip_shorter_than_a_few_beats_is_unreliable(
    analyzer: LibrosaBeatAnalyzer, make_click_track: MakeClickTrack
) -> None:
    track = make_click_track(120.0, seconds=1.4)

    _assert_unreliable(analyzer.analyze(track.path))


@pytest.mark.parametrize("seconds", [0.0, 0.01])
def test_empty_or_tiny_audio_does_not_raise(
    analyzer: LibrosaBeatAnalyzer, make_wav: MakeWav, seconds: float
) -> None:
    grid = analyzer.analyze(make_wav("tiny.wav", np.full(int(seconds * SR), 0.1)))

    assert grid == unreliable_grid()


def test_single_click_is_unreliable(analyzer: LibrosaBeatAnalyzer, make_wav: MakeWav) -> None:
    mono = np.zeros(4 * SR)
    mono[SR : SR + 200] = 0.9

    _assert_unreliable(analyzer.analyze(make_wav("one_click.wav", mono)))


def test_undecodable_file_returns_unreliable_grid(
    analyzer: LibrosaBeatAnalyzer, tmp_path: Path
) -> None:
    corrupt = tmp_path / "corrupt.wav"
    corrupt.write_bytes(b"RIFF\x00\x00not really audio")

    assert analyzer.analyze(corrupt) == unreliable_grid()


def test_unreliable_grid_matches_the_contract() -> None:
    grid = unreliable_grid()

    assert grid.model_dump(by_alias=True) == {
        "bpm": 0.0,
        "beats": [],
        "downbeats": [],
        "beatsPerBar": 4,
        "confidence": 0.0,
        "reliable": False,
    }


@pytest.mark.parametrize(
    ("confidence", "beat_count", "expected"),
    [(0.9, 8, True), (0.9, 3, False), (0.05, 40, False), (0.2, 4, True)],
)
def test_is_reliable(confidence: float, beat_count: int, expected: bool) -> None:
    assert is_reliable(confidence, beat_count) is expected

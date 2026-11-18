"""Tests of the frame features of the section analyser (014 T020, data-model 2.2 steps 1-3)."""

from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.analysis.structure.features import (
    HOP,
    SR,
    FrameGrid,
    frame_features,
    frame_grid,
    load_stems,
)
from stemstudio_worker.ports import StructureError
from stemstudio_worker.schemas import BeatGrid

from .songs import GUITAR_SOLO, make_song, tone


def _grid(lead_in: float, duration: float, *, reliable: bool) -> BeatGrid:
    beats = [round(lead_in + i * 0.5, 3) for i in range(int((duration - lead_in) / 0.5))]
    return BeatGrid(bpm=120, beats=beats, downbeats=beats[::4], beats_per_bar=4,
                    confidence=0.8, reliable=reliable)  # fmt: skip


def test_a_reliable_grid_gives_bars_and_a_lead_in_frame() -> None:
    frames = frame_grid(_grid(0.5, 9.0, reliable=True), 9.0)

    assert frames.bounds == (0.0, 0.5, 2.5, 4.5, 6.5, 8.5, 9.0)
    assert (frames.snap, frames.count) == ("downbeat", 6)


def test_a_reliable_grid_starting_at_zero_has_no_lead_in() -> None:
    assert frame_grid(_grid(0.0, 6.0, reliable=True), 6.0).bounds == (0.0, 2.0, 4.0, 6.0)


def test_an_unreliable_grid_groups_beats_by_the_bar_length() -> None:
    grid = BeatGrid(bpm=100, beats=[0.3, 0.9, 1.5, 2.1, 2.7, 3.3, 3.9], downbeats=[1.5],
                    beats_per_bar=3, confidence=0.2, reliable=False)  # fmt: skip

    frames = frame_grid(grid, 4.5)

    assert frames.bounds == (0.0, 0.3, 2.1, 3.9, 4.5)
    assert frames.snap == "beat"


@pytest.mark.parametrize(
    "grid",
    [None, BeatGrid(bpm=0, beats=[], downbeats=[], confidence=0, reliable=False)],
)
def test_without_beats_frames_are_two_seconds(grid: BeatGrid | None) -> None:
    frames = frame_grid(grid, 7.0)

    assert frames.bounds == (0.0, 2.0, 4.0, 6.0, 7.0)
    assert frames.snap == "time"


def test_grid_times_at_or_past_the_end_are_dropped() -> None:
    grid = BeatGrid(bpm=120, beats=[0, 0.5, 1, 1.5, 2, 2.5, 3.99], downbeats=[0, 2, 3.99, 6],
                    confidence=0.9, reliable=True)  # fmt: skip

    assert frame_grid(grid, 4.0).bounds == (0.0, 2.0, 4.0)


def _audio(seconds: float, vocals_on: tuple[int, ...]) -> dict[str, np.ndarray]:
    """Two-second frames; vocals sound only in the frames ``vocals_on``."""
    frames = round(seconds / 2)
    vocals = np.concatenate([tone(330, 2.0) if k in vocals_on else np.zeros(2 * SR)
                             for k in range(frames)])  # fmt: skip
    rest = tone(110, seconds)
    return {"vocals": vocals, "drums": 0.5 * rest, "bass": rest, "other": 0.3 * rest}


def test_shapes_per_frame_and_shares_sum_to_one() -> None:
    frames = FrameGrid(bounds=(0.0, 2.0, 4.0, 6.0, 8.0), snap="time")

    features = frame_features(_audio(8.0, (1, 2)), frames)

    assert features.matrix.shape[1] == 4
    assert features.matrix.shape[0] == 2 * (12 + 20 + 2 + 4)
    assert np.allclose(np.linalg.norm(features.matrix, axis=0), 1.0)
    assert features.mix_rms.shape == (4,)
    assert set(features.shares) == {"vocals", "drums", "bass", "other"}
    assert np.allclose(sum(features.shares.values()), 1.0)


def test_vocal_activity_follows_the_vocal_stem_exactly() -> None:
    frames = FrameGrid(bounds=(0.0, 2.0, 4.0, 6.0, 8.0, 10.0), snap="time")

    features = frame_features(_audio(10.0, (1, 2, 4)), frames)

    assert features.vocal_ratio.tolist() == pytest.approx([0, 1, 1, 0, 1], abs=0.05)
    assert features.vocals_p95 > 0.01 * features.mix_p95


def test_the_six_stem_song_has_six_shares_and_keeps_detail_energy() -> None:
    song = make_song(GUITAR_SOLO[:2])
    frames = frame_grid(song.grid(), song.duration)

    features = frame_features(song.stems, frames)

    assert set(features.shares) == {"vocals", "drums", "bass", "guitar", "piano", "other"}
    assert "other.woodwinds" in features.energy
    assert features.matrix.shape == (2 * (12 + 20 + 2 + 6), 12)
    assert features.vocal_ratio[:4].max() < 0.25 < features.vocal_ratio[4:].min()


def test_silent_frames_get_equal_shares() -> None:
    silent = {name: np.zeros(4 * SR) for name in ("vocals", "drums", "bass", "other")}

    features = frame_features(silent, FrameGrid(bounds=(0.0, 2.0, 4.0), snap="time"))

    assert features.shares["bass"].tolist() == [0.25, 0.25]
    assert features.vocal_ratio.tolist() == [0, 0]
    assert np.all(np.isfinite(features.matrix))


def test_load_stems_resamples_to_mono_and_fits_the_duration(tmp_path: Path) -> None:
    stereo = np.column_stack([np.ones(44_100 * 3), np.zeros(44_100 * 3)]) * 0.5
    sf.write(tmp_path / "vocals.flac", stereo, 44_100, subtype="PCM_24")
    sf.write(tmp_path / "bass.flac", np.full(SR, 0.2), SR, subtype="PCM_16")

    audio = load_stems({"vocals": tmp_path / "vocals.flac", "bass": tmp_path / "bass.flac"}, 2.0)

    assert {name: len(signal) for name, signal in audio.items()} == {
        "vocals": 2 * SR,
        "bass": 2 * SR,
    }
    assert audio["vocals"][SR] == pytest.approx(0.25, abs=0.01)
    assert audio["bass"][-1] == 0.0


def test_load_stems_fails_readably_on_a_broken_file(tmp_path: Path) -> None:
    (tmp_path / "vocals.flac").write_bytes(b"not audio")

    with pytest.raises(StructureError, match="could not be read"):
        load_stems({"vocals": tmp_path / "vocals.flac"}, 2.0)


def test_hop_and_rate_are_the_documented_ones() -> None:
    assert (SR, HOP) == (22_050, 512)

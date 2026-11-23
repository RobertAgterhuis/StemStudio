"""Chord post-processing: beat snapping, merges, slash bass, warnings (016 T015, data-model 2.3)."""

import itertools

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.harmony.postprocess import BeatSnapPostProcessor
from stemstudio_worker.ports import CHORD_SAMPLE_RATE, ChordFrames, PostProcessOptions
from stemstudio_worker.schemas import ChordChart

from .conftest import frames_of, grid_of

PROGRESSION = [("C:maj", 2.03), ("G:maj", 1.97), ("A:min", 2.1), ("F:maj", 1.9)]


def _run(frames: ChordFrames, options: PostProcessOptions) -> ChordChart:
    return BeatSnapPostProcessor().segments(frames, options)


def _codes(chart: ChordChart) -> list[str]:
    return [w.code for w in chart.warnings]


def _assert_covers(chart: ChordChart, frames: ChordFrames) -> None:
    assert chart.segments[0].start_ms == 0
    assert chart.segments[-1].end_ms == round(
        frames.posteriors.shape[0] * frames.frame_seconds * 1000
    )
    for earlier, later in itertools.pairwise(chart.segments):
        assert earlier.end_ms == later.start_ms


def test_changes_snap_to_beats_of_a_reliable_grid() -> None:
    frames = frames_of([("N", 0.5), *PROGRESSION])
    grid = grid_of(120, 9.0)
    chart = _run(frames, PostProcessOptions(grid=grid, vocabulary="simple"))
    beat_ms = {round(b * 1000) for b in grid.beats}
    assert [s.label for s in chart.segments] == ["N", "C:maj", "G:maj", "A:min", "F:maj"]
    assert all(s.start_ms in beat_ms for s in chart.segments[1:])
    assert chart.beat_snapped
    assert "grid-unreliable" not in _codes(chart)
    _assert_covers(chart, frames)


def test_snapped_chords_last_at_least_one_beat() -> None:
    frames = frames_of(
        [("C:maj", 2.0), ("D:maj", 0.15), ("C:maj", 1.0), ("E:min", 0.3), ("G:maj", 2)]
    )
    chart = _run(frames, PostProcessOptions(grid=grid_of(120, 6.0), vocabulary="simple"))
    inner = chart.segments[1:-1]
    assert all(s.end_ms - s.start_ms >= 500 for s in inner)
    assert "D:maj" not in [s.label for s in chart.segments]


def test_beat_votes_sum_posteriors_per_beat() -> None:
    frames = frames_of([("C:maj", 0.4), ("G:maj", 0.2), ("C:maj", 0.4)])
    chart = _run(frames, PostProcessOptions(grid=grid_of(120, 1.0, first=0.0), vocabulary="simple"))
    assert [s.label for s in chart.segments] == ["C:maj"]


def test_without_a_grid_segments_shorter_than_half_a_second_merge() -> None:
    frames = frames_of(
        [("C:maj", 2.0), ("D:maj", 0.3), ("C:maj", 1.0), ("E:min", 0.7), ("G:maj", 2)]
    )
    chart = _run(frames, PostProcessOptions(grid=None, vocabulary="simple"))
    assert [s.label for s in chart.segments] == ["C:maj", "E:min", "G:maj"]
    assert all(s.end_ms - s.start_ms >= 500 for s in chart.segments)
    assert not chart.beat_snapped
    assert "grid-unreliable" in _codes(chart)
    _assert_covers(chart, frames)


def test_short_segment_merges_into_the_more_confident_neighbour() -> None:
    weak = frames_of([("C:maj", 2.0)], weight=0.5)
    blip = frames_of([("D:maj", 0.2)])
    strong = frames_of([("G:maj", 2.0)], weight=0.95)
    posteriors = np.concatenate([weak.posteriors, blip.posteriors, strong.posteriors])
    chroma = np.concatenate([weak.chroma, blip.chroma, strong.chroma])
    frames = ChordFrames(posteriors, weak.labels, weak.frame_seconds, chroma, "btc")
    chart = _run(frames, PostProcessOptions(grid=None, vocabulary="simple"))
    assert [s.label for s in chart.segments] == ["C:maj", "G:maj"]
    assert chart.segments[0].end_ms == round(22 * frames.frame_seconds * 1000)


def test_consecutive_equal_labels_merge() -> None:
    frames = frames_of([("C:maj", 1.0), ("C:maj", 1.0), ("G:maj", 1.0)])
    chart = _run(frames, PostProcessOptions(grid=None, vocabulary="simple"))
    assert [s.label for s in chart.segments] == ["C:maj", "G:maj"]


def test_segments_carry_root_quality_and_mean_confidence() -> None:
    frames = frames_of([("N", 1.0), ("A:min", 2.0)], weight=0.8)
    chart = _run(frames, PostProcessOptions(grid=None, vocabulary="simple"))
    none, minor = chart.segments
    assert (none.root, none.quality, none.bass) == (None, None, None)
    assert (minor.root, minor.quality, minor.bass) == (9, "min", None)
    assert minor.confidence == pytest.approx(0.8, abs=1e-6)
    assert not chart.prefer_flats
    assert chart.key is None
    assert chart.key_regions == []


def _bass_tone(hz: float, seconds: float) -> npt.NDArray[np.float32]:
    t = np.arange(round(seconds * CHORD_SAMPLE_RATE)) / CHORD_SAMPLE_RATE
    return (0.5 * np.sin(2 * np.pi * hz * t)).astype(np.float32)


@pytest.mark.parametrize(
    ("bass_hz", "label", "bass"),
    [(82.41, "C:maj/3", 4), (98.0, "C:maj/5", 7), (65.41, "C:maj", None), (73.42, "C:maj", None)],
)
def test_full_vocabulary_adds_the_slash_bass(bass_hz: float, label: str, bass: int | None) -> None:
    frames = frames_of([("C:maj", 4.0)])
    options = PostProcessOptions(grid=None, vocabulary="full", bass=_bass_tone(bass_hz, 4.0))
    (segment,) = _run(frames, options).segments
    assert (segment.label, segment.bass) == (label, bass)


def test_simple_vocabulary_ignores_the_bass_stem() -> None:
    frames = frames_of([("C:maj", 4.0)])
    options = PostProcessOptions(grid=None, vocabulary="simple", bass=_bass_tone(82.41, 4.0))
    assert _run(frames, options).segments[0].label == "C:maj"


def test_silent_bass_adds_no_inversion() -> None:
    frames = frames_of([("N", 1.0), ("C:maj", 3.0)])
    silent = np.zeros(4 * CHORD_SAMPLE_RATE, np.float32)
    options = PostProcessOptions(grid=None, vocabulary="full", bass=silent)
    assert [s.label for s in _run(frames, options).segments] == ["N", "C:maj"]


def test_all_no_chord_frames_give_no_chords() -> None:
    options = PostProcessOptions(grid=grid_of(120, 3.0), vocabulary="simple")
    chart = _run(frames_of([("N", 3.0)]), options)
    assert chart.no_chords
    assert [s.label for s in chart.segments] == ["N"]
    assert "few-chords" not in _codes(chart)


def test_no_frames_at_all_give_an_empty_no_chords_chart() -> None:
    chart = _run(frames_of([]), PostProcessOptions(grid=None, vocabulary="simple"))
    assert chart.no_chords
    assert chart.segments == []


def test_one_chord_only_warns_few_chords() -> None:
    chart = _run(frames_of([("C:maj", 6.0)]), PostProcessOptions(grid=None, vocabulary="simple"))
    assert _codes(chart) == ["grid-unreliable", "few-chords"]
    assert chart.warnings[1].text == "Very few chords were detected."


def test_fallback_frames_warn_fallback_engine() -> None:
    frames = frames_of(PROGRESSION, engine="chroma-hmm", fallback=True)
    chart = _run(frames, PostProcessOptions(grid=grid_of(120, 8.0), vocabulary="simple"))
    assert chart.engine == "chroma-hmm"
    assert _codes(chart) == ["fallback-engine"]


def test_full_request_on_chroma_hmm_is_reduced_to_simple() -> None:
    frames = frames_of(PROGRESSION, engine="chroma-hmm")
    chart = _run(frames, PostProcessOptions(grid=grid_of(120, 8.0), vocabulary="full"))
    assert chart.vocabulary == "simple"
    assert _codes(chart) == ["vocabulary-reduced"]


def test_full_request_on_btc_stays_full() -> None:
    options = PostProcessOptions(grid=grid_of(120, 8.0), vocabulary="full")
    chart = _run(frames_of(PROGRESSION), options)
    assert chart.vocabulary == "full"
    assert chart.engine == "btc"


def _alternating_per_beat(grid_beats: list[float]) -> ChordFrames:
    base = frames_of([("C:maj", grid_beats[-1])])
    fs = base.frame_seconds
    posteriors = base.posteriors.copy()
    for k, (a, b) in enumerate(itertools.pairwise(grid_beats)):
        rows = slice(round(a / fs), round(b / fs))
        posteriors[rows] = base.posteriors[0][::-1] if k % 2 else base.posteriors[0]
    return ChordFrames(posteriors, base.labels, fs, base.chroma, "btc")


def test_segment_count_is_capped_at_4000() -> None:
    grid = grid_of(300, 850.0, first=0.0)
    frames = _alternating_per_beat(grid.beats)
    chart = _run(frames, PostProcessOptions(grid=grid, vocabulary="simple"))
    assert len(chart.segments) == 4000
    _assert_covers(chart, frames)


def test_a_beat_inside_the_first_frame_is_dropped() -> None:
    frames = frames_of([("C:maj", 2.0)])
    chart = _run(
        frames, PostProcessOptions(grid=grid_of(120, 2.0, first=0.04), vocabulary="simple")
    )
    assert [(s.start_ms, s.label) for s in chart.segments] == [(0, "C:maj")]

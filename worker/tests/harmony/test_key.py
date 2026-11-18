"""Key HMM over chroma and chord evidence (016 T019, research R-13, data-model 2.8, SC-010)."""

import itertools
from collections.abc import Sequence

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.harmony import labels
from stemstudio_worker.harmony.key import HmmKeyEstimator, apply_key
from stemstudio_worker.ports import KeyEstimate
from stemstudio_worker.schemas import (
    AnalysisKey,
    BeatGridTempo,
    ChordChart,
    ChordQuality,
    ChordSegment,
    KeyMode,
    KeyRegion,
)

from .conftest import FRAME_S, grid_of

BAR_S = 2.0
LEAD_S = 0.5
MAJOR_CADENCE: tuple[tuple[int, ChordQuality], ...] = (
    (0, "maj"), (5, "maj"), (7, "maj"), (0, "maj"), (9, "min"), (2, "min"), (7, "7"), (0, "maj"),
)  # fmt: skip
MINOR_CADENCE: tuple[tuple[int, ChordQuality], ...] = (
    (0, "min"), (5, "min"), (7, "maj"), (0, "min"), (8, "maj"), (5, "min"), (7, "7"), (0, "min"),
)  # fmt: skip


def cadence(tonic: int, mode: KeyMode, bars: int) -> list[tuple[int, ChordQuality]]:
    """``bars`` one-bar chords of the I-IV-V-I (or i-iv-V-i) cadence of the key."""
    base = MAJOR_CADENCE if mode == "major" else MINOR_CADENCE
    return [((tonic + step) % 12, q) for (step, q), _ in zip(itertools.cycle(base), range(bars))]


def song(
    chords: Sequence[tuple[int, ChordQuality]], seed: int = 7
) -> tuple[npt.NDArray[np.float32], list[ChordSegment]]:
    """Chroma frames (chord tones + noise, root louder) and segments; N before the first beat."""
    rng = np.random.default_rng(seed)
    per_bar = BAR_S / FRAME_S
    total = round((LEAD_S + BAR_S * len(chords)) / FRAME_S)
    chroma = rng.uniform(0.0, 0.15, (total, 12)).astype(np.float32)
    chroma[: round(LEAD_S / FRAME_S)] = 0.0
    segments = [ChordSegment(start_ms=0, end_ms=500, label="N", confidence=0.9)]
    for bar, (root, quality) in enumerate(chords):
        first = round((LEAD_S + bar * BAR_S) / FRAME_S)
        rows = slice(first, round(first + per_bar))
        chroma[rows, list(labels.chord_tones(root, quality))] += 1.0
        chroma[rows, root] += 0.5
        start = round((LEAD_S + bar * BAR_S) * 1000)
        segments.append(
            ChordSegment(
                start_ms=start,
                end_ms=start + 2000,
                label=labels.format_label(root, quality),
                root=root,
                quality=quality,
                confidence=0.9,
            )
        )
    return chroma, segments


def _grid(bars: int) -> BeatGridTempo:
    return grid_of(120, LEAD_S + BAR_S * bars, first=LEAD_S)


def _estimate(chords: Sequence[tuple[int, ChordQuality]], *, grid: bool = True) -> KeyEstimate:
    chroma, segments = song(chords)
    return HmmKeyEstimator().estimate(chroma, segments, _grid(len(chords)) if grid else None)


def _related(found: AnalysisKey, tonic: int, mode: KeyMode) -> bool:
    relative = (tonic + 9) % 12 if mode == "major" else (tonic + 3) % 12
    fifths = {(tonic + 7) % 12, (tonic + 5) % 12}
    return (
        (found.tonic, found.mode) == (tonic, mode)
        or (found.mode != mode and found.tonic == relative)
        or (found.mode == mode and found.tonic in fifths)
    )


KEYS: list[tuple[int, KeyMode]] = [(t, m) for t in range(12) for m in ("major", "minor")]


def test_cadences_in_all_24_keys_find_the_global_key() -> None:
    results = [(_estimate(cadence(t, m, 16)).key, t, m) for t, m in KEYS]
    assert all(key is not None for key, _, _ in results)
    exact = sum((key.tonic, key.mode) == (t, m) for key, t, m in results if key is not None)
    assert exact / len(KEYS) >= 0.9
    assert all(_related(key, t, m) for key, t, m in results if key is not None)


def test_modulation_c_to_d_is_reported_within_two_bars() -> None:
    estimate = _estimate(cadence(0, "major", 12) + cadence(2, "major", 12))
    assert [(r.tonic, r.mode) for r in estimate.regions] == [(0, "major"), (2, "major")]
    change_ms = round((LEAD_S + 12 * BAR_S) * 1000)
    assert abs(estimate.regions[1].start_ms - change_ms) <= 2 * BAR_S * 1000


def test_regions_cover_the_non_n_part_contiguously() -> None:
    estimate = _estimate(cadence(0, "major", 12) + cadence(2, "major", 12))
    assert estimate.regions[0].start_ms == 500
    assert estimate.regions[-1].end_ms == round((LEAD_S + 24 * BAR_S) * 1000)
    for earlier, later in itertools.pairwise(estimate.regions):
        assert earlier.end_ms == later.start_ms
    assert all(0 <= r.confidence <= 1 for r in estimate.regions)


def test_regions_shorter_than_eight_bars_are_merged() -> None:
    estimate = _estimate(cadence(0, "major", 12) + cadence(2, "major", 4) + cadence(0, "major", 12))
    assert [(r.tonic, r.mode) for r in estimate.regions] == [(0, "major")]


def test_global_key_is_the_longest_region_key() -> None:
    estimate = _estimate(cadence(0, "major", 16) + cadence(2, "major", 8))
    assert estimate.key is not None
    assert (estimate.key.tonic, estimate.key.mode) == (0, "major")


def test_clear_cadence_is_confident_without_an_alternative() -> None:
    key = _estimate(cadence(7, "major", 16)).key
    assert key is not None
    assert key.confidence >= 0.5
    assert key.alternative is None


def test_relative_keys_that_fit_almost_equally_name_the_alternative() -> None:
    chords: list[tuple[int, ChordQuality]] = [(0, "maj"), (9, "min")] * 8
    key = _estimate(chords).key
    assert key is not None
    assert (
        {(key.tonic, key.mode), (key.alternative.tonic, key.alternative.mode)}
        == {
            (0, "major"),
            (9, "minor"),
        }
        if key.alternative
        else (key.tonic, key.mode) in {(0, "major"), (9, "minor")}
    )


def test_noise_without_chords_is_uncertain() -> None:
    rng = np.random.default_rng(3)
    chroma = rng.uniform(0, 1, (400, 12)).astype(np.float32)
    segments = [
        ChordSegment(
            start_ms=0, end_ms=37_000, label="C:maj", root=0, quality="maj", confidence=0.2
        )
    ]
    key = HmmKeyEstimator().estimate(chroma, segments, None).key
    assert key is not None
    assert key.confidence < 0.5


def test_only_no_chord_segments_give_no_key() -> None:
    chroma, _ = song(cadence(0, "major", 4))
    segments = [ChordSegment(start_ms=0, end_ms=8500, label="N", confidence=0.9)]
    assert HmmKeyEstimator().estimate(chroma, segments, _grid(4)) == KeyEstimate(None, ())


def test_no_segments_give_no_key() -> None:
    assert HmmKeyEstimator().estimate(np.zeros((0, 12), np.float32), [], None) == KeyEstimate(
        None, ()
    )


def test_without_a_grid_one_second_frames_and_16_second_regions_are_used() -> None:
    estimate = _estimate(cadence(0, "major", 12) + cadence(2, "major", 12), grid=False)
    assert [(r.tonic, r.mode) for r in estimate.regions] == [(0, "major"), (2, "major")]
    assert all(r.end_ms - r.start_ms >= 16_000 for r in estimate.regions)


def test_short_song_is_one_region() -> None:
    estimate = _estimate(cadence(4, "minor", 4))
    assert estimate.key is not None
    assert len(estimate.regions) == 1
    assert (estimate.regions[0].tonic, estimate.regions[0].mode) == (4, "minor")


def _chart() -> ChordChart:
    return ChordChart(
        vocabulary="simple",
        engine="btc",
        segments=[
            ChordSegment(
                start_ms=0, end_ms=1000, label="F:maj", root=5, quality="maj", confidence=0.9
            )
        ],
    )


def test_apply_key_sets_key_regions_and_flats() -> None:
    region = KeyRegion(start_ms=0, end_ms=1000, tonic=5, mode="major", confidence=0.8)
    key = AnalysisKey(tonic=5, mode="major", confidence=0.8)
    chart = apply_key(_chart(), KeyEstimate(key, (region,)))
    assert chart.key == key
    assert chart.key_regions == [region]
    assert chart.prefer_flats
    assert chart.warnings == []


def test_apply_key_warns_when_the_key_is_uncertain() -> None:
    key = AnalysisKey(tonic=9, mode="minor", confidence=0.312)
    chart = apply_key(_chart(), KeyEstimate(key, ()))
    assert not chart.prefer_flats
    assert [(w.code, w.text) for w in chart.warnings] == [
        ("key-uncertain", "The key is uncertain (confidence 31 %).")
    ]


def test_apply_key_without_a_key_clears_it() -> None:
    chart = apply_key(_chart(), KeyEstimate(None, ()))
    assert (chart.key, chart.key_regions, chart.prefer_flats) == (None, [], False)


@pytest.mark.parametrize("p_stay", [0.9, 0.999])
def test_estimator_parameters_are_configurable(p_stay: float) -> None:
    chroma, segments = song(cadence(0, "major", 8))
    key = (
        HmmKeyEstimator(beta=3.0, p_stay=p_stay, smooth_beats=4)
        .estimate(chroma, segments, _grid(8))
        .key
    )
    assert key is not None
    assert (key.tonic, key.mode) == (0, "major")

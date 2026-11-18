"""Tests of the segment clusters and the labelling rules (014 T024, data-model 2.2 step 7, 2.3)."""

from collections.abc import Mapping

import numpy as np
import pytest

from stemstudio_worker.analysis.structure.clusters import cluster_letters, margins
from stemstudio_worker.analysis.structure.labels import (
    SegmentFacts,
    SongFacts,
    label_segments,
)

Seg = SegmentFacts
_EVEN = {"bass": 0.25, "guitar": 0.25, "piano": 0.25, "other": 0.25}
_SONG = SongFacts(median_shares=_EVEN, no_vocals=False)


def _seg(cluster: str, vocal: float, rms: float = 0.5, **parts: Mapping[str, float]) -> Seg:
    """A segment; ``parts`` may hold ``shares`` (default even) and ``details`` (default none)."""
    return SegmentFacts(cluster=cluster, vocal_ratio=vocal, mix_rms=rms,
                        shares=parts.get("shares", _EVEN),
                        detail_shares=parts.get("details", {}))  # fmt: skip


# ------------------------------------------------------------------ clusters


def test_repeated_segments_get_the_same_letter_in_order_of_first_appearance() -> None:
    a, b, c = np.eye(6)[0], np.eye(6)[1], np.eye(6)[2]
    noise = np.random.default_rng(1).normal(0, 0.02, (7, 6))
    means = np.array([c, a, b, a, b, c, b]) + noise

    assert cluster_letters(means) == ["A", "B", "C", "B", "C", "A", "C"]


def test_one_segment_is_cluster_a() -> None:
    assert cluster_letters(np.ones((1, 4))) == ["A"]
    assert margins(np.ones((1, 4)), ["A"]) == [0.5]


def test_more_than_26_clusters_reuse_z() -> None:
    letters = cluster_letters(np.eye(30))

    assert letters[:3] == ["A", "B", "C"]
    assert letters[-1] == "Z"


def test_margins_are_the_distance_to_the_nearest_other_cluster() -> None:
    means = np.array([[1.0, 0.0], [1.0, 0.0], [0.0, 1.0]])

    assert margins(means, ["A", "A", "B"]) == pytest.approx([1.0, 1.0, 1.0])
    assert margins(np.array([[1.0, 0.0], [1.0, 0.1]]), ["A", "B"])[0] < 0.01


# ------------------------------------------------------------------ labels


def _labels(segments: list[SegmentFacts], song: SongFacts = _SONG) -> list[tuple[str, str]]:
    result = label_segments(segments, song)
    return list(zip(result.kinds, result.labels, strict=True))


def test_a_pop_form_gets_intro_verse_chorus_bridge_outro() -> None:
    segments = [_seg("A", 0.0), _seg("B", 0.9, 0.4), _seg("C", 0.9, 0.8), _seg("B", 0.8, 0.4),
                _seg("C", 0.9, 0.8), _seg("D", 0.9, 0.5), _seg("C", 0.9, 0.8),
                _seg("E", 0.1)]  # fmt: skip

    assert _labels(segments) == [
        ("intro", "Intro"), ("verse", "Verse 1"), ("chorus", "Chorus 1"), ("verse", "Verse 2"),
        ("chorus", "Chorus 2"), ("bridge", "Bridge"), ("chorus", "Chorus 3"), ("outro", "Outro"),
    ]  # fmt: skip


def test_chorus_ties_go_to_the_earlier_cluster() -> None:
    segments = [_seg("A", 0.9, 0.5), _seg("B", 0.9, 0.5), _seg("A", 0.9, 0.5), _seg("B", 0.9, 0.5)]

    assert [k for k, _ in _labels(segments)] == ["chorus", "verse", "chorus", "verse"]


def test_a_single_vocal_cluster_before_the_first_chorus_is_a_verse() -> None:
    segments = [_seg("A", 0.9), _seg("B", 0.9, 0.9), _seg("C", 0.9), _seg("B", 0.9, 0.9)]

    assert [k for k, _ in _labels(segments)] == ["verse", "chorus", "bridge", "chorus"]
    assert _labels([_seg("D", 0.9), *segments])[:2] == [("verse", "Verse 1"), ("verse", "Verse 2")]


def test_in_between_vocal_ratios_default_to_verse() -> None:
    segments = [_seg("A", 0.4), _seg("B", 0.9, 0.9), _seg("A", 0.4), _seg("B", 0.9, 0.9)]

    assert [k for k, _ in _labels(segments)] == ["verse", "chorus", "verse", "chorus"]


def test_a_solo_needs_40_percent_and_1_3_times_the_median_share() -> None:
    guitar = {"bass": 0.2, "guitar": 0.5, "piano": 0.2, "other": 0.1}
    weak = {"bass": 0.3, "guitar": 0.36, "piano": 0.24, "other": 0.1}
    segments = [_seg("A", 0.0), _seg("B", 0.9), _seg("C", 0.1, shares=guitar), _seg("B", 0.9),
                _seg("D", 0.1, shares=weak), _seg("B", 0.9), _seg("E", 0.0)]  # fmt: skip

    result = label_segments(segments, _SONG)

    assert result.kinds[2:5] == ["solo", "chorus", "instrumental"]
    assert (result.labels[2], result.instruments[2]) == ("Guitar solo", "guitar")
    high_median = SongFacts(median_shares={**_EVEN, "guitar": 0.45}, no_vocals=False)
    assert label_segments(segments, high_median).kinds[2] == "instrumental"


def test_a_kept_detail_stem_names_the_solo() -> None:
    other = {"bass": 0.1, "guitar": 0.1, "piano": 0.1, "other": 0.7}
    segments = [_seg("A", 0.0), _seg("B", 0.9),
                _seg("C", 0.0, shares=other, details={"other.woodwinds": 0.8, "other.brass": 0.1}),
                _seg("B", 0.9), _seg("C", 0.0, shares=other, details={"other.brass": 0.4}),
                _seg("D", 0.0)]  # fmt: skip

    result = label_segments(segments, _SONG)

    assert result.instruments[2] == "other.woodwinds"
    assert result.labels[2] == "Woodwinds solo 1"
    assert (result.instruments[4], result.labels[4]) == ("other", "Other solo 2")


def test_numbered_solos_keep_their_instrument_names() -> None:
    bass = {"bass": 0.7, "guitar": 0.1, "piano": 0.1, "other": 0.1}
    piano = {"bass": 0.1, "guitar": 0.1, "piano": 0.7, "other": 0.1}
    segments = [_seg("A", 0.9), _seg("B", 0.0, shares=bass), _seg("A", 0.9),
                _seg("C", 0.0, shares=piano), _seg("A", 0.9)]  # fmt: skip

    assert label_segments(segments, _SONG).labels == [
        "Chorus 1", "Bass solo 1", "Chorus 2", "Piano solo 2", "Chorus 3"
    ]  # fmt: skip


def test_few_repetitions_names_parts_by_cluster() -> None:
    segments = [_seg("A", 0.0), _seg("B", 0.9), _seg("C", 0.9), _seg("D", 0.1)]

    result = label_segments(segments, _SONG)

    assert list(zip(result.kinds, result.labels, strict=True)) == [
        ("intro", "Intro"), ("other", "Part B"), ("other", "Part C"), ("outro", "Outro")
    ]  # fmt: skip
    assert result.warnings == ["few-repetitions"]


def test_no_vocals_never_makes_a_vocal_segment() -> None:
    song = SongFacts(median_shares=_EVEN, no_vocals=True)
    segments = [_seg("A", 0.9), _seg("B", 0.9), _seg("A", 0.9), _seg("B", 0.9)]

    result = label_segments(segments, song)

    assert result.kinds == ["intro", "instrumental", "instrumental", "outro"]
    assert result.warnings == ["no-vocals"]


def test_one_segment_is_the_song() -> None:
    result = label_segments([_seg("A", 0.9)], _SONG)

    assert (result.kinds, result.labels, result.warnings) == (["other"], ["Song"], [])


def test_without_solo_sources_a_low_vocal_middle_is_instrumental() -> None:
    segments = [_seg("A", 0.9), _seg("B", 0.0, shares={}), _seg("A", 0.9)]

    assert label_segments(segments, _SONG).kinds == ["chorus", "instrumental", "chorus"]

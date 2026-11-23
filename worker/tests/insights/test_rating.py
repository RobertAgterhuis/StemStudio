"""Levels, rating and reason codes (T034, data-model 4.5/4.6, FR-003)."""

from dataclasses import replace

import pytest

from stemstudio_worker.insights import rating
from stemstudio_worker.insights.thresholds import ARTEFACT_LEVELS, BLEED_LEVELS, Levels

CLEAN = rating.RatingInputs(
    active_seconds=120.0,
    active_share=0.7,
    level_db=-8.0,
    bleed_index=0.001,
    bleed_level="low",
    artefact_level="low",
    co_occurrence=False,
)


def test_levels_split_at_the_thresholds() -> None:
    levels = Levels(0.1, 0.3)
    assert [levels.level(x) for x in (0.0, 0.0999, 0.1, 0.2999, 0.3, 1.0)] == [
        "low", "low", "medium", "medium", "high", "high",
    ]  # fmt: skip


def test_bleed_level_uses_the_calibrated_thresholds() -> None:
    assert (
        rating.bleed_level(BLEED_LEVELS.low_below / 2, co_occurrence=False, status="not-measured")
        == "low"
    )
    assert (
        rating.bleed_level(BLEED_LEVELS.high_from, co_occurrence=False, status="not-measured")
        == "high"
    )
    assert rating.artefact_level(ARTEFACT_LEVELS.high_from, status="not-measured") == "high"
    assert rating.artefact_level(0.0, status="not-measured") == "low"


def test_co_occurrence_caps_bleed_at_medium() -> None:
    assert rating.bleed_level(0.9, co_occurrence=True, status="not-measured") == "medium"
    assert (
        rating.bleed_level(BLEED_LEVELS.low_below / 2, co_occurrence=True, status="not-measured")
        == "low"
    )


@pytest.mark.parametrize("status", ["not-measured", "passed"])
def test_measured_or_passed_dimensions_show_every_level(status: str) -> None:
    assert (
        rating.bleed_level(BLEED_LEVELS.low_below / 2, co_occurrence=False, status=status) == "low"
    )
    assert rating.artefact_level(ARTEFACT_LEVELS.low_below, status=status) == "medium"


def test_a_failed_dimension_shows_only_high() -> None:
    assert (
        rating.bleed_level(BLEED_LEVELS.low_below / 2, co_occurrence=False, status="failed")
        == "unknown"
    )
    assert (
        rating.bleed_level(BLEED_LEVELS.low_below, co_occurrence=False, status="failed")
        == "unknown"
    )
    assert rating.bleed_level(0.9, co_occurrence=False, status="failed") == "high"
    assert rating.artefact_level(0.1, status="failed") == "unknown"
    assert rating.artefact_level(0.99, status="failed") == "high"


def test_clean_stem_is_good() -> None:
    assert rating.rate(CLEAN) == ("good", "clean")


@pytest.mark.parametrize(
    ("bleed", "artefacts", "expected"),
    [
        ("low", "low", ("good", "clean")),
        ("medium", "low", ("fair", "bleed")),
        ("high", "low", ("poor", "bleed")),
        ("low", "medium", ("fair", "artefacts")),
        ("low", "high", ("poor", "artefacts")),
        ("medium", "high", ("poor", "bleed-and-artefacts")),
        ("high", "medium", ("poor", "bleed-and-artefacts")),
        ("medium", "medium", ("fair", "bleed-and-artefacts")),
        ("unknown", "low", ("good", "clean")),
        ("unknown", "high", ("poor", "artefacts")),
        ("medium", "unknown", ("fair", "bleed")),
        ("unknown", "unknown", ("unknown", "clean")),
    ],
)
def test_rating_is_the_worse_dimension(
    bleed: str, artefacts: str, expected: tuple[str, str]
) -> None:
    inputs = replace(CLEAN, bleed_level=bleed, artefact_level=artefacts)
    assert rating.rate(inputs) == expected


def test_co_occurrence_reason_is_bleed_together() -> None:
    inputs = replace(CLEAN, bleed_level="medium", co_occurrence=True)
    assert rating.rate(inputs) == ("fair", "bleed-together")
    both = replace(inputs, artefact_level="medium")
    assert rating.rate(both) == ("fair", "bleed-and-artefacts")


def test_too_short_comes_first() -> None:
    short = replace(
        CLEAN, active_seconds=9.99, active_share=0.01, bleed_index=0.9, bleed_level="high"
    )
    assert rating.rate(short) == ("unknown", "too-short")
    assert rating.rate(replace(CLEAN, active_seconds=10.0)) == ("good", "clean")


@pytest.mark.parametrize(
    ("share", "level_db", "index", "absent"),
    [
        (0.04, -10.0, 0.7, True),  # little activity
        (0.5, -34.0, 0.5, True),  # very quiet
        (0.08, -30.0, 0.9, False),  # at the B3 limits: present
        (0.04, -34.0, 0.49, False),  # quiet but not mostly spill
    ],
)
def test_not_present_needs_b3_and_much_bleed(
    share: float, level_db: float, index: float, absent: bool
) -> None:
    inputs = replace(
        CLEAN, active_share=share, level_db=level_db, bleed_index=index, bleed_level="high"
    )
    result = rating.rate(inputs)
    assert (result == ("not-present", "not-present")) is absent
    if not absent:
        assert result == ("poor", "bleed")

"""G1 reliability table (T038, FR-004, data-model 4.5, research R-04, Phase 0 measurements)."""

import pytest

from stemstudio_worker.insights import thresholds
from stemstudio_worker.insights.thresholds import (
    G1_RESULTS,
    G1_STEM_TYPES,
    Dimension,
    g1_status,
    reliability,
    shown_level,
)
from stemstudio_worker.schemas import STANDARD_STEMS, QualityInputKind, QualityLevel

DIMENSIONS: tuple[Dimension, ...] = ("bleed", "artefacts")


def test_phase_0_table_has_every_v2_stem_type_not_measured() -> None:
    assert set(G1_STEM_TYPES) == {"vocals", "drums", "bass", "other"}
    assert set(G1_RESULTS) == {(stem, dim) for stem in G1_STEM_TYPES for dim in DIMENSIONS}
    assert set(G1_RESULTS.values()) == {"not-measured"}


@pytest.mark.parametrize("stem", STANDARD_STEMS)
@pytest.mark.parametrize("dimension", DIMENSIONS)
def test_every_label_starts_indicative(stem: str, dimension: Dimension) -> None:
    assert reliability(stem, "standard", dimension) == "indicative"
    assert g1_status(stem, "standard", dimension) == "not-measured"


@pytest.mark.parametrize("level", ["low", "medium", "high"])
def test_not_measured_and_passed_dimensions_keep_every_level(level: QualityLevel) -> None:
    assert shown_level(level, "not-measured") == level
    assert shown_level(level, "passed") == level


def test_a_failed_dimension_shows_only_high() -> None:
    assert shown_level("low", "failed") == "unknown"
    assert shown_level("medium", "failed") == "unknown"
    assert shown_level("high", "failed") == "high"
    assert shown_level("unknown", "failed") == "unknown"


@pytest.fixture
def all_passed(monkeypatch: pytest.MonkeyPatch) -> None:
    passed = dict.fromkeys(G1_RESULTS, "passed")
    monkeypatch.setattr(thresholds, "G1_RESULTS", passed)


@pytest.mark.usefixtures("all_passed")
@pytest.mark.parametrize("stem", G1_STEM_TYPES)
def test_a_passed_v2_stem_type_is_validated(stem: str) -> None:
    assert reliability(stem, "standard", "bleed") == "validated"


@pytest.mark.usefixtures("all_passed")
@pytest.mark.parametrize(
    ("channel", "kind"),
    [("guitar", "standard"), ("piano", "standard"), ("drums.kick", "detail"),
     ("vocals.lead", "detail"), ("drums.rest", "rest"), ("drums", "detail")],
)  # fmt: skip
def test_guitar_piano_detail_and_rest_stems_are_always_indicative(
    channel: str, kind: QualityInputKind
) -> None:
    for dimension in DIMENSIONS:
        assert reliability(channel, kind, dimension) == "indicative"
        assert g1_status(channel, kind, dimension) == "not-measured"


def test_a_failed_v2_dimension_is_reported(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setitem(thresholds.G1_RESULTS, ("bass", "bleed"), "failed")
    assert g1_status("bass", "standard", "bleed") == "failed"
    assert g1_status("bass", "standard", "artefacts") == "not-measured"
    assert reliability("bass", "standard", "bleed") == "indicative"

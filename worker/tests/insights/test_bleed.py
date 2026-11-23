"""Bleed estimate (T030): leakage share B2 with the ratio-floor guard (data-model 4.2/4.3, Q1)."""

import math
from typing import Any

import numpy as np
import pytest

from stemstudio_worker.insights import bleed
from stemstudio_worker.insights.representation import BANDS, Spectrogram, spectrogram_of
from stemstudio_worker.schemas import StemName

from .synthetic import FAMILIES, SR, arranged_song, inject_bleed

ALPHAS = (0.0, 0.03, 0.1, 0.3)


def _spec(power: np.ndarray) -> Spectrogram:
    level = (10 * np.log10(power.mean(axis=1) + 1e-12)).astype(np.float32)
    return Spectrogram(power.astype(np.float32), level, level)


@pytest.fixture(scope="module")
def stems() -> dict[str, np.ndarray]:
    return arranged_song(seconds=20.0)


@pytest.fixture(scope="module")
def specs(stems: dict[str, np.ndarray]) -> dict[str, Spectrogram]:
    return {name: spectrogram_of(sound, SR) for name, sound in stems.items()}


def _source_of(target: str) -> str:
    """Every family is a target once and a source once (rotation)."""
    return FAMILIES[(FAMILIES.index(target) + 1) % len(FAMILIES)]


@pytest.mark.parametrize("target", FAMILIES)
def test_share_grows_with_the_injected_energy(
    target: str, stems: dict[str, Any], specs: dict[str, Any]
) -> None:
    source = _source_of(target)
    shares = [
        bleed.fit_pair(spectrogram_of(inject_bleed(stems[target], stems[source], a), SR),
                       specs[source]).share
        for a in ALPHAS
    ]  # fmt: skip
    assert shares == sorted(shares)
    assert shares[1] < shares[2] < shares[3]
    # The share is an energy share: linear in alpha squared (0.3 vs 0.1 is a factor of 9).
    growth = (shares[3] - shares[0]) / (shares[2] - shares[0])
    assert 4.0 < growth < 14.0


@pytest.mark.parametrize("target", FAMILIES)
def test_main_source_is_the_injected_stem(
    target: str, stems: dict[str, Any], specs: dict[str, Any]
) -> None:
    source = _source_of(target)
    bled = spectrogram_of(inject_bleed(stems[target], stems[source], 0.3), SR)
    sources = [bleed.BleedSource(name, specs[name]) for name in FAMILIES if name != target]
    measure = bleed.measure_bleed(target, bled, sources)
    assert measure.main_source == source
    assert measure.index == pytest.approx(bleed.fit_pair(bled, specs[source]).share)


def test_a_constant_ratio_copy_is_measured_fully() -> None:
    rng = np.random.default_rng(1)
    source = rng.gamma(0.5, 1e-4, size=(800, BANDS))
    fit = bleed.fit_pair(_spec(0.01 * source), _spec(source))
    assert fit.share == pytest.approx(1.0, abs=0.02)
    assert fit.guard_ratio == pytest.approx(1.0, abs=0.05)


def test_the_ratio_floor_rejects_instruments_that_play_together() -> None:
    rng = np.random.default_rng(2)
    source = rng.gamma(0.5, 1e-4, size=(800, BANDS))
    together = source * rng.uniform(0.0, 2.0, size=source.shape)  # loud when the source is loud
    fit = bleed.fit_pair(_spec(together), _spec(source))
    assert fit.full_share > 0.5  # the plain regression would call it bleed
    assert fit.share < 0.15  # the 5th percentile of U(0, 2) is 0.1
    assert fit.guard_ratio > bleed.CO_OCCURRENCE_RATIO


def test_silent_source_or_target_gives_no_bleed() -> None:
    sound = np.random.default_rng(3).gamma(0.5, 1e-4, size=(300, BANDS))
    silence = np.zeros_like(sound)
    assert bleed.fit_pair(_spec(sound), _spec(silence)).share == 0.0
    assert bleed.fit_pair(_spec(silence), _spec(sound)).share == 0.0
    measure = bleed.measure_bleed(
        "vocals", _spec(silence), [bleed.BleedSource("drums", _spec(sound))]
    )
    assert measure.index == 0.0
    assert measure.main_source is None
    assert not measure.co_occurrence


def test_no_sources_gives_no_bleed() -> None:
    sound = np.random.default_rng(3).gamma(0.5, 1e-4, size=(300, BANDS))
    measure = bleed.measure_bleed("bass", _spec(sound), [])
    assert (measure.index, measure.main_source) == (0.0, None)


def test_frame_counts_may_differ() -> None:
    rng = np.random.default_rng(4)
    source = rng.gamma(0.5, 1e-4, size=(500, BANDS))
    fit = bleed.fit_pair(_spec(0.01 * source[:400]), _spec(source))
    assert fit.share == pytest.approx(1.0, abs=0.02)


def test_guard_ratio_edge_cases() -> None:
    assert bleed.PairFit(0.0, 0.0).guard_ratio == 1.0
    assert math.isinf(bleed.PairFit(0.0, 0.2).guard_ratio)
    assert bleed.PairFit(0.1, 0.3).guard_ratio == pytest.approx(3.0)


def _together(seed: int) -> tuple[Spectrogram, Spectrogram]:
    rng = np.random.default_rng(seed)
    source = rng.gamma(0.5, 1e-4, size=(800, BANDS))
    target = 0.002 * source + source * rng.uniform(0.0, 0.5, size=source.shape)
    return _spec(target), _spec(source)


@pytest.mark.parametrize(
    ("family", "source"),
    [("drums", "bass"), ("bass", "drums"), ("guitar", "other"), ("other", "guitar"),
     ("piano", "other"), ("other", "piano"), ("guitar", "piano"), ("piano", "guitar")],
)  # fmt: skip
def test_co_occurrence_is_flagged_for_the_four_pairs(family: str, source: StemName) -> None:
    target, spec = _together(5)
    measure = bleed.measure_bleed(family, target, [bleed.BleedSource(source, spec)])
    assert measure.main_source == source
    assert measure.co_occurrence


@pytest.mark.parametrize(
    ("family", "source"), [("vocals", "drums"), ("bass", "guitar"), ("drums", "other")]
)
def test_co_occurrence_is_never_flagged_for_other_pairs(family: str, source: StemName) -> None:
    target, spec = _together(5)
    measure = bleed.measure_bleed(family, target, [bleed.BleedSource(source, spec)])
    assert not measure.co_occurrence


def test_a_copy_inside_a_pair_is_not_co_occurrence() -> None:
    rng = np.random.default_rng(6)
    source = rng.gamma(0.5, 1e-4, size=(800, BANDS))
    measure = bleed.measure_bleed(
        "bass", _spec(0.05 * source), [bleed.BleedSource("drums", _spec(source))]
    )
    assert not measure.co_occurrence
    assert measure.index > 0.9


def test_sources_of_a_standard_stem_are_the_other_five(specs: dict[str, Any]) -> None:
    sources = bleed.bleed_sources("guitar", specs, {})
    assert [source.family for source in sources] == ["vocals", "drums", "bass", "piano", "other"]


def test_sources_of_a_detail_stem_are_the_other_standard_stems_and_its_siblings(
    specs: dict[str, Any],
) -> None:
    details = {"drums.kick": specs["drums"], "drums.snare": specs["drums"],
               "drums.rest": specs["drums"], "vocals.lead": specs["vocals"]}  # fmt: skip
    sources = bleed.bleed_sources("drums.kick", specs, details)
    families = [source.family for source in sources]
    # The parent itself is excluded (the detail stem is part of it); siblings count as the parent.
    assert families == ["vocals", "bass", "guitar", "piano", "other", "drums", "drums"]
    assert bleed.family_of("drums.kick") == "drums"
    assert bleed.family_of("bass") == "bass"


def test_sources_of_a_rest_stem_include_the_kept_siblings(specs: dict[str, Any]) -> None:
    details = {"guitar.acoustic-guitar": specs["guitar"], "guitar.rest": specs["guitar"]}
    sources = bleed.bleed_sources("guitar.rest", specs, details)
    assert [source.family for source in sources][-1] == "guitar"
    assert len(sources) == 6


def test_a_quiet_copy_does_not_make_the_original_look_bled() -> None:
    """Spill of i in j (a phantom stem) must not read as bleed of j into i (MAX_GAIN, Q3)."""
    rng = np.random.default_rng(7)
    original = rng.gamma(0.5, 1e-1, size=(800, BANDS))  # spill stays above the activity floor
    spill = 0.0036 * original
    assert bleed.fit_pair(_spec(spill), _spec(original)).share == pytest.approx(1.0, abs=0.02)
    assert bleed.fit_pair(_spec(original), _spec(spill)).share == pytest.approx(0.0036, rel=0.05)

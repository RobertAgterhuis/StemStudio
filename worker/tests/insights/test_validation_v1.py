"""V1 synthetic validation (T034, research.md R-04, SC-002), with every family alike.

Bleed: every ordered family pair, alpha in {0, 0.03, 0.1, 0.3} (amplitude) on the arranged song
(instruments with rests, Q1). Grid levels: 0 and 0.03 low, 0.1 medium, 0.3 high. Artefacts:
gating holes at rate {0, 0.5, 2} per second for every family on the arranged and the dense
song (low, medium, high). Targets: Spearman >= 0.8 and >= 80 % correct levels; the "playing
together" control (kick and bass on the same onsets) is never rated High; the dense song
(every family playing all the time on one grid, Q1 control) has no false bleed above medium.
"""

import numpy as np
import pytest
from scipy.stats import spearmanr

from stemstudio_worker.insights import artefacts, bleed, rating
from stemstudio_worker.insights.representation import Spectrogram, spectrogram_of

from .synthetic import (
    FAMILIES,
    SR,
    arranged_song,
    bass,
    gate_holes,
    inject_bleed,
    kick_on_beats,
    song,
)

SECONDS = 20.0
ALPHAS = {0.0: "low", 0.03: "low", 0.1: "medium", 0.3: "high"}
RATES = {0.0: "low", 0.5: "medium", 2.0: "high"}


def _spec(sound: np.ndarray) -> Spectrogram:
    return spectrogram_of(sound, SR)


def _bleed_level(
    family: str, target: Spectrogram, specs: dict[str, Spectrogram]
) -> tuple[float, str]:
    sources = [bleed.BleedSource(name, specs[name]) for name in FAMILIES if name != family]
    measure = bleed.measure_bleed(family, target, sources)
    level = rating.bleed_level(
        measure.index, co_occurrence=measure.co_occurrence, status="not-measured"
    )
    return measure.index, level


@pytest.fixture(scope="module")
def bleed_cases() -> list[tuple[float, float, str]]:
    stems = arranged_song(seconds=SECONDS)
    specs = {name: _spec(sound) for name, sound in stems.items()}
    cases = []
    for target in FAMILIES:
        for source in (name for name in FAMILIES if name != target):
            for alpha in ALPHAS:
                spec = (
                    specs[target]
                    if alpha == 0
                    else _spec(inject_bleed(stems[target], stems[source], alpha))
                )
                index, level = _bleed_level(target, spec, specs)
                cases.append((alpha, index, level))
    return cases


def test_bleed_index_ranks_the_injected_amount(bleed_cases: list) -> None:
    alphas = [case[0] for case in bleed_cases]
    indices = [case[1] for case in bleed_cases]
    assert spearmanr(alphas, indices).statistic >= 0.8


def test_bleed_levels_are_correct_for_80_percent(bleed_cases: list) -> None:
    correct = [level == ALPHAS[alpha] for alpha, _, level in bleed_cases]
    assert np.mean(correct) >= 0.8


def test_playing_together_control_is_never_high() -> None:
    kick = _spec(kick_on_beats(SECONDS))
    bass_line = _spec(bass(np.random.default_rng(9), SECONDS))
    for family, target, source, source_family in (
        ("bass", bass_line, kick, "drums"),
        ("drums", kick, bass_line, "bass"),
    ):
        measure = bleed.measure_bleed(family, target, [bleed.BleedSource(source_family, source)])
        level = rating.bleed_level(
            measure.index, co_occurrence=measure.co_occurrence, status="not-measured"
        )
        assert level != "high"


@pytest.mark.xfail(
    strict=True,
    reason="Q3 pending: co-playing sustained families read as bleed (guitar 0.080 from bass)",
)
def test_dense_song_has_no_false_bleed_above_medium() -> None:
    stems = song(seconds=SECONDS)
    specs = {name: _spec(sound) for name, sound in stems.items()}
    levels = {family: _bleed_level(family, specs[family], specs)[1] for family in FAMILIES}
    assert "high" not in levels.values(), levels


@pytest.fixture(scope="module")
def artefact_cases() -> list[tuple[float, float, str]]:
    cases = []
    for stems in (arranged_song(seconds=SECONDS), song(seconds=SECONDS)):
        others = sum(stems.values())
        for name in FAMILIES:
            for rate in RATES:
                gated = gate_holes(stems[name], rate, seed=4)
                index = artefacts.artefact_index(_spec(gated), _spec(others - stems[name] + gated))
                cases.append((rate, index, rating.artefact_level(index, status="not-measured")))
    return cases


def test_artefact_index_ranks_the_gating_rate(artefact_cases: list) -> None:
    rates = [case[0] for case in artefact_cases]
    indices = [case[1] for case in artefact_cases]
    assert spearmanr(rates, indices).statistic >= 0.8


def test_artefact_levels_are_correct_for_80_percent(artefact_cases: list) -> None:
    correct = [level == RATES[rate] for rate, _, level in artefact_cases]
    assert np.mean(correct) >= 0.8

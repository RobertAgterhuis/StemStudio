"""Artefact estimate (T032, data-model 4.4 with the Q2 decision) and detail kinds (A3).

Q2 (orchestrator, 2026-11-23): the A1 flicker ratio did not respond to the V1 musical-noise
injection and failed the one-hour redesign, so it is not part of the artefact level and is not
shipped. A2 dropouts judge every stem against its own envelope (a hole falls and recovers
abruptly; natural decays do not), the same way for every family.
"""

from typing import Any

import numpy as np
import pytest

from stemstudio_worker.insights import artefacts, detail
from stemstudio_worker.insights.representation import BANDS, Spectrogram, spectrogram_of
from stemstudio_worker.insights.thresholds import ARTEFACT_LEVELS
from stemstudio_worker.schemas import DetailParentInput

from .synthetic import FAMILIES, SR, arranged_song, gate_holes, kick_on_beats, song

RATES = (0.0, 0.5, 2.0)


@pytest.fixture(scope="module", params=["arranged", "dense"])
def stems(request: pytest.FixtureRequest) -> dict[str, np.ndarray]:
    return arranged_song(seconds=20.0) if request.param == "arranged" else song(seconds=20.0)


def _index(stems: dict[str, np.ndarray], name: str, sound: np.ndarray) -> float:
    mix = sum(stems.values()) - stems[name] + sound
    return artefacts.artefact_index(spectrogram_of(sound, SR), spectrogram_of(mix, SR))


@pytest.mark.parametrize("name", FAMILIES)
def test_clean_stems_are_low_for_every_family(name: str, stems: dict[str, Any]) -> None:
    assert ARTEFACT_LEVELS.level(_index(stems, name, stems[name])) == "low"


def test_a_clean_kick_with_natural_decays_is_low() -> None:
    kick = kick_on_beats(20.0)
    assert artefacts.dropout_share(spectrogram_of(kick, SR)) == 0.0


@pytest.mark.parametrize("name", FAMILIES)
def test_dropouts_rise_with_the_gating_rate(name: str, stems: dict[str, Any]) -> None:
    indices = [_index(stems, name, gate_holes(stems[name], rate, seed=4)) for rate in RATES]
    assert indices[0] < indices[1] < indices[2]
    assert ARTEFACT_LEVELS.level(indices[2]) != "low"


def test_holes_the_mix_has_too_are_the_music() -> None:
    """A band stop (every stem silent at once) is in the mix as well: relative ratio about 1."""
    stems = arranged_song(seconds=20.0)
    gated = {name: gate_holes(sound, 2.0, seed=9) for name, sound in stems.items()}
    mix = spectrogram_of(sum(gated.values()), SR)
    for name in FAMILIES:
        stem = spectrogram_of(gated[name], SR)
        assert artefacts.dropout_share(stem) > 0.03
        assert ARTEFACT_LEVELS.level(artefacts.artefact_index(stem, mix)) == "low"


def test_a_slow_fade_is_not_a_hole() -> None:
    t = np.arange(SR * 6) / SR
    tone = 0.3 * np.sin(2 * np.pi * 440 * t)
    envelope = np.ones_like(t)
    fall = (t > 2.0) & (t < 2.25)
    envelope[fall] = 10 ** (-40 * (t[fall] - 2.0) / 0.25 / 20)  # -40 dB over 250 ms, 1.6 dB/block
    envelope[(t >= 2.25) & (t < 2.4)] = 0.01
    assert artefacts.dropout_share(spectrogram_of(tone * envelope, SR)) == 0.0


def test_a_hole_longer_than_0_3_s_is_a_rest_not_a_dropout() -> None:
    t = np.arange(SR * 6) / SR
    tone = 0.3 * np.sin(2 * np.pi * 440 * t)
    long_gap, short_gap = tone.copy(), tone.copy()
    long_gap[2 * SR : round(2.5 * SR)] = 0.0
    short_gap[2 * SR : round(2.15 * SR)] = 0.0
    assert artefacts.dropout_share(spectrogram_of(long_gap, SR)) == 0.0
    assert artefacts.dropout_share(spectrogram_of(short_gap, SR)) > 0.0


def test_silence_and_empty_input_have_no_dropouts() -> None:
    silent = spectrogram_of(np.zeros(SR * 2), SR)
    empty = Spectrogram(
        np.zeros((0, BANDS), np.float32), np.zeros(0, np.float32), np.zeros(0, np.float32)
    )
    assert artefacts.dropout_share(silent) == 0.0
    assert artefacts.dropout_share(empty) == 0.0
    assert artefacts.artefact_index(empty, empty) == 0.0


def test_index_mapping_is_x_over_x_plus_k() -> None:
    assert artefacts.normalise(0.0) == 0.0
    assert artefacts.normalise(artefacts.ARTEFACT_K) == pytest.approx(0.5)
    assert 0.99 < artefacts.normalise(1e6) < 1.0


def _parent(residual: float | None) -> DetailParentInput:
    return DetailParentInput(
        parent="drums", model_residual_db=residual, reconstruction_error_db=None
    )


def test_detail_kinds_model_residual() -> None:
    assert detail.detail_kinds("detail", _parent(-12.0), 0.1) == ["model-residual"]
    assert detail.detail_kinds("detail", _parent(-25.8), 0.1) == []
    assert detail.detail_kinds("detail", _parent(None), 0.1) == []
    assert detail.detail_kinds("detail", None, None) == []


def test_detail_kinds_rest_share() -> None:
    assert detail.detail_kinds("detail", _parent(-30.0), 0.4) == ["rest-share"]
    assert detail.detail_kinds("detail", _parent(-12.0), 0.4) == ["model-residual", "rest-share"]
    assert detail.detail_kinds("rest", _parent(-12.0), 0.4) == [
        "rest-share"
    ]  # rest: own share only
    assert detail.detail_kinds("rest", None, 0.1) == []
    assert detail.detail_kinds("standard", _parent(-12.0), 0.9) == []


def test_rest_share_is_the_energy_share_of_the_rest_stem() -> None:
    rng = np.random.default_rng(1)
    parent = rng.gamma(0.5, 1e-4, size=(400, BANDS))
    level = np.zeros(400, np.float32)

    def spec(power: np.ndarray) -> Spectrogram:
        return Spectrogram(power.astype(np.float32), level, level)

    assert detail.rest_share(spec(0.3 * parent), spec(parent)) == pytest.approx(0.3, rel=1e-3)
    assert detail.rest_share(spec(2.0 * parent), spec(parent)) == 1.0
    assert detail.rest_share(spec(parent), spec(0 * parent)) == 0.0

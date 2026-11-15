"""Tests of the STFT power-mask re-assignment of a sum-run family to its parents (T014)."""

import numpy as np
import pytest

from stemstudio_worker.detail.gating import relative_db
from stemstudio_worker.detail.reassign import HOP, N_FFT, reassign, reassign_families
from stemstudio_worker.schemas import DetailStemName, ParentStemName

from .signals import Audio, noise, silence, tone


def test_stft_parameters_follow_research() -> None:
    assert (N_FFT, HOP) == (4096, 1024)


def test_single_parent_receives_the_whole_family() -> None:
    family = noise(1.0, 0.2)
    shares = reassign(family, {"guitar": tone(1.0, 0.3)})
    assert list(shares) == ["guitar"]
    assert np.array_equal(shares["guitar"], family)


@pytest.mark.parametrize("count", [2, 3])
def test_shares_sum_to_the_family(count: int) -> None:
    names: tuple[ParentStemName, ...] = ("guitar", "piano", "other")
    parents = {name: noise(1.3, 0.1 * (i + 1), seed=i) for i, name in enumerate(names[:count])}
    family = noise(1.3, 0.25, seed=9)
    shares = reassign(family, parents)
    assert list(shares) == list(parents)
    assert all(share.shape == family.shape for share in shares.values())
    assert np.max(np.abs(sum(shares.values()) - family)) <= 1e-5


def test_shares_follow_the_power_of_the_parents() -> None:
    low = tone(1.0, 0.3, freq=220.0)
    high = tone(1.0, 0.3, freq=5000.0)
    family = 0.5 * low + 0.5 * high
    shares = reassign(family, {"guitar": low, "other": high})
    assert relative_db(shares["guitar"] - 0.5 * low, 0.5 * low) < -20
    assert relative_db(shares["other"] - 0.5 * high, 0.5 * high) < -20


def test_silent_parents_split_the_family_equally() -> None:
    family = noise(0.5, 0.2)
    shares = reassign(family, {"guitar": silence(0.5), "piano": silence(0.5)})
    assert np.allclose(shares["guitar"], family / 2, atol=1e-5)
    assert np.max(np.abs(shares["guitar"] + shares["piano"] - family)) <= 1e-5


def test_short_input_is_handled() -> None:
    family = noise(0.01, 0.2)
    shares = reassign(family, {"guitar": noise(0.01, 0.2, 2), "other": noise(0.01, 0.1, 3)})
    assert shares["guitar"].shape == family.shape


def test_reassign_families_maps_every_family_to_every_parent() -> None:
    parents: dict[ParentStemName, Audio] = {"guitar": noise(0.5, 0.2, 1)}
    parents["other"] = noise(0.5, 0.1, 2)
    families: dict[DetailStemName, Audio] = {"strings": noise(0.5, 0.2, 3)}
    families["brass"] = noise(0.5, 0.1, 4)
    result = reassign_families(families, parents)
    assert list(result) == ["guitar", "other"]
    assert list(result["guitar"]) == ["strings", "brass"]
    total = result["guitar"]["brass"] + result["other"]["brass"]
    assert np.max(np.abs(total - families["brass"])) <= 1e-5

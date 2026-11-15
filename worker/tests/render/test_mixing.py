"""Tests of the render mixing law (data-model.md 3, ``render-vectors.json`` pan cases)."""

import json
from pathlib import Path
from typing import Any

import numpy as np
import pytest

from stemstudio_worker.render.mixing import mix_block, pan_matrix
from stemstudio_worker.schemas import RenderInput

_VECTORS = (
    Path(__file__).resolve().parents[3]
    / "specs"
    / "007-export-library"
    / "contracts"
    / "render-vectors.json"
)


def _pan_cases() -> list[dict[str, Any]]:
    cases: list[dict[str, Any]] = json.loads(_VECTORS.read_text(encoding="utf-8"))["pan"]
    return cases


def _input(gain: float = 1.0, pan: float = 0.0) -> RenderInput:
    path = "tracks/00000000-0000-0000-0000-000000000000/stems/bass.flac"
    return RenderInput(path=path, gain=gain, pan=pan)


def _signal(*frames: tuple[float, float]) -> np.ndarray[Any, np.dtype[np.float32]]:
    return np.array(frames, dtype=np.float32)


@pytest.mark.parametrize("case", _pan_cases(), ids=lambda case: f"pan {case['pan']}")
def test_pan_matrix_equals_every_vector(case: dict[str, Any]) -> None:
    np.testing.assert_allclose(pan_matrix(case["pan"]), case["matrix"], rtol=0, atol=1e-9)


def test_vectors_cover_the_required_pans() -> None:
    assert {case["pan"] for case in _pan_cases()} >= {-1.0, -0.5, -0.4, 0.0, 0.25, 0.5, 1.0}


def test_pan_zero_is_exactly_the_identity() -> None:
    assert pan_matrix(0.0).tolist() == [[1.0, 0.0], [0.0, 1.0]]


def test_pan_matrix_is_a_fresh_float64_array() -> None:
    matrix = pan_matrix(-0.4)
    matrix[0, 0] = 9.0
    assert matrix.dtype == np.dtype(np.float64)
    assert pan_matrix(-0.4)[0, 0] == 1.0


@pytest.mark.parametrize("case", _pan_cases(), ids=lambda case: f"pan {case['pan']}")
def test_mix_block_pans_a_two_sample_signal(case: dict[str, Any]) -> None:
    block = _signal((1.0, 0.5), (-0.25, 0.75))
    matrix = np.array(case["matrix"])
    expected = block.astype(np.float64) @ matrix.T
    mixed = mix_block([block], [_input(pan=case["pan"])], 1.0)
    np.testing.assert_allclose(mixed, expected, rtol=0, atol=1e-6)


def test_mix_block_applies_gain_pan_and_master_gain_and_sums_inputs() -> None:
    first = _signal((1.0, 0.5), (0.2, -0.4))
    second = _signal((0.5, 0.5), (-1.0, 0.0))
    inputs = [_input(gain=0.8, pan=-0.4), _input(gain=0.5, pan=0.5)]
    m1, m2 = pan_matrix(-0.4), pan_matrix(0.5)
    expected = ((first * 0.8) @ m1.T + (second * 0.5) @ m2.T) * 0.7
    mixed = mix_block([first, second], inputs, 0.7)
    np.testing.assert_allclose(mixed, expected, rtol=0, atol=1e-6)


def test_mix_block_with_unity_settings_is_bit_identical() -> None:
    block = np.arange(-6, 6, dtype=np.float32).reshape(6, 2) / np.float32(32768)
    mixed = mix_block([block], [_input()], 1.0)
    assert mixed.dtype == np.dtype(np.float32)
    assert np.array_equal(mixed, block)


def test_mix_block_returns_float32_and_does_not_change_its_input() -> None:
    block = _signal((0.5, 0.25))
    mix_block([block], [_input(gain=0.5, pan=1.0)], 0.5)
    assert block.tolist() == [[0.5, 0.25]]


def test_mix_block_of_zero_frames_is_empty() -> None:
    mixed = mix_block([np.zeros((0, 2), dtype=np.float32)], [_input()], 1.0)
    assert mixed.shape == (0, 2)
    assert mixed.dtype == np.dtype(np.float32)


def test_mix_block_needs_one_block_per_input() -> None:
    with pytest.raises(ValueError, match="one block per input"):
        mix_block([_signal((0.0, 0.0))], [_input(), _input()], 1.0)


def test_mix_block_needs_at_least_one_block() -> None:
    with pytest.raises(ValueError, match="one block per input"):
        mix_block([], [], 1.0)

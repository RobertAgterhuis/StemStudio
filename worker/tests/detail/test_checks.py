"""Tests of the rest stem, the plausibility checks and the reconstruction check (T016)."""

from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.detail import checks
from stemstudio_worker.detail.audio_io import read_stem, write_stem
from stemstudio_worker.detail.checks import (
    CheckPolicy,
    double_count_db,
    from_pcm,
    model_residual_db,
    pre_write_outcome,
    reconstruction_db,
    rest_of,
    to_pcm,
)
from stemstudio_worker.detail.gating import SILENCE_DB
from stemstudio_worker.schemas import ParentOutcomeKind

from .signals import noise, tone

K = ParentOutcomeKind
PARTITION = CheckPolicy(model_residual=True, double_count=False)
FAMILIES = CheckPolicy(model_residual=False, double_count=True)


def test_constants_follow_research_d04() -> None:
    assert checks.MIN_KEPT == 2
    assert checks.MAX_MODEL_RESIDUAL_DB == -15.0
    assert checks.MAX_DOUBLE_COUNT_DB == 1.0
    assert checks.MAX_RECONSTRUCTION_DB == -30.0


def test_pcm_round_trip_is_on_the_16_bit_grid() -> None:
    audio = np.array([[0.5, -1.0, 1.0, 2.0, 1e-6]], dtype=np.float32)
    pcm = to_pcm(audio)
    assert pcm.dtype == np.int16
    assert pcm.tolist() == [[16384, -32768, 32767, 32767, 0]]
    assert from_pcm(pcm)[0, 0] == 0.5


def test_rest_is_parent_minus_the_kept_stems() -> None:
    parent = from_pcm(to_pcm(noise(0.5, 0.4)))
    kept = [to_pcm(parent * 0.5), to_pcm(parent * 0.25)]
    rest = rest_of(parent, kept)
    assert np.allclose(rest, parent - from_pcm(kept[0]) - from_pcm(kept[1]), atol=1e-7)


def test_model_residual_is_the_parent_minus_all_outputs() -> None:
    parent = noise(0.5, 0.4)
    assert model_residual_db(parent, [parent * 0.5, parent * 0.5]) == SILENCE_DB
    assert model_residual_db(parent, [parent * 0.9]) == pytest.approx(-20.0, abs=1e-3)


def test_double_count_is_the_kept_sum_relative_to_the_parent() -> None:
    parent = noise(0.5, 0.4)
    assert double_count_db(parent, [parent, parent]) == pytest.approx(6.02, abs=0.01)


@pytest.mark.parametrize(
    "case",
    [
        (1, -40.0, 0.0, PARTITION, K.NOTHING_FURTHER),
        (0, -40.0, 0.0, FAMILIES, K.NOTHING_FURTHER),
        (2, -15.0, 0.0, PARTITION, None),
        (2, -14.9, 0.0, PARTITION, K.NOT_RELIABLE),
        (2, -14.9, 0.0, FAMILIES, None),
        (3, -40.0, 1.0, FAMILIES, None),
        (3, -40.0, 1.1, FAMILIES, K.NOT_RELIABLE),
        (3, -40.0, 1.1, PARTITION, None),
        (1, -3.0, 6.0, FAMILIES, K.NOTHING_FURTHER),
    ],
)
def test_pre_write_outcome_follows_the_d04_order(
    case: tuple[int, float, float, CheckPolicy, K | None],
) -> None:
    kept, residual, double, policy, expected = case
    assert pre_write_outcome(kept, residual, double, policy) is expected


def _write(paths: list[Path], stems: list[np.ndarray]) -> list[np.ndarray]:
    for path, stem in zip(paths, stems, strict=True):
        write_stem(path, stem)
    return [read_stem(path) for path in paths]


def test_written_flacs_reconstruct_the_parent(tmp_path: Path) -> None:
    parent = from_pcm(to_pcm(noise(0.5, 0.4) + tone(0.5, 0.2)))
    kept = [to_pcm(parent * 0.6), to_pcm(parent * 0.3)]
    rest = to_pcm(rest_of(parent, kept))
    paths = [tmp_path / f"{n}.flac" for n in ("a", "b", "rest")]
    written = _write(paths, [*kept, rest])
    assert reconstruction_db(parent, written) <= checks.MAX_RECONSTRUCTION_DB
    assert reconstruction_db(parent, written) == SILENCE_DB
    assert checks.reconstruction_ok(reconstruction_db(parent, written))


def test_a_clipped_rest_fails_the_reconstruction_check(tmp_path: Path) -> None:
    parent = from_pcm(to_pcm(tone(0.5, 0.9)))
    kept = [to_pcm(-parent), to_pcm(parent * 0.01)]
    rest = to_pcm(rest_of(parent, kept))
    written = _write([tmp_path / "a.flac", tmp_path / "b.flac", tmp_path / "r.flac"],
                     [*kept, rest])  # fmt: skip
    error = reconstruction_db(parent, written)
    assert error > checks.MAX_RECONSTRUCTION_DB
    assert not checks.reconstruction_ok(error)


def test_stem_io_uses_channels_first_audio(tmp_path: Path) -> None:
    pcm = to_pcm(tone(0.1, 0.5))
    write_stem(tmp_path / "x.flac", pcm)
    back = read_stem(tmp_path / "x.flac")
    assert back.shape == pcm.shape
    assert back.dtype == np.float32
    assert np.array_equal(to_pcm(back), pcm)

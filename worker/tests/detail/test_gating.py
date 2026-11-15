"""Tests of the parent gate and the sub-stem audibility gate (T012, FR-003, FR-004)."""

import numpy as np
import pytest

from stemstudio_worker.detail import gating
from stemstudio_worker.detail.gating import (
    FRAME,
    HOP,
    SILENCE_DB,
    frame_levels,
    level_dbfs,
    parent_active,
    read_parent,
    relative_db,
    sub_active_floor,
    sub_stem_audible,
)

from .signals import burst, db, noise, silence, tone


def test_constants_follow_research_d04() -> None:
    assert (FRAME, HOP) == (2048, 512)
    assert gating.PARENT_ACTIVE_DBFS == -40.0
    assert gating.PARENT_MIN_ACTIVE_SHARE == 0.08
    assert gating.SUB_MIN_REL_DB == -20.0
    assert gating.SUB_FLOOR_DBFS == -50.0
    assert gating.SUB_FLOOR_BELOW_PARENT_DB == 25.0
    assert gating.SUB_MIN_ACTIVE_SHARE == 0.05


def test_frame_levels_use_2048_frames_with_hop_512() -> None:
    audio = tone(1.0, 0.5)
    levels = frame_levels(audio)
    assert len(levels) == 1 + (audio.shape[1] - FRAME) // HOP
    assert np.allclose(levels, db(0.5 / np.sqrt(2)), atol=0.1)


def test_frame_levels_of_a_short_input_give_one_padded_frame() -> None:
    audio = tone(0.01, 0.5)
    assert len(frame_levels(audio)) == 1


def test_frame_levels_mix_down_to_mono() -> None:
    left = tone(0.5, 0.5)[0]
    audio = np.stack([left, -left])
    assert np.all(frame_levels(audio) == SILENCE_DB)


def test_silence_has_the_floor_level() -> None:
    assert np.all(frame_levels(silence(0.5)) == SILENCE_DB)
    assert level_dbfs(silence(0.5)) == SILENCE_DB


def test_level_dbfs_is_the_rms_of_the_mono_mix_and_never_above_zero() -> None:
    assert level_dbfs(tone(1.0, 0.5)) == pytest.approx(db(0.5 / np.sqrt(2)), abs=0.01)
    assert level_dbfs(np.ones((2, 4096), dtype=np.float32)) == 0.0
    assert level_dbfs(np.full((2, 4096), 2.0, dtype=np.float32)) == 0.0


@pytest.mark.parametrize(("share", "active"), [(0.08, True), (0.0799, False), (0.0, False)])
def test_parent_gate_boundary_is_eight_percent(share: float, active: bool) -> None:
    assert parent_active(share) is active


def test_real_content_passes_the_parent_gate() -> None:
    reading = read_parent(noise(2.0, 0.3))
    assert reading.active
    assert reading.active_share == 1.0
    assert reading.level_dbfs == pytest.approx(db(0.3 / np.sqrt(3) / np.sqrt(2)), abs=0.5)


def test_quiet_noise_is_skipped() -> None:
    reading = read_parent(noise(2.0, 10 ** (-80 / 20)))
    assert not reading.active
    assert reading.active_share == 0.0


@pytest.mark.parametrize(("on", "active"), [(0.2, True), (0.04, False)])
def test_bleed_with_few_active_frames_is_skipped(on: float, active: bool) -> None:
    reading = read_parent(burst(4.0, on, 0.5))
    assert reading.active is active
    assert reading.active_share == pytest.approx(on, abs=0.01)


def test_relative_db_compares_energies() -> None:
    parent = tone(1.0, 0.5)
    assert relative_db(parent * 0.1, parent) == pytest.approx(-20.0, abs=1e-3)
    assert relative_db(silence(1.0), parent) == SILENCE_DB
    assert relative_db(parent, silence(1.0)) == SILENCE_DB


@pytest.mark.parametrize(("level", "floor"), [(-10.0, -35.0), (-25.0, -50.0), (-40.0, -50.0)])
def test_sub_floor_is_max_of_minus_50_and_parent_minus_25(level: float, floor: float) -> None:
    assert sub_active_floor(level) == floor


@pytest.mark.parametrize(("scale", "audible"), [(0.2, True), (0.1, True), (0.05, False)])
def test_sub_stem_needs_minus_20_db_energy_relative_to_the_parent(
    scale: float, audible: bool
) -> None:
    parent = noise(2.0, 0.5)
    assert sub_stem_audible(parent * scale, parent, read_parent(parent)) is audible


@pytest.mark.parametrize(("on", "audible"), [(0.03, False), (0.07, True)])
def test_sub_stem_needs_five_percent_active_frames(on: float, audible: bool) -> None:
    parent = tone(4.0, 0.1)
    sub = burst(4.0, on, 1.0)
    assert relative_db(sub, parent) > -20
    assert sub_stem_audible(sub, parent, read_parent(parent)) is audible


def test_sub_stem_frames_below_the_absolute_floor_do_not_count() -> None:
    parent = tone(2.0, 10 ** (-57 / 20))
    sub = parent * 0.9
    assert sub_stem_audible(sub, parent, read_parent(parent)) is False
